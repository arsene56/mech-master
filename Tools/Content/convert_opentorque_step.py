"""Convert standard OpenTorque STEP with the pinned Bolt OCCT build dependency.

Python 3.12, cadquery-ocp 7.8.1.1.post1 and vtk 9.3.1; output is metres.
The alternate low-backlash gear set is deliberately not added or substituted.
"""

import hashlib
import json
from pathlib import Path

import convert_bolt_step as cad
from OCP.BRepCheck import BRepCheck_Analyzer
from fetch_opentorque_sources import CACHE, COMMIT, STEP_PATH


def main():
    provenance = json.loads((CACHE / "source_manifest.json").read_text(encoding="utf-8"))
    record = next(item for item in provenance["files"] if item["path"] == STEP_PATH)
    source = CACHE / "official" / STEP_PATH
    digest = hashlib.sha256(source.read_bytes()).hexdigest()
    if provenance["commit"] != COMMIT or digest != record["sha256"]:
        raise ValueError("OpenTorque source provenance mismatch")
    reader = cad.STEPCAFControl_Reader()
    reader.SetNameMode(True)
    reader.SetColorMode(True)
    cad.Interface_Static.SetCVal_s("xstep.cascade.unit", "MM")
    if reader.ReadFile(str(source)) != cad.IFSelect_RetDone:
        raise ValueError("STEP read failed")
    document = cad.TDocStd_Document(cad.TCollection_ExtendedString("BinXCAF"))
    if not reader.Transfer(document):
        raise ValueError("STEP assembly transfer failed")
    shapes = cad.XCAFDoc_DocumentTool.ShapeTool_s(document.Main())
    roots = cad.TDF_LabelSequence()
    shapes.GetFreeShapes(roots)
    definitions, instances, tree = {}, [], []

    def walk(label, parent, ancestry, path, depth=0):
        if depth > 30:
            raise ValueError("Cyclic assembly")
        definition, location = label, parent
        if shapes.IsReference_s(label):
            definition = cad.TDF_Label()
            if not shapes.GetReferredShape_s(label, definition):
                raise ValueError("Unresolved assembly reference")
            location = parent.Multiplied(shapes.GetLocation_s(label))
        key, name = cad.entry(definition), cad.name(definition)
        path = path + [name + "@" + cad.entry(label)]
        ancestry = ancestry + [name]
        tree.append({"definition": key, "name": name, "depth": depth,
                     "path": "/".join(path), "matrix": cad.matrix(location)})
        if shapes.IsAssembly_s(definition):
            children = cad.TDF_LabelSequence()
            shapes.GetComponents_s(definition, children, False)
            for index in range(1, children.Length() + 1):
                walk(children.Value(index), location, ancestry, path, depth + 1)
            return
        shape = shapes.GetShape_s(definition)
        if shape.IsNull() or not BRepCheck_Analyzer(shape).IsValid():
            raise ValueError("Invalid CAD part: " + name)
        if key not in definitions:
            box = cad.Bnd_Box()
            cad.BRepBndLib.AddOptimal_s(shape, box, False, False)
            cad.BRepMesh_IncrementalMesh(shape, .05, False, .18, True)
            vertices, indices = [], []
            explorer = cad.TopExp_Explorer(shape, cad.TopAbs_FACE)
            while explorer.More():
                face = cad.TopoDS.Face_s(explorer.Current())
                local = cad.TopLoc_Location()
                mesh = cad.BRep_Tool.Triangulation_s(face, local)
                if mesh is None:
                    raise ValueError("Untessellated face: " + name)
                offset = len(vertices) // 3
                transform = local.Transformation()
                for index in range(1, mesh.NbNodes() + 1):
                    point = mesh.Node(index).Transformed(transform)
                    vertices.extend([point.X() / 1000, point.Y() / 1000, point.Z() / 1000])
                reverse = face.Orientation() == cad.TopAbs_REVERSED
                for index in range(1, mesh.NbTriangles() + 1):
                    a, b, c = mesh.Triangle(index).Get()
                    indices.extend([offset + a - 1, offset + (c if reverse else b) - 1,
                                    offset + (b if reverse else c) - 1])
                explorer.Next()
            if not indices:
                raise ValueError("Empty tessellation: " + name)
            definitions[key] = {"name": name, "bounds": [value / 1000 for value in box.Get()],
                                "vertices": vertices, "indices": indices}
        instances.append({"modelId": key, "sourcePath": "/".join(path),
                          "ancestry": ancestry, "matrix": cad.matrix(location)})

    for index in range(1, roots.Length() + 1):
        walk(roots.Value(index), cad.TopLoc_Location(), [], [])
    if len(instances) != 19 or len(definitions) != 13 or len({i["sourcePath"] for i in instances}) != 19:
        raise ValueError("Unexpected pinned standard CAD inventory")
    output = CACHE / "converted"
    output.mkdir(parents=True, exist_ok=True)
    base = {"schemaVersion": 1, "sourceCommit": COMMIT, "stepSha256": digest, "units": "m"}
    geometry = dict(base, meshes=definitions, instances=instances)
    (output / "geometry.json").write_text(json.dumps(geometry), encoding="utf-8")
    audit = dict(base, definitionCount=13, instanceCount=19, instances=instances, tree=tree,
                 linearDeflectionMm=.05, angularDeflectionRad=.18,
                 definitions={k: {field: value for field, value in item.items()
                              if field not in ("vertices", "indices")} for k, item in definitions.items()})
    (output / "assembly_audit.json").write_text(json.dumps(audit, indent=2), encoding="utf-8")
    print("OPENTORQUE_STEP_OK definitions=13 instances=19 metre output", flush=True)


if __name__ == "__main__":
    main()
