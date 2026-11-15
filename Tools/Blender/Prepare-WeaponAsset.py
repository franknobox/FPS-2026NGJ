"""Export two independent rigid weapons with grip origins and muzzle markers."""
import bpy,bmesh,hashlib,importlib.util,json,sys
from pathlib import Path
from mathutils import Vector,Matrix
import numpy as np

ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'Assets/ART'
NAME='SonicWeapon'

def main():
    sys.dont_write_bytecode=True
    spec=importlib.util.spec_from_file_location('enemy_asset_tools',Path(__file__).with_name('Prepare-EnemyAssets.py'))
    helpers=importlib.util.module_from_spec(spec);spec.loader.exec_module(helpers)
    source=ROOT.parent/'ArtSourceArchive'/ROOT.name/'GLB/Weapons/武器_原始.glb'
    original_hash=hashlib.sha256(source.read_bytes()).hexdigest()
    output=ART/'Models/Weapons'/NAME;working=output/'Source~';textures=ART/'Textures/Weapons'/NAME
    report_dir=ROOT/'.utmp/weapon-preparation'
    for directory in [working,textures,report_dir]:directory.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version=0
    bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene.unit_settings.scale_length=1
    bpy.ops.import_scene.gltf(filepath=str(source))
    obj=next(o for o in bpy.context.scene.objects if o.type=='MESH')
    obj.data.transform(obj.matrix_world);obj.parent=None;obj.matrix_world=Matrix.Identity(4)
    xs=sorted(set(round(v.co.x,7) for v in obj.data.vertices))
    gap,divider=max((b-a,(a+b)/2) for a,b in zip(xs,xs[1:]))
    assert gap>.04
    cleanup=helpers.clean_mesh(obj.data)
    images=helpers.export_textures(dict(name=NAME),textures)
    material=helpers.make_material(NAME,images)
    obj.data.materials.clear();obj.data.materials.append(material)
    template=obj.data.copy()
    bpy.data.objects.remove(obj,do_unlink=True)
    results=[]
    # Unity's FBX import maps this export convention's Blender +Y to Unity +Z.
    rotation=Matrix.Rotation(1.5707963267948966,4,'Z')
    for name,keep_left in [('霰弹枪',True),('爪型枪',False)]:
        for o in list(bpy.context.scene.objects):bpy.data.objects.remove(o,do_unlink=True)
        mesh=template.copy();bm=bmesh.new();bm.from_mesh(mesh)
        remove=[v for v in bm.verts if (v.co.x>divider if keep_left else v.co.x<divider)]
        bmesh.ops.delete(bm,geom=remove,context='VERTS');bm.to_mesh(mesh);bm.free();mesh.update()
        points=np.array([list(v.co) for v in mesh.vertices])
        low=points.min(axis=0);high=points.max(axis=0)
        if keep_left:
            handle=points[(points[:,0]>-.20)&(points[:,0]<-.05)&(points[:,2]<.085)]
            assert len(handle)>50
            grip=Vector((float(np.median(handle[:,0])),float((low[1]+high[1])/2),float(np.median(handle[:,2]))))
        else:
            grip=Vector((float(low[0]+.055),float((low[1]+high[1])/2),float((low[2]+high[2])/2)))
        tip=points[points[:,0]>high[0]-.025]
        muzzle_original=Vector((float(high[0]+.005),float((tip[:,1].min()+tip[:,1].max())/2),float((tip[:,2].min()+tip[:,2].max())/2)))
        transform=rotation@Matrix.Translation(-grip)
        mesh.transform(transform);mesh.name=name+'_Mesh'
        root=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(root)
        body=bpy.data.objects.new('Body',mesh);bpy.context.collection.objects.link(body);body.parent=root
        for marker,location in [('GripSocket',Vector((0,0,0))),('Muzzle',transform@muzzle_original)]:
            empty=bpy.data.objects.new(marker,None);bpy.context.collection.objects.link(empty);empty.parent=root;empty.location=location;empty.empty_display_size=.025
        root['ForwardInBlender']='+Y';root['Origin']='Grip; initial placement for viewmodel integration'
        for image in images.values():image.pack()
        bpy.ops.object.select_all(action='SELECT');bpy.context.view_layer.objects.active=root
        blend=working/(name+'.blend');bpy.ops.wm.save_as_mainfile(filepath=str(blend))
        fbx=output/(name+'.fbx')
        bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'EMPTY','MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,path_mode='RELATIVE',embed_textures=False,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS')
        results.append(dict(name=name,fbx=str(fbx),blend=str(blend),mesh=helpers.statistics(mesh),original_grip=list(grip),muzzle_blender=list(transform@muzzle_original),source_dimensions=list(high-low)))
    assert hashlib.sha256(source.read_bytes()).hexdigest()==original_hash
    (report_dir/'independent-weapons.json').write_text(json.dumps(dict(weapons=results,cleanup=cleanup,source_sha256=original_hash),indent=2),encoding='utf-8')
    print('INDEPENDENT_WEAPONS_EXPORTED',flush=True)

if __name__=='__main__':main()
