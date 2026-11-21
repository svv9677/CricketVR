"""Carry a captured action on past its last frame, so a blend out of it has something to blend from.

Unity drops a non-looping source state the moment its clip runs out mid-transition: the pose
snapped onto the next state in one frame (bowling arm twisting 98 deg in a frame, MocapAnderson
into Follow Through, measured in play 2026-11-13). Each bone keeps turning the way it was, slowing
to rest (its last frame-to-frame turn, decaying each frame); the hips keep travelling the same way,
slowing too. Run inside Blender:

    Blender -b out/anim/x.blend -P fusion/extend_end.py -- 12 out/anim/x_ext.fbx out/anim/x_ext.blend
"""
import os
import sys

import bpy
from mathutils import Quaternion

ARGS = sys.argv[sys.argv.index("--") + 1:]
EXTRA, OUT_FBX, OUT_BLEND = int(ARGS[0]), ARGS[1], ARGS[2]
DECAY = 0.8                 # each extra frame keeps this much of the last frame's motion


def main():
    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    act = arm.animation_data.action
    f0, f1 = (int(v) for v in act.frame_range)
    sc = bpy.context.scene
    sc.frame_set(f1 - 1)
    prev = {pb.name: pb.rotation_quaternion.copy() for pb in arm.pose.bones}
    prev_loc = arm.pose.bones[0].location.copy()
    sc.frame_set(f1)
    last = {pb.name: pb.rotation_quaternion.copy() for pb in arm.pose.bones}
    hips = arm.pose.bones[0]
    last_loc = hips.location.copy()
    step = {n: prev[n].rotation_difference(last[n]) for n in last}
    move = last_loc - prev_loc
    cur, loc, k = dict(last), last_loc.copy(), DECAY
    for i in range(1, EXTRA + 1):
        f = f1 + i
        for pb in arm.pose.bones:
            s = step[pb.name]
            ang = s.angle * k
            q = cur[pb.name].copy()
            if ang > 1e-6:
                q = cur[pb.name] @ Quaternion(s.to_axis_angle()[0], ang)
            cur[pb.name] = q
            pb.rotation_quaternion = q
            pb.keyframe_insert("rotation_quaternion", frame=f)
        loc = loc + move * k
        hips.location = loc
        hips.keyframe_insert("location", frame=f)
        k *= DECAY
    sc.frame_end = f1 + EXTRA
    print(f"extend_end: frames {f1 + 1}-{f1 + EXTRA} added (motion decaying x{DECAY} per frame)")
    for o in bpy.data.objects:
        o.select_set(o == arm or (o.type == "MESH" and o.parent == arm))
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(filepath=os.path.abspath(OUT_FBX), use_selection=True,
                             object_types={"ARMATURE", "MESH"}, add_leaf_bones=False, bake_anim=True,
                             bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
                             bake_anim_simplify_factor=0.0, bake_anim_force_startend_keying=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(OUT_BLEND), check_existing=False)
    print(f"extend_end: exported {OUT_FBX}, saved {OUT_BLEND}")


main()
