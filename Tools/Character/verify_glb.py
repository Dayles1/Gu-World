"""
Verifies an exported body against the current game model: imports both .glb
files in Blender (as the game's importer would see them: node names, armature,
meshes, materials, triangles, size, rest pose) and also reads both files raw
(glb_report.py: the skin's joint order, node tree, textures), then lists every
difference.

    blender -b --factory-startup --python verify_glb.py -- \
        --new out/human-teen-male.glb --old ../../Assets/GuWorld/Models/human-male.glb \
        --report out/verify-male.txt
"""

import argparse
import math
import os
import sys

import bpy
from mathutils import Vector

sys.dont_write_bytecode = True  # no __pycache__ beside the scripts
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import glb_report  # noqa: E402


def import_glb(path):
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj)
    for block in (bpy.data.meshes, bpy.data.armatures, bpy.data.materials, bpy.data.images):
        for item in list(block):
            block.remove(item)
    bpy.ops.import_scene.gltf(filepath=os.path.abspath(path))
    return [o for o in bpy.context.scene.objects]


def describe(path):
    objs = import_glb(path)
    info = {"path": path, "objects": [], "meshes": {}, "materials": set()}
    for o in sorted(objs, key=lambda o: (o.parent is not None, o.name)):
        info["objects"].append(f"{o.name} ({o.type}, parent={o.parent.name if o.parent else None})")
    arm = next(o for o in objs if o.type == "ARMATURE")
    info["armature"] = arm.name
    bones = arm.data.bones
    info["bones"] = {b.name: (b.parent.name if b.parent else None) for b in bones}
    mw = arm.matrix_world

    def at(name):
        return mw @ bones[name].head_local

    lo = Vector((1e9,) * 3)
    hi = Vector((-1e9,) * 3)
    for o in objs:
        if o.type != "MESH" or o.find_armature() is None:
            continue
        me = o.data
        me.calc_loop_triangles()
        mats = [m.name for m in me.materials if m]
        info["materials"].update(mats)
        info["meshes"][o.name] = {"mesh": me.name, "tris": len(me.loop_triangles), "verts": len(me.vertices), "materials": mats,
                                  "groups": len(o.vertex_groups)}
        if not o.name.startswith("Hair"):
            for v in me.vertices:
                p = o.matrix_world @ v.co
                lo = Vector(map(min, lo, p))
                hi = Vector(map(max, hi, p))
    info["height"] = hi.z - lo.z
    info["width"] = hi.x - lo.x
    info["sole"] = lo.z

    def angle_down(a, b):
        d = at(b) - at(a)
        return math.degrees(math.atan2(-d.z, math.hypot(d.x, d.y)))

    info["upperarm_l_down_deg"] = angle_down("upperarm_l", "lowerarm_l")
    info["lowerarm_l_down_deg"] = angle_down("lowerarm_l", "hand_l")
    info["thigh_l_down_deg"] = angle_down("thigh_l", "calf_l")
    info["left_is_plus_x"] = at("upperarm_l").x > at("upperarm_r").x
    toes = at("ball_leaf_l") - at("foot_l")
    info["faces"] = "-Y (Blender) = +Z (glTF)" if toes.y < 0 else "+Y (Blender) = -Z (glTF)"
    info["hip_above_sole"] = (at("thigh_l").z + at("thigh_r").z) / 2 - min(at("ball_leaf_l").z, at("ball_leaf_r").z)
    info["ball_leaf_above_mesh_bottom"] = min(at("ball_leaf_l").z, at("ball_leaf_r").z) - lo.z
    info["head_top_above_Head_joint"] = hi.z - at("Head").z
    # The game fits the model so its hip joints are the Mannequin's (0.96 above the sole, in
    # figure units of a 1.8 m grown-up); the figure's height in those units:
    info["figure_height_after_fit"] = 0.96 * (hi.z - min(at("ball_leaf_l").z, at("ball_leaf_r").z)) / info["hip_above_sole"]
    return info


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument("--new", required=True)
    ap.add_argument("--old", required=True)
    ap.add_argument("--report")
    args = ap.parse_args(argv)

    lines = []

    def out(text=""):
        lines.append(text)

    new, old = describe(args.new), describe(args.old)
    raw_new, raw_old = glb_report.report(args.new), glb_report.report(args.old)

    for label, info, raw in (("NEW", new, raw_new), ("CURRENT", old, raw_old)):
        out(f"===== {label}: {info['path']}")
        out("objects after Blender's glTF import:")
        for line in info["objects"]:
            out("  " + line)
        out(f"armature object: {info['armature']}, {len(info['bones'])} bones")
        out("skinned meshes:")
        for name, m in info["meshes"].items():
            out(f"  {name}: mesh '{m['mesh']}', {m['verts']} verts, {m['tris']} tris, materials {m['materials']}, {m['groups']} vertex groups")
        out(f"total triangles: {sum(m['tris'] for m in info['meshes'].values())}")
        out(f"body height (meshes without hair): {info['height']:.4f} m, arm span {info['width']:.4f} m, lowest point z={info['sole']:.4f}")
        out(f"rest pose: upper arm {info['upperarm_l_down_deg']:.2f} deg below horizontal, forearm {info['lowerarm_l_down_deg']:.2f} deg, "
            f"thigh {info['thigh_l_down_deg']:.2f} deg below horizontal -> {'T-pose' if abs(info['upperarm_l_down_deg']) < 5 else 'A-pose'}")
        out(f"left side on +X (glTF): {info['left_is_plus_x']}; faces {info['faces']}")
        out(f"hip joints above the sole (ball_leaf): {info['hip_above_sole']:.4f} m; ball_leaf above mesh bottom {info['ball_leaf_above_mesh_bottom']:.4f} m")
        out(f"height the game's Fit() gives it, in figure units (x HeroView.HeightOf/1.8 for metres): {info['figure_height_after_fit']:.4f}")
        sk = raw["skins"][0]
        out(f"glTF skin joints ({len(sk['joints'])}, in order): {', '.join(sk['joints'])}")
        out("glTF materials:")
        for m in raw["materials"]:
            tex = m.get("baseColor", {})
            out(f"  {m['name']}: alphaMode {m['alphaMode']}, doubleSided {m['doubleSided']}, baseColor {tex.get('mimeType')} {tex.get('size')}"
                + (", normal map" if "normal" in m else "") + (", metal/rough map" if "metalRough" in m else ""))
        out()

    out("===== DIFFERENCES (new vs current)")
    for d in glb_report.compare(raw_new, raw_old):
        out("  " + d)
    nb, ob = new["bones"], old["bones"]
    if nb == ob:
        out(f"  Blender armature: same {len(nb)} bones with the same parents")
    else:
        for b in sorted(set(nb) | set(ob)):
            if nb.get(b, "-") != ob.get(b, "-"):
                out(f"  bone {b}: parent new {nb.get(b, 'MISSING')!r}, current {ob.get(b, 'MISSING')!r}")
    out(f"  armature object name: new {new['armature']!r}, current {old['armature']!r}")
    for name in sorted(set(new["meshes"]) | set(old["meshes"])):
        a, b = new["meshes"].get(name), old["meshes"].get(name)
        if a is None:
            out(f"  mesh node {name!r}: only in the current model ({b['tris']} tris, {b['materials']})")
        elif b is None:
            out(f"  mesh node {name!r}: only in the new model ({a['tris']} tris, {a['materials']})")
        else:
            out(f"  mesh node {name!r}: tris new {a['tris']} / current {b['tris']}; materials new {a['materials']} / current {b['materials']}")
    out(f"  materials only in new: {sorted(new['materials'] - old['materials'])}; only in current: {sorted(old['materials'] - new['materials'])}")
    for key in ("height", "hip_above_sole", "ball_leaf_above_mesh_bottom", "head_top_above_Head_joint", "figure_height_after_fit",
                "upperarm_l_down_deg", "thigh_l_down_deg"):
        out(f"  {key}: new {new[key]:.4f}, current {old[key]:.4f}")
    for key in ("left_is_plus_x", "faces"):
        out(f"  {key}: new {new[key]}, current {old[key]}" + ("  (same)" if new[key] == old[key] else "  (DIFFERENT)"))

    text = "\n".join(lines)
    print(text)
    if args.report:
        with open(args.report, "w", encoding="utf-8") as f:
            f.write(text + "\n")


if __name__ == "__main__":
    main()
