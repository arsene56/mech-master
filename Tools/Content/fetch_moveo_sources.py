"""Fetch pinned, freely licensed Moveo sources into the ignored local cache."""

import argparse
import hashlib
import json
from pathlib import Path
import shutil
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[2]
CACHE = ROOT / "Library/MechMaster/MoveoSource"
SOURCES = (
    ("official", "BCN3D/BCN3D-Moveo", "0866a92501277636f76000a195d8a16d44b5b476"),
    ("ros", "jesseweisberg/moveo_ros", "b9282bdadbf2505a26d3b94b91e60a98d86efa34"),
)


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--include-ros", action="store_true",
                        help="Also fetch the community ROS conversion for audit; it is not an accepted game asset.")
    arguments = parser.parse_args()
    CACHE.mkdir(parents=True, exist_ok=True)
    records = []
    for name, repository, commit in SOURCES:
        if name == "ros" and not arguments.include_ros:
            continue
        url = f"https://codeload.github.com/{repository}/zip/{commit}"
        archive = CACHE / f"{name}-{commit}.zip"
        if not archive.exists():
            request = urllib.request.Request(url, headers={"User-Agent": "MechMaster-Moveo-import"})
            pending = archive.with_suffix(".download")
            with urllib.request.urlopen(request, timeout=120) as response, pending.open("wb") as output:
                shutil.copyfileobj(response, output)
            pending.replace(archive)
        files = []
        with zipfile.ZipFile(archive) as source:
            for entry in source.infolist():
                if entry.is_dir():
                    continue
                relative = Path(*Path(entry.filename).parts[1:])
                if name == "official" and relative.parts[0] not in (
                    "CAD files", "STL files", "BOM", "USER MANUAL", "LICENSE", "README.md"
                ):
                    continue
                if name == "ros" and relative.parts[0] not in ("moveo_urdf", "LICENSE", "README.md"):
                    continue
                if relative.name.lower() == "desktop.ini":
                    continue
                target = (CACHE / name / relative).resolve()
                if not target.is_relative_to((CACHE / name).resolve()):
                    raise ValueError(f"Unsafe archive member: {entry.filename}")
                target.parent.mkdir(parents=True, exist_ok=True)
                with source.open(entry) as stream, target.open("wb") as output:
                    shutil.copyfileobj(stream, output)
                files.append({"path": relative.as_posix(), "bytes": target.stat().st_size,
                              "sha256": sha256(target)})
        records.append({"name": name, "repository": f"https://github.com/{repository}",
                        "commit": commit, "archiveUrl": url, "archiveSha256": sha256(archive),
                        "files": files})
        print(f"Fetched {name}: {len(files)} files, archive {archive.stat().st_size:,} bytes", flush=True)
    manifest = CACHE / "source_manifest.json"
    manifest.write_text(json.dumps({"schemaVersion": 1, "sources": records}, indent=2), encoding="utf-8")
    print(f"Source provenance: {manifest}")


if __name__ == "__main__":
    main()
