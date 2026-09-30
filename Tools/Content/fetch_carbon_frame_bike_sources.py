"""Authenticate the author's anonymous, pinned Carbon Frame Bike download."""

import hashlib
import json
from pathlib import Path
import urllib.request
import urllib.error

ROOT = Path(__file__).resolve().parents[2]
CACHE = ROOT / "Library/MechMaster/CarbonFrameBikeSource"
REPOSITORY = "prefrontalcortex/glTF-Sample-Models"
COMMIT = "93c72b11cf78dd6a3cd50b875d752cd7e6dd4ab2"
BASE = "2.0/CarbonFrameBike/"
PATHS = {BASE + "glTF-Binary/CarbonFrameBike.glb": "CarbonFrameBike.glb",
         BASE + "README.md": "README.md"}
# Verified against this commit's Git tree, not derived from local downloads.
EXPECTED = {BASE + "glTF-Binary/CarbonFrameBike.glb": {"size": 12183440, "sha": "121959c4b83c1f88c82a4df94e8a60b481ff6338"},
            BASE + "README.md": {"size": 1747, "sha": "a8dd670e6be576f838ef5218a578f7cdf2f139b1"}}


def request(url):
    return urllib.request.urlopen(urllib.request.Request(url, headers={"User-Agent": "MechMaster-CarbonBike-import"}), timeout=60)


def main():
    official = CACHE / "official"
    official.mkdir(parents=True, exist_ok=True)
    try:
        with request(f"https://api.github.com/repos/{REPOSITORY}/git/trees/{COMMIT}?recursive=1") as response:
            tree = json.load(response)
        if tree.get("truncated"): raise ValueError("Incomplete source tree")
        entries = {e["path"]: e for e in tree["tree"] if e["type"] == "blob" and e["path"] in PATHS}
    except urllib.error.HTTPError as error:
        if error.code not in (403, 429): raise
        entries = EXPECTED
    if set(entries) != set(PATHS): raise ValueError("Missing pinned source")
    if any(entries[p]["sha"] != EXPECTED[p]["sha"] or entries[p]["size"] != EXPECTED[p]["size"] for p in PATHS):
        raise ValueError("Unexpected pinned source tree")
    files = []
    for path, filename in PATHS.items():
        target = official / filename
        url = f"https://raw.githubusercontent.com/{REPOSITORY}/{COMMIT}/{path}"
        if not target.exists():
            with request(url) as response: target.write_bytes(response.read())
        data = target.read_bytes()
        blob = hashlib.sha1(f"blob {len(data)}\0".encode() + data).hexdigest()
        if len(data) != entries[path]["size"] or blob != entries[path]["sha"]:
            raise ValueError("Pinned Git blob mismatch: " + path)
        files.append({"path": path, "url": url, "bytes": len(data), "gitBlob": blob,
                      "sha256": hashlib.sha256(data).hexdigest()})
    readme = (official / "README.md").read_text(encoding="utf-8")
    if "Creative Commons Attribution-ShareAlike 4.0 International License" not in readme:
        raise ValueError("Author's model license declaration missing")
    license_path = official / "CC-BY-SA-4.0-LICENSE.txt"
    if not license_path.exists():
        with request("https://creativecommons.org/licenses/by-sa/4.0/legalcode.txt") as response:
            license_path.write_bytes(response.read())
    (CACHE / "source_manifest.json").write_text(json.dumps({"schemaVersion": 1,
        "repository": f"https://github.com/{REPOSITORY}", "commit": COMMIT,
        "license": "CC-BY-SA-4.0", "authors": ["Robert Schweier (RobertS Bikes)", "Felix Herbst / prefrontal cortex"],
        "files": files}, indent=2), encoding="utf-8")
    print("CARBON_SOURCES_OK", COMMIT, files, flush=True)


if __name__ == "__main__": main()
