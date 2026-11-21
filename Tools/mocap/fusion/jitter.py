"""Jitter report for an animation on the bowler rig: per bone, how sharply its rotation changes
frame to frame (angular acceleration), and the worst spikes. Run inside Blender:

    Blender -b out/anim/x.blend -P fusion/jitter.py [-- --top 12]

Prints JITTER lines: per bone the mean and max angular acceleration (deg/frame^2) of its local
rotation, and the frames where the worst spikes are. A smooth swing has a steady acceleration;
a pop is one frame far above its neighbours.
"""
import math
import sys

import bpy

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
TOP = int(ARGS[ARGS.index("--top") + 1]) if "--top" in ARGS else 12
P = "mixamorig5:"
BONES = ["Hips", "Spine", "Spine1", "Spine2", "Neck", "Head", "LeftArm", "LeftForeArm", "LeftHand",
         "RightArm", "RightForeArm", "RightHand", "LeftUpLeg", "LeftLeg", "LeftFoot", "RightUpLeg", "RightLeg",
         "RightFoot"]


def angle(q1, q2):
    d = abs(q1.dot(q2))
    return math.degrees(2 * math.acos(min(1.0, d)))


def main():
    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    f0, f1 = (int(v) for v in arm.animation_data.action.frame_range)
    sc = bpy.context.scene
    qs = {b: [] for b in BONES}
    for f in range(f0, f1 + 1):
        sc.frame_set(f)
        for b in BONES:
            qs[b].append(arm.pose.bones[P + b].matrix_basis.to_quaternion())
    spikes = []
    total = []
    for b in BONES:
        q = qs[b]
        vel = [angle(q[i], q[i + 1]) for i in range(len(q) - 1)]
        acc = [abs(vel[i + 1] - vel[i]) for i in range(len(vel) - 1)]
        if not acc:
            continue
        total += acc
        worst = sorted(range(len(acc)), key=lambda i: -acc[i])[:3]
        print(f"JITTER {b:13s} mean {sum(acc) / len(acc):5.2f} max {max(acc):6.2f}  at frames "
              + ", ".join(f"{i + 1 + f0}({acc[i]:.1f})" for i in worst))
        spikes += [(acc[i], b, i + 1 + f0) for i in range(len(acc))]
    print(f"JITTER ALL mean {sum(total) / len(total):.3f} deg/frame^2")
    for a, b, f in sorted(spikes, reverse=True)[:TOP]:
        print(f"JITTER SPIKE {b} frame {f}: {a:.1f}")


main()
