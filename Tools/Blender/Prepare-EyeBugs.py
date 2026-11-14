"""Prepare the yellow and purple eye bugs with editable multi-leg base rigs."""
import bpy
import hashlib
import importlib.util
import json
import math
import sys
from pathlib import Path
from mathutils import Matrix, Vector

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'Assets/ART'
PROFILES = [
    dict(name='YellowEyeBug', label='黄色眼虫', download=r'E:\Download\20261115010938_dd11be5b.glb',
         front=(.5568, -.4540, .025), middle=(.55, 0, .25), rear=(.6936, .4669, .025)),
    dict(name='PurpleEyeBug', label='紫色眼虫', download=r'E:\Download\20261115005816_00101dbb.glb',
         front=(.3438, -.1456, .025), middle=(.70, 0, .20), rear=(.6500, .4140, .025)),
]
HEIGHT = .75


def build_rig(profile):
    data = bpy.data.armatures.new(profile['name'] + '_Skeleton')
    rig = bpy.data.objects.new(profile['name'] + '_Rig', data)
    bpy.context.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    segments = []
    def bone(name, head, tail, parent=None, deform=True):
        head, tail = Vector(head) * HEIGHT, Vector(tail) * HEIGHT
        item = data.edit_bones.new(name)
        item.head, item.tail, item.use_deform = head, tail, deform
        if parent: item.parent = data.edit_bones[parent]
        if name.startswith('Leg'): segments.append((name, head.copy(), tail.copy()))
    bone('Root', (0, 0, 0), (0, 0, .15), deform=False)
    bone('Body', (0, 0, .30), (0, 0, .52), 'Root')
    bone('Eye', (0, 0, .65), (0, -.20, .65), 'Body')
    bone('Head', (0, -.30, .30), (0, -.51, .30), 'Body')
    bone('SoundSocket', (0, -.58, .30), (0, -.65, .30), 'Head', False)
    for side, sign in [('L', 1), ('R', -1)]:
        for label in ['Front', 'Middle', 'Rear']:
            toe = Vector(profile[label.lower()]); toe.x *= sign
            hip = Vector((sign * (.39 if label == 'Middle' else .29),
                          dict(Front=-.20, Middle=0, Rear=.27)[label], .43))
            knee = Vector((sign * max(abs(toe.x), .48), (toe.y + hip.y) / 2, .34))
            if label == 'Middle': knee = Vector((sign*.65, 0, .42))
            ankle = toe + Vector((0, .015, .045))
            upper = 'Leg'+label+'Upper.'+side
            lower = 'Leg'+label+'Lower.'+side
            foot = 'Leg'+label+'Foot.'+side
            bone(upper, hip, knee, 'Body')
            bone(lower, knee, ankle, upper)
            bone(foot, ankle, toe + Vector((0, -.035, 0)), lower)
        bone('Mandible.'+side, (sign*.10, -.44, .28), (sign*.15, -.68, .17), 'Head')
    bpy.ops.object.mode_set(mode='OBJECT')
    rig.show_in_front = True
    rig['RigType'] = 'Multi-leg base rig; refine against final locomotion animations'
    rig['HeightMeters'] = HEIGHT
    return rig, segments


def distance_squared(point, a, b):
    direction = b-a
    t = max(0, min(1, (point-a).dot(direction)/direction.length_squared))
    return (point-(a+t*direction)).length_squared


def skin(obj, rig, segments, smooth):
    groups = {b.name: obj.vertex_groups.new(name=b.name) for b in rig.data.bones if b.use_deform}
    def mix(weights, other, value):
        result = {key:weight*(1-value) for key,weight in weights.items()}
        for key,weight in other.items(): result[key] = result.get(key,0) + value*weight
        return result
    for vertex in obj.data.vertices:
        x,y,z = vertex.co / HEIGHT
        eye = smooth(.42,.56,z)
        weights = {'Body':1-eye, 'Eye':eye}
        radial = math.sqrt((x/.43)**2+(y/.50)**2)
        amount = max(1-smooth(.18,.32,z), smooth(.75,1.12,radial)) * (1-smooth(.44,.57,z))
        distances = [(name,distance_squared(vertex.co,a,b)/HEIGHT**2) for name,a,b in segments]
        minimum = min(d for _,d in distances)
        leg = {name:math.exp(-(d-minimum)/(.045**2*2)) for name,d in distances}
        total = sum(leg.values())
        weights = mix(weights,{key:value/total for key,value in leg.items()},amount)
        head = (1-smooth(-.42,-.24,y))*(1-smooth(.24,.36,abs(x)))*(1-smooth(.43,.55,z))
        weights = mix(weights,{'Head':1},head)
        mandible = (1-smooth(-.61,-.48,y))*(1-smooth(.23,.34,z))*smooth(.035,.10,abs(x))*(1-smooth(.20,.28,abs(x)))
        weights = mix(weights,{'Mandible.'+('L' if x>=0 else 'R'):1},mandible)
        weights = dict(sorted(((key,value) for key,value in weights.items() if value>.0001),key=lambda item:item[1],reverse=True)[:4])
        total = sum(weights.values())
        for key,value in weights.items(): groups[key].add([vertex.index],value/total,'REPLACE')
    modifier = obj.modifiers.new('EyeBugSkin','ARMATURE')
    modifier.object = rig
    obj.parent = rig


def main():
    sys.dont_write_bytecode = True
    spec = importlib.util.spec_from_file_location('enemy_asset_tools',Path(__file__).with_name('Prepare-EnemyAssets.py'))
    helpers = importlib.util.module_from_spec(spec); spec.loader.exec_module(helpers)
    reports=[]
    for profile in PROFILES:
        source = ROOT.parent/'ArtSourceArchive'/ROOT.name/'GLB/Enemies'/(profile['label']+'_原始.glb')
        download = Path(profile['download'])
        source.parent.mkdir(parents=True, exist_ok=True)
        if download.exists():
            data = download.read_bytes()
            if source.exists(): assert source.read_bytes()==data, 'Different source already archived'
            else: source.write_bytes(data)
        else:
            data = source.read_bytes()
        source_hash = hashlib.sha256(data).hexdigest()
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.context.preferences.filepaths.save_version=0
        bpy.context.scene.unit_settings.system='METRIC'
        bpy.context.scene.unit_settings.scale_length=1
        bpy.ops.import_scene.gltf(filepath=str(source))
        obj = next(o for o in bpy.context.scene.objects if o.type=='MESH')
        obj.data.transform(obj.matrix_world);obj.parent=None;obj.matrix_world=Matrix.Identity(4)
        points=[v.co.copy() for v in obj.data.vertices]
        low=Vector([min(v[i] for v in points) for i in range(3)])
        high=Vector([max(v[i] for v in points) for i in range(3)])
        shift=Vector((-(low.x+high.x)/2,-(low.y+high.y)/2,-low.z))
        obj.data.transform(Matrix.Scale(HEIGHT/(high.z-low.z),4)@Matrix.Translation(shift))
        obj.name=profile['name']+'_Body';obj.data.name=profile['name']+'_Mesh'
        cleanup=helpers.clean_mesh(obj.data)
        model_dir=ART/'Models/Enemies'/profile['name'];working=model_dir/'Source~'
        textures=ART/'Textures/Enemies'/profile['name']
        working.mkdir(parents=True,exist_ok=True);textures.mkdir(parents=True,exist_ok=True)
        images=helpers.export_textures(profile,textures)
        material=helpers.make_material(profile['name'],images)
        obj.data.materials.clear();obj.data.materials.append(material)
        bpy.ops.object.select_all(action='DESELECT')
        rig,segments=build_rig(profile)
        skin(obj,rig,segments,helpers.smoothstep)
        weights=helpers.check_skin(obj,rig)
        bpy.data.orphans_purge(do_recursive=True)
        for image in images.values():image.pack()
        bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);rig.select_set(True)
        bpy.context.view_layer.objects.active=rig
        blend=working/(profile['name']+'.blend');bpy.ops.wm.save_as_mainfile(filepath=str(blend))
        fbx=model_dir/(profile['name']+'.fbx')
        bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'MESH','ARMATURE'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,path_mode='RELATIVE',embed_textures=False,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS')
        assert hashlib.sha256(source.read_bytes()).hexdigest()==source_hash
        reports.append(dict(name=profile['name'],label=profile['label'],fbx=str(fbx),blend=str(blend),cleanup=cleanup,skin=weights,height=HEIGHT))
        print('EYE_BUG_EXPORTED '+profile['name'],flush=True)
    report=ROOT/'.utmp/eye-bugs/prepared.json'
    report.parent.mkdir(parents=True,exist_ok=True)
    report.write_text(json.dumps(reports,ensure_ascii=False,indent=2),encoding='utf-8')


if __name__=='__main__':main()
