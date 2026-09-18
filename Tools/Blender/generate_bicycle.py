"""Generate the original MechMaster bicycle prototype in real-world metre units."""

import math
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[2]
BLEND_PATH = ROOT / "Assets" / "Art" / "Models" / "Source" / "BicyclePrototype.blend"
FBX_PATH = ROOT / "Assets" / "Resources" / "BicyclePrototype.fbx"
PREVIEW_PATH = ROOT / "Docs" / "Preview" / "BicyclePrototype.png"
FRONT_BRAKE_PREVIEW_PATH = ROOT / "Docs" / "Preview" / "FrontBrakePrototype.png"


def reset_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in (bpy.data.meshes, bpy.data.curves, bpy.data.materials):
        for item in list(collection):
            if item.users == 0:
                collection.remove(item)


def material(name, color, metallic=0.0, roughness=0.45):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    mat.use_nodes = True
    principled = next(
        (node for node in mat.node_tree.nodes if node.type == "BSDF_PRINCIPLED"),
        None,
    )
    if principled is None:
        principled = mat.node_tree.nodes.new("ShaderNodeBsdfPrincipled")
    principled.inputs["Base Color"].default_value = (*color, 1.0)
    principled.inputs["Metallic"].default_value = metallic
    principled.inputs["Roughness"].default_value = roughness
    return mat


def assign(obj, mat):
    if hasattr(obj.data, "materials"):
        obj.data.materials.append(mat)
    return obj


def cylinder_between(name, start, end, radius, mat, vertices=20):
    start = Vector(start)
    end = Vector(end)
    direction = end - start
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=direction.length,
        location=(start + end) * 0.5,
    )
    obj = bpy.context.object
    obj.name = name
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = direction.to_track_quat("Z", "Y")
    obj.rotation_mode = "XYZ"
    return assign(obj, mat)


def box(name, location, dimensions, mat, bevel=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel > 0.0:
        modifier = obj.modifiers.new("Soft machined edges", "BEVEL")
        modifier.width = bevel
        modifier.segments = 2
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    return assign(obj, mat)


def torus(
    name,
    location,
    major_radius,
    minor_radius,
    mat,
    rotation=(0.0, 0.0, 0.0),
    major_segments=64,
    minor_segments=10,
):
    bpy.ops.mesh.primitive_torus_add(
        align="WORLD",
        major_segments=major_segments,
        minor_segments=minor_segments,
        location=location,
        rotation=rotation,
        major_radius=major_radius,
        minor_radius=minor_radius,
    )
    obj = bpy.context.object
    obj.name = name
    return assign(obj, mat)


def join(name, objects):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    objects[0].name = name
    return objects[0]


def ring_mesh(name, center, outer_radius, inner_radius, thickness, mat, segments=64):
    vertices = []
    faces = []
    half = thickness * 0.5
    for y in (-half, half):
        for radius in (outer_radius, inner_radius):
            for index in range(segments):
                angle = 2.0 * math.pi * index / segments
                vertices.append((radius * math.cos(angle), y, radius * math.sin(angle)))

    front_outer = 0
    front_inner = segments
    back_outer = segments * 2
    back_inner = segments * 3
    for index in range(segments):
        nxt = (index + 1) % segments
        faces.extend(
            [
                (front_outer + index, front_outer + nxt, front_inner + nxt, front_inner + index),
                (back_outer + index, back_inner + index, back_inner + nxt, back_outer + nxt),
                (front_outer + index, back_outer + index, back_outer + nxt, front_outer + nxt),
                (front_inner + index, front_inner + nxt, back_inner + nxt, back_inner + index),
            ]
        )

    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    obj.location = center
    bpy.context.collection.objects.link(obj)
    return assign(obj, mat)


def create_wheel(prefix, center, mats, interactive=False):
    x, y, z = center
    tire = torus(
        prefix + "_Tire", center, 0.329, 0.02, mats["rubber"],
        rotation=(math.pi / 2, 0, 0), minor_segments=12,
    )
    rim = torus(
        prefix + "_Rim", center, 0.292, 0.01, mats["metal"],
        rotation=(math.pi / 2, 0, 0), minor_segments=8,
    )
    hub = cylinder_between(
        prefix + "_Hub", (x, y - 0.055, z), (x, y + 0.055, z),
        0.026, mats["dark_metal"], 24,
    )

    spokes = []
    spoke_count = 32
    for index in range(spoke_count):
        angle = 2.0 * math.pi * index / spoke_count
        rim_point = (x + 0.283 * math.cos(angle), y, z + 0.283 * math.sin(angle))
        flange_y = y + (0.038 if index % 2 == 0 else -0.038)
        hub_angle = angle + (0.16 if index % 2 == 0 else -0.16)
        hub_point = (
            x + 0.022 * math.cos(hub_angle),
            flange_y,
            z + 0.022 * math.sin(hub_angle),
        )
        spokes.append(
            cylinder_between(prefix + "_Spoke", hub_point, rim_point, 0.0011, mats["spoke"], 8)
        )
    spoke_group = join(prefix + "_Spokes", spokes)

    if interactive:
        caps = [
            cylinder_between(
                "Front_EndCap", (x, y - 0.066, z), (x, y - 0.054, z),
                0.019, mats["dark_metal"], 20,
            ),
            cylinder_between(
                "Front_EndCap", (x, y + 0.054, z), (x, y + 0.066, z),
                0.019, mats["dark_metal"], 20,
            ),
        ]
        join("Front_EndCaps", caps)

    return tire, rim, hub, spoke_group


def create_chain(name, rear_center, front_center, mats):
    curve = bpy.data.curves.new(name + "Curve", "CURVE")
    curve.dimensions = "3D"
    curve.bevel_depth = 0.003
    curve.bevel_resolution = 2
    spline = curve.splines.new("POLY")
    points = [
        (rear_center[0], -0.075, rear_center[2] + 0.105),
        (front_center[0], -0.075, front_center[2] + 0.092),
        (front_center[0], -0.075, front_center[2] - 0.092),
        (rear_center[0], -0.075, rear_center[2] - 0.105),
        (rear_center[0], -0.075, rear_center[2] + 0.105),
    ]
    spline.points.add(len(points) - 1)
    for point, value in zip(spline.points, points):
        point.co = (*value, 1.0)
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    assign(obj, mats["chain"])
    return obj


def point_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def setup_preview():
    bpy.ops.object.camera_add(location=(1.95, -3.2, 1.3))
    camera = bpy.context.object
    camera.name = "Preview_Camera"
    camera.data.lens = 64
    point_at(camera, (0.0, 0.0, 0.54))
    bpy.context.scene.camera = camera

    for name, location, energy, size, target in (
        ("Preview_Key", (0.45, -1.05, 2.25), 430, 2.0, (0.0, 0.0, 0.5)),
        ("Preview_Fill", (-1.25, 0.8, 1.2), 240, 1.5, (-0.1, 0.0, 0.55)),
        ("Preview_Rim", (0.0, 0.5, 2.1), 320, 1.0, (0.2, 0.0, 0.6)),
    ):
        bpy.ops.object.light_add(type="AREA", location=location)
        light = bpy.context.object
        light.name = name
        light.data.energy = energy
        light.data.shape = "DISK"
        light.data.size = size
        point_at(light, target)

    world = bpy.context.scene.world
    world.color = (0.012, 0.016, 0.022)
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.012, 0.016, 0.022, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.18
    scene = bpy.context.scene
    scene.render.resolution_x = 960
    scene.render.resolution_y = 540
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(PREVIEW_PATH)
    scene.render.film_transparent = False
    scene.view_settings.exposure = -0.7


def build_bicycle():
    mats = {
        "frame": material("Frame blue", (0.035, 0.16, 0.3), 0.78, 0.24),
        "accent": material("Safety orange", (0.95, 0.22, 0.035), 0.3, 0.3),
        "metal": material("Machined aluminium", (0.48, 0.53, 0.58), 0.88, 0.2),
        "dark_metal": material("Dark anodized metal", (0.035, 0.045, 0.055), 0.82, 0.24),
        "spoke": material("Spoke steel", (0.62, 0.67, 0.7), 0.92, 0.16),
        "rubber": material("Tyre rubber", (0.012, 0.014, 0.016), 0.05, 0.78),
        "rotor": material("Rotor stainless steel", (0.68, 0.72, 0.74), 0.92, 0.18),
        "pad": material("Brake friction material", (0.25, 0.12, 0.055), 0.12, 0.7),
        "chain": material("Chain steel", (0.24, 0.26, 0.28), 0.9, 0.24),
    }
    rear = (-0.56, 0.0, 0.349)
    front = (0.56, 0.0, 0.349)
    bottom = (-0.11, 0.0, 0.365)
    seat_top = (-0.27, 0.0, 0.83)
    head_low = (0.31, 0.0, 0.57)
    head_high = (0.25, 0.0, 0.83)

    create_wheel("Rear", rear, mats)
    create_wheel("Front", front, mats, interactive=True)
    frame_tubes = [
        cylinder_between("Frame_DownTube", head_low, bottom, 0.031, mats["frame"], 28),
        cylinder_between("Frame_TopTube", head_high, seat_top, 0.027, mats["frame"], 28),
        cylinder_between("Frame_SeatTube", bottom, seat_top, 0.029, mats["frame"], 28),
        cylinder_between("Frame_ChainStayL", rear, (bottom[0], -0.035, bottom[2]), 0.015, mats["frame"], 20),
        cylinder_between("Frame_ChainStayR", rear, (bottom[0], 0.035, bottom[2]), 0.015, mats["frame"], 20),
        cylinder_between("Frame_SeatStayL", rear, (seat_top[0], -0.028, seat_top[2] - 0.06), 0.012, mats["frame"], 20),
        cylinder_between("Frame_SeatStayR", rear, (seat_top[0], 0.028, seat_top[2] - 0.06), 0.012, mats["frame"], 20),
        cylinder_between("Frame_HeadTube", head_low, head_high, 0.036, mats["frame"], 28),
    ]
    join("Frame", frame_tubes)

    cylinder_between("Fork_Left", (head_low[0], -0.052, head_low[2]), (front[0], -0.052, front[2]), 0.018, mats["dark_metal"], 24)
    cylinder_between("Fork_Right", (head_low[0], 0.052, head_low[2]), (front[0], 0.052, front[2]), 0.018, mats["dark_metal"], 24)
    cylinder_between("Fork_Crown", (head_low[0] + 0.01, -0.062, head_low[2] - 0.015), (head_low[0] + 0.01, 0.062, head_low[2] - 0.015), 0.02, mats["dark_metal"], 24)
    cylinder_between("Fork_Steerer", head_high, (0.235, 0.0, 0.95), 0.018, mats["metal"], 24)
    cylinder_between("Handlebar", (0.21, -0.34, 0.97), (0.21, 0.34, 0.97), 0.011, mats["dark_metal"], 24)
    cylinder_between("Stem", (0.235, 0.0, 0.93), (0.21, 0.0, 0.97), 0.018, mats["dark_metal"], 24)
    cylinder_between("Seatpost", seat_top, (-0.3, 0.0, 0.98), 0.016, mats["dark_metal"], 24)
    saddle = box("Saddle", (-0.34, 0.0, 1.005), (0.25, 0.11, 0.045), mats["rubber"], 0.018)
    saddle.rotation_euler[1] = -0.06

    torus("Chainring_Large", bottom, 0.104, 0.005, mats["dark_metal"], rotation=(math.pi / 2, 0, 0), major_segments=48, minor_segments=6).location.y = -0.06
    torus("Chainring_Small", bottom, 0.078, 0.004, mats["dark_metal"], rotation=(math.pi / 2, 0, 0), major_segments=48, minor_segments=6).location.y = -0.048
    cylinder_between("CrankAxle", (bottom[0], -0.08, bottom[2]), (bottom[0], 0.08, bottom[2]), 0.014, mats["metal"], 20)
    cylinder_between("LeftCrank", (-0.11, 0.07, 0.365), (-0.09, 0.07, 0.195), 0.01, mats["dark_metal"], 18)
    cylinder_between("RightCrank", (-0.11, -0.07, 0.365), (-0.13, -0.07, 0.535), 0.01, mats["dark_metal"], 18)
    box("LeftPedal", (-0.09, 0.1, 0.195), (0.085, 0.065, 0.018), mats["dark_metal"], 0.004)
    box("RightPedal", (-0.13, -0.1, 0.535), (0.085, 0.065, 0.018), mats["dark_metal"], 0.004)

    cassette = []
    for index in range(10):
        cassette.append(torus("CassetteSprocket", (rear[0], -0.057 - index * 0.0033, rear[2]), 0.044 + index * 0.005, 0.002, mats["dark_metal"], rotation=(math.pi / 2, 0, 0), major_segments=36, minor_segments=5))
    join("Cassette_10Speed", cassette)
    create_chain("Chain", rear, bottom, mats)
    box("Front_Derailleur", (-0.24, -0.066, 0.5), (0.06, 0.035, 0.075), mats["dark_metal"], 0.008)
    box("Rear_Derailleur", (-0.6, -0.08, 0.23), (0.055, 0.04, 0.16), mats["dark_metal"], 0.008)

    rotor_y = -0.063
    ring_mesh("Front_Rotor", (front[0], rotor_y, front[2]), 0.09, 0.034, 0.002, mats["rotor"], 72)
    bolts = []
    for index in range(6):
        angle = index * math.pi / 3.0
        bx = front[0] + 0.043 * math.cos(angle)
        bz = front[2] + 0.043 * math.sin(angle)
        bolts.append(cylinder_between("RotorBolt", (bx, rotor_y - 0.004, bz), (bx, rotor_y + 0.004, bz), 0.0042, mats["dark_metal"], 12))
    join("RotorBolts", bolts)

    caliper_center = (0.485, -0.066, 0.475)
    caliper_parts = [
        box("CaliperBody", (caliper_center[0] - 0.016, caliper_center[1], caliper_center[2]), (0.055, 0.064, 0.083), mats["dark_metal"], 0.011),
        box("CaliperBody", (caliper_center[0] + 0.02, caliper_center[1], caliper_center[2] + 0.006), (0.05, 0.064, 0.068), mats["dark_metal"], 0.011),
    ]
    join("Front_Caliper", caliper_parts)
    box("LeftPad", (0.491, rotor_y - 0.0045, 0.465), (0.036, 0.004, 0.05), mats["pad"], 0.003)
    box("RightPad", (0.491, rotor_y + 0.0045, 0.465), (0.036, 0.004, 0.05), mats["pad"], 0.003)
    box("PadSpring", (0.491, rotor_y, 0.465), (0.03, 0.005, 0.04), mats["metal"], 0.002)
    cylinder_between("PadPin", (0.465, -0.105, 0.492), (0.465, -0.03, 0.492), 0.0033, mats["metal"], 12)
    torus("RetainingClip", (0.465, -0.108, 0.492), 0.006, 0.0015, mats["accent"], rotation=(math.pi / 2, 0, 0), major_segments=18, minor_segments=6)
    caliper_bolts = [
        cylinder_between("CaliperBolt", (0.44, -0.105, 0.438), (0.44, -0.025, 0.438), 0.0045, mats["metal"], 12),
        cylinder_between("CaliperBolt", (0.44, -0.105, 0.515), (0.44, -0.025, 0.515), 0.0045, mats["metal"], 12),
    ]
    join("CaliperMountBolts", caliper_bolts)
    cylinder_between("BrakeHose", (0.466, -0.085, 0.515), (0.2, -0.2, 0.96), 0.0032, mats["rubber"], 10)
    cylinder_between("Front_ThruAxle", (front[0], -0.094, front[2]), (front[0], 0.094, front[2]), 0.0075, mats["accent"], 20)
    platform = box("Display_Platform", (0.0, 0.0, -0.025), (1.65, 0.6, 0.035), material("Platform", (0.025, 0.032, 0.04), 0.2, 0.58), 0.025)
    platform.hide_select = True

    for obj in bpy.context.scene.objects:
        if obj.type == "MESH":
            for polygon in obj.data.polygons:
                polygon.use_smooth = True


def save_and_export():
    BLEND_PATH.parent.mkdir(parents=True, exist_ok=True)
    FBX_PATH.parent.mkdir(parents=True, exist_ok=True)
    PREVIEW_PATH.parent.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    bpy.ops.render.render(write_still=True)

    camera = bpy.data.objects.get("Preview_Camera")
    camera.location = (1.14, -1.12, 0.78)
    camera.data.lens = 67
    point_at(camera, (0.535, -0.02, 0.405))
    scene.render.filepath = str(FRONT_BRAKE_PREVIEW_PATH)
    bpy.ops.render.render(write_still=True)

    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(
        filepath=str(FBX_PATH),
        use_selection=True,
        object_types={"MESH", "EMPTY", "OTHER"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        add_leaf_bones=False,
        bake_anim=False,
        mesh_smooth_type="FACE",
        path_mode="AUTO",
    )
    print("Generated", BLEND_PATH)
    print("Generated", FBX_PATH)


if __name__ == "__main__":
    reset_scene()
    build_bicycle()
    setup_preview()
    save_and_export()
