"""Remove the brass dish and coins Meshy modelled on the floor beside the trestle table:

    blender -b -P tools/blender_clean_table.py -- <in.glb> <out.glb> <preview.png>

The Meshy mesh is thousands of loose fragments, so parts are found by position: faces near
the floor in front of the table, between the legs.
"""
import bpy, sys, bmesh, math
from mathutils import Vector
src, dst, png = sys.argv[-3:]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
o=[x for x in bpy.context.scene.objects if x.type=='MESH'][0]
mw=o.matrix_world
bm=bmesh.new(); bm.from_mesh(o.data)
kill=[f for f in bm.faces if (lambda c: c.z < -0.45 and -0.15 < c.x < 0.66 and -0.55 < c.y < -0.05)(mw @ f.calc_center_median())]
print("deleting", len(kill), "of", len(bm.faces))
bmesh.ops.delete(bm, geom=kill, context='FACES')
bm.to_mesh(o.data); bm.free()
bpy.ops.export_scene.gltf(filepath=dst, export_format='GLB')
# quick check render from the front-above
scn=bpy.context.scene; scn.render.engine='CYCLES'; scn.cycles.samples=16; scn.render.resolution_x=640; scn.render.resolution_y=420
cam=bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); scn.collection.objects.link(cam); scn.camera=cam
cam.location=(0.6,-2.6,1.2); cam.rotation_euler=(math.radians(68),0,math.radians(13))
sun=bpy.data.objects.new('sun', bpy.data.lights.new('sun','SUN')); scn.collection.objects.link(sun); sun.rotation_euler=(0.6,0.2,0.3); sun.data.energy=3
w=bpy.data.worlds.new('w'); scn.world=w; w.use_nodes=True; w.node_tree.nodes['Background'].inputs[0].default_value=(0.5,0.5,0.5,1)
scn.render.filepath=png; bpy.ops.render.render(write_still=True)
