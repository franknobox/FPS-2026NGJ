"""Prepare the two generated enemies; run with Invoke-Blender.ps1.

Source GLBs are immutable. Rig landmarks and the initial height are editable below.
The output is a base deformation rig, without gameplay animation clips.
"""
import bpy
import bmesh
import json
import math
import hashlib
from pathlib import Path
from mathutils import Vector, Matrix
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'Assets/ART'
SOURCE_ARCHIVE = ROOT.parent / 'ArtSourceArchive' / ROOT.name / 'GLB'
REPORT = ROOT / '.utmp/enemy-preparation'
PROFILES = [
    dict(name='CarapaceEnemy', source='甲壳怪_原始.glb', height=2.2, winged=False),
    dict(name='WingedEnemy', source='翼怪_原始.glb', height=1.8, winged=True),
]


def statistics(mesh):
    mesh.calc_loop_triangles()
    return dict(vertices=len(mesh.vertices), triangles=len(mesh.loop_triangles),
                uv_layers=len(mesh.uv_layers), materials=len(mesh.materials))


def clean_mesh(mesh):
    # UVs live on face corners; keep them while removing zero-area faces.
    original = statistics(mesh)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bad = [f for f in bm.faces if f.calc_area() < 1e-12]
    bmesh.ops.delete(bm, geom=bad, context='FACES_ONLY')
    clean_base = mesh.copy()
    bm.to_mesh(clean_base)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-6)
    if any(len(e.link_faces) > 2 for e in bm.edges):
        # Coincident armour surfaces must not be welded into non-manifold junctions.
        bm.free()
        bm = bmesh.new()
        bm.from_mesh(clean_base)
        welded = False
    else:
        welded = True
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context='VERTS')
    bm.normal_update()
    bm.to_mesh(mesh)
    bm.free()
    bpy.data.meshes.remove(clean_base)
    mesh.validate(clean_customdata=False)
    mesh.update()
    return dict(before=original, after=statistics(mesh), removed_zero_area_faces=len(bad),
                coincident_vertices_welded=welded)


def export_textures(profile, output):
    images = [im for im in bpy.data.images if im.source == 'FILE']
    normal = next(im for im in images if 'normal' in im.name.lower())
    packed = next(im for im in images if 'metallic' in im.name.lower())
    color = next(im for im in images if im != normal and im != packed)
    result = {}
    for role, source in [('BaseColor', color), ('Normal', normal)]:
        image = source.copy()
        image.name = profile['name'] + '_' + role
        image.colorspace_settings.name = 'sRGB' if role == 'BaseColor' else 'Non-Color'
        _ = image.pixels[0]
        if max(image.size) > 2048:
            image.scale(2048, 2048)
        image.filepath_raw = str(output / (image.name + '.png'))
        image.file_format = 'PNG'
        image.save()
        # Reload the saved 2K file: an image copied from a GLB can still hold the
        # original packed 4K payload even after its working pixel buffer is scaled.
        saved_path = image.filepath_raw
        bpy.data.images.remove(image)
        image = bpy.data.images.load(saved_path, check_existing=False)
        image.name = profile['name'] + '_' + role
        image.colorspace_settings.name = 'sRGB' if role == 'BaseColor' else 'Non-Color'
        result[role] = image
    mr = packed.copy()
    mr.colorspace_settings.name = 'Non-Color'
    _ = mr.pixels[0]
    mr.scale(2048, 2048)
    width, height = mr.size
    pixels = np.empty(width * height * 4, dtype=np.float32)
    mr.pixels.foreach_get(pixels)
    pixels = pixels.reshape((-1, 4))
    converted = np.zeros_like(pixels)
    converted[:, 0] = pixels[:, 2]  # glTF B metallic -> URP R metallic
    converted[:, 3] = 1.0 - pixels[:, 1]  # glTF G roughness -> URP A smoothness
    image = bpy.data.images.new(profile['name'] + '_MetallicSmoothness', width=width, height=height, alpha=True)
    image.colorspace_settings.name = 'Non-Color'
    image.pixels.foreach_set(converted.ravel())
    image.update()
    image.file_format = 'PNG'
    image.filepath_raw = str(output / (image.name + '.png'))
    image.save()
    result['MetallicSmoothness'] = image
    bpy.data.images.remove(mr)
    return result


def make_material(name, images):
    material = bpy.data.materials.new(name + '_Body')
    material.use_nodes = True
    tree = material.node_tree
    shader = tree.nodes.get('Principled BSDF')
    for role, image in images.items():
        node = tree.nodes.new('ShaderNodeTexImage')
        node.name = role
        node.label = role
        node.image = image
        if role == 'BaseColor':
            node.location = (-600, 240)
            tree.links.new(node.outputs['Color'], shader.inputs['Base Color'])
        elif role == 'Normal':
            node.location = (-600, -120)
            normal = tree.nodes.new('ShaderNodeNormalMap')
            normal.location = (-240, -100)
            tree.links.new(node.outputs['Color'], normal.inputs['Color'])
            tree.links.new(normal.outputs['Normal'], shader.inputs['Normal'])
        else:
            node.location = (-600, -450)
            channels = tree.nodes.new('ShaderNodeSeparateColor')
            channels.location = (-350, -380)
            tree.links.new(node.outputs['Color'], channels.inputs['Color'])
            tree.links.new(channels.outputs['Red'], shader.inputs['Metallic'])
            invert = tree.nodes.new('ShaderNodeMath')
            invert.operation = 'SUBTRACT'
            invert.inputs[0].default_value = 1
            invert.location = (-150, -480)
            tree.links.new(node.outputs['Alpha'], invert.inputs[1])
            tree.links.new(invert.outputs[0], shader.inputs['Roughness'])
    return material


def make_rig(profile):
    winged = profile['winged']
    height = profile['height']
    points = []
    def bone(name, head, tail, parent=None, deform=True):
        points.append((name, Vector(head) * height, Vector(tail) * height, parent, deform))
    if not winged:
        bone('Root', (0, 0, 0), (0, 0, .12), deform=False)
        bone('Pelvis', (0, .01, .33), (0, .01, .41), 'Root')
        bone('Spine', (0, .01, .41), (0, .01, .54), 'Pelvis')
        bone('Chest', (0, .01, .54), (0, .01, .68), 'Spine')
        bone('Neck', (0, .01, .68), (0, 0, .75), 'Chest')
        bone('Head', (0, 0, .75), (0, -.02, .93), 'Neck')
        bone('SoundSocket', (0, -.20, .77), (0, -.26, .77), 'Head', False)
        for side, sign in [('L', 1), ('R', -1)]:
            bone('UpperArm.' + side, (.19*sign, .01, .68), (.275*sign, -.005, .48), 'Chest')
            bone('Forearm.' + side, (.275*sign, -.005, .48), (.285*sign, -.035, .285), 'UpperArm.' + side)
            bone('Hand.' + side, (.285*sign, -.035, .285), (.285*sign, -.075, .18), 'Forearm.' + side)
            bone('Thigh.' + side, (.105*sign, .015, .34), (.13*sign, -.025, .205), 'Pelvis')
            bone('Shin.' + side, (.13*sign, -.025, .205), (.13*sign, .005, .075), 'Thigh.' + side)
            bone('Foot.' + side, (.13*sign, .005, .075), (.13*sign, -.13, .025), 'Shin.' + side)
    else:
        bone('Root', (0, 0, 0), (0, 0, .12), deform=False)
        bone('Pelvis', (0, .045, .40), (0, .04, .47), 'Root')
        bone('Spine', (0, .04, .47), (0, .035, .55), 'Pelvis')
        bone('Chest', (0, .035, .55), (0, .03, .63), 'Spine')
        bone('Neck', (0, .03, .63), (0, -.055, .65), 'Chest')
        bone('Head', (0, -.055, .65), (0, -.19, .65), 'Neck')
        bone('Jaw', (0, -.06, .57), (0, -.21, .52), 'Head')
        bone('SoundSocket', (0, -.20, .65), (0, -.25, .65), 'Head', False)
        for side, sign in [('L', 1), ('R', -1)]:
            bone('UpperArm.' + side, (.12*sign, .04, .59), (.22*sign, .04, .51), 'Chest')
            bone('Forearm.' + side, (.22*sign, .04, .51), (.31*sign, .04, .44), 'UpperArm.' + side)
            bone('Hand.' + side, (.31*sign, .04, .44), (.39*sign, .04, .37), 'Forearm.' + side)
            bone('Thigh.' + side, (.075*sign, .03, .43), (.115*sign, -.02, .31), 'Pelvis')
            bone('Shin.' + side, (.115*sign, -.02, .31), (.15*sign, .01, .075), 'Thigh.' + side)
            bone('Foot.' + side, (.15*sign, .01, .075), (.16*sign, -.07, .025), 'Shin.' + side)
            bone('WingBase.' + side, (.09*sign, .075, .64), (.20*sign, .07, .78), 'Chest')
            bone('WingTip.' + side, (.20*sign, .07, .78), (.36*sign, .04, .98), 'WingBase.' + side)
    data = bpy.data.armatures.new(profile['name'] + '_Skeleton')
    rig = bpy.data.objects.new(profile['name'] + '_Rig', data)
    bpy.context.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    for name, head, tail, parent, deform in points:
        item = data.edit_bones.new(name)
        item.head, item.tail = head, tail
        item.use_deform = deform
        if parent:
            item.parent = data.edit_bones[parent]
    bpy.ops.object.mode_set(mode='OBJECT')
    rig.show_in_front = True
    rig['RigType'] = 'Base deformation rig; adjust landmarks and weights for final animation'
    rig['HeightMeters'] = height
    rig['ForwardInBlender'] = '-Y'
    return rig


def smoothstep(a, b, value):
    t = min(1, max(0, (value-a)/(b-a)))
    return t*t*(3-2*t)


def chain(z, low, middle, high, lower_joint, upper_joint, width):
    upper = smoothstep(upper_joint-width, upper_joint+width, z)
    lower = 1-smoothstep(lower_joint-width, lower_joint+width, z)
    return {low: lower, middle: (1-lower)*(1-upper), high: (1-lower)*upper}


def skin(mesh_obj, rig, profile):
    height = profile['height']
    groups = {bone.name: mesh_obj.vertex_groups.new(name=bone.name)
              for bone in rig.data.bones if bone.use_deform}
    for vertex in mesh_obj.data.vertices:
        x, y, z = vertex.co / height
        side = 'L' if x >= 0 else 'R'
        ax = abs(x)
        def mix(weights, other, factor):
            result = {key:value*(1-factor) for key,value in weights.items()}
            for key,value in other.items():
                result[key] = result.get(key,0) + factor*value
            return result
        if not profile['winged']:
            weights = chain(z, 'Pelvis', 'Spine', 'Chest', .40, .53, .045)
            weights = mix(weights, {'Head':1}, smoothstep(.69,.76,z))
            leg = (1-smoothstep(.32,.37,z))*(1-smoothstep(.18,.22,ax))*smoothstep(.025,.065,ax)
            weights = mix(weights, chain(z,'Foot.'+side,'Shin.'+side,'Thigh.'+side,.075,.205,.028),leg)
            arm = smoothstep(.165,.225,ax)*(1-smoothstep(.72,.82,z))*smoothstep(.12,.18,z)
            weights = mix(weights, chain(z,'Hand.'+side,'Forearm.'+side,'UpperArm.'+side,.285,.48,.035),arm)
        else:
            weights = chain(z, 'Pelvis', 'Spine', 'Chest', .46, .55, .03)
            head = (1-smoothstep(-.09,-.03,y))*smoothstep(.49,.59,z)
            weights = mix(weights, {'Head':1}, head)
            leg = (1-smoothstep(.38,.46,z))*(1-smoothstep(.16,.23,ax))*smoothstep(.025,.065,ax)
            weights = mix(weights,chain(z,'Foot.'+side,'Shin.'+side,'Thigh.'+side,.075,.31,.025),leg)
            fore = smoothstep(.18,.25,ax)
            hand = smoothstep(.29,.34,ax)
            arm = smoothstep(.11,.19,ax)*(1-smoothstep(.60,.68,z))*smoothstep(.32,.39,z)
            weights = mix(weights,{'UpperArm.'+side:1-fore,'Forearm.'+side:fore*(1-hand),'Hand.'+side:fore*hand},arm)
            wing = smoothstep(.055,.12,ax)*smoothstep(.635,.71,z)*smoothstep(-.045,.015,y)
            tip = smoothstep(.75,.83,z)
            weights = mix(weights,{'WingBase.'+side:1-tip,'WingTip.'+side:tip},wing)
            jaw = smoothstep(.48,.515,z)*(1-smoothstep(.53,.58,z))*(1-smoothstep(-.15,-.09,y))*(1-smoothstep(.065,.105,ax))
            weights = mix(weights,{'Jaw':1},jaw)
        weights = dict(sorted(((key,value) for key,value in weights.items() if value>.0001),key=lambda item:item[1],reverse=True)[:4])
        total = sum(weights.values())
        for name, weight in weights.items():
            groups[name].add([vertex.index], weight/total, 'REPLACE')
    modifier = mesh_obj.modifiers.new('EnemySkin', 'ARMATURE')
    modifier.object = rig
    mesh_obj.parent = rig
    return modifier


def check_skin(obj, rig):
    unweighted = bad_sum = too_many = 0
    for vertex in obj.data.vertices:
        weights = [g.weight for g in vertex.groups if g.weight > .00001]
        unweighted += not weights
        bad_sum += abs(sum(weights)-1)>1e-5
        too_many += len(weights)>4
    assert not (unweighted or bad_sum or too_many), 'Invalid skin weights'
    assert all(math.isfinite(c) for vertex in obj.data.vertices for c in vertex.co), 'Invalid vertex position'
    return dict(unweighted_vertices=unweighted, non_normalized_vertices=bad_sum,
                vertices_over_four_influences=too_many, bones=len(rig.data.bones))


def main():
    REPORT.mkdir(parents=True, exist_ok=True)
    reports=[]
    for profile in PROFILES:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.context.preferences.filepaths.save_version = 0
        bpy.context.scene.unit_settings.system = 'METRIC'
        bpy.context.scene.unit_settings.scale_length = 1
        source=SOURCE_ARCHIVE/'Enemies'/profile['source']
        source_hash=hashlib.sha256(source.read_bytes()).hexdigest()
        bpy.ops.import_scene.gltf(filepath=str(source))
        obj=next(o for o in bpy.context.scene.objects if o.type=='MESH')
        obj.data.transform(obj.matrix_world)
        obj.parent=None
        obj.matrix_world=Matrix.Identity(4)
        vertices=[v.co.copy() for v in obj.data.vertices]
        low=Vector([min(v[i] for v in vertices) for i in range(3)])
        high=Vector([max(v[i] for v in vertices) for i in range(3)])
        factor=profile['height']/(high.z-low.z)
        shift=Vector((-(low.x+high.x)/2,-(low.y+high.y)/2,-low.z))
        obj.data.transform(Matrix.Scale(factor,4) @ Matrix.Translation(shift))
        obj.name=profile['name']+'_Body'
        obj.data.name=profile['name']+'_Mesh'
        cleanup=clean_mesh(obj.data)
        model_dir=ART/'Models/Enemies'/profile['name']
        blend_dir=model_dir/'Source~'
        texture_dir=ART/'Textures/Enemies'/profile['name']
        blend_dir.mkdir(parents=True,exist_ok=True)
        texture_dir.mkdir(parents=True,exist_ok=True)
        images=export_textures(profile,texture_dir)
        material=make_material(profile['name'],images)
        obj.data.materials.clear()
        obj.data.materials.append(material)
        bpy.ops.object.select_all(action='DESELECT')
        rig=make_rig(profile)
        skin(obj,rig,profile)
        weights=check_skin(obj,rig)
        for image in list(bpy.data.images):
            if image not in images.values() and image.users==0:
                bpy.data.images.remove(image)
        # Purge unused imported materials/images before packing the editable source.
        bpy.data.orphans_purge(do_recursive=True)
        for image in images.values():
            image.pack()
        bpy.context.scene.render.fps=30
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True);rig.select_set(True)
        bpy.context.view_layer.objects.active=rig
        bpy.ops.wm.save_as_mainfile(filepath=str(blend_dir/(profile['name']+'.blend')))
        fbx=model_dir/(profile['name']+'.fbx')
        bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'ARMATURE','MESH'},
            axis_forward='-Z',axis_up='Y',add_leaf_bones=False,use_armature_deform_only=False,
            bake_anim=False,path_mode='RELATIVE',embed_textures=False,use_mesh_modifiers=True,
            apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS')
        assert hashlib.sha256(source.read_bytes()).hexdigest()==source_hash
        reports.append(dict(name=profile['name'],source=str(source),source_sha256=source_hash,
            height_m=profile['height'],cleanup=cleanup,skin=weights,
            fbx=str(fbx),blend=str(blend_dir/(profile['name']+'.blend')),
            textures={key:str(texture_dir/(profile['name']+'_'+key+'.png')) for key in images},
            animations=0,rig_status='Base rig; final motion-specific weight refinement remains'))
        print('ENEMY_PREPARED '+profile['name'],flush=True)
    (REPORT/'prepared.json').write_text(json.dumps(reports,ensure_ascii=False,indent=2),encoding='utf-8')
    print('ENEMY_PREPARATION_COMPLETE',flush=True)


if __name__=='__main__':
    main()
