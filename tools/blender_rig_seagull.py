"""Rig the Meshy flying seagull for flapping (run headless with Blender 4.x):

    blender -b -P tools/blender_rig_seagull.py -- <in.glb> <out_rigged.glb> <preview.png>

Builds a body bone and two-segment wings (inner/outer), skins by distance from the body,
bakes a "Flap" loop and exports a GLB with skin + animation. SeagullCrash.cs drives the
same wing bones procedurally for the lopsided broken-wing flight.
"""
import bpy, sys, math, bmesh
from mathutils import Vector
src, dst, png = sys.argv[-3:]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
for o in bpy.context.scene.objects: o.select_set(False)
for o in meshes: o.select_set(True)
bpy.context.view_layer.objects.active=meshes[0]
if len(meshes)>1: bpy.ops.object.join()
gull=bpy.context.view_layer.objects.active
bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
gull.name='Seagull'

vs=[v.co.copy() for v in gull.data.vertices]
span=max(abs(v.x) for v in vs)
def band(lo,hi):
    pts=[v for v in vs if lo<=abs(v.x)<=hi]
    pts.sort(key=lambda v:v.z); return pts[len(pts)//2] if pts else Vector((0,0,0))
shoulder=band(0.06*span/0.95,0.14*span/0.95); elbow=band(0.40*span/0.95,0.48*span/0.95)
print("span",span,"shoulder",shoulder,"elbow",elbow)
SX=0.08*span/0.95; EX=0.44*span/0.95
zs, ze = shoulder.z, elbow.z
ys, ye = shoulder.y, elbow.y

arm_data=bpy.data.armatures.new('SeagullRig'); arm=bpy.data.objects.new('SeagullRig', arm_data)
bpy.context.scene.collection.objects.link(arm); bpy.context.view_layer.objects.active=arm
bpy.ops.object.mode_set(mode='EDIT')
eb=arm_data.edit_bones
body=eb.new('body'); body.head=(0,0.15,zs); body.tail=(0,-0.15,zs)
bones={}
for side,s in (('l',1),('r',-1)):
    a=eb.new(f'wing_{side}_inner'); a.head=(s*SX,ys,zs); a.tail=(s*EX,ye,ze); a.parent=body
    b=eb.new(f'wing_{side}_outer'); b.head=a.tail; b.tail=(s*span*1.0,ye+0.05,ze); b.parent=a; b.use_connect=True
    for bn in (a,b): bn.align_roll(Vector((0,0,1)))
bpy.ops.object.mode_set(mode='OBJECT')

# Skin weights from distance along the wing axis.
groups={n:gull.vertex_groups.new(name=n) for n in ('body','wing_l_inner','wing_l_outer','wing_r_inner','wing_r_outer')}
def smooth(e0,e1,x):
    t=min(max((x-e0)/(e1-e0),0),1); return t*t*(3-2*t)
for v in gull.data.vertices:
    t=abs(v.co.x); side='l' if v.co.x>0 else 'r'
    w_wing=smooth(SX*0.6,SX*1.6,t)
    w_outer=smooth(EX*0.85,EX*1.15,t)
    wb=1-w_wing; wi=w_wing*(1-w_outer); wo=w_wing*w_outer
    for name,w in (('body',wb),(f'wing_{side}_inner',wi),(f'wing_{side}_outer',wo)):
        if w>1e-4: groups[name].add([v.index],w,'REPLACE')
mod=gull.modifiers.new('Armature','ARMATURE'); mod.object=arm
gull.parent=arm

# 'Flap' loop: 16 frames at 30 fps (~1.9 beats/s). Down-stroke faster than up-stroke.
scn=bpy.context.scene; scn.render.fps=30; scn.frame_start=1; scn.frame_end=17
bpy.context.view_layer.objects.active=arm; bpy.ops.object.mode_set(mode='POSE')
act=bpy.data.actions.new('Flap'); arm.animation_data_create(); arm.animation_data.action=act
keys=[(1,40,10),(4,15,25),(8,-38,-5),(11,-25,-25),(14,10,-10),(17,40,10)]
for f,inner,outer in keys:
    for side,s in (('l',1),('r',-1)):
        for name,deg in ((f'wing_{side}_inner',inner),(f'wing_{side}_outer',outer)):
            pb=arm.pose.bones[name]; pb.rotation_mode='XYZ'
            pb.rotation_euler=(0,0,0)
            pb.rotation_euler[0]=math.radians(deg)  # +deg lifts both wings: the bone axes mirror
            pb.keyframe_insert('rotation_euler',frame=f)
bpy.ops.object.mode_set(mode='OBJECT')
arm.animation_data.action=act
scn.frame_set(1)

# Check render at the up-stroke and down-stroke.
scn.render.engine='CYCLES'; scn.cycles.samples=8; scn.render.resolution_x=480; scn.render.resolution_y=300
cam=bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); scn.collection.objects.link(cam); scn.camera=cam
cam.location=(0,-3.6,0.3); cam.rotation_euler=(math.radians(87),0,0)
sun=bpy.data.objects.new('sun', bpy.data.lights.new('sun','SUN')); scn.collection.objects.link(sun); sun.rotation_euler=(0.7,0.2,0.3); sun.data.energy=3
w=bpy.data.worlds.new('w'); scn.world=w; w.use_nodes=True; w.node_tree.nodes['Background'].inputs[0].default_value=(0.45,0.45,0.45,1)
for f in (1,8):
    scn.frame_set(f); scn.render.filepath=png.replace('.png',f'_{f}.png'); bpy.ops.render.render(write_still=True)
scn.frame_set(1)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.gltf(filepath=dst, export_format='GLB', export_animations=True, export_skins=True, export_animation_mode='ACTIONS')
print("exported", dst)
