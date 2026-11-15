"""Prepare generated shotgun/sniper as rigid FBX assets; preserve original GLBs."""
import bpy,sys,importlib.util,json,math
from pathlib import Path
from mathutils import Matrix,Vector
ROOT=Path(__file__).resolve().parents[2]
sys.dont_write_bytecode=True
spec=importlib.util.spec_from_file_location('asset_helpers',Path(__file__).with_name('Prepare-EnemyAssets.py'))
h=importlib.util.module_from_spec(spec);spec.loader.exec_module(h)
profiles=[('OrganicShotgun',r'E:\Download\f706c7aca0bafdf695b5d2880fb39853.glb',20000,1.0,.1),('OrganicSniper',r'E:\Download\20261115152737_7d85b9b9.glb',None,1.2,.075)]
records=[]
for name,path,target,length,grip_z in profiles:
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.context.preferences.filepaths.save_version=0
 bpy.ops.import_scene.gltf(filepath=path)
 meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
 assert len(meshes)==1
 obj=meshes[0];obj.data.transform(obj.matrix_world);obj.parent=None;obj.matrix_world=Matrix.Identity(4)
 before=h.statistics(obj.data);h.clean_mesh(obj.data)
 if target and before['triangles']>target:
  bpy.context.view_layer.objects.active=obj;obj.select_set(True)
  modifier=obj.modifiers.new('Game mesh simplification','DECIMATE');modifier.ratio=target/h.statistics(obj.data)['triangles'];modifier.use_collapse_triangulate=True
  bpy.ops.object.modifier_apply(modifier=modifier.name)
 output=ROOT/'Assets/ART/Models/Weapons'/name;tex=ROOT/'Assets/ART/Textures/Weapons'/name
 (output/'Source~').mkdir(parents=True,exist_ok=True);tex.mkdir(parents=True,exist_ok=True)
 images=h.export_textures(dict(name=name),tex);material=h.make_material(name,images)
 obj.data.materials.clear();obj.data.materials.append(material)
 lo=Vector([min(v.co[i] for v in obj.data.vertices) for i in range(3)]);hi=Vector([max(v.co[i] for v in obj.data.vertices) for i in range(3)])
 scale=length/(hi.x-lo.x);grip=Vector((-.28,(lo.y+hi.y)/2,grip_z))
 tip=[v.co for v in obj.data.vertices if v.co.x>hi.x-.025]
 muzzle=Vector((hi.x+.005,(lo.y+hi.y)/2,sum(v.z for v in tip)/len(tip)))
 transform=Matrix.Scale(scale,4)@Matrix.Rotation(math.pi/2,4,'Z')@Matrix.Translation(-grip)
 obj.data.transform(transform);obj.name='Body';obj.data.name=name+'_Mesh'
 root=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(root);obj.parent=root
 for tag,position in [('GripSocket',Vector((0,0,0))),('Muzzle',transform@muzzle)]:
  marker=bpy.data.objects.new(tag,None);bpy.context.collection.objects.link(marker);marker.parent=root;marker.location=position
 for im in images.values():im.pack()
 bpy.ops.object.select_all(action='SELECT')
 bpy.ops.wm.save_as_mainfile(filepath=str(output/'Source~'/f'{name}.blend'))
 bpy.ops.export_scene.fbx(filepath=str(output/f'{name}.fbx'),use_selection=True,object_types={'EMPTY','MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,path_mode='RELATIVE',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS')
 records.append(dict(name=name,source=path,before=before,after=h.statistics(obj.data),muzzle_blender=list(transform@muzzle),length=length))
report=ROOT/'.utmp/weapon-preparation/new-prepared.json';report.write_text(json.dumps(records,indent=2),encoding='utf-8')
print('NEW_WEAPONS_PREPARED',flush=True)
