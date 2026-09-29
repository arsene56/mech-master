"""Fetch and authenticate the pinned OpenTorque standard CAD and notices."""

import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path
import urllib.parse
import urllib.request
import urllib.error
import subprocess

ROOT = Path(__file__).resolve().parents[2]
CACHE = ROOT / "Library/MechMaster/OpenTorqueSource"
REPOSITORY = "G-Levine/OpenTorque-Actuator"
COMMIT = "412762e9a4ca424564d3ebed882db95ef4b22ed9"
STEP_PATH = "STEP/opentorque.step"
PATHS = {STEP_PATH, "STEP/low_backlash_gears.step", "LICENSE", "README.md",
         "Bill of Materials.csv", "Print Instructions.txt"}


def request(url):
    return urllib.request.urlopen(urllib.request.Request(
        url, headers={"User-Agent": "MechMaster-OpenTorque-import"}), timeout=60)


def main():
    CACHE.mkdir(parents=True, exist_ok=True)
    try:
        with request(f"https://api.github.com/repos/{REPOSITORY}/git/trees/{COMMIT}?recursive=1") as response:
            tree = json.load(response)
    except urllib.error.HTTPError as error:
        if error.code not in (403, 429): raise
        # Anonymous GitHub API quotas must not weaken provenance verification.
        checkout = CACHE / "source-git"
        if not (checkout / ".git").exists():
            subprocess.run(["git", "clone", "--filter=blob:none", "--no-checkout", "--depth", "1",
                            f"https://github.com/{REPOSITORY}.git", str(checkout)], check=True)
        def git(*args):
            return subprocess.check_output(["git", "-C", str(checkout), *args], text=True).strip()
        try: git("cat-file", "-e", COMMIT)
        except subprocess.CalledProcessError:
            subprocess.run(["git", "-C", str(checkout), "fetch", "--depth", "1", "origin", COMMIT], check=True)
        tree = {"tree": [{"type": "blob", "path": path,
                          "sha": git("rev-parse", COMMIT + ":" + path),
                          "size": None} for path in sorted(PATHS)]}
    if tree.get("truncated"):
        raise ValueError("Incomplete official source tree")
    files = []
    for entry in tree["tree"]:
        path = entry["path"]
        if entry["type"] != "blob" or path not in PATHS:
            continue
        target = (CACHE / "official" / path).resolve()
        if not target.is_relative_to((CACHE / "official").resolve()):
            raise ValueError("Unsafe source path")
        target.parent.mkdir(parents=True, exist_ok=True)
        url = f"https://raw.githubusercontent.com/{REPOSITORY}/{COMMIT}/" + urllib.parse.quote(path)
        if not target.exists():
            assessed = CACHE / "assessment" / Path(path).name
            if assessed.exists() and path.endswith(".step"):
                data = assessed.read_bytes()
            else:
                with request(url) as response:
                    data = response.read()
            target.write_bytes(data)
        data = target.read_bytes()
        blob = hashlib.sha1(f"blob {len(data)}\0".encode() + data).hexdigest()
        if entry["size"] is not None and len(data) != entry["size"] or blob != entry["sha"]:
            raise ValueError("Source differs from pinned Git blob: " + path)
        files.append({"path": path, "url": url, "bytes": len(data),
                      "sha256": hashlib.sha256(data).hexdigest(), "gitBlob": blob})
    if {item["path"] for item in files} != PATHS:
        raise ValueError("Missing required source file")
    manifest = {"schemaVersion": 1, "repository": f"https://github.com/{REPOSITORY}",
                "commit": COMMIT, "license": "CC-BY-SA-4.0", "author": "Gabrael Levine",
                "retrievedUtc": datetime.now(timezone.utc).isoformat(), "files": files}
    (CACHE / "source_manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(f"OPENTORQUE_SOURCES_OK {len(files)} files, pinned {COMMIT}", flush=True)


if __name__ == "__main__":
    main()
