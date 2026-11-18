"""Turn Meshy's rigged fox into the game's dancing fox: blender -b --python tools/blender_rig_fox.py

Input, in assets/models/fox/source/ (from tools/meshy_rig.py; not in git, Godot skips it):
rigged.glb (Meshy humanoid rig) and the in-place side-step animations anim_694.glb
("Walk Left") and anim_617.glb ("Cautious Crouch Walk Left").
Adds a four-bone tail chain (Tail1..Tail4, under Hips) and skins the tail to it, blending into
the hips at the root; keeps both animations as actions SideStep and CrouchStep; shrinks the
textures; writes assets/models/fox/fox.glb. The game sways the tail and holds the arms out in
code (FoxDance.cs).
"""
import bpy, os, sys
import numpy as np
from mathutils import Vector
from mathutils.geometry import intersect_point_line

SRC = os.environ.get("FOX_SRC", "assets/models/fox/source")
OUT = os.environ.get("FOX_OUT", "assets/models/fox/fox.glb")

bpy.ops.wm.read_factory_settings(use_empty=True)

def import_glb(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    return [o for o in bpy.data.objects if o not in before]

objs = import_glb(os.path.join(SRC, "rigged.glb"))
for o in objs:
    if o.type == "MESH" and o.parent is None:      # Meshy's preview sphere
        bpy.data.objects.remove(o)
arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
mesh = next(o for o in bpy.data.objects if o.type == "MESH" and o.parent == arm)
for a in list(bpy.data.actions):
    bpy.data.actions.remove(a)

# --- the tail chain, along the middle of the tail (world space, metres; the fox faces -Y)
joints = [Vector(p) for p in [(-0.05, 0.14, 0.88), (0.00, 0.24, 0.62), (0.12, 0.28, 0.40),
                              (0.24, 0.29, 0.24), (0.36, 0.30, 0.10)]]
inv = arm.matrix_world.inverted()
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode="EDIT")
eb = arm.data.edit_bones
parent = eb["Hips"]
names = []
for i in range(4):
    b = eb.new(f"Tail{i + 1}")
    b.head = inv @ joints[i]
    b.tail = inv @ joints[i + 1]
    b.parent = parent
    b.use_connect = i > 0
    parent = b
    names.append(b.name)
bpy.ops.object.mode_set(mode="OBJECT")

# --- skin the tail: every vertex behind the legs below the hips
mw = mesh.matrix_world
groups = {n: mesh.vertex_groups.get(n) or mesh.vertex_groups.new(name=n) for n in names}
hips = mesh.vertex_groups["Hips"]
count = 0
for v in mesh.data.vertices:
    p = mw @ v.co
    if not (p.y > 0.10 and p.z < 0.92):
        continue
    # position along the chain: s in [0, 4]
    best, s_best = 1e9, 0.0
    for i in range(4):
        q, t = intersect_point_line(p, joints[i], joints[i + 1])
        t = max(0.0, min(1.0, t))
        d = (p - (joints[i] + (joints[i + 1] - joints[i]) * t)).length
        if d < best:
            best, s_best = d, i + t
    # drop whatever it was skinned to, then share between the two nearest tail bones
    for g in list(v.groups):
        mesh.vertex_groups[g.group].remove([v.index])
    k = min(int(s_best), 3)
    f = s_best - k
    w = {names[k]: 1.0 - f}
    if k + 1 < 4:
        w[names[k + 1]] = f
    else:
        w[names[k]] = 1.0
    root = max(0.0, 1.0 - s_best / 0.8)              # near the hips the body leads
    for n, x in w.items():
        if x * (1 - root) > 0.001:
            groups[n].add([v.index], x * (1 - root), "REPLACE")
    if root > 0.001:
        hips.add([v.index], root, "REPLACE")
    count += 1
print("tail vertices", count)

# --- animations from the other files, onto this skeleton (same bone names)
for fname, action_name in (("anim_694.glb", "SideStep"), ("anim_617.glb", "CrouchStep")):
    new = import_glb(os.path.join(SRC, fname))
    act = next(a for a in bpy.data.actions if a.name not in ("SideStep", "CrouchStep"))
    act.name = action_name
    act.use_fake_user = True
    for o in new:
        bpy.data.objects.remove(o, do_unlink=True)
arm.animation_data_create()
for name in ("SideStep", "CrouchStep"):
    track = arm.animation_data.nla_tracks.new()
    track.name = name
    track.strips.new(name, 1, bpy.data.actions[name])
arm.animation_data.action = None

# --- smaller textures
for img in bpy.data.images:
    if img.size[0] > 1024:
        img.scale(1024, 1024)

for o in bpy.data.objects:
    o.select_set(o in (arm, mesh))
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", use_selection=True,
                          export_animations=True, export_animation_mode="NLA_TRACKS",
                          export_image_format="JPEG", export_jpeg_quality=88)
print("wrote", OUT, os.path.getsize(OUT) // 1024, "KB")
