"""
Reads a .glb (binary glTF 2.0) with nothing but the standard library and reports
what the game cares about: the node tree, the skin's joints in order, the meshes,
materials and textures, triangle counts, the height, the rest pose of the arms and
legs and which way the figure faces.

    python glb_report.py model.glb [--json report.json]
    python glb_report.py new.glb --compare old.glb      (lists every difference)

Runs under any Python 3.8+, including Blender's.
"""

import argparse
import json
import math
import struct
import sys

# ----------------------------------------------------------------------------
# Small matrix helpers (column-major 4x4 as glTF stores them; we use row lists).


def mat_identity():
    return [[1.0 if r == c else 0.0 for c in range(4)] for r in range(4)]


def mat_mul(a, b):
    return [[sum(a[r][k] * b[k][c] for k in range(4)) for c in range(4)] for r in range(4)]


def mat_from_gltf(m):
    # glTF: column-major array of 16.
    return [[m[c * 4 + r] for c in range(4)] for r in range(4)]


def mat_trs(t, q, s):
    x, y, z, w = q
    xx, yy, zz = x * x, y * y, z * z
    xy, xz, yz = x * y, x * z, y * z
    wx, wy, wz = w * x, w * y, w * z
    r = [
        [1 - 2 * (yy + zz), 2 * (xy - wz), 2 * (xz + wy)],
        [2 * (xy + wz), 1 - 2 * (xx + zz), 2 * (yz - wx)],
        [2 * (xz - wy), 2 * (yz + wx), 1 - 2 * (xx + yy)],
    ]
    m = mat_identity()
    for i in range(3):
        for j in range(3):
            m[i][j] = r[i][j] * s[j]
        m[i][3] = t[i]
    return m


def mat_point(m, p):
    return [m[r][0] * p[0] + m[r][1] * p[1] + m[r][2] * p[2] + m[r][3] for r in range(3)]


def mat_inverse(m):
    # General 4x4 inverse (Gauss-Jordan).
    a = [row[:] + [1.0 if r == c else 0.0 for c in range(4)] for r, row in enumerate(m)]
    for col in range(4):
        pivot = max(range(col, 4), key=lambda r: abs(a[r][col]))
        a[col], a[pivot] = a[pivot], a[col]
        p = a[col][col]
        if abs(p) < 1e-12:
            raise ValueError("singular matrix")
        a[col] = [v / p for v in a[col]]
        for r in range(4):
            if r != col:
                f = a[r][col]
                a[r] = [v - f * w for v, w in zip(a[r], a[col])]
    return [row[4:] for row in a]


def sub(a, b):
    return [a[i] - b[i] for i in range(3)]


def length(v):
    return math.sqrt(sum(x * x for x in v))


def fmt(v, digits=3):
    return "(" + ", ".join(f"{x:.{digits}f}" for x in v) + ")"


# ----------------------------------------------------------------------------


def load_glb(path):
    with open(path, "rb") as f:
        data = f.read()
    magic, version, total = struct.unpack_from("<4sII", data, 0)
    if magic != b"glTF":
        raise ValueError(f"{path} is not a binary glTF")
    offset = 12
    doc, binary = None, b""
    while offset < total:
        size, kind = struct.unpack_from("<I4s", data, offset)
        chunk = data[offset + 8: offset + 8 + size]
        if kind == b"JSON":
            doc = json.loads(chunk.decode("utf-8"))
        elif kind == b"BIN\x00":
            binary = chunk
        offset += 8 + size
    return doc, binary


COMPONENTS = {5120: ("b", 1), 5121: ("B", 1), 5122: ("h", 2), 5123: ("H", 2), 5125: ("I", 4), 5126: ("f", 4)}
WIDTH = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}


def read_accessor(doc, binary, index):
    acc = doc["accessors"][index]
    view = doc["bufferViews"][acc["bufferView"]]
    fmt_char, size = COMPONENTS[acc["componentType"]]
    width = WIDTH[acc["type"]]
    stride = view.get("byteStride", size * width)
    start = view.get("byteOffset", 0) + acc.get("byteOffset", 0)
    out = []
    for i in range(acc["count"]):
        out.append(struct.unpack_from("<" + fmt_char * width, binary, start + i * stride))
    return out


def report(path):
    doc, binary = load_glb(path)
    nodes = doc.get("nodes", [])
    parent = {}
    for i, n in enumerate(nodes):
        for c in n.get("children", []):
            parent[c] = i

    local = []
    for n in nodes:
        if "matrix" in n:
            local.append(mat_from_gltf(n["matrix"]))
        else:
            local.append(mat_trs(n.get("translation", [0, 0, 0]), n.get("rotation", [0, 0, 0, 1]), n.get("scale", [1, 1, 1])))

    world = [None] * len(nodes)

    def world_of(i):
        if world[i] is None:
            world[i] = mat_mul(world_of(parent[i]), local[i]) if i in parent else local[i]
        return world[i]

    for i in range(len(nodes)):
        world_of(i)

    scene = doc.get("scenes", [{}])[doc.get("scene", 0)]
    tree = []

    def walk(i, depth):
        n = nodes[i]
        kind = "mesh" if "mesh" in n else ""
        if "skin" in n:
            kind = "skinned mesh"
        extra = []
        if "translation" in n:
            extra.append("t=" + fmt(n["translation"]))
        if "rotation" in n:
            extra.append("r=" + fmt(n["rotation"]))
        if "scale" in n:
            extra.append("s=" + fmt(n["scale"]))
        if "mesh" in n:
            extra.append(f"mesh={doc['meshes'][n['mesh']].get('name', n['mesh'])!r}")
        tree.append(("  " * depth) + f"{n.get('name', '#' + str(i))}" + (f" [{kind}]" if kind else "") + (" " + " ".join(extra) if extra else ""))
        for c in n.get("children", []):
            walk(c, depth + 1)

    for r in scene.get("nodes", []):
        walk(r, 0)

    # Materials and textures.
    images = doc.get("images", [])
    textures = doc.get("textures", [])

    def image_info(tex_index):
        tex = textures[tex_index]
        img = images[tex["source"]] if "source" in tex else {}
        info = {"name": img.get("name"), "mimeType": img.get("mimeType")}
        if "bufferView" in img:
            view = doc["bufferViews"][img["bufferView"]]
            blob = binary[view.get("byteOffset", 0): view.get("byteOffset", 0) + view["byteLength"]]
            info["bytes"] = len(blob)
            if blob[:8] == b"\x89PNG\r\n\x1a\n":
                w, h = struct.unpack(">II", blob[16:24])
                info["size"] = f"{w}x{h}"
            elif blob[:2] == b"\xff\xd8":
                # Walk JPEG markers for SOF.
                p = 2
                while p < len(blob) - 9:
                    if blob[p] != 0xFF:
                        p += 1
                        continue
                    marker = blob[p + 1]
                    seg = struct.unpack(">H", blob[p + 2: p + 4])[0]
                    if marker in (0xC0, 0xC1, 0xC2):
                        h, w = struct.unpack(">HH", blob[p + 5: p + 9])
                        info["size"] = f"{w}x{h}"
                        break
                    p += 2 + seg
        elif "uri" in img:
            info["uri"] = img["uri"]
        return info

    materials = []
    for m in doc.get("materials", []):
        pbr = m.get("pbrMetallicRoughness", {})
        entry = {
            "name": m.get("name"),
            "alphaMode": m.get("alphaMode", "OPAQUE"),
            "doubleSided": m.get("doubleSided", False),
            "baseColorFactor": pbr.get("baseColorFactor"),
        }
        for key, src in (("baseColor", pbr.get("baseColorTexture")), ("metalRough", pbr.get("metallicRoughnessTexture")),
                         ("normal", m.get("normalTexture")), ("occlusion", m.get("occlusionTexture"))):
            if src is not None:
                entry[key] = image_info(src["index"])
        materials.append(entry)

    # Meshes.
    meshes = []
    for mi, mesh in enumerate(doc.get("meshes", [])):
        prims = []
        for p in mesh["primitives"]:
            acc_pos = doc["accessors"][p["attributes"]["POSITION"]]
            tris = (doc["accessors"][p["indices"]]["count"] if "indices" in p else acc_pos["count"]) // 3
            prims.append({
                "material": doc["materials"][p["material"]].get("name") if "material" in p else None,
                "vertices": acc_pos["count"],
                "triangles": tris,
                "attributes": sorted(p["attributes"].keys()),
                "min": acc_pos.get("min"),
                "max": acc_pos.get("max"),
                "morphTargets": len(p.get("targets", [])),
            })
        meshes.append({"name": mesh.get("name"), "primitives": prims})

    mesh_nodes = []
    for i, n in enumerate(nodes):
        if "mesh" in n:
            mesh = meshes[n["mesh"]]
            mesh_nodes.append({
                "node": n.get("name"),
                "mesh": mesh["name"],
                "skin": n.get("skin"),
                "materials": [p["material"] for p in mesh["primitives"]],
                "triangles": sum(p["triangles"] for p in mesh["primitives"]),
                "vertices": sum(p["vertices"] for p in mesh["primitives"]),
            })

    # Skins.
    skins = []
    for si, skin in enumerate(doc.get("skins", [])):
        joints = skin["joints"]
        names = [nodes[j].get("name") for j in joints]
        ibm = read_accessor(doc, binary, skin["inverseBindMatrices"]) if "inverseBindMatrices" in skin else None
        bind = {}
        rest = {}
        for k, j in enumerate(joints):
            rest[names[k]] = mat_point(world[j], [0, 0, 0])
            if ibm:
                bind[names[k]] = mat_point(mat_inverse(mat_from_gltf(ibm[k])), [0, 0, 0])
        jparents = {}
        for k, j in enumerate(joints):
            jparents[names[k]] = nodes[parent[j]].get("name") if j in parent else None
        skins.append({
            "name": skin.get("name"),
            "skeleton": nodes[skin["skeleton"]].get("name") if "skeleton" in skin else None,
            "joints": names,
            "parents": jparents,
            "rest": rest,
            "bind": bind,
        })

    # Body measurements from the skinned body (the largest skinned mesh), in bind space.
    body = None
    for mn in mesh_nodes:
        if mn["skin"] is not None and (body is None or mn["triangles"] > body["triangles"]):
            body = mn
    measure = {}
    if body is not None:
        mesh = next(m for m in meshes if m["name"] == body["mesh"])
        lo = [min(p["min"][a] for p in mesh["primitives"]) for a in range(3)]
        hi = [max(p["max"][a] for p in mesh["primitives"]) for a in range(3)]
        measure["body_min"] = lo
        measure["body_max"] = hi
        measure["height_mesh_space"] = hi[1] - lo[1]
        sk = skins[nodes[[i for i, n in enumerate(nodes) if n.get("name") == body["node"]][0]]["skin"]]
        pos = sk["bind"] or sk["rest"]

        def angle_below_horizontal(a, b):
            d = sub(pos[b], pos[a])
            horiz = math.sqrt(d[0] ** 2 + d[2] ** 2)
            return math.degrees(math.atan2(-d[1], horiz))

        if all(k in pos for k in ("upperarm_l", "lowerarm_l", "hand_l", "upperarm_r", "lowerarm_r")):
            measure["upperarm_l_down_deg"] = angle_below_horizontal("upperarm_l", "lowerarm_l")
            measure["upperarm_r_down_deg"] = angle_below_horizontal("upperarm_r", "lowerarm_r")
            measure["lowerarm_l_down_deg"] = angle_below_horizontal("lowerarm_l", "hand_l")
            measure["arm_forward_l"] = sub(pos["lowerarm_l"], pos["upperarm_l"])[2]
            measure["left_side_x_sign"] = "+X" if pos["upperarm_l"][0] > pos["upperarm_r"][0] else "-X"
        if all(k in pos for k in ("hand_l", "index_01_l", "pinky_01_l", "middle_01_l", "thumb_03_l")):
            # Which way the left palm faces: across the knuckles (index to pinky) and where the thumb is.
            across = sub(pos["pinky_01_l"], pos["index_01_l"])
            measure["hand_l_index_to_pinky"] = [round(x / max(1e-9, length(across)), 3) for x in across]
            thumb = sub(pos["thumb_03_l"], pos["hand_l"])
            measure["hand_l_thumb_offset"] = [round(x, 3) for x in thumb]
            tip = sub(pos.get("middle_04_leaf_l", pos["middle_01_l"]), pos["middle_01_l"])
            measure["middle_finger_l_dir"] = [round(x / max(1e-9, length(tip)), 3) for x in tip]
        if all(k in pos for k in ("foot_l", "ball_l")):
            d = sub(pos["ball_l"], pos["foot_l"])
            axis = max(range(3), key=lambda a: abs(d[a]))
            measure["toes_point"] = ("+" if d[axis] > 0 else "-") + "XYZ"[axis]
        if all(k in pos for k in ("thigh_l", "calf_l", "foot_l")):
            measure["thigh_l_dir"] = [round(x, 3) for x in sub(pos["calf_l"], pos["thigh_l"])]
            measure["leg_length"] = length(sub(pos["calf_l"], pos["thigh_l"])) + length(sub(pos["foot_l"], pos["calf_l"]))
        for name in ("pelvis", "thigh_l", "Head", "neck_01", "upperarm_l", "hand_l", "foot_l", "ball_l", "ball_leaf_l"):
            if name in pos:
                measure["at_" + name] = pos[name]
        if "thigh_l" in pos and "ball_leaf_l" in pos:
            measure["hip_height_above_sole"] = pos["thigh_l"][1] - min(pos["ball_leaf_l"][1], pos.get("ball_leaf_r", pos["ball_leaf_l"])[1])
        # Rest == bind?
        if sk["bind"]:
            worst = max(length(sub(sk["bind"][n], sk["rest"][n])) for n in sk["joints"])
            measure["max_rest_vs_bind_offset"] = worst

    return {
        "file": path,
        "asset": doc.get("asset"),
        "extensionsUsed": doc.get("extensionsUsed", []),
        "tree": tree,
        "mesh_nodes": mesh_nodes,
        "meshes": meshes,
        "materials": materials,
        "skins": [{k: v for k, v in s.items() if k not in ("rest", "bind")} for s in skins],
        "measure": measure,
        "animations": len(doc.get("animations", [])),
    }


def print_report(r):
    print(f"=== {r['file']}")
    print(f"generator: {r['asset'].get('generator')}  extensions: {r['extensionsUsed']}  animations: {r['animations']}")
    print("--- node tree")
    for line in r["tree"]:
        print("  " + line)
    print("--- mesh nodes")
    total = 0
    for mn in r["mesh_nodes"]:
        total += mn["triangles"]
        print(f"  node {mn['node']!r}: mesh {mn['mesh']!r} skin={mn['skin']} materials={mn['materials']} verts={mn['vertices']} tris={mn['triangles']}")
    print(f"  total triangles: {total}")
    print("--- mesh primitives")
    for m in r["meshes"]:
        for p in m["primitives"]:
            print(f"  {m['name']!r}: {p['material']!r} attrs={p['attributes']} morphs={p['morphTargets']} min={fmt(p['min'])} max={fmt(p['max'])}")
    print("--- materials")
    for m in r["materials"]:
        print(f"  {m['name']!r}: alpha={m['alphaMode']} doubleSided={m['doubleSided']} factor={m['baseColorFactor']}")
        for key in ("baseColor", "metalRough", "normal", "occlusion"):
            if key in m:
                print(f"      {key}: {m[key]}")
    print("--- skins")
    for s in r["skins"]:
        print(f"  skin {s['name']!r} skeleton={s['skeleton']!r} joints={len(s['joints'])}")
        print("  joints: " + ", ".join(s["joints"]))
    print("--- measurements (skin bind space, metres if the file is)")
    for k, v in r["measure"].items():
        print(f"  {k}: {fmt(v) if isinstance(v, list) else (f'{v:.4f}' if isinstance(v, float) else v)}")


def compare(new, old):
    diffs = []
    oj = old["skins"][0]["joints"] if old["skins"] else []
    nj = new["skins"][0]["joints"] if new["skins"] else []
    if oj != nj:
        missing = [j for j in oj if j not in nj]
        extra = [j for j in nj if j not in oj]
        if missing:
            diffs.append(f"joints missing in new: {missing}")
        if extra:
            diffs.append(f"joints only in new: {extra}")
        if not missing and not extra:
            diffs.append("same joints, different order")
    else:
        diffs.append(f"joints: identical ({len(nj)}, same order)")
    op = old["skins"][0]["parents"] if old["skins"] else {}
    np_ = new["skins"][0]["parents"] if new["skins"] else {}
    for j in oj:
        if j in np_ and np_[j] != op[j]:
            diffs.append(f"parent of {j}: old {op[j]!r}, new {np_[j]!r}")
    if old["skins"] and new["skins"] and old["skins"][0]["skeleton"] != new["skins"][0]["skeleton"]:
        diffs.append(f"skin.skeleton: old {old['skins'][0]['skeleton']!r}, new {new['skins'][0]['skeleton']!r}")
    on = {m["node"] for m in old["mesh_nodes"]}
    nn = {m["node"] for m in new["mesh_nodes"]}
    diffs.append(f"mesh nodes: old {sorted(on)}  new {sorted(nn)}")
    om = {m["name"] for m in old["materials"]}
    nm = {m["name"] for m in new["materials"]}
    diffs.append(f"materials: old {sorted(om)}  new {sorted(nm)}")
    ot = sum(m["triangles"] for m in old["mesh_nodes"])
    nt = sum(m["triangles"] for m in new["mesh_nodes"])
    diffs.append(f"triangles total: old {ot}  new {nt}")
    for key in ("height_mesh_space", "hip_height_above_sole", "leg_length", "upperarm_l_down_deg", "lowerarm_l_down_deg",
                "left_side_x_sign", "toes_point", "hand_l_index_to_pinky", "hand_l_thumb_offset", "middle_finger_l_dir",
                "thigh_l_dir", "max_rest_vs_bind_offset"):
        o, n = old["measure"].get(key), new["measure"].get(key)
        if isinstance(o, float) or isinstance(n, float):
            diffs.append(f"{key}: old {o if o is None else round(o, 4)}  new {n if n is None else round(n, 4)}")
        elif o != n:
            diffs.append(f"{key}: old {o}  new {n}")
        else:
            diffs.append(f"{key}: same ({o})")
    return diffs


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("glb")
    ap.add_argument("--json")
    ap.add_argument("--compare")
    args = ap.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else None)
    r = report(args.glb)
    print_report(r)
    if args.compare:
        o = report(args.compare)
        print(f"--- differences from {args.compare}")
        for d in compare(r, o):
            print("  " + d)
    if args.json:
        with open(args.json, "w", encoding="utf-8") as f:
            json.dump(r, f, indent=1)
