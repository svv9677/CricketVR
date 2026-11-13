"""Hand corrections on the finished animation's bones, frame range by frame range.

Run on a .blend made by fuse_views.py (--blend), then blender_extras.py for the preview:

    Blender -b out/anim/x.blend -P fusion/bone_fix.py -P fusion/blender_extras.py -- \
        fixes.json out/anim/x_final.fbx --save-blend out/anim/x_final.blend --preview-dir out/anim/x_final_frames

fixes.json: a list of corrections, applied in order, each a rotation of one bone (and so
everything below it) in world space, eased in and out so nothing pops:

    [{"bone": "Spine1", "frames": [27, 29, 32, 32], "turn": "forward", "deg": 7,
      "why": "follow-through bends further than fused (side video 34-38 deg, fused 22-26)"}]

frames [a, b, c, d]: zero at a, full from b to c, back to zero at d (a == b or c == d: no ramp).
turn: "forward" pitches the bone toward the run (about the bowler's left-right axis),
      "raise" lifts a limb's far end (about the axis across the limb and vertical),
      "side" leans it toward his left (about the run axis).
Bone names without the mixamorig5: prefix.
"""
import json
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

ARGS = sys.argv[sys.argv.index("--") + 1:]
FIXES, OUT_FBX = ARGS[0], ARGS[1]
P = "mixamorig5:"


def weight(f, a, b, c, d):
    if f < a or f > d:
        return 0.0
    if f < b:
        t = (f - a) / max(1e-6, b - a)
    elif f > c:
        t = (d - f) / max(1e-6, d - c)
    else:
        return 1.0
    return t * t * (3 - 2 * t)


def world_axis(arm, pb, turn):
    mw = arm.matrix_world
    if turn == "forward":
        return Vector((1.0, 0.0, 0.0))           # rig faces -Y: +X turns up toward -Y
    if turn == "side":
        return Vector((0.0, -1.0, 0.0))
    head = mw @ pb.head
    tail = mw @ pb.tail
    d = (tail - head).normalized()
    ax = d.cross(Vector((0.0, 0.0, 1.0)))
    return ax.normalized() if ax.length > 1e-6 else Vector((1.0, 0.0, 0.0))


def main():
    fixes = json.load(open(FIXES))
    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    act = arm.animation_data.action
    f0, f1 = int(act.frame_range[0]), int(act.frame_range[1])
    scene = bpy.context.scene
    to_arm = arm.matrix_world.to_3x3().normalized().inverted()
    for f in range(f0, f1 + 1):
        for fx in fixes:
            w = weight(f, *fx["frames"])
            if w == 0.0:
                continue
            scene.frame_set(f)
            pb = arm.pose.bones[P + fx["bone"]]
            sign = 1.0
            axis = (to_arm @ world_axis(arm, pb, fx["turn"])).normalized()
            R = Matrix.Rotation(sign * math.radians(fx["deg"]) * w, 4, axis)
            h = pb.head.copy()
            pb.matrix = Matrix.Translation(h) @ R @ Matrix.Translation(-h) @ pb.matrix
            bpy.context.view_layer.update()
            pb.keyframe_insert("rotation_quaternion", frame=f)
    print(f"bone_fix: {len(fixes)} corrections over frames {f0}-{f1}")
    for o in bpy.data.objects:
        o.select_set(o == arm or (o.type == "MESH" and o.parent == arm))
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(filepath=os.path.abspath(OUT_FBX), use_selection=True,
                             object_types={"ARMATURE", "MESH"}, add_leaf_bones=False, bake_anim=True,
                             bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
                             bake_anim_simplify_factor=0.0, bake_anim_force_startend_keying=True)
    print(f"bone_fix: exported {OUT_FBX}")


main()
