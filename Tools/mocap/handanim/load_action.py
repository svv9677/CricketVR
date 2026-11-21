"""Load a game bowling action (actions/<Name>.json, from Unity's BowlingClipExport) onto the Bowler rig
in Blender as an editable action. Used by build_scene.py; also runnable on its own for checking:

    Blender -b --python-exit-code 1 -P load_action.py -- actions/Steyn.json ../../../Assets/Resources/Models/Bowler.fbx --check

The JSON has every bone's world position and rotation in Unity's axes, per frame, and the bind pose.
Unity world -> Blender world is a fixed change of axes found from the bind pose itself (the same 65
joints in both): least-squares, reflection allowed (Unity is left-handed), snapped to the nearest
signed permutation. Each bone's pose is then the world rotation it has relative to its bind pose,
carried across: R_blender = C (R_unity R_unity_bind^-1) C^-1 R_blender_bind.
"""
import json
import math
import sys

import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector

P = "mixamorig5:"


def unity_quat_to_matrix(q):
    x, y, z, w = q
    return np.array([
        [1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
        [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
        [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])


def axes_from_bind(arm, data):
    """3x3 signed permutation C with blender_world ~= C @ unity_world (about the hips)."""
    names = data["bones"]
    rest = np.array(data["rest"])
    mw = arm.matrix_world
    U, B = [], []
    for i, n in enumerate(names):
        b = arm.data.bones.get(n)
        if b is None:
            continue
        U.append(rest[i, :3])
        B.append(np.array(mw @ b.head_local))
    U, B = np.array(U), np.array(B)
    iu = names.index(P + "Hips")
    hips_u = rest[iu, :3]
    hips_b = np.array(mw @ arm.data.bones[P + "Hips"].head_local)
    Uc, Bc = U - hips_u, B - hips_b
    M = Bc.T @ Uc
    u, _, vt = np.linalg.svd(M)
    C = u @ vt                                   # orthogonal, det may be -1 (handedness)
    C = np.round(C)                              # it is a change of axes: snap to +-1/0
    err = np.linalg.norm(Uc @ C.T - Bc, axis=1)
    return C, hips_u, hips_b, float(err.mean()), float(err.max())


def load(arm, path, name=None, check=False):
    data = json.load(open(path))
    C, hips_u, hips_b, e_mean, e_max = axes_from_bind(arm, data)
    names = data["bones"]
    rest = np.array(data["rest"])
    frames = np.array(data["frames"])              # (F, bones, 7)
    mw = arm.matrix_world
    mw_rot = mw.to_3x3().normalized()
    to_arm = mw_rot.inverted()
    bones = arm.data.bones
    idx = {n: i for i, n in enumerate(names)}
    rest_R = {n: unity_quat_to_matrix(rest[i, 3:]) for n, i in idx.items()}
    for pb in arm.pose.bones:
        pb.rotation_mode = "QUATERNION"

    act = bpy.data.actions.new(name or data["name"])
    if arm.animation_data is None:
        arm.animation_data_create()
    arm.animation_data.action = act
    scene = bpy.context.scene
    scene.render.fps = int(data["fps"])
    F = len(frames)
    prev = {}
    for f in range(F):
        posed = {}
        for b in bones:                            # parents before children
            n = b.name
            B0 = (mw_rot @ b.matrix_local.to_3x3())   # bind, world
            if n in idx:
                Ru = unity_quat_to_matrix(frames[f, idx[n], 3:])
                D = C @ (Ru @ rest_R[n].T) @ C.T
                Rw = Matrix(D.tolist()) @ B0
            else:
                Rw = None
            if b.parent is None:
                pu = frames[f, idx[n], :3]
                pw = Vector((C @ (pu - hips_u)).tolist()) + Vector(hips_b.tolist())
                rot = (to_arm @ Rw).normalized() if Rw is not None else b.matrix_local.to_3x3()
                Marm = Matrix.Translation(mw.inverted() @ pw) @ rot.to_4x4()
                basis = b.matrix_local.inverted() @ Marm
                posed[n] = Marm
            else:
                base = posed[b.parent.name] @ (b.parent.matrix_local.inverted() @ b.matrix_local)
                if Rw is not None:
                    rot = (to_arm @ Rw).normalized()
                    basis = (base.to_3x3().inverted() @ rot).to_4x4()
                else:
                    basis = Matrix.Identity(4)
                posed[n] = base @ basis
            pb = arm.pose.bones[n]
            q = basis.to_quaternion()
            if n in prev:
                q.make_compatible(prev[n])
            prev[n] = q
            pb.rotation_quaternion = q
            pb.keyframe_insert("rotation_quaternion", frame=f)
            if b.parent is None:
                pb.location = basis.to_translation()
                pb.keyframe_insert("location", frame=f)
    scene.frame_start, scene.frame_end = 0, F - 1
    if data.get("release_frame", -1) >= 0:
        m = scene.timeline_markers.get("release") or scene.timeline_markers.new("release")
        m.frame = int(data["release_frame"])
    report = f"load_action: {act.name} {F} frames @ {data['fps']}, release {data.get('release_frame')}; axes C={C.tolist()}, bind fit {e_mean * 100:.2f} cm mean / {e_max * 100:.2f} max"
    if check:
        report += "\n" + verify(arm, data, C, hips_u, hips_b)
    print(report)
    return act


def verify(arm, data, C, hips_u, hips_b):
    """Joint positions in Blender against Unity's, at a few frames (after the Blender pose)."""
    names = data["bones"]
    frames = np.array(data["frames"])
    scene = bpy.context.scene
    out = []
    for f in sorted(set([0, len(frames) // 2, data.get("release_frame", 0), len(frames) - 1])):
        scene.frame_set(f)
        errs = []
        for n in (P + "LeftHand", P + "RightHand", P + "LeftFoot", P + "RightFoot", P + "Head", P + "RightForeArm"):
            if n not in names:
                continue
            pu = frames[f, names.index(n), :3]
            want = C @ (pu - hips_u) + hips_b
            got = np.array(arm.matrix_world @ arm.pose.bones[n].head)
            errs.append(np.linalg.norm(want - got))
        out.append(f"frame {f}: joint error mean {np.mean(errs) * 100:.2f} cm, max {np.max(errs) * 100:.2f} cm")
    return "load_action check: " + "; ".join(out)


if __name__ == "__main__" and "--" in sys.argv:
    args = sys.argv[sys.argv.index("--") + 1:]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=args[1])
    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)
    load(arm, args[0], check="--check" in args)
