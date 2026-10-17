"""
Builds a teenage East Asian body for Gu World with MakeHuman's Blender plugin
(MPFB2) and exports it as a drop-in replacement for the Quaternius models in
Assets/GuWorld/Models (same joint names, order and hierarchy, T-pose, metres,
Y-up, facing +Z, the node/material names the game looks for).

Run headless with the portable Blender that has MPFB installed:

    blender -b --python build_teen.py -- --gender male --out out/human-teen-male.glb
    blender -b --python build_teen.py -- --gender female --out out/human-teen-female.glb

Options:
    --params file.json   overrides for the preset (same keys as PRESETS below, merged)
    --blend file.blend   also save the finished scene (for looking at it in Blender)

Everything it uses is CC0: the MakeHuman base mesh and targets that ship with
MPFB, and the MakeHuman system assets (skins, eyes, eyebrows, eyelashes).
"""

import argparse
import copy
import importlib
import json
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

# ---------------------------------------------------------------------------
# The two teenagers.
#
# MakeHuman's age macro: 0 = 1 year, 0.1875 = 11, 0.5 = 25, 1 = 90 years,
# linear in between, so 16 years = 1 + 48 * age -> age = 0.3125.
# Heights are reached exactly by scaling the finished body (MakeHuman's own
# height macro stays near the middle so the proportions stay natural).

AGE_16 = (16 - 1) / 48.0
# MakeHuman's own "16" blends 64 % of its 11-year-old target and reads as 13-14
# (a child's chest and hips); a little older on its scale looks like a real 16.
AGE_MALE = 0.34    # ~17.3 on MakeHuman's scale
AGE_FEMALE = 0.36  # ~18.3 on MakeHuman's scale


def both(target, weight):
    """A left/right pair of MakeHuman targets ("eye-scale-incr" -> l-eye-scale-incr, r-eye-scale-incr)."""
    return {"l-" + target: weight, "r-" + target: weight}


PRESETS = {
    "male": {
        "name": "TeenMale",
        "phenotype": {
            "gender": 1.0, "age": AGE_MALE, "muscle": 0.3, "weight": 0.4,
            "proportions": 0.5, "height": 0.5, "cupsize": 0.5, "firmness": 0.5,
            "race": {"asian": 1.0, "caucasian": 0.0, "african": 0.0},
        },
        # Detail targets on top of the macros (name -> weight): a youthful face, a touch
        # anime-friendly - bigger eyes with a slight upward tilt, lower and bolder brows,
        # a smaller nose, a smaller and thinner-lipped mouth, a slimmer jaw and neck.
        "targets": {
            **both("eye-scale-incr", 0.35), **both("eye-height2-incr", 0.1), **both("eye-corner2-up", 0.25),
            "eyebrows-trans-down": 0.35,
            "nose-scale-horiz-decr": 0.3, "nose-nostrils-width-decr": 0.4, "nose-point-width-decr": 0.3,
            "nose-volume-decr": 0.2,
            "mouth-scale-horiz-decr": 0.15, "mouth-lowerlip-volume-decr": 0.5, "mouth-upperlip-volume-decr": 0.4,
            "mouth-angles-up": 0.35, "mouth-scale-depth-decr": 0.3, "mouth-scale-vert-decr": 0.2,
            "mouth-trans-backward": 0.15, "mouth-lowerlip-height-decr": 0.3, "mouth-upperlip-height-decr": 0.2,
            **both("cheek-volume-decr", 0.15), "chin-width-decr": 0.15, "chin-prominent-incr": 0.2,
            "head-invertedtriangular": 0.2, "head-square": 0.15, "head-age-decr": 0.15,
            "neck-scale-horiz-decr": 0.15,
        },
        "height_m": 1.70,
        "skin": "young_asian_male",
        "eye_material": "brown",
        # MakeHuman's brown is a red-brown: recoloured dark brown (multiplier of the iris' brightness).
        "iris_tint": [1.45, 0.88, 0.5],
        "eyebrows": "eyebrow004",
        "eyelashes": "eyelashes01",
        # Alpha below this is cut (glTF alphaMode MASK) on the brows and lashes.
        "alpha_cutoff": 0.15,
        # How far the brows' card stands off the skin (m).
        "brow_gap_m": 0.0012,
        # The brows' stroke drawn this much thicker than MakeHuman's.
        "brow_thickness": 1.6,
        "body_node": "SuperHero_Male",
        "skin_material": "MI_Superhero_Male",
    },
    "female": {
        "name": "TeenFemale",
        "phenotype": {
            "gender": 0.0, "age": AGE_FEMALE, "muscle": 0.3, "weight": 0.42,
            "proportions": 0.5, "height": 0.5, "cupsize": 0.5, "firmness": 0.6,
            "race": {"asian": 1.0, "caucasian": 0.0, "african": 0.0},
        },
        "targets": {
            **both("eye-scale-incr", 0.4), **both("eye-height2-incr", 0.12), **both("eye-corner2-up", 0.25),
            "eyebrows-trans-down": 0.2,
            "nose-scale-horiz-decr": 0.35, "nose-nostrils-width-decr": 0.4, "nose-point-width-decr": 0.35,
            "nose-volume-decr": 0.3, "nose-scale-vert-decr": 0.15,
            "mouth-scale-horiz-decr": 0.25, "mouth-lowerlip-volume-decr": 0.2, "mouth-upperlip-volume-decr": 0.2,
            "mouth-angles-up": 0.35, "mouth-scale-depth-decr": 0.2, "mouth-trans-backward": 0.1,
            **both("cheek-volume-decr", 0.1), "chin-width-decr": 0.45,
            "head-invertedtriangular": 0.3, "head-oval": 0.4, "head-age-decr": 0.25,
            "neck-scale-horiz-decr": 0.25,
        },
        "height_m": 1.60,
        "skin": "young_asian_female",
        "eye_material": "brown",
        "iris_tint": [1.45, 0.88, 0.5],
        "eyebrows": "eyebrow001",
        "eyelashes": "eyelashes02",
        "alpha_cutoff": 0.15,
        "brow_gap_m": 0.0012,
        "brow_thickness": 1.3,
        "body_node": "SuperHero_Female",
        "skin_material": "MI_Superhero_Female",
    },
}

# The game's skeleton (Quaternius / Unreal-like names), in the exact joint order
# of the current models, each with its parent. MPFB's "game_engine" rig has the
# same bones except the names Root/head and the *_leaf end bones, made here.
JOINTS = [
    ("root", None), ("pelvis", "root"), ("spine_01", "pelvis"), ("spine_02", "spine_01"),
    ("spine_03", "spine_02"), ("neck_01", "spine_03"), ("Head", "neck_01"),
]
for _s in ("l", "r"):
    JOINTS += [(f"clavicle_{_s}", "spine_03"), (f"upperarm_{_s}", f"clavicle_{_s}"),
               (f"lowerarm_{_s}", f"upperarm_{_s}"), (f"hand_{_s}", f"lowerarm_{_s}")]
    for _f in ("index", "middle", "pinky", "ring", "thumb"):
        JOINTS += [(f"{_f}_01_{_s}", f"hand_{_s}"), (f"{_f}_02_{_s}", f"{_f}_01_{_s}"),
                   (f"{_f}_03_{_s}", f"{_f}_02_{_s}"), (f"{_f}_04_leaf_{_s}", f"{_f}_03_{_s}")]
for _s in ("l", "r"):
    JOINTS += [(f"thigh_{_s}", "pelvis"), (f"calf_{_s}", f"thigh_{_s}"), (f"foot_{_s}", f"calf_{_s}"),
               (f"ball_{_s}", f"foot_{_s}"), (f"ball_leaf_{_s}", f"ball_{_s}")]
assert len(JOINTS) == 65

# Game name -> MPFB game_engine name, where they differ.
FROM_MPFB = {"root": "Root", "Head": "head"}


def mpfb(module):
    return importlib.import_module("bl_ext.user_default.mpfb.services." + module)


HumanService = mpfb("humanservice").HumanService
RigService = mpfb("rigservice").RigService
ObjectService = mpfb("objectservice").ObjectService
LocationService = mpfb("locationservice").LocationService


def log(*args):
    print("[teen]", *args, flush=True)


# ---------------------------------------------------------------------------


def make_human(preset):
    """The MakeHuman body with rig, eyes, brows and lashes, as MPFB builds it (A-pose)."""
    info = HumanService._create_default_human_info_dict()
    info["name"] = preset["name"]
    info["phenotype"] = copy.deepcopy(preset["phenotype"])
    info["targets"] = [{"target": k, "value": v} for k, v in preset["targets"].items()]
    info["rig"] = "game_engine"
    info["eyes"] = "high-poly/high-poly.mhclo"
    info["eyebrows"] = f"{preset['eyebrows']}/{preset['eyebrows']}.mhclo"
    info["eyelashes"] = f"{preset['eyelashes']}/{preset['eyelashes']}.mhclo"
    info["skin_material_type"] = "NONE"
    info["eyes_material_type"] = "NONE"

    settings = HumanService.get_default_deserialization_settings()
    settings["subdiv_levels"] = 0
    settings["load_clothes"] = False
    settings["scale"] = 0.1  # MakeHuman decimetres -> metres

    basemesh = HumanService.deserialize_from_dict(info, settings)
    rig = ObjectService.find_object_of_type_amongst_nearest_relatives(basemesh, "Skeleton")
    parts = {}
    for kind in ("Eyes", "Eyebrows", "Eyelashes"):
        parts[kind] = ObjectService.find_object_of_type_amongst_nearest_relatives(basemesh, kind)
        if parts[kind] is None:
            raise RuntimeError(f"MPFB did not add the {kind}")
    return basemesh, rig, parts


def update():
    bpy.context.view_layer.update()


def turn_bone(rig, name, rotation):
    """Turns a pose bone (and so its children) by a rotation in the armature's space
    (the rig sits at the origin unrotated, so that is world space) about its head."""
    pb = rig.pose.bones[name]
    head = pb.head.copy()
    m = Matrix.Translation(head) @ rotation.to_matrix().to_4x4() @ Matrix.Translation(-head)
    pb.matrix = m @ pb.matrix
    update()


def aim_bone(rig, name, direction):
    """Turns a pose bone the shortest way so it points along `direction`."""
    pb = rig.pose.bones[name]
    current = (pb.tail - pb.head).normalized()
    turn_bone(rig, name, current.rotation_difference(direction.normalized()))


def t_pose(rig):
    """
    The game's rest pose (that of the current models): arms straight out to the
    sides, palms down, fingers straight; legs straight down, feet flat and forward.
    MakeHuman's own rest pose is an A-pose with bent elbows.
    """
    for pb in rig.pose.bones:
        pb.rotation_mode = "QUATERNION"
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.location = (0, 0, 0)
        pb.scale = (1, 1, 1)
    update()
    pose = rig.pose.bones

    # The feet keep their world orientation (flat, forward) while the legs are straightened.
    feet = {s: pose[f"foot_{s}"].matrix.to_quaternion() for s in "lr"}

    for s, side in (("l", 1), ("r", -1)):
        out = Vector((side, 0, 0))

        # Arms: straight out along X.
        aim_bone(rig, f"upperarm_{s}", out)
        aim_bone(rig, f"lowerarm_{s}", out)
        aim_bone(rig, f"hand_{s}", out)

        # Palm down: the knuckles' line (index -> pinky) runs backwards (+Y), level.
        across = pose[f"pinky_01_{s}"].head - pose[f"index_01_{s}"].head
        across_yz = Vector((0, across.y, across.z)).normalized()
        angle = math.atan2(across_yz.z, across_yz.y)
        turn_bone(rig, f"hand_{s}", Matrix.Rotation(-angle, 4, Vector((1, 0, 0))).to_quaternion())
        thumb_below = pose[f"thumb_03_{s}"].head.z < pose[f"hand_{s}"].head.z
        if not thumb_below:
            # The palm faces up: roll the hand over.
            turn_bone(rig, f"hand_{s}", Matrix.Rotation(math.pi, 4, Vector((1, 0, 0))).to_quaternion())

        # The middle finger straight on from the forearm (a neutral wrist), as in the current models.
        mid = pose[f"middle_03_{s}"].tail - pose[f"middle_01_{s}"].head
        turn = math.atan2(0, side) - math.atan2(mid.y, mid.x)
        turn = (turn + math.pi) % (2 * math.pi) - math.pi
        turn_bone(rig, f"hand_{s}", Matrix.Rotation(turn, 4, Vector((0, 0, 1))).to_quaternion())

        # Fingers: straight, flat in the palm's plane, keeping their spread.
        for finger in ("index", "middle", "ring", "pinky"):
            base = pose[f"{finger}_01_{s}"]
            tip = pose[f"{finger}_03_{s}"].tail
            d = tip - base.head
            d = Vector((d.x, d.y, 0)).normalized()
            for k in ("01", "02", "03"):
                aim_bone(rig, f"{finger}_{k}_{s}", d)

        # Legs: straight down.
        aim_bone(rig, f"thigh_{s}", Vector((0, 0, -1)))
        aim_bone(rig, f"calf_{s}", Vector((0, 0, -1)))
        foot = pose[f"foot_{s}"]
        now = foot.matrix.to_quaternion()
        turn_bone(rig, f"foot_{s}", feet[s] @ now.inverted())

        # The toes point straight ahead (-Y in Blender = +Z in glTF).
        d = pose[f"ball_{s}"].tail - pose[f"foot_{s}"].head
        yaw = math.atan2(d.x, -d.y)
        turn_bone(rig, f"foot_{s}", Matrix.Rotation(-yaw, 4, Vector((0, 0, 1))).to_quaternion())


def apply_rest(rig):
    """Bakes the shape keys and the pose into the meshes and makes the pose the rest pose."""
    RigService.apply_pose_as_rest_pose(rig)


def bake_meshes(objects):
    """Applies every remaining modifier but the armature (helpers hidden -> deleted) and moves the meshes into world space."""
    for obj in objects:
        ObjectService.deselect_and_deactivate_all()
        ObjectService.activate_blender_object(obj)
        if obj.data.shape_keys is not None:
            obj.shape_key_add(name="baked", from_mix=True)
            for key in list(obj.data.shape_keys.key_blocks):
                obj.shape_key_remove(key)
        for mod in list(obj.modifiers):
            if mod.type == "ARMATURE":
                obj.modifiers.remove(mod)
            else:
                bpy.ops.object.modifier_apply(modifier=mod.name)
        mw = obj.matrix_world.copy()
        obj.parent = None
        obj.data.transform(mw)
        obj.matrix_world = Matrix.Identity(4)


def remove_cornea(eyes):
    """The high-poly eyes have a see-through cornea shell over the eyeball (the blue disc in the
    texture's corner); the game draws opaque eyes, so only the eyeball is kept."""
    mesh = eyes.data
    uv = mesh.uv_layers.active.data
    drop = []
    for poly in mesh.polygons:
        u = sum(uv[i].uv[0] for i in poly.loop_indices) / poly.loop_total
        v = sum(uv[i].uv[1] for i in poly.loop_indices) / poly.loop_total
        if u > 0.85 and v < 0.15:
            drop.append(poly.index)
    log(f"eyes: {len(mesh.polygons)} faces, cornea {len(drop)} removed")
    import bmesh
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.faces.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.faces[i] for i in drop], context="FACES")
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context="VERTS")
    bm.to_mesh(mesh)
    bm.free()


def thicken_brows(card, factor):
    """MakeHuman's brows are thin pencil lines; anime brows are bolder. Each brow's card is
    stretched up and down about its own middle line (the vertices within a few mm across
    give the line's height there), so the painted stroke gets thicker, not longer."""
    if abs(factor - 1) < 1e-3:
        return
    verts = [v.co.copy() for v in card.data.vertices]
    for i, v in enumerate(card.data.vertices):
        near = [p.z for p in verts if abs(p.x - v.co.x) < 0.004 and p.x * v.co.x > 0]
        middle = sum(near) / len(near)
        v.co.z = middle + (verts[i].z - middle) * factor


def lift_off_skin(card, body, gap):
    """Moves the vertices of a card mesh (the brows) that lie in or too close to the skin out
    along the skin's normal to `gap` above it, so the card is not buried (MakeHuman fits the
    brows flush with the skin, and partly under it once the face is reshaped)."""
    from mathutils.bvhtree import BVHTree
    tree = BVHTree.FromPolygons([v.co.copy() for v in body.data.vertices], [p.vertices[:] for p in body.data.polygons])
    moved = 0
    for v in card.data.vertices:
        loc, normal, _, _ = tree.find_nearest(v.co)
        if loc is None:
            continue
        side = (v.co - loc).dot(normal)
        if side < gap:
            v.co = v.co + normal * (gap - side)
            moved += 1
    log(f"{card.name}: {moved} of {len(card.data.vertices)} vertices lifted to {gap * 1000:.1f} mm off the skin")


def merge(into, other):
    """Joins `other` into `into` (keeps into's name)."""
    ObjectService.deselect_and_deactivate_all()
    other.select_set(True)
    into.select_set(True)
    bpy.context.view_layer.objects.active = into
    bpy.ops.object.join()


def rebuild_armature(rig, meshes, scale, lift):
    """
    A new armature with the game's 65 joints, created in the game's joint order
    (the glTF exporter writes joints depth-first in creation order), from the
    MPFB bones' rest positions; the *_leaf bones are added at the finger tips and
    toe tips. Everything is moved up by `lift` (soles on the ground) and scaled.
    """
    def place(p):
        return (Vector(p) + Vector((0, 0, lift))) * scale

    mw = rig.matrix_world.copy()
    source = {}
    for bone in rig.data.bones:
        source[bone.name] = (mw @ bone.head_local, mw @ bone.tail_local)
    ObjectService.deselect_and_deactivate_all()
    ObjectService.activate_blender_object(rig)
    bpy.ops.object.mode_set(mode="EDIT")
    rolls = {eb.name: eb.roll for eb in rig.data.edit_bones}
    bpy.ops.object.mode_set(mode="OBJECT")

    data = bpy.data.armatures.new("Armature")
    arm = bpy.data.objects.new("Armature", data)
    bpy.context.scene.collection.objects.link(arm)
    ObjectService.deselect_and_deactivate_all()
    ObjectService.activate_blender_object(arm)
    bpy.ops.object.mode_set(mode="EDIT")
    made = {}
    for name, parent in JOINTS:
        eb = data.edit_bones.new(name)
        src = FROM_MPFB.get(name, name)
        if src in source:
            head, tail = source[src]
            eb.head, eb.tail = place(head), place(tail)
            eb.roll = rolls[src]
            if name == "root":
                # On the ground between the feet, as the current models' root.
                eb.tail = eb.tail - Vector((eb.head.x, eb.head.y, eb.head.z))
                eb.head = Vector((0, 0, 0))
        else:
            # A leaf: from its parent's tail, on along its parent, half as long.
            p = made[parent]
            direction = p.tail - p.head
            eb.head = p.tail.copy()
            eb.tail = p.tail + direction * 0.5
            eb.roll = p.roll
            eb.use_deform = False
        if parent is not None:
            eb.parent = made[parent]
        eb.use_connect = False
        made[name] = eb
    bpy.ops.object.mode_set(mode="OBJECT")

    # The meshes follow the new skeleton.
    bones = {name for name, _ in JOINTS}
    to_game = {mpfb_name: name for name, mpfb_name in FROM_MPFB.items()}
    for obj in meshes:
        obj.data.transform(Matrix.Scale(scale, 4) @ Matrix.Translation((0, 0, lift)))
        for group in list(obj.vertex_groups):
            new_name = to_game.get(group.name, group.name)
            if new_name in bones:
                group.name = new_name
            else:
                obj.vertex_groups.remove(group)
        clean_weights(obj)
        obj.parent = arm
        obj.matrix_parent_inverse = Matrix.Identity(4)
        mod = obj.modifiers.new("Armature", "ARMATURE")
        mod.object = arm

    bpy.data.objects.remove(rig)
    return arm


def clean_weights(obj, limit=4):
    """At most four bones per vertex, weights summing to one (what Unity skins with)."""
    names = {g.index: g.name for g in obj.vertex_groups}
    groups = obj.vertex_groups
    unweighted = 0
    for v in obj.data.vertices:
        ws = sorted(((g.weight, g.group) for g in v.groups if g.weight > 1e-5), reverse=True)
        keep = ws[:limit]
        total = sum(w for w, _ in keep)
        for gi in [g.group for g in v.groups]:
            groups[names[gi]].remove([v.index])
        if total <= 0:
            unweighted += 1
            continue
        for w, gi in keep:
            groups[names[gi]].add([v.index], w / total, "REPLACE")
    if unweighted:
        log(f"warning: {obj.name}: {unweighted} vertices without bone weights")


# ---------------------------------------------------------------------------
# Materials: plain Principled BSDF + one texture, which the glTF exporter writes
# as metallic-roughness PBR (no extensions).


def user_data(*parts):
    return os.path.join(LocationService.get_user_data(), *parts)


def load_texture(path, work, jpeg=False, size=None, recolor=None):
    image = bpy.data.images.load(path, check_existing=True)
    image.pixels[0]  # loads the pixels (images load lazily)
    if size and (image.size[0] != size):
        image.scale(size, size)
    if recolor is not None:
        import numpy as np
        w, h = image.size
        px = np.empty(w * h * 4, dtype=np.float32)
        image.pixels.foreach_get(px)
        px = recolor(px.reshape(h, w, 4))
        image.pixels.foreach_set(px.ravel())
        image.update()
    if jpeg:
        os.makedirs(work, exist_ok=True)
        out = os.path.join(work, os.path.splitext(os.path.basename(path))[0] + ".jpg")
        image.filepath_raw = out
        image.file_format = "JPEG"
        image.save(filepath=out, quality=92)
        bpy.data.images.remove(image)
        image = bpy.data.images.load(out)
    return image


def tint_iris(tint):
    """The eye texture's iris (its strongly coloured texels; the white, the veins' pale pink and
    the grey around stay) recoloured to `tint` times its brightness: MakeHuman's "brown" is a
    red-brown, East Asian eyes are a dark brown."""
    import numpy as np

    def recolor(px):
        rgb = px[..., :3]
        hi, lo = rgb.max(axis=2), rgb.min(axis=2)
        sat = (hi - lo) / np.maximum(hi, 1e-4)
        w = np.clip((sat - 0.3) / 0.2, 0, 1)[..., None]
        lum = (0.299 * rgb[..., 0] + 0.587 * rgb[..., 1] + 0.114 * rgb[..., 2])[..., None]
        target = np.clip(lum * np.array(tint, dtype=np.float32), 0, 1)
        px[..., :3] = rgb * (1 - w) + target * w
        return px

    return recolor


def make_material(name, image, roughness=0.6, cutoff=None, double_sided=True):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = image
    links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = 0.0
    links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    if cutoff is not None:
        # Alpha clip as the glTF exporter reads it: 1 - (alpha < cutoff) -> alphaMode MASK.
        less = nodes.new("ShaderNodeMath")
        less.operation = "LESS_THAN"
        less.inputs[1].default_value = cutoff
        one_minus = nodes.new("ShaderNodeMath")
        one_minus.operation = "SUBTRACT"
        one_minus.inputs[0].default_value = 1.0
        links.new(tex.outputs["Alpha"], less.inputs[0])
        links.new(less.outputs[0], one_minus.inputs[1])
        links.new(one_minus.outputs[0], bsdf.inputs["Alpha"])
        mat.surface_render_method = "DITHERED"
    mat.use_backface_culling = not double_sided
    return mat


def set_material(obj, mat):
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    for poly in obj.data.polygons:
        poly.material_index = 0


# ---------------------------------------------------------------------------


def measure(obj):
    zs = [v.co.z for v in obj.data.vertices]
    return min(zs), max(zs)


def build(preset, out_path, blend_path=None):
    # An empty scene (the startup file's cube, camera and light go; the add-ons stay).
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj)
    work = os.path.join(os.path.dirname(os.path.abspath(out_path)), "work")

    basemesh, rig, parts = make_human(preset)
    log("MPFB human made:", basemesh.name, rig.name, [p.name for p in parts.values()])

    t_pose(rig)
    apply_rest(rig)

    meshes = [basemesh, parts["Eyes"], parts["Eyebrows"], parts["Eyelashes"]]
    bake_meshes(meshes)
    remove_cornea(parts["Eyes"])

    low, high = measure(basemesh)
    height = high - low
    scale = preset["height_m"] / height
    log(f"MakeHuman height {height:.4f} m -> scale {scale:.4f} to {preset['height_m']} m")

    arm = rebuild_armature(rig, meshes, scale, -low)

    body, eyes, brows, lashes = meshes
    thicken_brows(brows, preset["brow_thickness"])
    lift_off_skin(brows, body, preset["brow_gap_m"])

    # Names and materials as the game expects.
    body.name = body.data.name = preset["body_node"]
    eyes.name = eyes.data.name = "Eyes"
    brows.name = brows.data.name = "Eyebrows"

    skin_dir = user_data("skins", preset["skin"])
    skin_png = next(f for f in os.listdir(skin_dir) if f.lower().endswith(".png"))
    skin = make_material(preset["skin_material"], load_texture(os.path.join(skin_dir, skin_png), work, jpeg=True), roughness=0.6)
    set_material(body, skin)
    eye_tex = load_texture(user_data("eyes", "materials", preset["eye_material"] + "_eye.png"), work, jpeg=True, size=512,
                           recolor=tint_iris(preset["iris_tint"]) if preset.get("iris_tint") else None)
    set_material(eyes, make_material("MI_Eyes", eye_tex, roughness=0.25))
    brow_tex = load_texture(user_data("eyebrows", preset["eyebrows"], preset["eyebrows"] + ".png"), work)
    set_material(brows, make_material("MI_Hair_Eyebrows", brow_tex, roughness=0.8, cutoff=preset["alpha_cutoff"]))
    lash_tex = load_texture(user_data("eyelashes", preset["eyelashes"], preset["eyelashes"] + ".png"), work)
    set_material(lashes, make_material("MI_Hair_Eyelashes", lash_tex, roughness=0.8, cutoff=preset["alpha_cutoff"]))
    # The lashes ride with the brows (the eyes' mesh is measured for the eye centres).
    merge(brows, lashes)
    brows.name = brows.data.name = "Eyebrows"

    for obj in (body, eyes, brows):
        for poly in obj.data.polygons:
            poly.use_smooth = True

    # Nothing else in the scene.
    keep = {arm, body, eyes, brows}
    for obj in list(bpy.data.objects):
        if obj not in keep:
            bpy.data.objects.remove(obj)

    os.makedirs(os.path.dirname(os.path.abspath(out_path)), exist_ok=True)
    if blend_path:
        bpy.context.preferences.filepaths.save_version = 0  # no .blend1 backups
        bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(blend_path))
    export(out_path, keep)

    low, high = measure(body)
    log(f"done: {out_path}  height {high - low:.4f} m")


def export(path, objects):
    ObjectService.deselect_and_deactivate_all()
    for obj in objects:
        obj.select_set(True)
    bpy.ops.export_scene.gltf(
        filepath=os.path.abspath(path),
        export_format="GLB",
        use_selection=True,
        export_yup=True,
        export_apply=False,
        export_animations=False,
        export_skins=True,
        export_def_bones=False,
        export_leaf_bone=False,
        export_rest_position_armature=True,
        export_influence_nb=4,
        export_morph=False,
        export_materials="EXPORT",
        export_image_format="AUTO",
        export_texcoords=True,
        export_normals=True,
        export_tangents=False,
        export_attributes=False,
        export_extras=False,
        export_cameras=False,
        export_lights=False,
    )


def merge_dict(base, extra):
    for k, v in extra.items():
        if isinstance(v, dict) and isinstance(base.get(k), dict) and k != "targets":
            merge_dict(base[k], v)
        else:
            base[k] = v
    return base


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument("--gender", choices=sorted(PRESETS), required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--params")
    ap.add_argument("--blend")
    args = ap.parse_args(argv)
    preset = copy.deepcopy(PRESETS[args.gender])
    if args.params:
        with open(args.params, encoding="utf-8") as f:
            merge_dict(preset, json.load(f).get(args.gender, {}))
    log("preset:", json.dumps(preset))
    build(preset, args.out, args.blend)
