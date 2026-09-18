"""Generate MechMaster's repair-training-grade 27.5 inch hardtail source model."""

import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(Path(__file__).resolve().parent))
from engineering_geometry import annulus, bolt, box, curve, cylinder, gear, join, material, point_at, sphere, tag, torus  # noqa: E402

BLEND_PATH = ROOT / "Assets/Art/Models/Source/BicycleEngineeringSource.blend"
PREVIEW_PATH = ROOT / "Docs/Preview/BicycleEngineering.png"
DETAIL_PATH = ROOT / "Docs/Preview/BicycleEngineeringDrivetrain.png"
MANIFEST_PATH = ROOT / "Assets/StreamingAssets/MechanicalCatalog/bicycle_model_manifest.json"


class Bike:
    def __init__(self):
        self.c = {}
        self.m = {}
        self.rear = Vector((-0.560, 0, 0.349))
        self.front = Vector((0.560, 0, 0.349))
        self.bb = Vector((-0.105, 0, 0.365))
        self.seat = Vector((-0.270, 0, 0.835))
        self.head_low = Vector((0.305, 0, 0.585))
        self.head_high = Vector((0.245, 0, 0.805))

    def tagged(self, obj, part_id, assembly, boundary="individual"):
        return tag(obj, part_id, assembly, boundary)

    def reset(self):
        bpy.ops.object.select_all(action="SELECT")
        bpy.ops.object.delete(use_global=False)
        for value in list(bpy.data.collections):
            bpy.data.collections.remove(value)
        for group in (bpy.data.meshes, bpy.data.curves, bpy.data.materials):
            for value in list(group):
                if value.users == 0:
                    group.remove(value)

    def setup(self):
        root = bpy.data.collections.new("MM_ENGINEERING_SOURCE")
        bpy.context.scene.collection.children.link(root)
        for key, label in (
            ("frame", "01_FRAME"), ("cockpit_headset", "02_COCKPIT_HEADSET"),
            ("fork", "03_FORK"), ("wheel_front", "04_WHEEL_FRONT"),
            ("wheel_rear", "05_WHEEL_REAR"), ("brake_front", "06_BRAKE_FRONT"),
            ("brake_rear", "07_BRAKE_REAR"), ("crank_bottom_bracket", "08_CRANK_BB"),
            ("front_derailleur", "09_FRONT_DERAILLEUR"), ("rear_derailleur", "10_REAR_DERAILLEUR"),
            ("chain", "11_CHAIN"), ("pedals", "12_PEDALS"),
            ("saddle_seatpost", "13_SADDLE"), ("controls_cables", "14_CONTROLS_CABLES"),
            ("presentation", "99_PRESENTATION"),
        ):
            child = bpy.data.collections.new(label)
            root.children.link(child)
            self.c[key] = child
        self.m = {
            "frame": material("MM Frame blue", (0.018, 0.105, 0.235), 0.78, 0.22),
            "orange": material("MM Service orange", (0.96, 0.19, 0.025), 0.42, 0.25),
            "al": material("MM Machined aluminium", (0.50, 0.56, 0.61), 0.91, 0.18),
            "steel": material("MM Stainless steel", (0.66, 0.70, 0.72), 0.94, 0.16),
            "black": material("MM Black anodized", (0.018, 0.024, 0.031), 0.82, 0.22),
            "grey": material("MM Forged grey", (0.13, 0.15, 0.17), 0.88, 0.24),
            "rubber": material("MM Tyre rubber", (0.006, 0.008, 0.009), 0.02, 0.82),
            "tube": material("MM Inner tube", (0.045, 0.028, 0.026), 0.0, 0.78),
            "seal": material("MM Hydraulic seal", (0.11, 0.018, 0.012), 0.05, 0.62),
            "pad": material("MM Friction pad", (0.22, 0.095, 0.028), 0.08, 0.72),
            "plastic": material("MM Engineering polymer", (0.025, 0.032, 0.038), 0.12, 0.52),
            "oil": material("MM Mineral oil", (0.60, 0.045, 0.025), 0.05, 0.20),
            "floor": material("MM Studio floor", (0.018, 0.023, 0.030), 0.18, 0.60),
        }

    def build_frame(self):
        c, m = self.c["frame"], self.m
        pieces = [
            cylinder("frame_down", self.head_low, self.bb, .033, m["frame"], c, 48),
            cylinder("frame_top", self.head_high, self.seat, .028, m["frame"], c, 48),
            cylinder("frame_seat", self.bb, self.seat, .030, m["frame"], c, 48),
            cylinder("frame_head", self.head_low, self.head_high, .038, m["frame"], c, 48),
            cylinder("frame_bb", self.bb + Vector((0, -.0365, 0)), self.bb + Vector((0, .0365, 0)), .026, m["frame"], c, 48),
        ]
        for side in (-1, 1):
            dropout_y = side * .074
            bb_y = side * .037
            pieces += [
                cylinder("frame_chainstay", self.rear + Vector((0, dropout_y, 0)), self.bb + Vector((0, bb_y, 0)), .015, m["frame"], c, 32),
                cylinder("frame_seatstay", self.rear + Vector((0, dropout_y, 0)), self.seat + Vector((0, bb_y * .72, -.055)), .012, m["frame"], c, 32),
                box("frame_dropout", self.rear + Vector((0, dropout_y, 0)), (.060, .010, .085), m["frame"], c, .006),
            ]
        self.tagged(join("MM_frame_main_weldment", pieces), "bike.frame.frame_weldment.01", "frame", "non-separable")
        self.tagged(box("MM_frame_derailleur_hanger", (-.557, -.052, .318), (.058, .009, .085), m["al"], c, .004), "bike.frame.derailleur_hanger.01", "frame")
        self.tagged(bolt("MM_frame_hanger_bolt", (-.54, -.060, .34), (-.54, -.045, .34), .0025, .005, m["steel"], c), "bike.frame.derailleur_hanger_bolt.01", "frame")
        for i in range(8):
            point = self.head_low.lerp(self.bb, .10 + i * .105) + Vector((0, -.034, 0))
            self.tagged(box(f"MM_frame_cable_guide_{i+1:02d}", point, (.022, .010, .013), m["plastic"], c, .002), f"bike.frame.cable_guide.{i+1:02d}", "frame")
            self.tagged(bolt(f"MM_frame_cable_guide_bolt_{i+1:02d}", point+Vector((0,-.007,0)), point+Vector((0,.004,0)), .0015, .003, m["steel"], c), f"bike.frame.cable_guide_bolt.{i+1:02d}", "frame")
        self.tagged(box("MM_frame_chainstay_protector", (-.39, -.054, .39), (.265, .008, .032), m["rubber"], c, .006), "bike.frame.chainstay_protector.01", "frame", "non-separable")
        for i, x in enumerate((-.02, .035), 1):
            self.tagged(bolt(f"MM_frame_bottle_boss_bolt_{i:02d}", (x, -.010, .645), (x, .010, .645), .0025, .005, m["steel"], c), f"bike.frame.bottle_boss_bolt.{i:02d}", "frame")

    def tire(self, prefix, center, c):
        parts = [torus(prefix + "_carcass", center, .3175, .0285, self.m["rubber"], c, major_segments=144, minor_segments=24)]
        for i in range(72):
            a = 2 * math.pi * i / 72
            p = center + Vector((.3455 * math.cos(a), 0, .3455 * math.sin(a)))
            parts.append(box(prefix + "_tread", p, (.024, .050 + .004 * (i % 2), .007), self.m["rubber"], c, .0015, (0, math.pi/2-a, 0)))
        return join(prefix + "_tire", parts)

    def rotor(self, prefix, center, radius, y, c, assembly):
        parts = [
            annulus(prefix + "_ring", (center.x, y, center.z), radius, radius-.015, .0018, self.m["steel"], c, 144, .0015, 12),
            annulus(prefix + "_carrier", (center.x, y, center.z), .037, .022, .0018, self.m["steel"], c, 72),
        ]
        for i in range(6):
            a = i * math.pi / 3
            mid = (radius - .015 + .037) / 2
            p = center + Vector((mid * math.cos(a), y, mid * math.sin(a)))
            parts.append(box(prefix + "_arm", p, (radius-.050, .0018, .008), self.m["steel"], c, .002, (0, -a, 0)))
        self.tagged(join(prefix + "_rotor", parts), f"bike.{assembly}.rotor.01", assembly)
        for i in range(6):
            a = i * math.pi / 3
            x, z = center.x + .044 * math.cos(a), center.z + .044 * math.sin(a)
            self.tagged(bolt(prefix + f"_rotor_bolt_{i+1:02d}", (x, y-.004, z), (x, y+.004, z), .0022, .0044, self.m["black"], c, .002), f"bike.{assembly}.rotor_bolt.{i+1:02d}", assembly)
            self.tagged(annulus(prefix + f"_rotor_washer_{i+1:02d}", (x, y+.004, z), .0046, .0024, .0006, self.m["steel"], c, 24), f"bike.{assembly}.rotor_lock_washer.{i+1:02d}", assembly)

    def wheel(self, which):
        is_front = which == "front"
        center = self.front if is_front else self.rear
        assembly = "wheel_" + which
        c, m, prefix = self.c[assembly], self.m, "MM_wheel_" + which
        self.tagged(self.tire(prefix, center, c), f"bike.{assembly}.tire.01", assembly)
        self.tagged(torus(prefix + "_inner_tube", center, .3205, .020, m["tube"], c, major_segments=128), f"bike.{assembly}.inner_tube.01", assembly)
        self.tagged(torus(prefix + "_rim_tape", center, .293, .006, m["plastic"], c, major_segments=128, minor_segments=8), f"bike.{assembly}.rim_tape.01", assembly)
        self.tagged(annulus(prefix + "_rim", center, .309, .289, .029, m["al"], c, 160), f"bike.{assembly}.rim.01", assembly)
        valve = cylinder(prefix + "_valve_core", center + Vector((0, -.020, .292)), center + Vector((0, .020, .292)), .003, m["steel"], c, 20)
        self.tagged(valve, f"bike.{assembly}.valve_core.01", assembly)
        self.tagged(torus(prefix + "_valve_nut", center + Vector((0, .022, .292)), .004, .0015, m["steel"], c, rotation=(0,0,0), major_segments=20, minor_segments=6), f"bike.{assembly}.valve_nut.01", assembly)
        spacing_half = .055 if is_front else .074
        hub_half, flange_half = ((.043, .036) if is_front else (.062, .052))
        self.tagged(cylinder(prefix + "_hub_shell", center+Vector((0,-hub_half,0)), center+Vector((0,hub_half,0)), .026, m["black"], c, 48), f"bike.{assembly}.hub_shell.01", assembly)
        self.tagged(cylinder(prefix + "_hub_axle", center+Vector((0,-hub_half-.006,0)), center+Vector((0,hub_half+.006,0)), .010, m["al"], c, 36), f"bike.{assembly}.hub_axle.01", assembly)
        for i, side in enumerate((-1, 1), 1):
            self.tagged(torus(prefix+f"_hub_bearing_{i}", center+Vector((0,side*(hub_half-.008),0)), .014, .004, m["steel"], c, major_segments=48, minor_segments=10), f"bike.{assembly}.hub_bearing.{i:02d}", assembly, "service-unit")
            self.tagged(cylinder(prefix+f"_hub_end_cap_{i}", center+Vector((0,side*hub_half,0)), center+Vector((0,side*spacing_half,0)), .018, m["black"], c, 36), f"bike.{assembly}.hub_end_cap.{i:02d}", assembly)
        axle_half = .085 if is_front else .096
        self.tagged(cylinder(prefix+"_thru_axle", center+Vector((0,-axle_half,0)), center+Vector((0,axle_half,0)), .0075 if is_front else .006, m["orange"], c, 36), f"bike.{assembly}.thru_axle.01", assembly)
        for i in range(32):
            a, side = 2*math.pi*i/32, (-1 if i%2==0 else 1)
            hp = center + Vector((.030*math.cos(a+side*.19), side*flange_half, .030*math.sin(a+side*.19)))
            rp = center + Vector((.294*math.cos(a), side*.010, .294*math.sin(a)))
            self.tagged(cylinder(prefix+f"_spoke_{i+1:02d}", hp, rp, .00095, m["steel"], c, 8), f"bike.{assembly}.spoke.{i+1:02d}", assembly, "individual-with-batch")
            ne = rp + (rp-center).normalized()*.010
            self.tagged(cylinder(prefix+f"_nipple_{i+1:02d}", rp, ne, .0018, m["steel"], c, 10), f"bike.{assembly}.nipple.{i+1:02d}", assembly, "individual-with-batch")
        rotor_y = -.045 if is_front else .055
        self.rotor(prefix, center, .090 if is_front else .080, rotor_y, c, assembly)
        if not is_front:
            self.rear_hub(center, c)

    def rear_hub(self, center, c):
        m, a = self.m, "wheel_rear"
        self.tagged(cylinder("MM_wheel_rear_freehub_body", center+Vector((0,-.062,0)), center+Vector((0,-.030,0)), .019, m["grey"], c, 36), "bike.wheel_rear.freehub_body.01", a)
        for i, y in enumerate((-.058, -.036), 1):
            self.tagged(torus(f"MM_wheel_rear_freehub_bearing_{i}", center+Vector((0,y,0)), .012, .0035, m["steel"], c, major_segments=40, minor_segments=8), f"bike.wheel_rear.freehub_bearing.{i:02d}", a, "service-unit")
        self.tagged(torus("MM_wheel_rear_freehub_seal", center+Vector((0,-.064,0)), .018, .002, m["seal"], c, major_segments=48, minor_segments=8), "bike.wheel_rear.freehub_seal.01", a)
        for i in range(3):
            angle = i*2*math.pi/3
            p = center+Vector((.013*math.cos(angle),-.061,.013*math.sin(angle)))
            self.tagged(box(f"MM_wheel_rear_pawl_{i+1}", p, (.012,.005,.005), m["steel"], c, .001, (0,-angle,0)), f"bike.wheel_rear.pawl.{i+1:02d}", a)
            self.tagged(torus(f"MM_wheel_rear_pawl_spring_{i+1}", p, .003, .0006, m["steel"], c, major_segments=20, minor_segments=6), f"bike.wheel_rear.pawl_spring.{i+1:02d}", a)
        teeth = (11,13,15,17,19,21,24,28,32,36)
        for i, count in enumerate(teeth):
            root = .024+i*.0041
            self.tagged(gear(f"MM_wheel_rear_cassette_sprocket_{i+1:02d}", center+Vector((0,-.062+i*.0032,0)), count, root, root+.0045, .0019, .0195, m["steel"], c), f"bike.wheel_rear.cassette_sprocket.{i+1:02d}", a)
            if i < 9:
                self.tagged(annulus(f"MM_wheel_rear_cassette_spacer_{i+1:02d}", center+Vector((0,-.0605+i*.0032,0)), .025, .020, .0008, m["black"], c, 40), f"bike.wheel_rear.cassette_spacer.{i+1:02d}", a)
        self.tagged(annulus("MM_wheel_rear_cassette_lockring", center+Vector((0,-.065,0)), .026,.019,.006,m["black"],c,48), "bike.wheel_rear.cassette_lockring.01", a)

    def build_fork(self):
        c, m, a = self.c["fork"], self.m, "fork"
        crown = Vector((.324, 0, .610))
        stanchions = []
        for i, side in enumerate((-1, 1), 1):
            lower_top, dropout = crown+Vector((.015,side*.054,-.055)), self.front+Vector((0,side*.055,0))
            lower = cylinder(f"MM_fork_lower_leg_{i}", lower_top, dropout, .023, m["black"], c, 48)
            if i == 1:
                lower_parts = [lower]
            else:
                lower_parts.append(lower)
            stanchion = cylinder(f"MM_fork_stanchion_{i}", crown+Vector((0,side*.054,.01)), lower_top+Vector((0,0,-.035)), .016, m["steel"], c, 48)
            stanchions.append(stanchion)
            self.tagged(torus(f"MM_fork_dust_wiper_{i}", lower_top, .018, .003, m["seal"], c, major_segments=48, minor_segments=10), f"bike.fork.dust_wiper.{i:02d}", a)
            self.tagged(torus(f"MM_fork_foam_ring_{i}", lower_top+Vector((-.004,0,-.009)), .0155,.0025,m["oil"],c,major_segments=40,minor_segments=8), f"bike.fork.foam_ring.{i:02d}", a)
            for bushing_i, offset in enumerate((-.035, -.145), 1):
                self.tagged(torus(f"MM_fork_guide_bushing_{i}_{bushing_i}", lower_top+Vector((offset,0,offset*.35)), .0165,.0018,m["al"],c,major_segments=40,minor_segments=8), f"bike.fork.guide_bushing.{(i-1)*2+bushing_i:02d}", a, "installed")
            self.tagged(bolt(f"MM_fork_foot_nut_{i}", dropout+Vector((0,0,-.025)), dropout+Vector((.015,0,.005)), .003,.006,m["steel"],c), f"bike.fork.foot_nut.{i:02d}", a)
            self.tagged(annulus(f"MM_fork_crush_washer_{i}", dropout+Vector((-.003,0,-.010)), .006,.003,.001,m["seal"],c,24), f"bike.fork.crush_washer.{i:02d}", a)
        lower_parts.append(curve("MM_fork_arch", [(.44,-.055,.49),(.47,0,.53),(.44,.055,.49)], .014,m["black"],c))
        self.tagged(join("MM_fork_lower_casting", lower_parts), "bike.fork.lower_casting.01", a)
        for i, obj in enumerate(stanchions, 1):
            self.tagged(obj, f"bike.fork.stanchion.{i:02d}", a, "installed")
        self.tagged(cylinder("MM_fork_crown", crown+Vector((0,-.070,0)), crown+Vector((0,.070,0)), .025,m["black"],c,48), "bike.fork.crown.01", a, "installed")
        self.tagged(cylinder("MM_fork_steerer", crown, (.235,0,.950), .0175,m["al"],c,48), "bike.fork.steerer.01", a)
        self.tagged(cylinder("MM_fork_air_shaft", (.33,-.054,.42),(.31,-.054,.63),.006,m["al"],c,24), "bike.fork.air_shaft.01", a)
        self.tagged(cylinder("MM_fork_air_piston", (.318,-.054,.555),(.316,-.054,.575),.014,m["plastic"],c,32), "bike.fork.air_piston.01", a)
        self.tagged(cylinder("MM_fork_air_seal_head", (.319,-.054,.49),(.317,-.054,.51),.014,m["seal"],c,32), "bike.fork.air_seal_head.01", a)
        self.tagged(torus("MM_fork_negative_spring", (.318,-.054,.54),.010,.003,m["steel"],c,major_segments=32,minor_segments=8), "bike.fork.negative_spring.01", a)
        self.tagged(cylinder("MM_fork_air_top_cap", (.307,-.054,.625),(.303,-.054,.650),.014,m["orange"],c,32), "bike.fork.air_top_cap.01", a)
        self.tagged(cylinder("MM_fork_air_valve_core", (.302,-.054,.650),(.300,-.054,.662),.003,m["steel"],c,20), "bike.fork.air_valve_core.01", a)
        self.tagged(cylinder("MM_fork_damper_cartridge", (.33,.054,.42),(.31,.054,.64),.012,m["grey"],c,32), "bike.fork.damper_cartridge.01", a, "service-unit")
        self.tagged(cylinder("MM_fork_rebound_shaft", (.33,.054,.40),(.31,.054,.62),.005,m["steel"],c,24), "bike.fork.rebound_shaft.01", a)
        self.tagged(cylinder("MM_fork_damper_top_cap", (.307,.054,.625),(.303,.054,.650),.014,m["black"],c,32), "bike.fork.damper_top_cap.01", a)
        self.tagged(cylinder("MM_fork_compression_adjuster", (.302,.054,.650),(.300,.054,.665),.010,m["orange"],c,28), "bike.fork.compression_adjuster.01", a)
        self.tagged(cylinder("MM_fork_rebound_knob", (.556,.054,.310),(.558,.054,.326),.009,m["orange"],c,28), "bike.fork.rebound_knob.01", a)

    def build_cockpit(self):
        c, m, a = self.c["cockpit_headset"], self.m, "cockpit_headset"
        self.tagged(cylinder("MM_cockpit_handlebar", (.285,-.365,.955),(.285,.365,.955),.011,m["black"],c,48), "bike.cockpit_headset.handlebar.01", a)
        for i, side in enumerate((-1,1),1):
            y = side*.348
            self.tagged(cylinder(f"MM_cockpit_grip_{i}",(.285,y-side*.070,.955),(.285,y,.955),.016,m["rubber"],c,40), f"bike.cockpit_headset.grip.{i:02d}", a)
            self.tagged(cylinder(f"MM_cockpit_bar_end_plug_{i}",(.285,y,.955),(.285,y+side*.008,.955),.015,m["plastic"],c,32), f"bike.cockpit_headset.bar_end_plug.{i:02d}", a)
        self.tagged(box("MM_cockpit_stem_body",(.226,0,.927),(.115,.052,.047),m["black"],c,.010,(0,-.20,0)), "bike.cockpit_headset.stem_body.01", a)
        self.tagged(box("MM_cockpit_stem_faceplate",(.286,0,.950),(.016,.058,.052),m["black"],c,.006,(0,-.20,0)), "bike.cockpit_headset.stem_faceplate.01", a)
        for i, (y,z) in enumerate(((-.022,.929),(.022,.929),(-.022,.970),(.022,.970)),1):
            self.tagged(bolt(f"MM_cockpit_faceplate_bolt_{i}",(.295,y,z),(.275,y,z),.0023,.0046,m["steel"],c), f"bike.cockpit_headset.stem_faceplate_bolt.{i:02d}", a)
        for i, z in enumerate((.910,.935),1):
            self.tagged(bolt(f"MM_cockpit_clamp_bolt_{i}",(.190,-.030,z),(.190,.030,z),.0024,.0048,m["steel"],c), f"bike.cockpit_headset.stem_clamp_bolt.{i:02d}", a)
        self.tagged(cylinder("MM_cockpit_top_cap",(.205,0,.947),(.202,0,.958),.017,m["black"],c,40), "bike.cockpit_headset.top_cap.01", a)
        self.tagged(bolt("MM_cockpit_top_cap_bolt",(.201,0,.960),(.215,0,.925),.0025,.0055,m["steel"],c), "bike.cockpit_headset.top_cap_bolt.01", a)
        for i,z in enumerate((.858,.870,.882),1):
            self.tagged(torus(f"MM_cockpit_headset_spacer_{i}",(.229,0,z),.020,.004,m["black"],c), f"bike.cockpit_headset.headset_spacer.{i:02d}", a)
        self.tagged(torus("MM_cockpit_upper_cover",(.236,0,.842),.029,.009,m["black"],c), "bike.cockpit_headset.upper_cover.01", a)
        self.tagged(torus("MM_cockpit_compression_ring",(.240,0,.827),.023,.003,m["al"],c), "bike.cockpit_headset.compression_ring.01", a)
        for i,z in enumerate((.817,.790),1):
            self.tagged(torus(f"MM_cockpit_headset_bearing_{i}",(.246,0,z),.027,.004,m["steel"],c), f"bike.cockpit_headset.headset_bearing.{i:02d}", a, "service-unit")
        self.tagged(torus("MM_cockpit_crown_race",(.270,0,.780),.024,.0035,m["steel"],c), "bike.cockpit_headset.crown_race.01", a)
        self.tagged(cylinder("MM_cockpit_star_nut",(.215,0,.900),(.220,0,.912),.012,m["steel"],c,12), "bike.cockpit_headset.star_nut.01", a, "installed")

    def build_brake(self, which):
        front = which == "front"
        a, c, m = "brake_"+which, self.c["brake_"+which], self.m
        p, y = "MM_brake_"+which, (-.245 if front else .245)
        self.tagged(box(p+"_lever_body",(.29,y,.935),(.075,.034,.038),m["black"],c,.008,(0,-.10,0)), f"bike.{a}.lever_body.01", a)
        self.tagged(torus(p+"_lever_clamp",(.285,y,.955),.014,.004,m["black"],c,rotation=(math.pi/2,0,0)), f"bike.{a}.lever_clamp.01", a)
        self.tagged(bolt(p+"_lever_clamp_bolt",(.285,y-.020,.945),(.285,y+.020,.945),.002,.004,m["steel"],c), f"bike.{a}.lever_clamp_bolt.01", a)
        self.tagged(curve(p+"_lever_blade",[(.31,y,.93),(.35,y*1.1,.91),(.39,y*1.12,.92)],.006,m["al"],c), f"bike.{a}.lever_blade.01", a)
        self.tagged(cylinder(p+"_lever_pivot",(.315,y-.022,.935),(.315,y+.022,.935),.003,m["steel"],c,20), f"bike.{a}.lever_pivot.01", a)
        self.tagged(cylinder(p+"_master_piston",(.275,y,.925),(.302,y,.925),.006,m["al"],c,28), f"bike.{a}.master_piston.01", a)
        for i,x in enumerate((.283,.293),1):
            self.tagged(torus(p+f"_master_seal_{i}",(x,y,.925),.006,.0015,m["seal"],c,rotation=(0,math.pi/2,0),major_segments=28,minor_segments=8), f"bike.{a}.master_seal.{i:02d}", a)
        self.tagged(torus(p+"_return_spring",(.268,y,.925),.006,.001,m["steel"],c,rotation=(0,math.pi/2,0),major_segments=28,minor_segments=6), f"bike.{a}.return_spring.01", a)
        self.tagged(box(p+"_reservoir_diaphragm",(.258,y,.944),(.043,.030,.009),m["oil"],c,.003), f"bike.{a}.reservoir_diaphragm.01", a)
        self.tagged(box(p+"_reservoir_cap",(.258,y,.967),(.050,.039,.006),m["al"],c,.003), f"bike.{a}.reservoir_cap.01", a)
        for i,x in enumerate((.245,.270),1):
            self.tagged(bolt(p+f"_reservoir_cap_bolt_{i}",(x,y-.023,.970),(x,y+.023,.970),.0015,.003,m["steel"],c), f"bike.{a}.reservoir_cap_bolt.{i:02d}", a)
        cal = Vector((.487,-.048,.474) if front else (-.490,.058,.430))
        for i,yo in enumerate((-.016,.016),1):
            self.tagged(box(p+f"_caliper_half_{i}",cal+Vector((0,yo,0)),(.070,.032,.088),m["black"],c,.012), f"bike.{a}.caliper_half.{i:02d}", a)
            self.tagged(bolt(p+f"_caliper_body_bolt_{i}",cal+Vector((-.020,-.040,-.025+.050*(i-1))),cal+Vector((-.020,.040,-.025+.050*(i-1))),.0025,.005,m["steel"],c), f"bike.{a}.caliper_body_bolt.{i:02d}", a)
        for i,yo in enumerate((-.012,.012),1):
            self.tagged(cylinder(p+f"_caliper_piston_{i}",cal+Vector((0,yo-.006,0)),cal+Vector((0,yo+.006,0)),.011,m["al"],c,36), f"bike.{a}.caliper_piston.{i:02d}", a)
            self.tagged(torus(p+f"_piston_seal_{i}",cal+Vector((0,yo,0)),.0115,.0014,m["seal"],c,major_segments=36,minor_segments=8), f"bike.{a}.caliper_piston_seal.{i:02d}", a)
            self.tagged(box(p+f"_brake_pad_{i}",cal+Vector((0,yo*.36,0)),(.040,.0035,.055),m["pad"],c,.003), f"bike.{a}.brake_pad.{i:02d}", a)
        self.tagged(box(p+"_pad_spring",cal,(.034,.005,.048),m["steel"],c,.002), f"bike.{a}.pad_spring.01", a)
        self.tagged(cylinder(p+"_pad_pin",cal+Vector((-.026,-.042,.025)),cal+Vector((-.026,.042,.025)),.0028,m["steel"],c,16), f"bike.{a}.pad_pin.01", a)
        self.tagged(torus(p+"_retaining_clip",cal+Vector((-.026,-.044,.025)),.005,.0012,m["orange"],c,major_segments=20,minor_segments=6), f"bike.{a}.retaining_clip.01", a)
        self.tagged(bolt(p+"_bleed_screw",cal+Vector((.022,-.040,.032)),cal+Vector((.022,-.018,.032)),.002,.004,m["steel"],c), f"bike.{a}.bleed_screw.01", a)
        self.tagged(torus(p+"_bleed_o_ring",cal+Vector((.022,-.019,.032)),.0022,.0007,m["seal"],c,major_segments=18,minor_segments=6), f"bike.{a}.bleed_o_ring.01", a)
        self.tagged(box(p+"_mount_adapter",cal+Vector((-.045,.020,0)),(.030,.010,.105),m["al"],c,.006), f"bike.{a}.mount_adapter.01", a)
        for i,z in enumerate((-.030,.030),1):
            self.tagged(bolt(p+f"_mount_bolt_{i}",cal+Vector((-.040,-.050,z)),cal+Vector((-.040,.045,z)),.003,.006,m["steel"],c), f"bike.{a}.mount_bolt.{i:02d}", a)
            self.tagged(torus(p+f"_mount_snap_ring_{i}",cal+Vector((-.040,.047,z)),.004,.001,m["steel"],c,major_segments=18,minor_segments=6), f"bike.{a}.mount_snap_ring.{i:02d}", a)
        hose_pts = [(.265,y,.93),(.12,-.10 if front else .10,.87),tuple(cal+Vector((.02,-.03,.04)))]
        self.tagged(curve(p+"_hose",hose_pts,.0027,m["rubber"],c), f"bike.{a}.hose.01", a)
        for part, loc, mat_name in (("compression_nut",hose_pts[0],"steel"),("olive",(.273,y,.93),"steel"),("connector_insert",(.280,y,.93),"al")):
            self.tagged(torus(p+"_"+part,loc,.004,.0013,m[mat_name],c,rotation=(0,math.pi/2,0),major_segments=24,minor_segments=6), f"bike.{a}.{part}.01", a)

    def build_crank(self):
        c, m, a = self.c["crank_bottom_bracket"], self.m, "crank_bottom_bracket"
        for i, side in enumerate((-1, 1), 1):
            y = side*.043
            self.tagged(annulus(f"MM_crank_bb_cup_{i}",self.bb+Vector((0,y,0)),.024,.015,.012,m["black"],c,64), f"bike.{a}.bottom_bracket_cup.{i:02d}", a)
            self.tagged(torus(f"MM_crank_bb_bearing_{i}",self.bb+Vector((0,side*.038,0)),.015,.004,m["steel"],c,major_segments=48,minor_segments=10), f"bike.{a}.bottom_bracket_bearing.{i:02d}", a, "service-unit")
            self.tagged(torus(f"MM_crank_bb_seal_{i}",self.bb+Vector((0,side*.049,0)),.018,.002,m["seal"],c,major_segments=48,minor_segments=8), f"bike.{a}.bottom_bracket_seal.{i:02d}", a)
            self.tagged(annulus(f"MM_crank_bb_spacer_{i}",self.bb+Vector((0,side*.055,0)),.023,.015,.0025,m["plastic"],c,48), f"bike.{a}.bottom_bracket_spacer.{i:02d}", a)
        self.tagged(cylinder("MM_crank_bb_sleeve",self.bb+Vector((0,-.030,0)),self.bb+Vector((0,.030,0)),.021,m["plastic"],c,36), f"bike.{a}.bottom_bracket_sleeve.01", a)
        self.tagged(cylinder("MM_crank_spindle",self.bb+Vector((0,-.070,0)),self.bb+Vector((0,.070,0)),.012,m["steel"],c,36), f"bike.{a}.crank_spindle.01", a, "installed")
        crank_ends = (self.bb+Vector((.025,.068,-.170)), self.bb+Vector((-.025,-.068,.170)))
        for i,(start,end) in enumerate(((self.bb+Vector((0,.068,0)),crank_ends[0]),(self.bb+Vector((0,-.068,0)),crank_ends[1])),1):
            self.tagged(cylinder(f"MM_crank_arm_{i}",start,end,.011,m["black"],c,32), f"bike.{a}.crank_arm.{i:02d}", a)
        for i,z in enumerate((-.010,.010),1):
            self.tagged(bolt(f"MM_crank_pinch_bolt_{i}",self.bb+Vector((.010,.078,z)),self.bb+Vector((-.010,.060,z)),.0025,.005,m["steel"],c), f"bike.{a}.pinch_bolt.{i:02d}", a)
        self.tagged(annulus("MM_crank_preload_cap",self.bb+Vector((0,.076,0)),.017,.005,.005,m["orange"],c,48), f"bike.{a}.preload_cap.01", a)
        for i,(teeth,radius,y) in enumerate(((36,.095,-.062),(22,.060,-.052)),1):
            self.tagged(gear(f"MM_crank_chainring_{i}",self.bb+Vector((0,y,0)),teeth,radius,radius+.006,.0024,.029,m["black"],c), f"bike.{a}.chainring.{i:02d}", a)
        for i in range(4):
            angle = i*math.pi/2
            p = self.bb+Vector((.040*math.cos(angle),-.066,.040*math.sin(angle)))
            self.tagged(bolt(f"MM_crank_chainring_bolt_{i+1}",p+Vector((0,-.005,0)),p+Vector((0,.005,0)),.0025,.005,m["steel"],c), f"bike.{a}.chainring_bolt.{i+1:02d}", a)
            self.tagged(annulus(f"MM_crank_chainring_nut_{i+1}",p+Vector((0,.006,0)),.005,.0025,.003,m["black"],c,24), f"bike.{a}.chainring_nut.{i+1:02d}", a)

    def build_front_derailleur(self):
        c, m, a = self.c["front_derailleur"], self.m, "front_derailleur"
        p = Vector((-.245,-.070,.505))
        self.tagged(torus("MM_front_derailleur_clamp",(-.245,0,.505),.032,.005,m["black"],c,rotation=(0,math.pi/2,0)), f"bike.{a}.clamp.01", a)
        self.tagged(bolt("MM_front_derailleur_clamp_bolt",(-.245,-.038,.525),(-.245,.038,.525),.0025,.005,m["steel"],c), f"bike.{a}.clamp_bolt.01", a)
        self.tagged(box("MM_front_derailleur_body",p,(.050,.032,.055),m["grey"],c,.008), f"bike.{a}.body.01", a)
        for i,y in enumerate((-.090,-.080),1):
            self.tagged(box(f"MM_front_derailleur_cage_plate_{i}",p+Vector((.02,y+.070,-.055)),(.090,.004,.070),m["steel"],c,.004,(0,-.25,0)), f"bike.{a}.cage_plate.{i:02d}", a)
        for i,z in enumerate((.012,-.012),1):
            self.tagged(cylinder(f"MM_front_derailleur_link_plate_{i}",p+Vector((-.015,0,z)),p+Vector((.025,0,z-.040)),.004,m["black"],c,20), f"bike.{a}.link_plate.{i:02d}", a)
            self.tagged(cylinder(f"MM_front_derailleur_pivot_pin_{i}",p+Vector((-.012,-.020,z)),p+Vector((-.012,.020,z)),.0025,m["steel"],c,16), f"bike.{a}.pivot_pin.{i:02d}", a)
            self.tagged(torus(f"MM_front_derailleur_return_spring_{i}",p+Vector((-.012,0,z)),.006,.001,m["steel"],c,major_segments=24,minor_segments=6), f"bike.{a}.return_spring.{i:02d}", a)
            self.tagged(bolt(f"MM_front_derailleur_limit_screw_{i}",p+Vector((-.020+(i-1)*.014,-.026,.030)),p+Vector((-.020+(i-1)*.014,.002,.030)),.0015,.003,m["steel"],c), f"bike.{a}.limit_screw.{i:02d}", a)
        self.tagged(bolt("MM_front_derailleur_cable_anchor",p+Vector((.025,-.025,.025)),p+Vector((.025,.010,.025)),.0025,.005,m["steel"],c), f"bike.{a}.cable_anchor.01", a, "service-unit")

    def build_rear_derailleur(self):
        c, m, a = self.c["rear_derailleur"], self.m, "rear_derailleur"
        p = Vector((-.555,-.080,.305))
        self.tagged(box("MM_rear_derailleur_b_knuckle",p,(.060,.040,.065),m["black"],c,.010), f"bike.{a}.b_knuckle.01", a)
        self.tagged(box("MM_rear_derailleur_p_knuckle",p+Vector((-.075,-.005,-.060)),(.055,.038,.060),m["black"],c,.010), f"bike.{a}.p_knuckle.01", a)
        for i in range(4):
            z = .026 if i%2==0 else -.026
            start = p+Vector((-.012,-.008*(i//2),z))
            end = p+Vector((-.075,-.008*(i//2),z-.045))
            self.tagged(cylinder(f"MM_rear_derailleur_link_plate_{i+1}",start,end,.005,m["grey"],c,24), f"bike.{a}.link_plate.{i+1:02d}", a)
            self.tagged(cylinder(f"MM_rear_derailleur_pivot_pin_{i+1}",start+Vector((0,-.018,0)),start+Vector((0,.018,0)),.0025,m["steel"],c,16), f"bike.{a}.pivot_pin.{i+1:02d}", a)
        for i,loc in enumerate((p,p+Vector((-.075,0,-.060))),1):
            self.tagged(torus(f"MM_rear_derailleur_return_spring_{i}",loc,.018,.003,m["steel"],c,major_segments=36,minor_segments=8), f"bike.{a}.return_spring.{i:02d}", a)
        for i,y in enumerate((-.089,-.078),1):
            self.tagged(box(f"MM_rear_derailleur_cage_plate_{i}",(-.646,y,.205),(.030,.006,.185),m["black"],c,.005,(0,0,-.12)), f"bike.{a}.cage_plate.{i:02d}", a)
        for i,z in enumerate((.260,.150),1):
            self.tagged(gear(f"MM_rear_derailleur_jockey_wheel_{i}",(-.646,-.093,z),12,.021,.025,.008,.006,m["plastic"],c), f"bike.{a}.jockey_wheel.{i:02d}", a)
            for b in range(2):
                self.tagged(torus(f"MM_rear_derailleur_jockey_bushing_{i}_{b+1}",(-.646,-.089+b*.006,z),.006,.0015,m["al"],c,major_segments=24,minor_segments=6), f"bike.{a}.jockey_bushing.{(i-1)*2+b+1:02d}", a)
            self.tagged(bolt(f"MM_rear_derailleur_jockey_bolt_{i}",(-.646,-.103,z),(-.646,-.080,z),.0025,.005,m["steel"],c), f"bike.{a}.jockey_bolt.{i:02d}", a)
        for i in range(2):
            self.tagged(bolt(f"MM_rear_derailleur_limit_screw_{i+1}",p+Vector((.020-i*.016,-.028,.020)),p+Vector((.020-i*.016,.003,.020)),.0014,.003,m["steel"],c), f"bike.{a}.limit_screw.{i+1:02d}", a)
        self.tagged(bolt("MM_rear_derailleur_b_tension_screw",p+Vector((.028,-.020,-.025)),p+Vector((.050,.010,-.035)),.0015,.003,m["steel"],c), f"bike.{a}.b_tension_screw.01", a)
        self.tagged(bolt("MM_rear_derailleur_cable_anchor",p+Vector((-.025,-.025,.020)),p+Vector((-.025,.010,.020)),.0025,.005,m["steel"],c), f"bike.{a}.cable_anchor.01", a, "service-unit")

    def build_chain(self):
        c, m, a = self.c["chain"], self.m, "chain"
        rear, front = Vector((self.rear.x,-.055,self.rear.z)), Vector((self.bb.x,-.064,self.bb.z))
        points = [rear+Vector((0,0,.070)),front+Vector((0,0,.101)),front-Vector((0,0,.101)),rear-Vector((0,0,.070))]
        lengths = [(points[(i+1)%4]-points[i]).length for i in range(4)]
        perimeter = sum(lengths)
        template = box("MM_chain_template",(0,0,0),(.0127,.008,.003),m["steel"],c,.0015)
        mesh = template.data
        bpy.data.objects.remove(template,do_unlink=True)
        for i in range(110):
            distance, edge = perimeter*i/110, 0
            while distance > lengths[edge]:
                distance -= lengths[edge]
                edge += 1
            direction = (points[(edge+1)%4]-points[edge]).normalized()
            obj = bpy.data.objects.new(f"MM_chain_link_{i+1:03d}",mesh.copy())
            c.objects.link(obj)
            obj.location = points[edge]+direction*distance
            obj.location.y += .0025 if i%2 else -.0025
            obj.rotation_euler[1] = math.atan2(direction.z,direction.x)
            self.tagged(obj,f"bike.{a}.chain_link.{i+1:03d}",a,"visual-repeat")
        for i,offset in enumerate((-.004,.004),1):
            self.tagged(box(f"MM_chain_quick_link_{i}",points[0]+Vector((0,offset,0)),(.014,.004,.004),m["orange"],c,.0015), f"bike.{a}.quick_link.{i:02d}", a)

    def build_pedals(self):
        c, m, a = self.c["pedals"], self.m, "pedals"
        locations = (self.bb+Vector((.025,.105,-.170)),self.bb+Vector((-.025,-.105,.170)))
        for side,center in enumerate(locations,1):
            self.tagged(box(f"MM_pedal_body_{side}",center,(.095,.070,.018),m["black"],c,.006), f"bike.{a}.pedal_body.{side:02d}", a)
            self.tagged(cylinder(f"MM_pedal_axle_{side}",center+Vector((0,-.036,0)),center+Vector((0,.036,0)),.006,m["steel"],c,28), f"bike.{a}.pedal_axle.{side:02d}", a)
            for b,y in enumerate((-.018,.018),1):
                self.tagged(torus(f"MM_pedal_bearing_{side}_{b}",center+Vector((0,y,0)),.007,.002,m["steel"],c,major_segments=28,minor_segments=8), f"bike.{a}.pedal_bearing.{(side-1)*2+b:02d}", a, "service-unit")
            self.tagged(torus(f"MM_pedal_seal_{side}",center+Vector((0,-.030,0)),.008,.0015,m["seal"],c,major_segments=28,minor_segments=8), f"bike.{a}.pedal_seal.{side:02d}", a)
            self.tagged(cylinder(f"MM_pedal_end_cap_{side}",center+Vector((0,.033,0)),center+Vector((0,.042,0)),.009,m["plastic"],c,28), f"bike.{a}.pedal_end_cap.{side:02d}", a)
            self.tagged(annulus(f"MM_pedal_washer_{side}",center+Vector((0,.026,0)),.008,.004,.001,m["steel"],c,24), f"bike.{a}.pedal_washer.{side:02d}", a)
            self.tagged(cylinder(f"MM_pedal_nut_{side}",center+Vector((0,.027,0)),center+Vector((0,.035,0)),.006,m["steel"],c,6), f"bike.{a}.pedal_nut.{side:02d}", a)
            for pin in range(8):
                x = center.x+(-.035 if pin%2 else .035)
                y = center.y+(-.026+.017*(pin//2))
                self.tagged(cylinder(f"MM_pedal_traction_pin_{side}_{pin+1}",(x,y,center.z-.011),(x,y,center.z+.011),.0012,m["steel"],c,10), f"bike.{a}.traction_pin.{(side-1)*8+pin+1:02d}", a, "individual-with-batch")

    def build_saddle(self):
        c, m, a = self.c["saddle_seatpost"], self.m, "saddle_seatpost"
        self.tagged(cylinder("MM_saddle_seatpost",self.seat,(-.315,0,.995),.0155,m["black"],c,40), f"bike.{a}.seatpost.01", a)
        self.tagged(box("MM_saddle_shell",(-.355,0,1.025),(.265,.135,.038),m["plastic"],c,.026,(0,-.055,0)), f"bike.{a}.saddle_shell.01", a, "non-separable")
        self.tagged(box("MM_saddle_padding",(-.355,0,1.038),(.270,.140,.026),m["pad"],c,.028,(0,-.055,0)), f"bike.{a}.saddle_padding.01", a, "non-separable")
        self.tagged(box("MM_saddle_cover",(-.355,0,1.050),(.274,.144,.016),m["rubber"],c,.030,(0,-.055,0)), f"bike.{a}.saddle_cover.01", a, "non-separable")
        for i,y in enumerate((-.037,.037),1):
            self.tagged(curve(f"MM_saddle_rail_{i}",[(-.44,y,1.00),(-.34,y,.995),(-.27,y,1.015)],.0035,m["steel"],c), f"bike.{a}.saddle_rail.{i:02d}", a, "installed")
            self.tagged(box(f"MM_saddle_clamp_plate_{i}",(-.31,y,.998),(.045,.020,.018),m["black"],c,.004), f"bike.{a}.saddle_clamp_plate.{i:02d}", a)
            self.tagged(bolt(f"MM_saddle_clamp_bolt_{i}",(-.31,y-.015,.998),(-.31,y+.015,.998),.0025,.005,m["steel"],c), f"bike.{a}.saddle_clamp_bolt.{i:02d}", a)
        self.tagged(torus("MM_saddle_seatpost_collar",self.seat,.032,.006,m["orange"],c,rotation=(0,math.pi/2,0),major_segments=48,minor_segments=10), f"bike.{a}.seatpost_collar.01", a)
        self.tagged(bolt("MM_saddle_seatpost_collar_bolt",(-.275,-.038,.837),(-.275,.038,.837),.003,.006,m["steel"],c), f"bike.{a}.seatpost_collar_bolt.01", a)

    def build_controls(self):
        c, m, a = self.c["controls_cables"], self.m, "controls_cables"
        routes = [
            ("front",[(.285,-.18,.95),(.20,-.05,.83),(-.10,-.04,.62),(-.245,-.075,.51)]),
            ("rear",[(.285,.18,.95),(.18,.06,.82),(-.10,.045,.62),(-.55,-.10,.31)]),
        ]
        for route,(name,points) in enumerate(routes,1):
            self.tagged(box(f"MM_controls_shifter_{route}",points[0],(.055,.035,.035),m["black"],c,.006), f"bike.{a}.shifter.{route:02d}", a, "service-unit")
            self.tagged(curve(f"MM_controls_inner_cable_{route}",[(x,y-.003,z) for x,y,z in points],.0007,m["steel"],c), f"bike.{a}.shift_inner_cable.{route:02d}", a)
            self.tagged(curve(f"MM_controls_housing_{route}",points,.0024,m["rubber"],c), f"bike.{a}.shift_housing.{route:02d}", a)
            ferrule_points = (points[0],points[1],points[-2],points[-1])
            for local_i,point in enumerate(ferrule_points,1):
                global_i = (route-1)*4+local_i
                self.tagged(torus(f"MM_controls_ferrule_{global_i}",point,.0035,.0012,m["al"],c,rotation=(0,math.pi/2,0),major_segments=20,minor_segments=6), f"bike.{a}.housing_ferrule.{global_i:02d}", a)
            self.tagged(cylinder(f"MM_controls_cable_end_cap_{route}",Vector(points[-1])+Vector((-.006,0,0)),Vector(points[-1])+Vector((.006,0,0)),.002,m["orange"],c,16), f"bike.{a}.cable_end_cap.{route:02d}", a)

    def build(self):
        self.reset()
        self.setup()
        self.build_frame()
        self.build_cockpit()
        self.build_fork()
        self.wheel("front")
        self.wheel("rear")
        self.build_brake("front")
        self.build_brake("rear")
        self.build_crank()
        self.build_front_derailleur()
        self.build_rear_derailleur()
        self.build_chain()
        self.build_pedals()
        self.build_saddle()
        self.build_controls()
        floor = box("MM_presentation_floor",(0,0,-.040),(1.72,.72,.050),self.m["floor"],self.c["presentation"],.025)
        floor["mm_presentation_only"] = True


def setup_scene():
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.length_unit = "METERS"
    scene.unit_settings.scale_length = 1.0
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x, scene.render.resolution_y, scene.render.resolution_percentage = 1280, 720, 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = -.35
    scene.world.use_nodes = True
    background = scene.world.node_tree.nodes["Background"]
    background.inputs["Color"].default_value = (.008,.012,.018,1)
    background.inputs["Strength"].default_value = .24
    bpy.ops.object.camera_add(location=(1.82,-3.05,1.34))
    camera = bpy.context.object
    camera.name, camera.data.lens = "MM_Preview_Camera", 62
    point_at(camera,(0,0,.52))
    scene.camera = camera
    for name,location,energy,size,target in (
        ("MM_Key",(.50,-1.20,2.35),720,2.4,(0,0,.52)),
        ("MM_Fill",(-1.35,-.35,1.30),420,2.0,(-.1,0,.55)),
        ("MM_Rim",(.30,1.10,2.05),680,1.6,(.15,0,.62)),
    ):
        bpy.ops.object.light_add(type="AREA",location=location)
        light = bpy.context.object
        light.name, light.data.energy, light.data.shape, light.data.size = name, energy, "DISK", size
        point_at(light,target)


def model_records():
    return sorted(
        ({"object":obj.name,"partId":obj["mm_part_id"],"assemblyId":obj["mm_assembly_id"],"serviceBoundary":obj["mm_service_boundary"],"type":obj.type}
         for obj in bpy.context.scene.objects if "mm_part_id" in obj),
        key=lambda item:item["partId"],
    )


def validate_source(records):
    ids = [record["partId"] for record in records]
    if len(records) != 595:
        raise RuntimeError(f"Expected 595 source part objects, got {len(records)}")
    if len(set(ids)) != len(ids):
        duplicates = sorted({value for value in ids if ids.count(value) > 1})
        raise RuntimeError("Duplicate part IDs: " + ", ".join(duplicates[:10]))
    counts = {}
    for record in records:
        counts[record["assemblyId"]] = counts.get(record["assemblyId"],0)+1
    expected = {
        "frame":22,"cockpit_headset":24,"fork":28,"wheel_front":90,"wheel_rear":120,
        "brake_front":37,"brake_rear":37,"crank_bottom_bracket":25,"front_derailleur":14,
        "rear_derailleur":26,"chain":112,"pedals":32,"saddle_seatpost":12,"controls_cables":16,
    }
    if counts != expected:
        raise RuntimeError(f"Assembly counts differ: expected {expected}, got {counts}")


def write_manifest(records):
    mesh_objects = [obj for obj in bpy.context.scene.objects if obj.type == "MESH" and "mm_part_id" in obj]
    payload = {
        "schemaVersion":1,
        "moduleId":"bike.hardtail.27_5.2x10.v1",
        "sourceAsset":"Assets/Art/Models/Source/BicycleEngineeringSource.blend",
        "unit":"metre",
        "partObjects":len(records),
        "meshObjects":len(mesh_objects),
        "vertices":sum(len(obj.data.vertices) for obj in mesh_objects),
        "polygons":sum(len(obj.data.polygons) for obj in mesh_objects),
        "parts":records,
    }
    MANIFEST_PATH.parent.mkdir(parents=True,exist_ok=True)
    MANIFEST_PATH.write_text(json.dumps(payload,ensure_ascii=False,indent=2),encoding="utf-8")


def save_and_render():
    BLEND_PATH.parent.mkdir(parents=True,exist_ok=True)
    PREVIEW_PATH.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    bpy.context.scene.render.filepath = str(PREVIEW_PATH)
    bpy.ops.render.render(write_still=True)
    camera = bpy.context.scene.camera
    camera.location, camera.data.lens = (.20,-1.48,.66),70
    point_at(camera,(-.31,-.04,.39))
    bpy.context.scene.render.filepath = str(DETAIL_PATH)
    bpy.ops.render.render(write_still=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))


def main():
    bike = Bike()
    bike.build()
    setup_scene()
    records = model_records()
    validate_source(records)
    write_manifest(records)
    save_and_render()
    mesh_objects = [obj for obj in bpy.context.scene.objects if obj.type == "MESH" and "mm_part_id" in obj]
    print(f"ENGINEERING_MODEL parts={len(records)} meshes={len(mesh_objects)} vertices={sum(len(obj.data.vertices) for obj in mesh_objects)} polygons={sum(len(obj.data.polygons) for obj in mesh_objects)}")
    print("Generated",BLEND_PATH)
    print("Generated",PREVIEW_PATH)
    print("Generated",DETAIL_PATH)
    print("Generated",MANIFEST_PATH)


if __name__ == "__main__":
    main()
