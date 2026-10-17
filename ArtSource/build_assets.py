# Broomstick Bash art generator (original, procedural). Run: blender -b -P ArtSource/build_assets.py -- <out_dir>
# All geometry is procedural and original (no third-party assets).
import bpy, bmesh, math, sys, os, random
from mathutils import Vector, Matrix, Euler
OUT = sys.argv[sys.argv.index("--")+1] if "--" in sys.argv else "/tmp/ed_art"
os.makedirs(OUT, exist_ok=True)
random.seed(7)
PAL = {  # flat Halloween-night palette (shared with the Unity material remap). Names ending in Glow are emissive; Ghost* are translucent.
 "Skin":(0.55,0.78,0.45),"SkinWarm":(0.85,0.62,0.45),"Robe":(0.30,0.12,0.45),"RobeDark":(0.16,0.06,0.26),"HatBand":(0.95,0.50,0.08),
 "Hair":(0.85,0.30,0.10),"Broom":(0.45,0.28,0.12),"Straw":(0.90,0.72,0.30),"Boot":(0.10,0.08,0.10),"Stripe":(0.95,0.55,0.10),
 "Pumpkin":(0.95,0.45,0.05),"PumpkinDark":(0.70,0.28,0.03),"Stem":(0.25,0.45,0.12),"Sheet":(0.93,0.92,0.88),"Cardboard":(0.72,0.55,0.32),
 "Metal":(0.62,0.66,0.72),"Shoe":(0.85,0.15,0.15),"ShoeBlue":(0.15,0.35,0.85),"Candy":(0.95,0.75,0.10),"Jeans":(0.20,0.30,0.55),
 "Black":(0.05,0.05,0.07),"BatWing":(0.22,0.12,0.30),"GhostBody":(0.58,1.0,0.93),"EyeGlow":(0.70,1.0,0.30),"Fur":(0.35,0.30,0.32),
 "FurDark":(0.20,0.17,0.20),"Claw":(0.92,0.90,0.80),"Shorts":(0.35,0.18,0.45),"Tooth":(0.95,0.95,0.90),"Tongue":(0.80,0.25,0.35),
 "CarRed":(0.70,0.12,0.15),"CarTeal":(0.10,0.45,0.50),"Glass":(0.25,0.35,0.55),"Tire":(0.08,0.08,0.09),"LightGlow":(1.0,0.85,0.45),
 "Bark":(0.22,0.15,0.12),"BarkDark":(0.14,0.10,0.09),"LeafOrange":(0.85,0.40,0.08),"Lamp":(0.18,0.18,0.22),
 "HouseA":(0.17,0.17,0.27),"HouseB":(0.14,0.16,0.27),"HouseC":(0.19,0.16,0.26),"Roof":(0.08,0.08,0.14),"WindowGlow":(0.55,0.46,0.34),
 "FarHouse":(0.09,0.09,0.18),"Moon":(0.95,0.93,0.75),"MoonCrater":(0.80,0.78,0.62),"Cloud":(0.20,0.22,0.36),"Fence":(0.10,0.10,0.16),
 "PumpkinGlow":(0.62,0.42,0.20),"PumpkinBG":(0.42,0.24,0.12),"StemBG":(0.14,0.20,0.12),"SpellGlow":(0.75,0.40,1.0),"SparkleGlow":(1.0,0.95,0.55),"FireGlow":(1.0,0.45,0.08),"FireCore":(1.0,0.85,0.30),
 "Grass":(0.08,0.11,0.14),"OutlineWarm":(1.0,0.82,0.50),"OutlineCool":(0.70,0.95,1.0),"Gold":(1.0,0.76,0.15),
}
def mat(n):
    m = bpy.data.materials.get(n)
    if not m:
        m = bpy.data.materials.new(n); m.use_nodes = True
        b = m.node_tree.nodes["Principled BSDF"]; c = PAL[n]
        b.inputs["Base Color"].default_value = (*c, 1); b.inputs["Roughness"].default_value = 0.8
        if n.endswith("Glow"): b.inputs["Emission Color"].default_value = (*c, 1); b.inputs["Emission Strength"].default_value = 2.0
        if n.startswith("Outline"):  # preview-only node setup: hide back-facing hull faces (= Unity Cull Back), so only the rim behind the model shows
            nt = m.node_tree; out = nt.nodes["Material Output"]; em = nt.nodes.new("ShaderNodeEmission"); em.inputs[0].default_value = (*c, 1)
            tr = nt.nodes.new("ShaderNodeBsdfTransparent"); mx = nt.nodes.new("ShaderNodeMixShader"); geo = nt.nodes.new("ShaderNodeNewGeometry")
            lp = nt.nodes.new("ShaderNodeLightPath"); inv = nt.nodes.new("ShaderNodeMath"); inv.operation = "SUBTRACT"; inv.inputs[0].default_value = 1.0
            nt.links.new(lp.outputs["Is Camera Ray"], inv.inputs[1]); mxv = nt.nodes.new("ShaderNodeMath"); mxv.operation = "MAXIMUM"
            nt.links.new(geo.outputs["Backfacing"], mxv.inputs[0]); nt.links.new(inv.outputs[0], mxv.inputs[1])  # no light emitted onto the model
            nt.links.new(mxv.outputs[0], mx.inputs[0]); nt.links.new(em.outputs[0], mx.inputs[1]); nt.links.new(tr.outputs[0], mx.inputs[2])
            nt.links.new(mx.outputs[0], out.inputs[0]); m.use_backface_culling = True
        if n == "GhostBody": b.inputs["Alpha"].default_value = 0.8; m.blend_method = "BLEND"
        m.diffuse_color = (*c, 1)
    return m
def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
class Builder:
    """Accumulates flat-shaded parts into one bmesh; each part gets a material and (optionally) a bone group."""
    def __init__(s, name): s.name=name; s.bm=bmesh.new(); s.mats=[]; s.groups=[]; s.vg={}
    def _mi(s,m):
        if m not in s.mats: s.mats.append(m)
        return s.mats.index(m)
    def add(s, kind, m, loc=(0,0,0), size=(1,1,1), rot=(0,0,0), bone=None, seg=8, taper=1.0, jitter=0.0):
        tmp = bmesh.new()
        if kind=="box": bmesh.ops.create_cube(tmp, size=1.0)
        elif kind=="cyl": bmesh.ops.create_cone(tmp, cap_ends=True, segments=seg, radius1=0.5, radius2=0.5*taper, depth=1.0)
        elif kind=="sphere": bmesh.ops.create_icosphere(tmp, subdivisions=1 if seg<=8 else 2, radius=0.5)
        elif kind=="cone": bmesh.ops.create_cone(tmp, cap_ends=True, segments=seg, radius1=0.5, radius2=0.0, depth=1.0)
        if jitter:
            for v in tmp.verts: v.co += Vector((random.uniform(-1,1) for _ in range(3)))*jitter
        M = Matrix.Translation(loc) @ Euler([math.radians(a) for a in rot]).to_matrix().to_4x4() @ Matrix.Diagonal((*size,1))
        bmesh.ops.transform(tmp, matrix=M, verts=tmp.verts)
        mi = s._mi(m)
        for f in tmp.faces: f.material_index = mi; f.smooth = False
        me = bpy.data.meshes.new("t"); tmp.to_mesh(me); tmp.free()
        off = len(s.bm.verts); s.bm.from_mesh(me); bpy.data.meshes.remove(me)
        s.bm.verts.ensure_lookup_table()
        s.groups.append((bone, list(range(off, len(s.bm.verts)))))
        # from_mesh resets material indices? keep explicit
        return s
    def build(s, outline=None):
        if outline:  # inverted hull: duplicate the shell, push it out along vertex normals, flip it, give it the outline material
            omat, th = outline; mi = s._mi(omat)
            s.bm.verts.ensure_lookup_table(); s.bm.normal_update()
            orig = list(s.bm.verts); norms = [v.normal.copy() for v in orig]
            ret = bmesh.ops.duplicate(s.bm, geom=orig + list(s.bm.edges) + list(s.bm.faces))
            vmap = ret["vert_map"]; newf = [e for e in ret["geom"] if isinstance(e, bmesh.types.BMFace)]
            for v, n in zip(orig, norms): vmap[v].co += n * th
            bmesh.ops.reverse_faces(s.bm, faces=newf)
            for f in newf: f.material_index = mi
            s.bm.verts.ensure_lookup_table(); s.bm.verts.index_update()
            extra = []
            for bone, idx in s.groups:
                if bone: extra.append((bone, [vmap[orig[k]].index for k in idx]))
            s.groups += extra
        me = bpy.data.meshes.new(s.name); s.bm.to_mesh(me)
        ob = bpy.data.objects.new(s.name, me); bpy.context.scene.collection.objects.link(ob)
        for m in s.mats: me.materials.append(mat(m))
        for bone, idx in s.groups:
            if bone:
                g = ob.vertex_groups.get(bone) or ob.vertex_groups.new(name=bone)
                g.add(idx, 1.0, 'REPLACE')
        return ob
    def tris(s): return sum(len(f.verts)-2 for f in s.bm.faces)
# fix: bmesh.from_mesh keeps material_index from mesh polys; our tmp mesh has no materials so indices are preserved as set.

def armature(name, bones):
    """bones: list of (name, head, tail, parent)"""
    ad = bpy.data.armatures.new(name+"_Rig"); ao = bpy.data.objects.new(name+"_Rig", ad)
    bpy.context.scene.collection.objects.link(ao); bpy.context.view_layer.objects.active = ao
    bpy.ops.object.mode_set(mode='EDIT')
    for n,h,t,p in bones:
        b = ad.edit_bones.new(n); b.head=h; b.tail=t; b.roll=0
        if p: b.parent = ad.edit_bones[p]
    bpy.ops.object.mode_set(mode='OBJECT')
    for pb in ao.pose.bones: pb.rotation_mode='XYZ'
    return ao
def skin(ob, arm):
    ob.parent = arm; md = ob.modifiers.new("Armature",'ARMATURE'); md.object = arm
def action(arm, name, frames, keys, loop=True):
    """keys: {frame: {bone: (rx,ry,rz) deg or ('loc',(x,y,z))}}"""
    act = bpy.data.actions.new(name); arm.animation_data_create(); arm.animation_data.action = act
    for pb in arm.pose.bones: pb.rotation_euler=(0,0,0); pb.location=(0,0,0)
    for f in sorted(keys):
        for pb in arm.pose.bones:  # key every bone each keyframe so clips never inherit stale pose
            v = keys[f].get(pb.name, (0,0,0))
            if not isinstance(v,dict): v = {"r":v}
            v = {"r":v.get("r",(0,0,0)),"l":v.get("l",(0,0,0))}
            if isinstance(v,dict):
                if "r" in v: pb.rotation_euler=[math.radians(a) for a in v["r"]]; pb.keyframe_insert("rotation_euler", frame=f)
                if "l" in v: pb.location=v["l"]; pb.keyframe_insert("location", frame=f)
            else:
                pb.rotation_euler=[math.radians(a) for a in v]; pb.keyframe_insert("rotation_euler", frame=f)
    act.use_fake_user = True
    tr = arm.animation_data.nla_tracks.new(); tr.name=name; st = tr.strips.new(name, int(frames[0]), act); tr.mute=True
    arm.animation_data.action = None
    for pb in arm.pose.bones: pb.rotation_euler=(0,0,0); pb.location=(0,0,0)
    return act
def export(name, objs):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=f"{OUT}/{name}.fbx", use_selection=True, apply_scale_options='FBX_SCALE_UNITS',
        axis_forward='-Z', axis_up='Y', bake_space_transform=True, mesh_smooth_type='FACE', add_leaf_bones=False,
        bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True,
        bake_anim_simplify_factor=0.0, object_types={'MESH','ARMATURE'})
    bpy.ops.wm.save_as_mainfile(filepath=f"{OUT}/{name}.blend")
STATS = {}
def stat(name, *builders): STATS[name] = sum(b.tris() for b in builders)
# Conventions: 1 unit = 1 m; characters face Blender +X (the flight direction); side camera looks along Blender +Y.
# Rotation rule for this rig layout: local Z rotation, negative = swing/tilt forward (+X), positive = backward.
def U(n, h, t, p=None): return (n, h, t, p)

def witch():
    reset()
    arm = armature("Witch", [U("Root",(0,0,0),(0,0,0.3)), U("Broom",(0,0,0),(0.6,0,0),"Root"),
        U("Spine",(0,0,0.05),(0,0,0.45),"Root"), U("Head",(0.02,0,0.45),(0.02,0,0.7),"Spine"), U("Hat",(0.02,0,0.68),(-0.1,0,1.1),"Head"),
        U("ArmF",(0.04,-0.12,0.40),(0.18,-0.14,0.15),"Spine"), U("ArmB",(0.04,0.12,0.40),(0.18,0.14,0.15),"Spine"),
        U("Cape",(-0.06,0,0.42),(-0.30,0,0.05),"Spine"), U("Legs",(0.0,0,0.05),(0.30,0,-0.25),"Root")])
    b = Builder("Witch")
    # broom: long handle + straw bristle bundle with an orange band (bristles trail behind, -X)
    b.add("cyl","Broom",(0.10,0,0),(0.05,0.05,1.5),rot=(0,90,0),seg=6,bone="Broom")
    b.add("cone","Straw",(-0.80,0,0),(0.32,0.32,0.55),rot=(0,90,0),seg=8,bone="Broom")
    b.add("cyl","HatBand",(-0.56,0,0),(0.20,0.20,0.06),rot=(0,90,0),seg=8,bone="Broom")
    # body: purple robe, orange-striped stockings, pointy boots, green skin, orange hair
    b.add("cyl","Robe",(0,0,0.24),(0.34,0.30,0.42),seg=8,taper=0.6,bone="Spine")
    b.add("box","HatBand",(0,0,0.10),(0.30,0.28,0.05),bone="Spine")
    b.add("box","Robe",(0.16,0,-0.02),(0.36,0.26,0.14),rot=(0,25,0),bone="Legs")
    for y in (-0.07,0.07):
        b.add("cyl","Stripe",(0.30,y,-0.14),(0.08,0.08,0.20),rot=(0,-25,0),seg=6,bone="Legs")
        b.add("box","Boot",(0.38,y,-0.26),(0.16,0.08,0.08),rot=(0,-10,0),bone="Legs")
        b.add("cone","Boot",(0.48,y,-0.22),(0.07,0.07,0.12),rot=(0,40,0),seg=4,bone="Legs")
    b.add("box","Skin",(0.04,0,0.56),(0.22,0.20,0.22),bone="Head")
    b.add("cone","Skin",(0.17,0,0.53),(0.07,0.07,0.12),rot=(0,100,0),seg=4,bone="Head")      # long nose
    b.add("box","Black",(0.15,-0.06,0.60),(0.02,0.04,0.04),bone="Head"); b.add("box","Black",(0.15,0.06,0.60),(0.02,0.04,0.04),bone="Head")
    b.add("box","Hair",(-0.06,0,0.50),(0.18,0.26,0.24),bone="Head")
    b.add("box","Hair",(-0.14,0,0.38),(0.10,0.22,0.18),rot=(0,-20,0),bone="Head")
    b.add("cyl","RobeDark",(0.02,0,0.68),(0.52,0.52,0.03),seg=10,bone="Hat")
    b.add("cone","RobeDark",(-0.02,0,0.88),(0.28,0.28,0.42),rot=(0,-14,0),seg=8,bone="Hat")
    b.add("cyl","HatBand",(0.01,0,0.72),(0.27,0.27,0.05),seg=8,bone="Hat")
    b.add("cone","RobeDark",(-0.17,0,1.10),(0.10,0.10,0.14),rot=(0,-60,0),seg=6,bone="Hat")  # bent tip
    for bn,y in (("ArmF",-0.15),("ArmB",0.15)):
        b.add("box","Robe",(0.10,y,0.30),(0.09,0.09,0.24),rot=(0,-30,0),bone=bn)
        b.add("box","Skin",(0.18,y,0.16),(0.07,0.07,0.07),bone=bn)
    b.add("box","RobeDark",(-0.18,0,0.24),(0.06,0.34,0.40),rot=(0,-30,0),bone="Cape")
    b.add("sphere","SpellGlow",(0.22,-0.17,0.12),(0.05,0.05,0.05),bone="ArmF")  # wand spark
    b.add("cyl","Broom",(0.20,-0.16,0.14),(0.02,0.02,0.18),rot=(0,60,0),seg=4,bone="ArmF")
    ob = b.build(outline=("OutlineWarm",0.022)); skin(ob, arm); stat("Witch", b)
    fly = lambda u: {"Root":{"r":(0,0,0),"l":(0,0.06*u,0)},"Cape":(0,0,25+15*u),"Hat":(0,0,5+6*u),"Broom":(0,0,-3*u),"ArmB":(0,0,-10),"ArmF":(0,0,-10)}
    action(arm,"Fly",(1,25),{1:fly(-1),7:fly(0),13:fly(1),19:fly(0),25:fly(-1)})
    flap = {"Root":{"r":(0,0,8),"l":(0,0.12,0)},"Cape":(0,0,60),"Hat":(0,0,20),"Broom":(0,0,-12),"ArmF":(0,0,-20),"ArmB":(0,0,-20)}
    action(arm,"Flap",(1,13),{1:fly(0),4:flap,13:fly(0)},loop=False)
    cast = {"ArmF":(0,0,-95),"Spine":(0,0,-8),"Cape":(0,0,35),"Hat":(0,0,10),"Head":(0,0,-6)}
    action(arm,"Cast",(1,16),{1:fly(0),5:cast,10:cast,16:fly(0)},loop=False)
    hurt = {"Root":{"r":(0,0,25),"l":(-0.15,0,0)},"Spine":(0,0,20),"Head":(0,0,15),"Hat":(0,0,40),"ArmF":(0,0,60),"ArmB":(0,0,70),"Cape":(0,0,-20)}
    action(arm,"Hurt",(1,19),{1:fly(0),4:hurt,10:{**hurt,"Root":{"r":(0,0,-10),"l":(-0.05,0,0)}},19:fly(0)},loop=False)
    export("Witch",[arm,ob])

def kid(name, look):
    reset()
    arm = armature(name,[U("Body",(0,0,0.45),(0,0,0.9)),U("Head",(0,0,0.9),(0,0,1.15),"Body"),
        U("LegF",(0,-0.08,0.48),(0,-0.08,0.05),"Body"),U("LegB",(0,0.08,0.48),(0,0.08,0.05),"Body"),
        U("ArmF",(0,-0.20,0.82),(0,-0.22,0.50),"Body"),U("ArmB",(0,0.20,0.82),(0,0.22,0.50),"Body")])
    b = Builder(name)
    for bn,y,shoe in (("LegF",-0.08,"Shoe"),("LegB",0.08,"Shoe")):
        b.add("box","Jeans",(0,y,0.27),(0.11,0.11,0.40),bone=bn)
        b.add("box",shoe if look!="robot" else "ShoeBlue",(0.04,y,0.05),(0.20,0.12,0.09),bone=bn)   # visible chunky shoes
    for bn,y in (("ArmF",-0.21),("ArmB",0.21)): b.add("box","SkinWarm",(0,y,0.55),(0.07,0.07,0.08),bone=bn)
    # candy bag in the front hand (orange pail for pumpkin/robot, sack for ghost kid)
    b.add("cyl","Pumpkin" if look!="ghost" else "Stripe",(0.04,-0.24,0.42),(0.16,0.16,0.16),seg=8,bone="ArmF")
    b.add("cyl","Candy",(0.04,-0.24,0.51),(0.13,0.13,0.03),seg=8,bone="ArmF")
    b.add("box","Black",(0.04,-0.24,0.58),(0.01,0.01,0.12),bone="ArmF")
    if look=="pumpkin":
        b.add("sphere","Pumpkin",(0,0,0.70),(0.50,0.44,0.46),bone="Body")
        for z in (-0.12,0.12): b.add("sphere","PumpkinDark",(0,z,0.70),(0.46,0.14,0.44),bone="Body")
        b.add("box","Black",(0.24,0,0.68),(0.02,0.30,0.05),rot=(0,0,0),bone="Body")   # grin
        b.add("box","SkinWarm",(0,0,1.03),(0.22,0.22,0.22),bone="Head"); b.add("box","Hair",(-0.02,0,1.15),(0.22,0.23,0.06),bone="Head")
        b.add("box","Black",(0.111,-0.05,1.05),(0.01,0.03,0.04),bone="Head"); b.add("box","Black",(0.111,0.05,1.05),(0.01,0.03,0.04),bone="Head")
        b.add("cyl","Stem",(0,0,1.20),(0.06,0.06,0.10),seg=5,bone="Head")
        for y in (-0.2,0.2): b.add("box","Pumpkin",(0,y,0.80),(0.08,0.08,0.10),bone="ArmF" if y<0 else "ArmB")
    elif look=="ghost":   # bedsheet kid: warm-white opaque sheet, cut-out eyes, shoes + jeans showing below
        b.add("cyl","Sheet",(0,0,0.72),(0.46,0.42,0.62),seg=8,taper=0.55,bone="Body")
        b.add("sphere","Sheet",(0,0,1.04),(0.30,0.30,0.30),bone="Head")
        b.add("box","Black",(0.14,-0.06,1.06),(0.02,0.05,0.06),bone="Head"); b.add("box","Black",(0.14,0.06,1.06),(0.02,0.05,0.06),bone="Head")
        for y in (-0.2,0.2): b.add("box","Sheet",(0,y,0.70),(0.10,0.10,0.22),bone="ArmF" if y<0 else "ArmB")
        b.add("cone","Stripe",(0,0,1.22),(0.10,0.10,0.10),seg=4,bone="Head")  # little orange party-hat tip = clearly a kid
    else:                 # cardboard robot
        b.add("box","Cardboard",(0,0,0.70),(0.38,0.36,0.42),bone="Body")
        b.add("box","Metal",(0.19,0,0.72),(0.02,0.18,0.14),bone="Body")
        for i,c in enumerate(("Shoe","Candy","ShoeBlue")): b.add("box",c,(0.20,-0.05+0.05*i,0.74),(0.02,0.03,0.03),bone="Body")
        b.add("box","Cardboard",(0,0,1.04),(0.26,0.26,0.24),bone="Head")
        b.add("box","Glass",(0.13,0,1.05),(0.02,0.18,0.07),bone="Head")
        b.add("cyl","Metal",(0,0,1.22),(0.02,0.02,0.14),seg=4,bone="Head"); b.add("sphere","Shoe",(0,0,1.30),(0.06,0.06,0.06),bone="Head")
        for y in (-0.2,0.2): b.add("box","Metal",(0,y,0.70),(0.09,0.09,0.24),bone="ArmF" if y<0 else "ArmB")
    ob=b.build(); skin(ob,arm); stat(name,b)
    w=lambda u:{"LegF":(0,0,-25*u),"LegB":(0,0,25*u),"ArmB":(0,0,-20*u),"ArmF":(0,0,-15),"Body":{"r":(0,0,0),"l":(0,0.03*abs(u),0)}}
    action(arm,"Walk",(1,21),{1:w(1),6:w(0),11:w(-1),16:w(0),21:w(1)})
    wave={"ArmF":(0,0,-160),"Body":{"r":(0,0,0),"l":(0,0.08,0)},"Head":(0,0,-8)}
    action(arm,"Cheer",(1,21),{1:w(0),6:wave,11:{**wave,"ArmF":(0,0,-130)},16:wave,21:w(0)},loop=False)
    export(name,[arm,ob])

def bat():
    reset(); arm=armature("Bat",[U("Body",(0,0,0),(0,0,0.15)),U("WingF",(0,-0.05,0.05),(0,-0.45,0.05),"Body"),U("WingB",(0,0.05,0.05),(0,0.45,0.05),"Body")])
    b=Builder("Bat")
    b.add("sphere","Black",(0,0,0.05),(0.20,0.16,0.20),bone="Body"); b.add("sphere","Black",(0.10,0,0.14),(0.14,0.13,0.13),bone="Body")
    for y in (-0.04,0.04):
        b.add("cone","Black",(0.10,y,0.23),(0.05,0.05,0.08),seg=4,bone="Body"); b.add("box","EyeGlow",(0.17,y,0.15),(0.02,0.025,0.025),bone="Body")
    for bn,sg in (("WingF",-1),("WingB",1)):
        b.add("box","BatWing",(-0.03,sg*0.25,0.06),(0.34,0.40,0.015),bone=bn)
        for k in range(3): b.add("cone","BatWing",(-0.20,sg*(0.12+0.13*k),0.06),(0.10,0.12,0.015),rot=(0,0,90),seg=3,bone=bn)  # scallops
        b.add("cone","BatWing",(0.10,sg*0.47,0.06),(0.06,0.10,0.015),rot=(0,0,0),seg=3,bone=bn)  # wing claw tip
    ob=b.build(outline=("OutlineCool",0.018)); skin(ob,arm); stat("Bat",b)
    # wings point sideways (bone Y = +-world Y); flap = rotate about local X? use bone-local Z swing which here is world X axis roll
    up={"WingF":(0,0,0),"WingB":(0,0,0),"Body":{"r":(0,0,0),"l":(0,0,0)}}
    up_={"WingF":(-78,0,-12),"WingB":(78,0,12),"Body":{"r":(0,0,0),"l":(0,0.03,0)}}; dn_={"WingF":(-5,0,-12),"WingB":(5,0,12),"Body":{"r":(0,0,0),"l":(0,-0.03,0)}}
    action(arm,"Flap",(1,9),{1:up_,5:dn_,9:up_})  # wings sweep between near-vertical (reads as a V silhouette from the side) and level
    export("Bat",[arm,ob])

def ghost():
    reset(); arm=armature("Ghost",[U("Body",(0,0,0.0),(0,0,0.5)),U("Tail",(0,0,0.0),(-0.3,0,-0.3),"Body")])
    b=Builder("Ghost")   # enemy: translucent cyan, no feet, glowing green eyes, wavy tail
    b.add("sphere","GhostBody",(0,0,0.45),(0.60,0.50,0.60),seg=16,bone="Body")
    b.add("cyl","GhostBody",(0,0,0.15),(0.60,0.50,0.40),seg=8,bone="Body")
    for i in range(4): b.add("cone","GhostBody",(-0.12-0.08*i, (-1)**i*0.10, -0.12-0.06*i),(0.22-0.03*i,0.22-0.03*i,0.24),rot=(180,0,30),seg=5,bone="Tail")
    for y in (-0.12,0.12): b.add("sphere","EyeGlow",(0.27,y,0.52),(0.10,0.06,0.14),bone="Body")
    b.add("sphere","Black",(0.29,0,0.30),(0.08,0.16,0.10),bone="Body")
    for y in (-0.33,0.33): b.add("cone","GhostBody",(0.05,y,0.25),(0.14,0.14,0.22),rot=(70 if y<0 else -70,0,0),seg=4,bone="Body")
    ob=b.build(outline=("OutlineCool",0.025)); skin(ob,arm); stat("Ghost",b)
    f=lambda u:{"Body":{"r":(0,0,-6*u),"l":(0,0.12*u,0)},"Tail":(0,0,15*u)}
    action(arm,"Float",(1,41),{1:f(-1),11:f(0),21:f(1),31:f(0),41:f(-1)})
    export("Ghost",[arm,ob])

def props():
    for nm,col in (("Car_Red","CarRed"),("Car_Teal","CarTeal")):
        reset(); b=Builder(nm)   # 4 m long sedan, side-readable silhouette
        b.add("box",col,(0,0,0.55),(4.0,1.7,0.6)); b.add("box",col,(-0.2,0,1.05),(2.2,1.5,0.5))
        b.add("box","Glass",(-0.2,0,1.06),(2.0,1.52,0.38))
        for x in (-1.3,1.3):
            for y in (-0.8,0.8): b.add("cyl","Tire",(x,y,0.35),(0.70,0.70,0.22),rot=(90,0,0),seg=10); b.add("cyl","Metal",(x,y*1.02,0.35),(0.32,0.32,0.22),rot=(90,0,0),seg=6)
        b.add("box","LightGlow",(2.0,-0.55,0.65),(0.05,0.3,0.15)); b.add("box","LightGlow",(2.0,0.55,0.65),(0.05,0.3,0.15)); b.add("box","Shoe",(-2.0,0,0.65),(0.05,1.4,0.12))
        b.add("cone","Pumpkin",(-0.2,0,1.40),(0.30,0.30,0.20),seg=6) if nm=="Car_Teal" else None
        ob=b.build(); stat(nm,b); export(nm,[ob])
    reset(); b=Builder("Branch")   # hangs DOWN from origin (top of screen), ~2.5 m reach
    def seg(a,c,r,m="Bark"):
        a,c=Vector(a),Vector(c); d=c-a; q=d.to_track_quat('Z','Y').to_euler()
        b.add("cyl",m,tuple((a+c)/2),(r,r,d.length+0.03),rot=tuple(math.degrees(x) for x in q),seg=6,jitter=0.01)
    pts=[(-1.5,0,0.2),(-0.6,0,-0.4),(0.1,0,-1.1),(0.5,0,-1.9),(0.6,0,-2.5)]
    for i in range(len(pts)-1): seg(pts[i],pts[i+1],0.45-0.08*i)
    seg((-0.6,0,-0.4),(0.4,0.1,-0.5),0.16,"BarkDark"); seg((0.1,0,-1.1),(-0.5,-0.1,-1.6),0.14,"BarkDark"); seg((0.5,0,-1.9),(1.0,0,-2.2),0.10,"BarkDark")
    for p in ((0.45,0.1,-0.55),(-0.5,-0.1,-1.65),(1.0,0,-2.25),(0.62,0,-2.55)): b.add("sphere","LeafOrange",p,(0.30,0.25,0.22),jitter=0.02)
    ob=b.build(); stat("Branch",b); export("Branch",[ob])
    reset(); b=Builder("LampPost")   # 4.2 m, glowing lantern head with a little bat sign
    b.add("cyl","Lamp",(0,0,0.15),(0.45,0.45,0.30),seg=8); b.add("cyl","Lamp",(0,0,2.1),(0.14,0.14,3.8),seg=6)
    b.add("box","Lamp",(0.30,0,3.95),(0.70,0.10,0.10)); b.add("box","Lamp",(0.62,0,3.82),(0.32,0.32,0.06))
    b.add("box","LightGlow",(0.62,0,3.62),(0.24,0.24,0.34)); b.add("cone","Lamp",(0.62,0,3.40),(0.30,0.30,0.12),rot=(180,0,0),seg=4)
    b.add("box","Pumpkin",(-0.0,-0.08,2.6),(0.03,0.02,0.45))
    ob=b.build(); stat("LampPost",b); export("LampPost",[ob])

def background():
    W=40.0  # every layer tiles seamlessly every 40 m along X, centred on origin; ground at z=0
    reset(); b=Builder("BG_Moon")   # far layer: big moon + soft clouds + stars
    b.add("cyl","Moon",(6,0,14),(7,7,0.2),rot=(90,0,0),seg=20)
    for p,s in (((4.5,-0.15,15.5),1.6),((7.8,-0.15,12.6),1.0),((6.8,-0.15,15.8),0.7)): b.add("cyl","MoonCrater",p,(s,s,0.1),rot=(90,0,0),seg=8)
    for x,z,s in ((-12,12,6),(-4,9.5,5),(14,10,7),(18,15,4)): b.add("sphere","Cloud",(x,-0.6,z),(s,0.3,s*0.35))
    random.seed(3)
    for i in range(22): b.add("cone","SparkleGlow",(random.uniform(-W/2,W/2),-0.3,random.uniform(8,20)),(0.18,0.05,0.18),rot=(90,0,0),seg=4)
    ob=b.build(); stat("BG_Moon",b); export("BG_Moon",[ob])
    reset(); b=Builder("BG_HousesFar"); random.seed(4); x=-W/2
    while x < W/2-1:
        w=random.uniform(2.5,4.5); h=random.uniform(3,6); b.add("box","FarHouse",(x+w/2,0,h/2),(w,1,h)); b.add("cone","FarHouse",(x+w/2,0,h+w*0.3),(w*1.15,1,w*0.6),rot=(0,0,45),seg=4)
        if random.random()<0.5: b.add("box","WindowGlow",(x+w/2,-0.52,h*0.6),(0.5,0.02,0.6))
        x+=w+random.uniform(0.3,1.5)
    b.add("box","FarHouse",(0,0,0.3),(W,1,0.6)); ob=b.build(); stat("BG_HousesFar",b); export("BG_HousesFar",[ob])
    reset(); b=Builder("BG_HousesNear"); random.seed(5); x=-W/2+0.5
    for i in range(5):
        w=6.0; h=random.uniform(4.5,6.0); c=random.choice(["HouseA","HouseB","HouseC"]); cx=x+w/2
        b.add("box",c,(cx,0,h/2),(w,3,h)); b.add("cone","Roof",(cx,0,h+1.4),(w*1.25,3.6,2.8),rot=(0,0,45),seg=4)
        b.add("box","Roof",(cx+1.6,0,h+1.6),(0.6,0.6,1.6))  # chimney
        for wx in (-1.6,1.6):
            for wz in (h*0.35,h*0.72): b.add("box","WindowGlow",(cx+wx,-1.52,wz),(1.0,0.04,1.0)); b.add("box","Roof",(cx+wx,-1.55,wz),(1.15,0.02,0.10))
        b.add("box","Roof",(cx,-1.52,1.1),(1.1,0.05,2.2)); b.add("box","Pumpkin",(cx-0.35,-1.56,1.1),(0.08,0.02,0.08))
        if i%2==0: b.add("cone","Roof",(cx-1.0,-0.6,h+3.2),(0.5,0.5,1.0),seg=4)  # spooky spire
        x+=w+2.0
    ob=b.build(); stat("BG_HousesNear",b); export("BG_HousesNear",[ob])
    reset(); b=Builder("BG_Pumpkins"); random.seed(6)   # near layer: picket fence, grass strip and jack-o'-lanterns
    b.add("box","Grass",(0,0,0.1),(W,1.2,0.2))
    for i in range(int(W/1.0)): b.add("box","Fence",(-W/2+0.5+i*1.0,0.3,0.6),(0.14,0.06,1.0))
    b.add("box","Fence",(0,0.3,0.85),(W,0.05,0.10)); b.add("box","Fence",(0,0.3,0.40),(W,0.05,0.10))
    for i in range(7):
        px=-W/2+3+i*5.5+random.uniform(-1,1); s=random.uniform(0.5,0.8)
        b.add("sphere","PumpkinBG",(px,-0.2,0.2+s*0.4),(s,s*0.9,s*0.8)); b.add("cyl","StemBG",(px,-0.2,0.2+s*0.82),(0.08,0.08,0.15),seg=5)
        for dx in (-0.12*s,0.12*s): b.add("cone","PumpkinGlow",(px+dx,-0.2-s*0.44,0.2+s*0.48),(0.14*s,0.02,0.14*s),rot=(90,0,0),seg=3)
        b.add("box","PumpkinGlow",(px,-0.2-s*0.44,0.2+s*0.28),(0.36*s,0.02,0.06*s))
    ob=b.build(); stat("BG_Pumpkins",b); export("BG_Pumpkins",[ob])

def boss():
    reset()
    arm=armature("Werewolf",[U("Hips",(0,0,1.0),(0,0,1.3)),U("Chest",(0,0,1.3),(0,0,2.0),"Hips"),U("Head",(0.15,0,2.0),(0.15,0,2.5),"Chest"),
        U("Jaw",(0.25,0,2.1),(0.6,0,2.05),"Head"),U("ArmF",(0.05,-0.45,1.9),(0.15,-0.55,1.0),"Chest"),U("ArmB",(0.05,0.45,1.9),(0.15,0.55,1.0),"Chest"),
        U("LegF",(0,-0.22,1.0),(0.1,-0.22,0.1),"Hips"),U("LegB",(0,0.22,1.0),(0.1,0.22,0.1),"Hips")])
    b=Builder("Werewolf")   # original: hulking grey-purple wolf in torn purple shorts with a big orange scarf, faces +X
    b.add("box","Fur",(0,0,1.65),(0.85,0.80,0.80),rot=(0,-10,0),bone="Chest"); b.add("box","FurDark",(0.30,0,1.55),(0.30,0.60,0.55),bone="Chest")
    b.add("cyl","HatBand",(0.05,0,1.98),(0.65,0.65,0.18),seg=8,bone="Chest")
    b.add("box","Shorts",(0,0,1.10),(0.70,0.70,0.35),bone="Hips")
    b.add("box","Fur",(0.25,0,2.25),(0.45,0.45,0.42),bone="Head"); b.add("box","Fur",(0.55,0,2.18),(0.35,0.28,0.18),bone="Head")
    b.add("box","Black",(0.73,0,2.22),(0.06,0.10,0.07),bone="Head")
    for y in (-0.13,0.13):
        b.add("cone","FurDark",(0.15,y,2.55),(0.14,0.10,0.24),seg=4,bone="Head"); b.add("box","FireGlow",(0.48,y,2.33),(0.02,0.07,0.04),bone="Head")
        b.add("cone","Tooth",(0.66,y*0.7,2.08),(0.04,0.04,0.07),rot=(180,0,0),seg=3,bone="Head")
    b.add("box","Fur",(0.48,0,2.04),(0.32,0.24,0.08),bone="Jaw"); b.add("box","Tongue",(0.45,0,2.08),(0.22,0.16,0.02),bone="Jaw")
    for bn,y in (("ArmF",-0.48),("ArmB",0.48)):
        b.add("box","Fur",(0.08,y,1.55),(0.26,0.26,0.70),rot=(0,-6,0),bone=bn); b.add("box","FurDark",(0.14,y,1.05),(0.28,0.28,0.26),bone=bn)
        for i in range(3): b.add("cone","Claw",(0.24,y-0.08+0.08*i,0.92),(0.05,0.05,0.10),rot=(180,0,0),seg=3,bone=bn)
    for bn,y in (("LegF",-0.22),("LegB",0.22)):
        b.add("box","Fur",(0.05,y,0.60),(0.28,0.28,0.80),bone=bn); b.add("box","FurDark",(0.18,y,0.08),(0.45,0.26,0.16),bone=bn)
    b.add("cone","Fur",(-0.45,0,1.15),(0.22,0.22,0.6),rot=(0,-120,0),seg=5,bone="Hips")  # tail
    ob=b.build(outline=("OutlineWarm",0.035)); skin(ob,arm); stat("Werewolf",b)
    idle=lambda u:{"Chest":(0,0,-3*u),"Head":(0,0,4*u),"Jaw":(0,0,3+3*u),"ArmF":(0,0,6*u),"ArmB":(0,0,-6*u),"Hips":{"r":(0,0,0),"l":(0,0.03*u,0)}}
    action(arm,"Idle",(1,41),{1:idle(-1),21:idle(1),41:idle(-1)})
    wind={"ArmF":(0,0,150),"Chest":(0,0,15),"Head":(0,0,-5),"Jaw":(0,0,0),"Hips":{"r":(0,0,0),"l":(0,0,0)}}
    toss={"ArmF":(0,0,-110),"Chest":(0,0,-20),"Head":(0,0,-10),"Jaw":(0,0,-25),"Hips":{"r":(0,0,0),"l":(0,0,0)}}
    action(arm,"Throw",(1,25),{1:idle(0),9:wind,14:toss,25:idle(0)},loop=False)
    hurt={"Chest":(0,0,25),"Head":(0,0,30),"Jaw":(0,0,-30),"ArmF":(0,0,50),"ArmB":(0,0,60),"Hips":{"r":(0,0,0),"l":(-0.15,0,0)}}
    action(arm,"Hurt",(1,19),{1:idle(0),5:hurt,19:idle(0)},loop=False)
    action(arm,"Defeat",(1,41),{1:idle(0),10:hurt,25:{"Hips":{"r":(0,0,80),"l":(-0.4,-0.35,0)},"Chest":(0,0,10),"Head":(0,0,20),"Jaw":(0,0,-30),"ArmF":(0,0,120),"ArmB":(0,0,140),"LegF":(0,0,-30)},
        41:{"Hips":{"r":(0,0,90),"l":(-0.5,-0.8,0)},"Chest":(0,0,5),"Head":(0,0,10),"Jaw":(0,0,-35),"ArmF":(0,0,170),"ArmB":(0,0,170),"LegF":(0,0,-40),"LegB":(0,0,-10)}},loop=False)
    export("Werewolf",[arm,ob])

def fx():
    reset(); b=Builder("FX_Spell")   # purple star bolt with trailing motes, travels +X
    b.add("sphere","SpellGlow",(0,0,0),(0.30,0.30,0.30))
    for i in range(4): b.add("cone","SpellGlow",(0,0,0),(0.12,0.12,0.55),rot=(0,90*i,0),seg=4)
    for i in range(3): b.add("sphere","SparkleGlow",(-0.35-0.25*i,0,0.05*(-1)**i),(0.12-0.03*i,)*3)
    ob=b.build(); stat("FX_Spell",b); export("FX_Spell",[ob])
    reset(); b=Builder("FX_Sparkle")
    for i in range(2): b.add("cone","SparkleGlow",(0,0,0),(0.08,0.02,0.45),rot=(0,90*i,0),seg=4); b.add("cone","SparkleGlow",(0,0,0),(0.08,0.02,0.45),rot=(0,90*i+180,0),seg=4)
    b.add("sphere","SparkleGlow",(0,0,0),(0.10,0.10,0.10))
    ob=b.build(); stat("FX_Sparkle",b); export("FX_Sparkle",[ob])
    reset(); b=Builder("FX_Fireball")   # boss projectile, travels -X (toward the witch), flames trail +X
    b.add("sphere","FireCore",(0,0,0),(0.40,0.40,0.40)); b.add("sphere","FireGlow",(0.06,0,0),(0.52,0.48,0.48))
    for i,(z,y) in enumerate(((0.12,0),(-0.10,0.08),(0.0,-0.12),(0.2,0.1))): b.add("cone","FireGlow",(0.35,y,z),(0.20,0.20,0.45),rot=(0,90+15*(i-1),0),seg=4)
    ob=b.build(); stat("FX_Fireball",b); export("FX_Fireball",[ob])

witch(); kid("Kid_Pumpkin","pumpkin"); kid("Kid_SheetGhost","ghost"); kid("Kid_Robot","robot"); bat(); ghost(); props(); background(); boss(); fx()
import json; json.dump(STATS, open(f"{OUT}/tri_counts.json","w"), indent=1); print("STATS", STATS)
