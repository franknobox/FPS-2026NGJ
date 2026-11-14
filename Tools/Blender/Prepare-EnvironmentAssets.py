"""Prepare static environment GLBs: immutable sources, spatial split, UV-safe LODs.

Run with Invoke-Blender.ps1. Names, working dimensions and triangle budgets are
kept in PROFILES. Unity builds the placement prefabs from EnvironmentManifest.json.
"""
import bpy
import bmesh
import hashlib
import importlib.util
import json
import math
import shutil
import sys
from pathlib import Path
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'Assets/ART'
MODELS = ART / 'Models/Environment'
REPORT = ROOT / '.utmp/environment-preparation'
PROFILES = [
    dict(id='EnergyStation', label='能源站', source='nengliangzhan.glb', axis=2, size=4, target=15000),
    dict(id='OutpostCommand', label='前哨指挥楼', source='前哨指挥楼.glb', axis=2, size=8, target=11000),
    dict(id='Bridge', label='桥', source='bridge.glb', axis=0, size=12, target=18000),
    dict(id='Tower', label='塔', source='tower.glb', axis=2, size=12, target=14000),
    dict(id='Container', label='集装箱', source='jizhuangxiang.glb', axis=1, size=6, target=10000),
    dict(id='Wall01', label='墙体01', source='95803632545ac9dd9038ee6f47c4703a.glb', axis=0, size=4, target=10000),
    dict(id='Wall02', label='墙体02_转角', source='a91e7e4054dbd8500598e3ff0a264d4a.glb', axis=0, size=4, target=12000),
    dict(id='Wall03', label='墙体03', source='1b65b574656a186fc51a2433647c77b1.glb', axis=0, size=4, target=10000),
    dict(id='Roadblocks', label='路障合集', source='luzhang.glb', split=True, scale=6, target=3500),
]


def bounds(mesh):
    pts = np.asarray([tuple(v.co) for v in mesh.vertices], dtype=float)
    return pts.min(axis=0), pts.max(axis=0)


def triangles(mesh):
    mesh.calc_loop_triangles()
    return len(mesh.loop_triangles)


def surface_distance(source, result):
    """Sample in both directions, so lost parts and spurious bridges are detected."""
    trees = [BVHTree.FromPolygons([v.co for v in mesh.vertices], [p.vertices[:] for p in mesh.polygons])
             for mesh in (source, result)]
    distances = []
    for mesh, tree in ((source, trees[1]), (result, trees[0])):
        step = max(1, len(mesh.vertices) // 6000)
        for vertex in list(mesh.vertices)[::step]:
            nearest = tree.find_nearest(vertex.co)
            distances.append(float(nearest[3]) if nearest[0] is not None else float('inf'))
    low, high = bounds(source)
    diagonal = float(np.linalg.norm(high - low))
    return dict(max_relative=float(max(distances) / diagonal),
                p99_relative=float(np.percentile(distances, 99) / diagonal),
                samples=len(distances))


def reduced(source, target, name, validate=False):
    total = triangles(source)
    desired = min(target, total)
    while True:
        mesh = source.copy()
        obj = bpy.data.objects.new('ReductionWork', mesh)
        bpy.context.collection.objects.link(obj)
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        if desired < total:
            modifier = obj.modifiers.new('TriangleBudget', 'DECIMATE')
            modifier.decimate_type = 'COLLAPSE'
            modifier.ratio = min(1.0, desired / total)
            modifier.use_collapse_triangulate = True
            bpy.ops.object.modifier_apply(modifier=modifier.name)
        result = obj.data
        distance = surface_distance(source, result)
        acceptable = distance['max_relative'] <= .025 and distance['p99_relative'] <= .004
        if not validate or acceptable or desired >= total:
            break
        bpy.data.objects.remove(obj, do_unlink=True)
        bpy.data.meshes.remove(result)
        desired = min(total, max(desired + 1000, int(desired * 1.35)))
    result.name = name
    bpy.data.objects.remove(obj, do_unlink=True)
    return result, distance


def split_roadblocks(mesh):
    """Six vertically arranged source shapes, including each shape's loose details.

    Derive gaps from projected triangle intervals, rather than separating loose
    parts: the source has 22 disconnected islands but only six actual props.
    """
    intervals = sorted((min(mesh.vertices[v].co.z for v in p.vertices),
                        max(mesh.vertices[v].co.z for v in p.vertices)) for p in mesh.polygons)
    bands = []
    for low, high in intervals:
        if not bands or low > bands[-1][1] + 1e-4:
            bands.append([low, high])
        else:
            bands[-1][1] = max(bands[-1][1], high)
    assert len(bands) == 6, ('Expected six roadblocks', bands)
    results = []
    for low, high in bands:
        piece = mesh.copy()
        bm = bmesh.new()
        bm.from_mesh(piece)
        remove = [v for v in bm.verts if v.co.z < low - 1e-5 or v.co.z > high + 1e-5]
        bmesh.ops.delete(bm, geom=remove, context='VERTS')
        bm.to_mesh(piece)
        bm.free()
        results.append(piece)
    assert sum(triangles(m) for m in results) == triangles(mesh), 'Split lost faces'
    return results, bands


def prepare_piece(profile, mesh, index, material, images, source_stats):
    is_split = profile.get('split', False)
    name = 'Roadblock%02d' % (index + 1) if is_split else profile['id']
    label = '路障%02d' % (index + 1) if is_split else profile['label']
    low, high = bounds(mesh)
    factor = profile['scale'] if is_split else profile['size'] / (high[profile['axis']] - low[profile['axis']])
    shift = Vector((-(low[0] + high[0]) / 2, -(low[1] + high[1]) / 2, -low[2]))
    mesh.transform(Matrix.Scale(float(factor), 4) @ Matrix.Translation(shift))
    mesh.materials.clear()
    mesh.materials.append(material)
    source_triangles = triangles(mesh)
    lod0, distance0 = reduced(mesh, profile['target'], name + '_LOD0_Mesh', validate=True)
    count0 = triangles(lod0)
    lod1, distance1 = reduced(lod0, max(400, int(count0 * .5)), name + '_LOD1_Mesh')
    lod2, distance2 = reduced(lod0, max(200, int(count0 * .22)), name + '_LOD2_Mesh')
    output = MODELS / name
    working = output / 'Source~'
    working.mkdir(parents=True, exist_ok=True)
    root = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(root)
    root['WorkingScale'] = 'meters; art working size, adjustable in profile'
    root['SourceAsset'] = profile['id']
    root['Origin'] = 'ground level, horizontal bounds center'
    objects = [root]
    counts = []
    for level, lod in enumerate((lod0, lod1, lod2)):
        assert len(lod.uv_layers) > 0
        assert all(math.isfinite(v) for vertex in lod.vertices for v in vertex.co)
        assert all(math.isfinite(v) for loop in lod.uv_layers.active.data for v in loop.uv)
        assert triangles(lod) > 0
        body = bpy.data.objects.new('LOD%d' % level, lod)
        bpy.context.collection.objects.link(body)
        body.parent = root
        objects.append(body)
        counts.append(triangles(lod))
    for image in images.values():
        if not image.packed_file:
            image.pack()
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    blend = working / (name + '.blend')
    bpy.ops.wm.save_as_mainfile(filepath=str(blend))
    fbx = output / (name + '.fbx')
    bpy.ops.export_scene.fbx(filepath=str(fbx), use_selection=True, object_types={'EMPTY', 'MESH'},
                            axis_forward='-Z', axis_up='Y', bake_anim=False, add_leaf_bones=False,
                            path_mode='RELATIVE', embed_textures=False, apply_unit_scale=True,
                            apply_scale_options='FBX_SCALE_UNITS')
    # All three are exported, but display only the main mesh when editing the blend.
    objects[2].hide_set(True)
    objects[3].hide_set(True)
    bpy.ops.wm.save_as_mainfile(filepath=str(blend))
    low, high = bounds(lod0)
    result = dict(id=name, label=label, group=profile['id'],
                  fbx=fbx.relative_to(ROOT).as_posix(),
                  materialName=material.name,
                  originalTriangles=source_triangles, triangles=counts,
                  dimensions=[float(v) for v in high - low],
                  scale=float(factor),
                  reduction=1 - count0 / source_triangles,
                  surfaceError=[distance0, distance1, distance2])
    for obj in objects:
        bpy.data.objects.remove(obj, do_unlink=True)
    print('ENVIRONMENT_PREPARED ' + name + ' ' + str(source_triangles) + ' -> ' + str(counts), flush=True)
    return result


def main():
    sys.dont_write_bytecode = True
    spec = importlib.util.spec_from_file_location('environment_helpers', Path(__file__).with_name('Prepare-EnemyAssets.py'))
    helpers = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(helpers)
    REPORT.mkdir(parents=True, exist_ok=True)
    source_dir = ROOT.parent / 'ArtSourceArchive' / ROOT.name / 'GLB/Environment'
    source_dir.mkdir(parents=True, exist_ok=True)
    entries, sources = [], []
    for profile in PROFILES:
        download = Path('E:/Download') / profile['source']
        source = source_dir / (profile['label'] + '_原始.glb')
        if download.exists():
            digest = hashlib.sha256(download.read_bytes()).hexdigest()
            if source.exists():
                assert hashlib.sha256(source.read_bytes()).hexdigest() == digest, 'Archived source mismatch'
            else:
                shutil.copy2(download, source)
        assert source.exists(), 'Source missing: ' + str(source)
        digest = hashlib.sha256(source.read_bytes()).hexdigest()
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.context.preferences.filepaths.save_version = 0
        bpy.context.scene.unit_settings.system = 'METRIC'
        bpy.context.scene.unit_settings.scale_length = 1
        bpy.ops.import_scene.gltf(filepath=str(source))
        objects = [o for o in bpy.context.scene.objects if o.type == 'MESH']
        assert len(objects) == 1, 'Source structure changed; review before preparing'
        obj = objects[0]
        obj.data.transform(obj.matrix_world)
        obj.parent = None
        obj.matrix_world = Matrix.Identity(4)
        cleanup = helpers.clean_mesh(obj.data)
        textures = ART / 'Textures/Environment' / profile['id']
        textures.mkdir(parents=True, exist_ok=True)
        images = helpers.export_textures(dict(name=profile['id']), textures)
        material = helpers.make_material(profile['id'], images)
        mesh = obj.data
        if profile.get('split'):
            pieces, bands = split_roadblocks(mesh)
        else:
            pieces, bands = [mesh.copy()], []
        bpy.data.objects.remove(obj, do_unlink=True)
        # Keep only the shared final material and 2K images in editable sources.
        for piece in pieces:
            piece.materials.clear()
            piece.materials.append(material)
        for m in list(bpy.data.materials):
            if m != material:
                bpy.data.materials.remove(m)
        for im in list(bpy.data.images):
            if im not in images.values():
                bpy.data.images.remove(im)
        for index, piece in enumerate(pieces):
            entries.append(prepare_piece(profile, piece, index, material, images, cleanup))
        assert hashlib.sha256(source.read_bytes()).hexdigest() == digest
        sources.append(dict(id=profile['id'], label=profile['label'], sha256=digest,
                            source='GLB/Environment/' + source.name, originalDownload=profile['source'],
                            cleanup=cleanup, splitBands=bands))
    manifest = dict(version=1, assets=entries, sources=sources)
    (MODELS / 'EnvironmentManifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    (REPORT / 'prepared.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    print('ENVIRONMENT_PREPARATION_COMPLETE assets=' + str(len(entries)), flush=True)


if __name__ == '__main__':
    main()
