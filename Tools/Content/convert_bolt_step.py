"""Convert the pinned Bolt STEP using OCCT/XCAF; retain assembly occurrences.

Requires Python 3.12 and the local cadquery-ocp 7.8.1.1.post1 / vtk 9.3.1
packages under Library/MechMaster/BoltSource/parser/python. The converter is
build-time only. OCCT reads solids in millimetres; output geometry is metres.
"""

import argparse
import hashlib
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
CACHE = ROOT / "Library/MechMaster/BoltSource"
sys.path.insert(0, str(CACHE / "parser/python"))

from OCP.BRep import BRep_Tool
from OCP.BRepBndLib import BRepBndLib
from OCP.BRepMesh import BRepMesh_IncrementalMesh
from OCP.Bnd import Bnd_Box
from OCP.IFSelect import IFSelect_RetDone
from OCP.Interface import Interface_Static
from OCP.Quantity import Quantity_Color
from OCP.STEPCAFControl import STEPCAFControl_Reader
from OCP.TCollection import TCollection_AsciiString, TCollection_ExtendedString
from OCP.TDataStd import TDataStd_Name
from OCP.TDF import TDF_Label, TDF_LabelSequence, TDF_Tool
from OCP.TDocStd import TDocStd_Document
from OCP.TopAbs import TopAbs_FACE, TopAbs_REVERSED
from OCP.TopExp import TopExp_Explorer
from OCP.TopLoc import TopLoc_Location
from OCP.TopoDS import TopoDS
from OCP.XCAFDoc import XCAFDoc_DocumentTool, XCAFDoc_ColorGen, XCAFDoc_ColorSurf

COMMIT = "66af1522b4fba0ec4a1d7790e66f5e4652208d30"
STEP_PATH = "mechanics/biped_6dof_v1/cad_files/STEP/biped_6dof_v1.STEP"


def entry(label):
    text = TCollection_AsciiString()
    TDF_Tool.Entry_s(label, text)
    return text.ToCString()


def name(label):
    attribute = TDataStd_Name()
    if label.FindAttribute(TDataStd_Name.GetID_s(), attribute):
        return attribute.Get().ToExtString()
    return entry(label)


def matrix(location):
    transform = location.Transformation()
    return [[transform.Value(row + 1, column + 1) / (1000 if column == 3 else 1)
             for column in range(4)] for row in range(3)] + [[0, 0, 0, 1]]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--audit-only", action="store_true")
    arguments = parser.parse_args()
    provenance = json.loads((CACHE / "source_manifest.json").read_text(encoding="utf-8"))
    record = next(item for item in provenance["files"] if item["path"] == STEP_PATH)
    source = CACHE / "official" / STEP_PATH
    digest = hashlib.sha256(source.read_bytes()).hexdigest()
    if provenance["commit"] != COMMIT or digest != record["sha256"]:
        raise ValueError("Bolt source provenance mismatch")
    reader = STEPCAFControl_Reader()
    reader.SetNameMode(True)
    reader.SetColorMode(True)
    Interface_Static.SetCVal_s("xstep.cascade.unit", "MM")
    if reader.ReadFile(str(source)) != IFSelect_RetDone:
        raise ValueError("STEP reader rejected the source")
    document = TDocStd_Document(TCollection_ExtendedString("BinXCAF"))
    if not reader.Transfer(document):
        raise ValueError("STEP assembly transfer failed")
    shapes = XCAFDoc_DocumentTool.ShapeTool_s(document.Main())
    colors = XCAFDoc_DocumentTool.ColorTool_s(document.Main())
    roots = TDF_LabelSequence()
    shapes.GetFreeShapes(roots)
    instances, definitions, tree = [], {}, []

    def walk(label, parent, ancestry, occurrence_path, depth=0):
        if depth > 30:
            raise ValueError("Cyclic assembly tree")
        referred = TDF_Label()
        definition = label
        location = parent
        if shapes.IsReference_s(label):
            if not shapes.GetReferredShape_s(label, referred):
                raise ValueError("Unresolved assembly reference")
            definition = referred
            location = parent.Multiplied(shapes.GetLocation_s(label))
        key = entry(definition)
        source_name = name(definition)
        path = occurrence_path + [f"{source_name}@{entry(label)}"]
        names = ancestry + [source_name]
        tree.append({"definition": key, "name": source_name, "depth": depth,
                     "path": "/".join(path), "matrix": matrix(location)})
        if shapes.IsAssembly_s(definition):
            children = TDF_LabelSequence()
            shapes.GetComponents_s(definition, children, False)
            for index in range(1, children.Length() + 1):
                walk(children.Value(index), location, names, path, depth + 1)
            return
        shape = shapes.GetShape_s(definition)
        if shape.IsNull():
            raise ValueError(f"Empty CAD part: {source_name}")
        if key not in definitions:
            box = Bnd_Box()
            BRepBndLib.Add_s(shape, box, False)
            bounds = [value / 1000 for value in box.Get()]
            color = Quantity_Color()
            rgb = None
            for kind in (XCAFDoc_ColorSurf, XCAFDoc_ColorGen):
                if colors.GetColor_s(definition, kind, color):
                    rgb = [color.Red(), color.Green(), color.Blue()]
                    break
            definition_record = {"name": source_name, "bounds": bounds, "color": rgb}
            if not arguments.audit_only:
                BRepMesh_IncrementalMesh(shape, .08, False, .22, True)
                vertices, indices = [], []
                explorer = TopExp_Explorer(shape, TopAbs_FACE)
                while explorer.More():
                    face = TopoDS.Face_s(explorer.Current())
                    face_location = TopLoc_Location()
                    triangulation = BRep_Tool.Triangulation_s(face, face_location)
                    if triangulation is None:
                        raise ValueError(f"Untessellated CAD face: {source_name}")
                    offset = len(vertices) // 3
                    transform = face_location.Transformation()
                    for index in range(1, triangulation.NbNodes() + 1):
                        point = triangulation.Node(index).Transformed(transform)
                        vertices.extend([point.X() / 1000, point.Y() / 1000, point.Z() / 1000])
                    reverse = face.Orientation() == TopAbs_REVERSED
                    for index in range(1, triangulation.NbTriangles() + 1):
                        a, b, c = triangulation.Triangle(index).Get()
                        indices.extend([offset + a - 1, offset + (c if reverse else b) - 1,
                                        offset + (b if reverse else c) - 1])
                    explorer.Next()
                if not indices:
                    raise ValueError(f"Empty tessellation: {source_name}")
                definition_record.update(vertices=vertices, indices=indices)
            definitions[key] = definition_record
        instances.append({"modelId": key, "sourcePath": "/".join(path),
                          "ancestry": names, "matrix": matrix(location)})

    for index in range(1, roots.Length() + 1):
        walk(roots.Value(index), TopLoc_Location(), [], [])
    if len({item["sourcePath"] for item in instances}) != len(instances):
        raise ValueError("Duplicate CAD occurrence identities")
    target = CACHE / "converted"
    target.mkdir(parents=True, exist_ok=True)
    audit = {"schemaVersion": 1, "sourceCommit": COMMIT, "stepSha256": digest,
             "units": "m", "definitionCount": len(definitions), "instanceCount": len(instances),
             "definitions": {key: {k: v for k, v in item.items() if k not in ("vertices", "indices")}
                             for key, item in definitions.items()}, "instances": instances, "tree": tree}
    (target / "assembly_audit.json").write_text(json.dumps(audit, indent=2), encoding="utf-8")
    if not arguments.audit_only:
        geometry = {"schemaVersion": 1, "sourceCommit": COMMIT, "stepSha256": digest,
                    "units": "m", "meshes": definitions, "instances": instances}
        (target / "geometry.json").write_text(json.dumps(geometry), encoding="utf-8")
    print(f"BOLT_STEP_OK definitions={len(definitions)} instances={len(instances)} metre output", flush=True)


if __name__ == "__main__":
    main()
