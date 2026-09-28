"""Recover the pinned Moveo assembly's saved meshes and instance transforms.

This reads the legacy OLE container, not the parametric CAD solid. Triangle-strip
decoding is adapted from Josh Donner's MIT-licensed model_viewer, commit
9aabcab92cfaaac03b1276229e11ce798b60e6f9, research/d9-decode.py. Its notice is
retained in ThirdParty/Wintaru-MIT-LICENSE.txt. Assembly transforms come directly
from the official assembly's COMPINSTANCETREE, rather than community URDF poses.
"""

import hashlib
import json
import math
from pathlib import Path
import re
import struct
import sys
import xml.etree.ElementTree as ET
import zlib

ROOT = Path(__file__).resolve().parents[2]
CACHE = ROOT / "Library/MechMaster/MoveoSource"
sys.path.insert(0, str(CACHE / "parser/python"))
try:
    import olefile
except ImportError as error:
    raise SystemExit("Install the reader locally: python -m pip install --target "
                     "Library/MechMaster/MoveoSource/parser/python olefile==0.47") from error

NS = {"s": "http://www.solidworks.com/sw2003/schema"}
HEADER = struct.pack("<III", 4, 8, 2)
IDENTITY = [[float(row == column) for column in range(4)] for row in range(4)]


def unpack_zlb(data):
    """The legacy stream has a 16-byte tag, then size-prefixed zlib blocks."""
    cursor = 16
    output = []
    while cursor + 8 <= len(data):
        size, compressed_size = struct.unpack_from("<II", data, cursor)
        if size == compressed_size == 0 and cursor + 8 == len(data):
            cursor += 8
            break
        if size > 16 * 1024 * 1024 or cursor + 8 + compressed_size > len(data):
            raise ValueError("Invalid legacy compressed stream")
        decoder = zlib.decompressobj()
        decoded = decoder.decompress(data[cursor + 8:cursor + 8 + compressed_size], size + 1)
        if len(decoded) != size or not decoder.eof:
            raise ValueError("Incomplete legacy compressed block")
        output.append(decoded)
        cursor += 8 + compressed_size
    if cursor != len(data):
        raise ValueError("Unaccounted legacy compressed bytes")
    return b"".join(output)


def read_stream(document, name):
    data = document.openstream(name).read()
    return unpack_zlb(data) if name.endswith("__ZLB") else data


def serialized_instance_spans(data):
    """Bound meshes by exact MFC UTF-16 instance paths in this saved assembly."""
    paths = []
    for match in re.finditer(b"\xff\xfe\xff", data):
        start = match.start()
        if start + 4 > len(data):
            continue
        length = data[start + 3]
        if not 0 < length < 255 or start + 4 + length * 2 > len(data):
            continue
        try:
            name = data[start + 4:start + 4 + length * 2].decode("utf-16-le")
        except UnicodeDecodeError:
            continue
        if all(char.isprintable() for char in name) and "@" in name \
                and all("@" in part for part in name.split("/")):
            paths.append((start, name))
    if len({name for _, name in paths}) != len(paths):
        raise ValueError("Ambiguous cached instance paths")
    return {name: (start, paths[i + 1][0] if i + 1 < len(paths) else len(data))
            for i, (start, name) in enumerate(paths)}


def decode_tessellation(data):
    """Decode validated 4/8/2 strip records at any byte alignment, in metres."""
    candidates = []
    cursor = 0
    while True:
        start = data.find(HEADER, cursor)
        if start < 0:
            break
        cursor = start + 1
        if start + 16 > len(data):
            continue
        count = struct.unpack_from("<I", data, start + 12)[0]
        if not 1 <= count <= 20000 or start + 16 + count * 4 + 32 > len(data):
            continue
        sizes = struct.unpack_from(f"<{count}I", data, start + 16)
        if any(size < 3 or size > 100000 for size in sizes):
            continue
        total = sum(sizes)
        tail = start + 16 + count * 4
        position = None
        for word in range(1, 8):
            marker, vertex_count = struct.unpack_from("<II", data, tail + (word - 1) * 4)
            if marker == 2 and vertex_count == total:
                position = tail + (word + 1) * 4
                break
        if position is None or position + total * 24 > len(data):
            continue
        vertices = struct.unpack_from(f"<{total * 3}f", data, position)
        normals = struct.unpack_from(f"<{total * 3}f", data, position + total * 12)
        if any(not math.isfinite(value) or abs(value) > 100 for value in vertices):
            continue
        nonzero = unit = 0
        for offset in range(0, len(normals), 3):
            length = math.sqrt(sum(value * value for value in normals[offset:offset + 3]))
            if length > 1e-9:
                nonzero += 1
                unit += abs(length - 1) < .5
        if not nonzero or unit / nonzero < .8:
            continue
        end = position + total * 24
        candidates.append((start, end, vertices, sizes))
        cursor = end
    kept = []
    for candidate in sorted(candidates, key=lambda item: -(item[1] - item[0])):
        if not any(candidate[0] < end and start < candidate[1] for start, end, _, _ in kept):
            kept.append(candidate)
    vertices, triangles = [], []
    for _, _, values, sizes in sorted(kept):
        base = len(vertices) // 3
        vertices.extend(values)
        for size in sizes:
            for index in range(size - 2):
                triangles.extend((base + index, base + index + 1, base + index + 2)
                                 if index % 2 == 0 else
                                 (base + index + 1, base + index, base + index + 2))
            base += size
    return vertices, triangles


def multiply(a, b):
    return [[sum(a[row][i] * b[i][column] for i in range(4))
             for column in range(4)] for row in range(4)]


def main():
    path = CACHE / "official/CAD files/BCN3D Moveo assembly.SLDASM"
    provenance = json.loads((CACHE / "source_manifest.json").read_text(encoding="utf-8"))
    source = next(item for item in provenance["sources"] if item["name"] == "official")
    expected = next(item["sha256"] for item in source["files"]
                    if item["path"] == "CAD files/BCN3D Moveo assembly.SLDASM")
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    if digest != expected or source["commit"] != "0866a92501277636f76000a195d8a16d44b5b476":
        raise ValueError("The source is not the audited official revision")
    with olefile.OleFileIO(path) as document:
        xml = ET.fromstring(read_stream(document, "swXmlContents/COMPINSTANCETREE__ZLB"))
        data = read_stream(document, "Contents/DisplayLists__ZLB")
    models = {model.get("id"): model for model in xml.findall(".//s:swModel", NS)}
    files = {item.get("id"): item for item in xml.findall(".//s:swFile", NS)}
    configuration = next(item for item in xml.findall(".//s:swConfiguration", NS)
                         if item.get("swMostRecentConfiguration") == "YES")
    spans = serialized_instance_spans(data)
    meshes, instances, excluded = {}, [], []
    recovered = set()

    def recover(model_id, instance_path):
        model = models[model_id]
        refs = model.findall("s:swReference", NS)
        if refs:
            for ref in refs:
                child = f"{ref.get('swName')}-{ref.get('swReferenceNumber')}@{model.get('swName')}"
                recover(ref.get("swModelRef"), f"{instance_path}/{child}" if instance_path else child)
            return
        if model_id in recovered:
            return
        recovered.add(model_id)
        if instance_path not in spans:
            raise ValueError(f"Missing saved mesh path: {instance_path}")
        start, end = spans[instance_path]
        vertices, triangles = decode_tessellation(data[start:end])
        document_type = files[model.get("swFileRef")].get("swDocType")
        if not triangles:
            if document_type != "ASSEMBLY":
                raise ValueError(f"No saved geometry: {model.get('swName')}")
            return
        meshes[model_id] = {
            "name": model.get("swName"), "vertices": vertices, "indices": triangles,
            "sourceFile": files[model.get("swFileRef")].get("swPath").split("\\")[-1],
            "cacheByteRange": [start, end],
        }

    def visit(model_id, matrix, instance_path, ancestry):
        model = models[model_id]
        refs = model.findall("s:swReference", NS)
        if not refs:
            if model_id not in meshes:
                excluded.append({"path": instance_path, "reason": "empty-assembly-reference"})
                return
            instances.append({"modelId": model_id, "matrix": matrix,
                              "sourcePath": instance_path, "ancestry": ancestry})
            return
        for ref in refs:
            child = f"{ref.get('swName')}-{ref.get('swReferenceNumber')}@{model.get('swName')}"
            child_path = f"{instance_path}/{child}" if instance_path else child
            if ref.get("swSuppressed") == "YES" or ref.get("swHidden") == "YES":
                excluded.append({"path": child_path, "reason": "source-hidden-or-suppressed"})
                continue
            # The standalone servo duplicates the complete gripper's servo, with
            # intersecting housings (<0.5 mm apart). The official BOM specifies one.
            if child_path == "4M-1@BCN3D Moveo assembly/2-1@4M":
                excluded.append({"path": child_path, "reason": "overlapping-duplicate-gripper-servo"})
                continue
            values = list(map(float, ref.get("swTransform").split()))
            if len(values) != 16:
                raise ValueError("Invalid saved affine transform")
            transform = [[values[column * 4 + row] for column in range(4)] for row in range(4)]
            visit(ref.get("swModelRef"), multiply(matrix, transform), child_path,
                  ancestry + [ref.get("swName")])

    # A hidden first occurrence can own the one shared cache used by later
    # visible instances, so recover definitions before applying visibility.
    recover(configuration.get("swModelRef"), "")
    visit(configuration.get("swModelRef"), IDENTITY, "", [])
    if len(meshes) != 87 or len(instances) != 366:
        raise ValueError(f"Unexpected audited inventory: {len(meshes)} types, {len(instances)} instances")
    output = CACHE / "converted"
    output.mkdir(parents=True, exist_ok=True)
    result = {"schemaVersion": 1, "units": "m", "sourceCommit": source["commit"],
              "assemblySha256": digest, "meshes": meshes, "instances": instances,
              "excludedReferences": excluded}
    (output / "geometry.json").write_text(json.dumps(result), encoding="utf-8")
    print(f"Recovered {len(meshes)} source types / {len(instances)} independent instances")
    print(f"Saved {output / 'geometry.json'}")


if __name__ == "__main__":
    main()
