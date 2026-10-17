"""
Renders comparison previews of body models (.glb), side by side at the same
scale, on a neutral grey background with simple three-point lighting:

    <prefix>-body-front.png   full body, front
    <prefix>-body-side.png    full body, side (each model turned 90 degrees)
    <prefix>-face-front.png   heads, front
    <prefix>-face-34.png      heads, three-quarter
    <prefix>-arms-down.png    full body, front, posed as the game holds the
                              body at rest (arms hanging 0.1 rad out, fingers
                              loosely curled) - shows how the skinning bends

    blender -b --python render_previews.py -- --out out/preview \
        --model "current male=../../Assets/GuWorld/Models/human-male.glb" \
        --model "teen male=out/human-teen-male.glb" ...

Hair meshes (Hair_*) of the current models are hidden so the heads compare
fairly (the new models have none; the game grows hair). --keep-hair shows them.
Uses EEVEE; --cycles renders on the CPU instead.
"""

import argparse
import math
import os
import sys

import bpy
from mathutils import Euler, Vector

GAP = 0.85  # metres between models (full body)
FACE_GAP = 0.32  # metres between heads


def clear():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj)


def import_model(path, label, keep_hair):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.abspath(path))
    # Only what the import put in the scene (it also makes bone-shape meshes outside it).
    objs = [o for o in bpy.context.scene.objects if o not in before]
    holder = bpy.data.objects.new("holder:" + label, None)
    bpy.context.scene.collection.objects.link(holder)
    for o in objs:
        if o.parent is None:
            o.parent = holder
        if o.type == "MESH" and o.name.startswith("Hair") and not keep_hair:
            o.hide_render = True
            o.hide_viewport = True
        if o.type == "MESH":
            # glTF images are linked to Principled BSDF; keep them as they are.
            for slot in o.material_slots:
                if slot.material:
                    slot.material.use_backface_culling = False
    bpy.context.view_layer.update()
    lo = Vector((1e9, 1e9, 1e9))
    hi = -lo
    for o in objs:
        # The skinned meshes only (the importer's bone-shape sphere is not one).
        if o.type == "MESH" and not o.hide_render and o.find_armature() is not None:
            for corner in o.bound_box:
                p = o.matrix_world @ Vector(corner)
                lo = Vector(map(min, lo, p))
                hi = Vector(map(max, hi, p))
    return holder, objs, lo, hi


def head_box(objs):
    """Bounds of the head: the vertices that follow the Head bone (evaluated in rest pose)."""
    lo = Vector((1e9, 1e9, 1e9))
    hi = -lo
    for o in objs:
        if o.type != "MESH" or o.hide_render or "Head" not in o.vertex_groups:
            continue
        gi = o.vertex_groups["Head"].index
        mw = o.matrix_world
        for v in o.data.vertices:
            for g in v.groups:
                if g.group == gi and g.weight > 0.9:
                    p = mw @ v.co
                    lo = Vector(map(min, lo, p))
                    hi = Vector(map(max, hi, p))
    return lo, hi


def add_label(text, x, z, size=0.07):
    curve = bpy.data.curves.new("label", "FONT")
    curve.body = text
    curve.align_x = "CENTER"
    curve.size = size
    obj = bpy.data.objects.new("label:" + text, curve)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = (x, -0.5, z)
    obj.rotation_euler = (math.radians(90), 0, 0)
    mat = bpy.data.materials.new("label")
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (0.85, 0.85, 0.85, 1)
    bsdf.inputs["Roughness"].default_value = 1
    curve.materials.append(mat)
    return obj


def light(name, kind, energy, location, size=1.0, color=(1, 1, 1)):
    data = bpy.data.lights.new(name, kind)
    data.energy = energy
    data.color = color
    if kind == "AREA":
        data.size = size
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = location
    direction = Vector((0, 0, 0.9)) - Vector(location)
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    return obj


def setup_scene(cycles):
    scene = bpy.context.scene
    if cycles:
        scene.render.engine = "CYCLES"
        scene.cycles.device = "CPU"
        scene.cycles.samples = 48
        scene.cycles.use_denoising = True
    else:
        scene.render.engine = "BLENDER_EEVEE_NEXT"
        scene.eevee.taa_render_samples = 32
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    scene.render.film_transparent = False
    world = bpy.data.worlds.new("grey")
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (0.42, 0.42, 0.42, 1)
    bg.inputs["Strength"].default_value = 0.35
    scene.world = world
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGB"


def lights_for(width):
    for obj in [o for o in bpy.data.objects if o.type == "LIGHT"]:
        bpy.data.objects.remove(obj)
    s = max(1.0, width / 2.0)
    light("key", "AREA", 500 * s, (-2.2 * s, -4.0, 3.2), size=3.0 * s, color=(1.0, 0.97, 0.93))
    light("fill", "AREA", 180 * s, (2.6 * s, -3.6, 1.4), size=3.0 * s, color=(0.93, 0.96, 1.0))
    light("rim", "AREA", 400 * s, (0.5, 3.5, 3.0), size=3.0 * s)


def camera(center, height, aspect, name="cam"):
    cam_data = bpy.data.cameras.new(name)
    cam_data.type = "ORTHO"
    cam = bpy.data.objects.new(name, cam_data)
    bpy.context.scene.collection.objects.link(cam)
    cam.location = (center.x, -10, center.z)
    cam.rotation_euler = (math.radians(90), 0, 0)
    cam_data.ortho_scale = max(height * aspect, height) if aspect >= 1 else height
    cam_data.clip_end = 50
    bpy.context.scene.camera = cam
    return cam


def render(path, cam_center, view_h, width_px, height_px):
    scene = bpy.context.scene
    scene.render.resolution_x = width_px
    scene.render.resolution_y = height_px
    scene.render.resolution_percentage = 100
    aspect = width_px / height_px
    for obj in [o for o in bpy.data.objects if o.type == "CAMERA"]:
        bpy.data.objects.remove(obj)
    cam = camera(cam_center, view_h, aspect)
    # ortho_scale is the larger side.
    cam.data.ortho_scale = view_h * aspect if aspect >= 1 else view_h
    scene.render.filepath = os.path.abspath(path)
    bpy.ops.render.render(write_still=True)
    print("[preview] wrote", path, flush=True)


def turn_bone(arm, name, rotation):
    """Turns a pose bone by a rotation given in the armature's space, about its head."""
    from mathutils import Matrix
    pb = arm.pose.bones[name]
    head = pb.head.copy()
    pb.matrix = Matrix.Translation(head) @ rotation.to_matrix().to_4x4() @ Matrix.Translation(-head) @ pb.matrix
    bpy.context.view_layer.update()


def hang_arms(objs):
    """The game's rest pose (HumanBody.Calibrate): arms down, 0.1 rad out from the body,
    plus its relaxed finger curl (0.3 rad per knuckle)."""
    arm = next((o for o in objs if o.type == "ARMATURE"), None)
    if arm is None:
        return
    pose = arm.pose.bones
    for s, side in (("l", 1), ("r", -1)):
        if f"upperarm_{s}" not in pose:
            continue
        # The armature's space: whatever the importer made it; work in world directions.
        mw = arm.matrix_world
        inv = mw.inverted().to_quaternion()
        out = 1 if (mw @ pose[f"upperarm_{s}"].head).x > (mw @ pose["pelvis"].head).x else -1
        down = Vector((out * math.sin(0.1), 0, -math.cos(0.1)))
        for bone, child in ((f"upperarm_{s}", f"lowerarm_{s}"), (f"lowerarm_{s}", f"hand_{s}"), (f"hand_{s}", f"middle_01_{s}")):
            a = mw @ pose[bone].head
            b = mw @ pose[child].head
            q = (b - a).normalized().rotation_difference(down)
            turn_bone(arm, bone, inv @ q @ inv.inverted())


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", action="append", required=True, help="label=path.glb")
    ap.add_argument("--out", required=True, help="path prefix for the PNGs")
    ap.add_argument("--keep-hair", action="store_true")
    ap.add_argument("--cycles", action="store_true")
    args = ap.parse_args(argv)

    clear()
    setup_scene(args.cycles)
    models = []
    for i, spec in enumerate(args.model):
        label, path = spec.split("=", 1)
        holder, objs, lo, hi = import_model(path, label, args.keep_hair)
        models.append({"label": label, "holder": holder, "objs": objs, "lo": lo, "hi": hi})
    n = len(models)
    xs = [(i - (n - 1) / 2) * GAP for i in range(n)]
    for m, x in zip(models, xs):
        m["holder"].location.x = x
    bpy.context.view_layer.update()
    for m in models:
        m["head"] = head_box(m["objs"])

    tallest = max(m["hi"].z for m in models)
    width = (n - 1) * GAP + 0.9
    lights_for(width)
    labels = [add_label(f"{m['label']}  {m['hi'].z - m['lo'].z:.2f} m", x, tallest + 0.06) for m, x in zip(models, xs)]

    os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
    view_h = tallest + 0.25
    px_h = 1100
    px_w = int(px_h * width / view_h)
    centre = Vector((0, 0, view_h / 2 - 0.05))
    render(args.out + "-body-front.png", centre, view_h, px_w, px_h)

    for m in models:
        m["holder"].rotation_euler = Euler((0, 0, math.radians(90)))  # facing the camera's right
    bpy.context.view_layer.update()
    render(args.out + "-body-side.png", centre, view_h, px_w, px_h)

    # Heads, closer together: the band from the lowest chin to the highest crown.
    for lbl in labels:
        lbl.hide_render = True
    fx = [(i - (n - 1) / 2) * FACE_GAP for i in range(n)]
    # Same scale, but each model raised or lowered so the heads line up.
    level = max((m["head"][0].z + m["head"][1].z) / 2 for m in models)
    for m, x in zip(models, fx):
        m["holder"].rotation_euler = Euler((0, 0, 0))
        m["holder"].location.x = x
        m["holder"].location.z = level - (m["head"][0].z + m["head"][1].z) / 2
    bpy.context.view_layer.update()
    for m in models:
        m["head"] = head_box(m["objs"])
    lo = min(m["head"][0].z for m in models) - 0.07
    hi = max(m["head"][1].z for m in models) + 0.03
    face_h = hi - lo
    face_w = (n - 1) * FACE_GAP + 0.3
    face_centre = Vector((0, 0, (lo + hi) / 2))
    fpx_h = 1000
    fpx_w = int(fpx_h * face_w / face_h)
    render(args.out + "-face-front.png", face_centre, face_h, fpx_w, fpx_h)

    for m in models:
        m["holder"].rotation_euler = Euler((0, 0, math.radians(-35)))
    bpy.context.view_layer.update()
    render(args.out + "-face-34.png", face_centre, face_h, fpx_w, fpx_h)

    # The arms down, as the game holds the body.
    for m, x in zip(models, xs):
        m["holder"].rotation_euler = Euler((0, 0, 0))
        m["holder"].location = (x * 0.75, 0, 0)
        hang_arms(m["objs"])
    for lbl, x in zip(labels, xs):
        lbl.hide_render = False
        lbl.location.x = x * 0.75
    bpy.context.view_layer.update()
    render(args.out + "-arms-down.png", centre, view_h, int(px_w * 0.75), px_h)


if __name__ == "__main__":
    main()
