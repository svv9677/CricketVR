"""Copy a built clip into the Unity project with the sidecar its importer reads.

    .venv/bin/python install_clip.py out/img9676 MocapPace

Writes Assets/Resources/Animations/Bowling/Mocap/MocapPace.fbx and MocapPace.mocap.json (frames,
fps, release frame). On import, MocapBowlerImport makes it a humanoid clip on the bowler's avatar
with the ReleaseBall event, and wires it into the bowling controller as the state MocapPace.
"""
import json
import os
import shutil
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
DEST = os.path.join(HERE, "..", "..", "Assets", "Resources", "Animations", "Bowling", "Mocap")

src, name = sys.argv[1], sys.argv[2]
track = json.load(open(src + ".json"))
os.makedirs(DEST, exist_ok=True)
shutil.copyfile(src + ".fbx", os.path.join(DEST, name + ".fbx"))
side = {
    "frames": track["frames"],
    "fps": track["fps"],
    "release_frame": track["release_frame"],
    "source": os.path.basename(src),
    "contacts": track.get("contacts", []),
}
with open(os.path.join(DEST, name + ".mocap.json"), "w") as fh:
    json.dump(side, fh, indent=2)
print(f"installed {name}: {side['frames']} frames @ {side['fps']:.2f}, release frame {side['release_frame']} "
      f"({side['release_frame'] / (side['frames'] - 1):.3f} of the clip)")
