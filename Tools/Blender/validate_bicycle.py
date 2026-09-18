"""Validate stable object names and real-world dimensions in the generated bike."""

import math
import sys

import bpy


REQUIRED = {
    "Frame",
    "Front_Tire",
    "Front_Rim",
    "Front_Hub",
    "Front_Spokes",
    "Front_EndCaps",
    "Front_ThruAxle",
    "Front_Rotor",
    "RotorBolts",
    "Front_Caliper",
    "CaliperMountBolts",
    "LeftPad",
    "RightPad",
    "PadSpring",
    "PadPin",
    "RetainingClip",
    "Rear_Tire",
    "Cassette_10Speed",
    "Chainring_Large",
}


def fail(message):
    print("FAIL", message)


def validate():
    failures = 0
    meshes = [obj for obj in bpy.data.objects if obj.type == "MESH"]
    names = {obj.name for obj in bpy.data.objects}

    for missing_name in sorted(REQUIRED - names):
        fail("missing object " + missing_name)
        failures += 1

    front_tire = bpy.data.objects.get("Front_Tire")
    rear_tire = bpy.data.objects.get("Rear_Tire")
    if front_tire is not None and rear_tire is not None:
        wheelbase = abs(front_tire.location.x - rear_tire.location.x)
        if not 1.08 <= wheelbase <= 1.16:
            fail("wheelbase outside expected range: %.3f m" % wheelbase)
            failures += 1

        wheel_diameter = max(front_tire.dimensions.x, front_tire.dimensions.z)
        if not 0.69 <= wheel_diameter <= 0.71:
            fail("27.5-inch wheel diameter outside expected range: %.3f m" % wheel_diameter)
            failures += 1
        print(
            "INFO wheelbase=%.3f m front_wheel_diameter=%.3f m"
            % (wheelbase, wheel_diameter)
        )

    rotor = bpy.data.objects.get("Front_Rotor")
    if rotor is not None:
        rotor_diameter = max(rotor.dimensions.x, rotor.dimensions.z)
        if not math.isclose(rotor_diameter, 0.18, abs_tol=0.003):
            fail("rotor diameter outside expected range: %.3f m" % rotor_diameter)
            failures += 1
        print("INFO front_rotor_diameter=%.3f m" % rotor_diameter)

    front_spokes = bpy.data.objects.get("Front_Spokes")
    if front_spokes is not None and len(front_spokes.data.polygons) < 300:
        fail("front spoke group looks incomplete")
        failures += 1

    if failures:
        print("MECH_MASTER_ASSET_VALIDATION_FAILED failures=%d" % failures)
        return 1

    vertices = sum(len(obj.data.vertices) for obj in meshes)
    polygons = sum(len(obj.data.polygons) for obj in meshes)
    print(
        "MECH_MASTER_ASSET_VALIDATION_OK required_objects=%d "
        "mesh_objects=%d vertices=%d polygons=%d"
        % (len(REQUIRED), len(meshes), vertices, polygons)
    )
    return 0


if __name__ == "__main__":
    sys.exit(validate())

