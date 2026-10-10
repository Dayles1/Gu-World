using System.Text;
using UnityEditor;
using UnityEngine;

namespace GuWorld.Editor
{
    /// <summary>
    /// Prints how a body model came in (its hierarchy, meshes and materials),
    /// for checking the import: Unity -batchmode -executeMethod GuWorld.Editor.GuWorldModelDump.Run
    /// </summary>
    public static class GuWorldModelDump
    {
        public static void Run()
        {
            AssetDatabase.Refresh();

            foreach (var path in new[] { "Assets/GuWorld/Models/human-male.glb", "Assets/GuWorld/Models/human-female.glb" })
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var text = new StringBuilder($"GU World dump: {path} → {(model == null ? "NOT IMPORTED" : model.name)}\n");

                if (model != null)
                {
                    Walk(model.transform, 0, text);

                    foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                    {
                        text.Append($"  renderer {renderer.name} ({renderer.GetType().Name})");

                        if (renderer is SkinnedMeshRenderer skinned)
                        {
                            var mesh = skinned.sharedMesh;
                            text.Append($" bones {skinned.bones.Length} root {(skinned.rootBone ? skinned.rootBone.name : "-")} readable {mesh.isReadable} verts {mesh.vertexCount} submeshes {mesh.subMeshCount} meshBounds {mesh.bounds} uv {mesh.uv.Length} normals {mesh.normals.Length} weights/vertex {mesh.GetBonesPerVertex().Length}");
                            text.Append($" bindpose0 {mesh.bindposes[0].GetColumn(3)} bone0 {skinned.bones[0].name}");
                        }

                        text.Append('\n');

                        foreach (var material in renderer.sharedMaterials)
                        {
                            if (material == null) continue;
                            text.Append($"    material {material.name} shader {material.shader.name} props:");

                            foreach (var name in material.GetTexturePropertyNames())
                            {
                                var texture = material.GetTexture(name);
                                if (texture != null) text.Append($" tex:{name}({texture.width}x{texture.height} readable {texture.isReadable} aniso {texture.anisoLevel})");
                            }

                            foreach (var name in new[] { "baseColorFactor", "_BaseColor", "_Color" })
                            {
                                if (material.HasProperty(name)) text.Append($" {name}={material.GetColor(name)}");
                            }

                            text.Append('\n');
                        }
                    }
                }

                Debug.Log(text.ToString());
            }
        }

        static void Walk(Transform t, int depth, StringBuilder text)
        {
            var p = t.localPosition;
            var r = t.localRotation.eulerAngles;
            text.Append(new string(' ', depth * 2))
                .Append($"{t.name} pos({p.x:0.###},{p.y:0.###},{p.z:0.###}) rot({r.x:0.#},{r.y:0.#},{r.z:0.#}) scale {t.localScale.x:0.###}\n");

            foreach (Transform child in t)
            {
                Walk(child, depth + 1, text);
            }
        }
    }
}
