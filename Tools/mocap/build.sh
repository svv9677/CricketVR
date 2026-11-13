#!/bin/bash
# Video -> bowler animation, end to end. Usage:
#   ./build.sh <video> <name> [process_motion.py args, e.g. --end 45 --arm right]
# Writes out/<name>.npz/.json, out/<name>.fbx, out/<name>_side.png and out/<name>_back.png (preview
# sheets). Copy the .fbx into Assets/Resources/Animations/Bowling/Mocap/ for Unity.
set -euo pipefail
cd "$(dirname "$0")"
VIDEO=$1; NAME=$2; shift 2
BLENDER=${BLENDER:-/Applications/Blender.app/Contents/MacOS/Blender}
mkdir -p out
[ -f "out/$NAME.npz" ] || .venv/bin/python extract_pose.py "$VIDEO" "out/$NAME.npz" --overlay
.venv/bin/python process_motion.py "out/$NAME.npz" "out/$NAME.json" "$@"
rm -rf "out/frames_$NAME"
"$BLENDER" -b --python-exit-code 1 -P retarget_blender.py -- "out/$NAME.json" \
    ../../Assets/Resources/Models/Bowler.fbx "out/$NAME.fbx" --render "out/frames_$NAME" \
    2>&1 | grep -e "^ground" -e "^feet" -e "^exported" -e "Error" -e "Traceback" || true
FPS=$(.venv/bin/python -c "import json;print(json.load(open('out/$NAME.json'))['fps'])")
for v in side back; do
    ffmpeg -v error -y -pattern_type glob -i "out/frames_$NAME/${v}_*.png" \
        -vf "select='not(mod(n\,3))',scale=180:-1,tile=8x2:color=white" -frames:v 1 "out/${NAME}_$v.png"
done
# Preview: the source (with landmarks) beside the rig from behind and from the side, at real speed.
.venv/bin/python preview.py "out/$NAME.json" "out/${NAME}_overlay.mp4" "out/frames_$NAME" "out/${NAME}_preview.mp4"
echo "sheets: out/${NAME}_side.png out/${NAME}_back.png  preview: out/${NAME}_preview.mp4"
