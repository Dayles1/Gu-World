using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

namespace GuWorld.Editor
{
    /// <summary>
    /// Reads a profile the game wrote in a timed run (a development build, `-profile file`)
    /// and writes where the frames went, as text: for the main and the render thread, the
    /// most costly markers by their own time, and the call tree down a few levels, each in
    /// ms per frame on average; the job workers' busiest markers together.
    ///
    /// Unity -batchmode -projectPath . -executeMethod GuWorld.Editor.ProfileReport.Run -raw file.raw -out report.txt
    /// </summary>
    public static class ProfileReport
    {
        const int TreeDepth = 7;
        const float TreeAtLeast = 0.15f;
        const int TopCount = 45;

        public static void Run()
        {
            var args = System.Environment.GetCommandLineArgs();
            string Arg(string name)
            {
                var at = System.Array.IndexOf(args, name);
                return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
            }

            var raw = Arg("-raw");
            var output = Arg("-out") ?? Path.ChangeExtension(raw, ".txt");
            var text = new StringBuilder();

            try
            {
                if (!ProfilerDriver.LoadProfile(raw, false))
                {
                    text.AppendLine($"could not load {raw}");
                }
                else
                {
                    Report(text);
                }
            }
            catch (System.Exception exception)
            {
                text.AppendLine(exception.ToString());
            }

            File.WriteAllText(output, text.ToString());
            Debug.Log($"GU World: profile report written to {output}");

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }

        /// <summary>The counters worth a look (the GPU Resident Drawer's: what it draws and what it leaves out, and why; the renderer's), averaged.</summary>
        static readonly string[] CounterNames =
        {
            "GRD Renderers", "Excluded Renderers", "Non-Rendering Renderers", "Inactive Renderers", "Coverage %", "Total Instances", "Visible Instances",
            "Frustum Culled", "LOD Group Culled", "Small Mesh Culled",
            "Excl: LOD Animate CrossFading", "Excl: Custom MaterialPropertyBlock", "Excl: Render Callback", "Excl: Non-Standard Sort Key", "Excl: Proxy Volume Probe",
            "Excl: Blend Probes With Anchor", "Excl: Enlighten Vertex Stream", "Excl: Missing DOTS Instancing", "Excl: Null Material", "Excl: Too Many Submeshes",
            "Excl: Missing Mesh", "Excl: GPU Driven Disabled", "Excl: Animation Visibility", "Excl: TextMesh Component", "Excl: Inactive Or Disabled",
            "Batches Count", "Draw Calls Count", "SetPass Calls Count", "Triangles Count", "Shadow Casters Count", "Visible Skinned Meshes Count", "Render Textures Count",
            "GC Allocated In Frame", "GC Allocation In Frame Count",
        };

        static void Counters(StringBuilder text, int first, int last)
        {
            text.AppendLine();
            text.AppendLine("== counters, average per frame ==");

            foreach (var name in CounterNames)
            {
                double sum = 0;
                var count = 0;

                for (var frame = first; frame <= last; frame++)
                {
                    using var view = ProfilerDriver.GetRawFrameDataView(frame, 0);

                    if (view == null || !view.valid)
                    {
                        continue;
                    }

                    var id = view.GetMarkerId(name);

                    if (id == FrameDataView.invalidMarkerId)
                    {
                        continue;
                    }

                    sum += view.GetCounterValueAsDouble(id);
                    count++;
                }

                if (count > 0)
                {
                    text.AppendLine($"{sum / count,14:0.##}  {name}");
                }
            }
        }

        static void Report(StringBuilder text)
        {
            var first = ProfilerDriver.firstFrameIndex;
            var last = ProfilerDriver.lastFrameIndex;
            // The first frames after the profiler starts carry its own setting up.
            first = Mathf.Min(last, first + 5);
            var frames = last - first + 1;
            text.AppendLine($"frames {first}…{last} ({frames})");

            // Threads by name, with the time of each marker: own (self) and in all (total), summed over the frames.
            var selfByThread = new Dictionary<string, Dictionary<string, double>>();
            var treeByThread = new Dictionary<string, Dictionary<string, (double total, int depth, int order)>>();
            var frameMs = 0.0;
            var order = 0;

            for (var frame = first; frame <= last; frame++)
            {
                for (var thread = 0; ; thread++)
                {
                    using var view = ProfilerDriver.GetHierarchyFrameDataView(frame, thread, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnSelfTime, false);

                    if (view == null || !view.valid)
                    {
                        break;
                    }

                    var name = view.threadGroupName == "Job" ? "Job workers" : view.threadName;

                    if (thread == 0)
                    {
                        frameMs += view.frameTimeMs;
                    }

                    if (name != "Main Thread" && name != "Render Thread" && name != "Job workers")
                    {
                        continue;
                    }

                    if (!selfByThread.TryGetValue(name, out var self))
                    {
                        selfByThread[name] = self = new Dictionary<string, double>();
                        treeByThread[name] = new Dictionary<string, (double, int, int)>();
                    }

                    var tree = treeByThread[name];
                    var children = new List<int>();

                    void Walk(int id, string path, int depth)
                    {
                        children.Clear();
                        view.GetItemChildren(id, children);

                        foreach (var child in children.ToArray())
                        {
                            var marker = view.GetItemName(child);
                            var childPath = path + " / " + marker;
                            self[marker] = (self.TryGetValue(marker, out var s) ? s : 0) + view.GetItemColumnDataAsFloat(child, HierarchyFrameDataView.columnSelfTime);

                            if (depth < TreeDepth)
                            {
                                var total = view.GetItemColumnDataAsFloat(child, HierarchyFrameDataView.columnTotalTime);
                                tree[childPath] = tree.TryGetValue(childPath, out var t) ? (t.total + total, depth, t.order) : (total, depth, order++);
                            }

                            Walk(child, childPath, depth + 1);
                        }
                    }

                    Walk(view.GetRootItemID(), "", 0);
                }
            }

            text.AppendLine($"frame {frameMs / frames:0.00} ms on average ({1000 * frames / frameMs:0} fps, profiler on)");
            Counters(text, first, last);

            foreach (var thread in new[] { "Main Thread", "Render Thread", "Job workers" })
            {
                if (!selfByThread.TryGetValue(thread, out var self))
                {
                    continue;
                }

                text.AppendLine();
                text.AppendLine($"== {thread}: own time, ms per frame ==");

                foreach (var pair in self.OrderByDescending(p => p.Value).Take(TopCount))
                {
                    text.AppendLine($"{pair.Value / frames,8:0.00}  {pair.Key}");
                }

                if (thread == "Job workers")
                {
                    continue;
                }

                text.AppendLine();
                text.AppendLine($"== {thread}: tree, ms per frame (at least {TreeAtLeast}) ==");

                foreach (var pair in treeByThread[thread].OrderBy(p => p.Value.order))
                {
                    var ms = pair.Value.total / frames;

                    if (ms < TreeAtLeast)
                    {
                        continue;
                    }

                    var name = pair.Key.Substring(pair.Key.LastIndexOf(" / ") + 3);
                    text.AppendLine($"{ms,8:0.00}  {new string(' ', pair.Value.depth * 2)}{name}");
                }
            }
        }
    }
}
