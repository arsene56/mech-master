"""Geometry primitives shared by MechMaster's engineering bicycle pipeline."""

import math
import bpy
from mathutils import Vector


def material(name, color, metallic=0.0, roughness=0.45):
    value = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    value.diffuse_color = (*color, 1.0)
    value.use_nodes = True
    shader = next(node for node in value.node_tree.nodes if node.type == "BSDF_PRINCIPLED")
    shader.inputs["Base Color"].default_value = (*color, 1.0)
    shader.inputs["Metallic"].default_value = metallic
    shader.inputs["Roughness"].default_value = roughness
    return value


def finish(obj, name, mat, collection=None):
    obj.name = name
    if mat is not None and hasattr(obj.data, "materials"):
        obj.data.materials.append(mat)
    if collection is not None:
        for owner in list(obj.users_collection):
            owner.objects.unlink(obj)
        collection.objects.link(obj)
    if obj.type == "MESH":
        for polygon in obj.data.polygons:
            polygon.use_smooth = True
    return obj


def tag(obj, part_id, assembly_id, boundary="individual"):
    obj["mm_part_id"] = part_id
    obj["mm_assembly_id"] = assembly_id
    obj["mm_service_boundary"] = boundary
    obj["mm_lod"] = "source"
    return obj


def box(name, location, dimensions, mat, collection=None, bevel=0.0, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        modifier = obj.modifiers.new("Machined edge", "BEVEL")
        modifier.width = bevel
        modifier.segments = 3
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    return finish(obj, name, mat, collection)


def cylinder(name, start, end, radius, mat, collection=None, vertices=32, bevel=0.0):
    start, end = Vector(start), Vector(end)
    direction = end - start
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=direction.length, location=(start + end) / 2)
    obj = bpy.context.object
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = direction.to_track_quat("Z", "Y")
    obj.rotation_mode = "XYZ"
    if bevel:
        modifier = obj.modifiers.new("Edge radius", "BEVEL")
        modifier.width = bevel
        modifier.segments = 2
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    return finish(obj, name, mat, collection)


def torus(name, location, major, minor, mat, collection=None, rotation=(math.pi / 2, 0, 0), major_segments=96, minor_segments=16):
    bpy.ops.mesh.primitive_torus_add(
        major_radius=major, minor_radius=minor, major_segments=major_segments,
        minor_segments=minor_segments, location=location, rotation=rotation,
    )
    return finish(bpy.context.object, name, mat, collection)


def sphere(name, location, scale, mat, collection=None, segments=28, rings=14):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=location)
    obj = bpy.context.object
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(obj, name, mat, collection)


def annulus(name, center, outer, inner, thickness, mat, collection=None, segments=96, wave=0.0, lobes=0):
    vertices, faces = [], []
    half = thickness / 2
    for y in (-half, half):
        for radial in (0, 1):
            for i in range(segments):
                angle = 2 * math.pi * i / segments
                radius = inner if radial else outer + (wave * math.sin(lobes * angle) if lobes else 0)
                vertices.append((radius * math.cos(angle), y, radius * math.sin(angle)))
    fo, fi, bo, bi = 0, segments, 2 * segments, 3 * segments
    for i in range(segments):
        n = (i + 1) % segments
        faces += [
            (fo+i, fo+n, fi+n, fi+i), (bo+i, bi+i, bi+n, bo+n),
            (fo+i, bo+i, bo+n, fo+n), (fi+i, fi+n, bi+n, bi+i),
        ]
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    (collection or bpy.context.collection).objects.link(obj)
    obj.location = center
    return finish(obj, name, mat)


def gear(name, center, teeth, root, tip, thickness, bore, mat, collection=None):
    segments = teeth * 4
    vertices, faces = [], []
    half = thickness / 2
    for y in (-half, half):
        for radial in (0, 1):
            for i in range(segments):
                angle = 2 * math.pi * i / segments
                radius = bore if radial else (tip if i % 4 in (1, 2) else root)
                vertices.append((radius * math.cos(angle), y, radius * math.sin(angle)))
    fo, fi, bo, bi = 0, segments, 2 * segments, 3 * segments
    for i in range(segments):
        n = (i + 1) % segments
        faces += [
            (fo+i, fo+n, fi+n, fi+i), (bo+i, bi+i, bi+n, bo+n),
            (fo+i, bo+i, bo+n, fo+n), (fi+i, fi+n, bi+n, bi+i),
        ]
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    (collection or bpy.context.collection).objects.link(obj)
    obj.location = center
    result = finish(obj, name, mat)
    for polygon in result.data.polygons:
        polygon.use_smooth = False
    return result


def curve(name, points, radius, mat, collection=None):
    data = bpy.data.curves.new(name + "Curve", "CURVE")
    data.dimensions = "3D"
    data.bevel_depth = radius
    data.bevel_resolution = 3
    spline = data.splines.new("BEZIER")
    spline.bezier_points.add(len(points) - 1)
    for point, value in zip(spline.bezier_points, points):
        point.co = value
        point.handle_left_type = "AUTO"
        point.handle_right_type = "AUTO"
    obj = bpy.data.objects.new(name, data)
    (collection or bpy.context.collection).objects.link(obj)
    return finish(obj, name, mat)


def join(name, objects):
    objects = [obj for obj in objects if obj is not None]
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    objects[0].name = name
    return objects[0]


def bolt(name, start, end, shaft_radius, head_radius, mat, collection=None, head_depth=0.004):
    start, end = Vector(start), Vector(end)
    direction = (end - start).normalized()
    shaft = cylinder(name + "_shaft", start, end, shaft_radius, mat, collection, 20)
    head = cylinder(name + "_head", start - direction * head_depth, start, head_radius, mat, collection, 6, 0.0003)
    return join(name, [shaft, head])


def point_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()
    return obj
