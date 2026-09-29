"""Fetch the pinned ODRI Bolt CAD and documentation into the ignored cache."""

import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path
import urllib.request

ROOT = Path(__file__).resolve().parents[2]
CACHE = ROOT / "Library/MechMaster/BoltSource"
REPOSITORY = "open-dynamic-robot-initiative/open_robot_actuator_hardware"
COMMIT = "66af1522b4fba0ec4a1d7790e66f5e4652208d30"
STEP_PATH = "mechanics/biped_6dof_v1/cad_files/STEP/biped_6dof_v1.STEP"


def request(url):
    return urllib.request.urlopen(urllib.request.Request(
        url, headers={"User-Agent": "MechMaster-Bolt-import"}), timeout=60)


def main():
    CACHE.mkdir(parents=True, exist_ok=True)
    with request(f"https://api.github.com/repos/{REPOSITORY}/git/trees/{COMMIT}?recursive=1") as response:
        tree = json.load(response)
    if tree.get("truncated"):
        raise ValueError("Incomplete official source tree")
    prefixes = ("mechanics/biped_6dof_v1/", "mechanics/biped_leg_3dof_v1/",
                "mechanics/actuator_module_v1/", "mechanics/actuator_module_v1.1/",
                "mechanics/general/")
    files = []
    for entry in tree["tree"]:
        path = entry["path"]
        if entry["type"] != "blob" or not (path in ("LICENSE", "README.md", STEP_PATH)
                or path.startswith(prefixes) and path.lower().endswith(".md")):
            continue
        target = (CACHE / "official" / path).resolve()
        if not target.is_relative_to((CACHE / "official").resolve()):
            raise ValueError("Unsafe source path")
        target.parent.mkdir(parents=True, exist_ok=True)
        url = f"https://raw.githubusercontent.com/{REPOSITORY}/{COMMIT}/{path}"
        if not target.exists():
            with request(url) as response:
                data = response.read()
            if len(data) != entry["size"]:
                raise ValueError(f"Incomplete source download: {path}")
            target.write_bytes(data)
        data = target.read_bytes()
        if len(data) != entry["size"]:
            raise ValueError(f"Cached source size differs: {path}")
        # Git's blob ID authenticates cached bytes against the pinned tree too.
        blob = hashlib.sha1(f"blob {len(data)}\0".encode() + data).hexdigest()
        if blob != entry["sha"]:
            raise ValueError(f"Cached source content differs: {path}")
        files.append({"path": path, "url": url, "bytes": len(data),
                      "sha256": hashlib.sha256(data).hexdigest(), "gitBlob": blob})
    manifest = {"schemaVersion": 1, "repository": f"https://github.com/{REPOSITORY}",
                "commit": COMMIT, "license": "BSD-3-Clause",
                "retrievedUtc": datetime.now(timezone.utc).isoformat(), "files": files}
    (CACHE / "source_manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(f"BOLT_SOURCES_OK {len(files)} official files, pinned {COMMIT}", flush=True)


if __name__ == "__main__":
    main()
