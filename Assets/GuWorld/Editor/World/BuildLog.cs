using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace GuWorld.Editor
{
    /// <summary>
    /// The mountain builder's log: a line a step, written out at once (if the build dies,
    /// its last line says where), each with the memory held then — the process's own,
    /// Unity's, the scripts' heap — and, where asked, what kinds of things hold it
    /// (meshes, terrains, textures, objects). Goes to Logs/QingMaoBuild/progress.log.
    /// </summary>
    public static class BuildLog
    {
        public static readonly string Folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "QingMaoBuild"));
        public static string File => Path.Combine(Folder, "progress.log");

        static readonly System.Diagnostics.Stopwatch clock = new System.Diagnostics.Stopwatch();
        static long peak;

        /// <summary>The most memory the process has held at any mark so far, MB.</summary>
        public static long PeakMB => peak / (1024 * 1024);

        public static void Begin(string what)
        {
            Directory.CreateDirectory(Folder);
            clock.Restart();
            peak = 0;
            Write($"==== {DateTime.Now:yyyy-MM-dd HH:mm:ss} {what}");
        }

        /// <summary>A step done: where the build is, how long since the start, the memory held (this process's own, and the system's free).</summary>
        public static void Mark(string where, bool detail = false)
        {
            var (mine, working) = ProcessMemory();
            peak = Math.Max(peak, mine);
            var line = $"[{clock.Elapsed:hh\\:mm\\:ss}] {where} | private {Mb(mine)} MB, working {Mb(working)} MB, unity {Mb(Profiler.GetTotalAllocatedMemoryLong())} MB, scripts {Mb(GC.GetTotalMemory(false))} MB, system free {Mb(SystemFree())} MB";

            if (detail)
            {
                line += " | " + Holders();
            }

            Write(line);
        }

        public static void Error(string where, Exception e) => Write($"[{clock.Elapsed:hh\\:mm\\:ss}] FAILED in {where}: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");

        public static void Write(string line)
        {
            Directory.CreateDirectory(Folder);
            System.IO.File.AppendAllText(File, line + Environment.NewLine);
            UnityEngine.Debug.Log("GU World build: " + line);
        }

        /// <summary>What holds the memory: how many of each kind of thing are loaded, and how much they take.</summary>
        static string Holders()
        {
            string Of<T>(string label) where T : UnityEngine.Object
            {
                var all = Resources.FindObjectsOfTypeAll<T>();
                var bytes = all.Sum(o => Profiler.GetRuntimeMemorySizeLong(o));

                return $"{label} {all.Length} ({Mb(bytes)} MB)";
            }

            return string.Join(", ",
                Of<Mesh>("meshes"),
                Of<TerrainData>("terrains"),
                Of<Texture2D>("textures"),
                Of<RenderTexture>("render textures"),
                Of<Material>("materials"),
                $"objects {Resources.FindObjectsOfTypeAll<GameObject>().Length}",
                $"colliders {Resources.FindObjectsOfTypeAll<Collider>().Length}");
        }

        static long Mb(long bytes) => bytes / (1024 * 1024);

        // Mono's Process gives no memory figures on Windows: asked of the system itself.
        [StructLayout(LayoutKind.Sequential)]
        struct Counters
        {
            public uint cb;
            public uint PageFaultCount;
            public UIntPtr PeakWorkingSetSize;
            public UIntPtr WorkingSetSize;
            public UIntPtr QuotaPeakPagedPoolUsage;
            public UIntPtr QuotaPagedPoolUsage;
            public UIntPtr QuotaPeakNonPagedPoolUsage;
            public UIntPtr QuotaNonPagedPoolUsage;
            public UIntPtr PagefileUsage;
            public UIntPtr PeakPagefileUsage;
            public UIntPtr PrivateUsage;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MemoryStatus
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", EntryPoint = "K32GetProcessMemoryInfo")]
        static extern bool GetProcessMemoryInfo(IntPtr process, out Counters counters, uint size);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll")]
        static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);

        /// <summary>This process's private bytes and working set.</summary>
        public static (long privateBytes, long working) ProcessMemory()
        {
            try
            {
                if (GetProcessMemoryInfo(GetCurrentProcess(), out var c, (uint)Marshal.SizeOf<Counters>()))
                {
                    return ((long)c.PrivateUsage.ToUInt64(), (long)c.WorkingSetSize.ToUInt64());
                }
            }
            catch (Exception)
            {
                // Not Windows: no figure.
            }

            return (0, 0);
        }

        /// <summary>The physical memory the system has free, bytes.</summary>
        public static long SystemFree()
        {
            try
            {
                var status = new MemoryStatus { dwLength = (uint)Marshal.SizeOf<MemoryStatus>() };

                return GlobalMemoryStatusEx(ref status) ? (long)status.ullAvailPhys : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }
}
