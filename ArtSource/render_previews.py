# blender -b -P ArtSource/render_previews.py -- <art_dir> <out_dir>   (side camera, like the game)
import bpy, sys, os, math, glob
from mathutils import Vector
A, O = sys.argv[sys.argv.index("--")+1:][:2]; os.makedirs(O+"/anim", exist_ok=True)
def setup(res):
    sc=bpy.context.scene; sc.render.engine='CYCLES'; sc.cycles.samples=24; sc.cycles.device='CPU'
    sc.render.resolution_x, sc.render.resolution_y = res
    w=bpy.data.worlds.new("w"); sc.world=w; w.use_nodes=True; bg=w.node_tree.nodes["Background"]; bg.inputs[0].default_value=(0.10,0.08,0.22,1); bg.inputs[1].default_value=1.0
    for rot,e,col in (((55,0,-30),3.0,(0.75,0.8,1.0)),((60,0,150),1.2,(1.0,0.6,0.3))):
        l=bpy.data.objects.new("l",bpy.data.lights.new("l",'SUN')); l.data.energy=e; l.data.color=col; l.rotation_euler=[math.radians(a) for a in rot]; sc.collection.objects.link(l)
def bounds():
    pts=[o.matrix_world@Vector(b) for o in bpy.data.objects if o.type=='MESH' for b in o.bound_box]
    return Vector([min(p[i] for p in pts) for i in range(3)]), Vector([max(p[i] for p in pts) for i in range(3)])
def cam(c, size, az=0):
    sc=bpy.context.scene; cm=bpy.data.objects.new("cam",bpy.data.cameras.new("cam")); cm.data.type='ORTHO'; cm.data.ortho_scale=size; sc.collection.objects.link(cm); sc.camera=cm
    d=Vector((math.sin(math.radians(az)), -math.cos(math.radians(az)), 0.25)).normalized(); cm.location=c+d*50; cm.rotation_euler=(-d).to_track_quat('-Z','Y').to_euler(); cm.data.clip_end=200
def render(p): bpy.context.scene.render.filepath=p; bpy.ops.render.render(write_still=True)
names=[os.path.basename(f)[:-6] for f in sorted(glob.glob(A+"/*.blend"))]
for n in names:
    bpy.ops.wm.open_mainfile(filepath=f"{A}/{n}.blend"); wide=n.startswith("BG_"); setup((960,360) if wide else (420,420))
    mn,mx=bounds(); c=(mn+mx)/2; s=max(mx.x-mn.x, (mx.z-mn.z)*(960/360 if wide else 1))*1.12
    cam(c, s, az=0 if wide else -25); render(f"{O}/{n}.png")
ANIMS={"Witch":{"Fly":[1,13],"Flap":[4],"Cast":[7],"Hurt":[4,10]},"Werewolf":{"Idle":[1],"Throw":[9,14],"Hurt":[5],"Defeat":[25,41]},
       "Bat":{"Flap":[1,5]},"Ghost":{"Float":[1,21]},"Kid_Pumpkin":{"Walk":[1,11],"Cheer":[6]},"Kid_SheetGhost":{"Walk":[1]},"Kid_Robot":{"Walk":[1]}}
for n,acts in ANIMS.items():
    for a,frames in acts.items():
        bpy.ops.wm.open_mainfile(filepath=f"{A}/{n}.blend"); setup((300,300))
        arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]; arm.animation_data.action=bpy.data.actions[a]
        bpy.context.scene.frame_set(1); mn,mx=bounds(); c=(mn+mx)/2; s=max(mx.x-mn.x,mx.z-mn.z)*1.5
        cam(c, s, az=-15)
        for f in frames: bpy.context.scene.frame_set(f); render(f"{O}/anim/{n}_{a}_{f:02d}.png")
