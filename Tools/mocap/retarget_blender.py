"""Joint track (.json from process_motion.py) -> the game rig's animation (FBX), run inside Blender.

    /Applications/Blender.app/Contents/MacOS/Blender -b --python-exit-code 1 -P retarget_blender.py -- \
        out/img9676.json ../../Assets/Resources/Models/Bowler.fbx out/Delivery.fbx [--render out/frames]

Three passes:
1. Directions. Each driven bone gets the rotation that carries its rest frame onto the captured
   one. A frame is a primary direction (the bone's own, e.g. shoulder->elbow) and a secondary that
   fixes the twist (a limb's hinge axis; hip-to-hip or shoulder-to-shoulder for the torso). Both
   are measured the same way on the rig's rest pose, so only directions transfer, not the
   subject's proportions. Spine and neck are blended between the pelvis, chest and head.
2. Ground. The body is moved up or down so each planted foot's ankle is at standing height,
   the correction running smoothly between footfalls.
3. Feet. Each footfall is pinned where it landed for the whole contact, by two-bone IK on the
   leg in the plane the knee already bends in. Without it every foot slid.
"""
import json
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

ARGS = sys.argv[sys.argv.index("--") + 1:]
TRACK, RIG_FBX, OUT_FBX = ARGS[0], ARGS[1], ARGS[2]
RENDER_DIR = ARGS[ARGS.index("--render") + 1] if "--render" in ARGS else None
P = "mixamorig5:"
SIDES = (("l", "Left"), ("r", "Right"))

# Frames either side of a footfall over which its pin eases in and out.
PLANT_BLEND = 2


def V(a):
    return Vector((a[0], a[1], a[2]))


def frame(primary, secondary):
    """Orthonormal 3x3 with columns (primary, secondary made orthogonal, primary x secondary)."""
    x = primary.normalized()
    y = secondary - x * secondary.dot(x)
    if y.length < 1e-6:
        y = x.orthogonal()
    y.normalize()
    return Matrix((x, y, x.cross(y))).transposed()


def align(p_rest, s_rest, p, s):
    """The rotation taking the rest frame onto the captured one."""
    return frame(p, s) @ frame(p_rest, s_rest).transposed()


class Rig:
    def __init__(self, path):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=path)
        self.arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
        for a in list(bpy.data.actions):
            bpy.data.actions.remove(a)
        self.arm.animation_data_clear()
        self.mw = self.arm.matrix_world.copy()
        self.mw_rot = self.mw.to_3x3().normalized()
        self.to_arm = self.mw_rot.transposed()
        self.bones = self.arm.data.bones
        for pb in self.arm.pose.bones:
            pb.rotation_mode = "QUATERNION"
        self._rest()

    def head(self, n):
        return self.mw @ self.bones[P + n].head_local

    def tail(self, n):
        return self.mw @ self.bones[P + n].tail_local

    def _rest(self):
        """Rest-pose frames (world), built exactly as the captured ones are."""
        h = self.head
        mid_hip = (h("LeftUpLeg") + h("RightUpLeg")) / 2
        mid_sh = (h("LeftArm") + h("RightArm")) / 2
        fwd, back = Vector((0, -1, 0)), Vector((0, 1, 0))     # the rig faces -Y
        rest = {
            "pelvis": (mid_sh - mid_hip, h("LeftUpLeg") - h("RightUpLeg")),
            "chest": (mid_sh - mid_hip, h("LeftArm") - h("RightArm")),
            "head": (self.tail("Head") - h("Head"), Vector((1, 0, 0))),
        }
        for s, S in SIDES:
            up = h(f"{S}ForeArm") - h(f"{S}Arm")
            fo = h(f"{S}Hand") - h(f"{S}ForeArm")
            rest[f"{s}_upperarm"] = (up, up.cross(fwd))
            rest[f"{s}_forearm"] = (fo, up.cross(fwd))
            hand_p = (h(f"{S}HandIndex1") + h(f"{S}HandPinky1")) / 2 - h(f"{S}Hand")
            rest[f"{s}_hand"] = (hand_p, h(f"{S}HandIndex1") - h(f"{S}HandPinky1"))
            th = h(f"{S}Leg") - h(f"{S}UpLeg")
            sh = h(f"{S}Foot") - h(f"{S}Leg")
            rest[f"{s}_thigh"] = (th, th.cross(back))
            rest[f"{s}_shin"] = (sh, th.cross(back))
            rest[f"{s}_foot"] = (self.tail(f"{S}ToeBase") - h(f"{S}Foot"), th.cross(back))
        self.rest = rest
        self.hips_off = h("Hips") - mid_hip

    def pose(self, want, hips_world):
        """Armature-space posed matrices and pose-basis matrices for world bone rotations `want`."""
        posed, basis = {}, {}
        for b in self.bones:                     # parents come before children
            name = b.name[len(P):]
            if b.parent is None:
                rot = self.to_arm @ want["Hips"] @ self.mw_rot @ b.matrix_local.to_3x3()
                M = Matrix.Translation(self.mw.inverted() @ hips_world) @ rot.to_4x4()
                basis[b.name] = b.matrix_local.inverted() @ M
                posed[b.name] = M
                continue
            base = posed[b.parent.name] @ (b.parent.matrix_local.inverted() @ b.matrix_local)
            if name in want:
                rot = self.to_arm @ want[name] @ self.mw_rot @ b.matrix_local.to_3x3()
                B = (base.to_3x3().inverted() @ rot).to_4x4()
            else:
                B = Matrix.Identity(4)
            basis[b.name] = B
            posed[b.name] = base @ B
        return posed, basis

    def world_head(self, posed, n):
        return self.mw @ posed[P + n].to_translation()

    def world_tail(self, posed, n):
        return self.mw @ (posed[P + n] @ Vector((0, self.bones[P + n].length, 0)))


def capture(g, prev_n):
    """This frame's captured (primary, secondary) per segment, from the joint track."""
    mid_hip = (g["l_hip"] + g["r_hip"]) / 2
    mid_sh = (g["l_sh"] + g["r_sh"]) / 2
    up_t = mid_sh - mid_hip
    lat_hip = g["l_hip"] - g["r_hip"]
    cap = {
        "pelvis": (up_t, lat_hip),
        "chest": (up_t, g["l_sh"] - g["r_sh"]),
        "head": ((g["l_ear"] + g["r_ear"]) / 2 - mid_sh, g["l_ear"] - g["r_ear"]),
    }
    for s, _ in SIDES:
        up = g[f"{s}_el"] - g[f"{s}_sh"]
        fo = g[f"{s}_wr"] - g[f"{s}_el"]
        n = up.cross(fo)
        bend = n.length / (up.length * fo.length + 1e-9)          # sin of the elbow angle
        if bend > 0.2 or s not in prev_n:
            n_arm = n if bend > 0.05 else up.cross(cap["chest"][1].cross(up_t))
        else:
            n_arm = prev_n[s]                                       # straight arm: keep last hinge
        n_arm = n_arm.normalized()
        prev_n[s] = n_arm
        cap[f"{s}_upperarm"] = (up, n_arm)
        cap[f"{s}_forearm"] = (fo, n_arm)
        hand_p = (g[f"{s}_index"] + g[f"{s}_pinky"]) / 2 - g[f"{s}_wr"]
        # MediaPipe's hand points are its noisiest: the hand leans 40% toward the forearm's line.
        cap[f"{s}_hand"] = (hand_p.lerp(fo.normalized() * hand_p.length, 0.4), g[f"{s}_index"] - g[f"{s}_pinky"])
        th = g[f"{s}_knee"] - g[f"{s}_hip"]
        sh = g[f"{s}_ank"] - g[f"{s}_knee"]
        nk = th.cross(sh)
        kb = min(1.0, nk.length / (th.length * sh.length + 1e-9) / 0.3)
        n_leg = (nk.normalized() * kb + lat_hip.normalized() * (1 - kb)) if nk.length > 1e-9 else lat_hip
        cap[f"{s}_thigh"] = (th, n_leg)
        cap[f"{s}_shin"] = (sh, n_leg)
        cap[f"{s}_foot"] = (g[f"{s}_toe"] - g[f"{s}_ank"], n_leg)
    return cap, mid_hip


def want_from(rig, cap):
    A = {k: align(*rig.rest[k], *cap[k]) for k in cap}
    q = {k: m.to_quaternion() for k, m in A.items()}

    def sl(a, b, t):
        return q[a].slerp(q[b], t).to_matrix()

    want = {"Hips": A["pelvis"], "Spine": sl("pelvis", "chest", 0.35), "Spine1": sl("pelvis", "chest", 0.7),
            "Spine2": A["chest"], "LeftShoulder": A["chest"], "RightShoulder": A["chest"],
            "Neck": sl("chest", "head", 0.5), "Head": A["head"]}
    for s, S in SIDES:
        for seg, bone in (("upperarm", "Arm"), ("forearm", "ForeArm"), ("hand", "Hand"),
                          ("thigh", "UpLeg"), ("shin", "Leg"), ("foot", "Foot")):
            want[f"{S}{bone}"] = A[f"{s}_{seg}"]
    return want, A["pelvis"]


def two_bone(H, K, A, T):
    """Knee and ankle placed so the ankle reaches T, the knee bending the way it already does."""
    l1, l2 = (K - H).length, (A - K).length
    d_vec = T - H
    d = min(max(d_vec.length, abs(l1 - l2) + 1e-4), l1 + l2 - 1e-4)
    u = d_vec.normalized()
    v = (K - H) - u * (K - H).dot(u)
    v = v.normalized() if v.length > 1e-6 else (A - K).cross(u).cross(u).normalized()
    a = (l1 * l1 - l2 * l2 + d * d) / (2 * d)
    k_new = H + u * a + v * math.sqrt(max(l1 * l1 - a * a, 0.0))
    return k_new, H + u * d


def main():
    track = json.load(open(TRACK))
    fps, F = track["fps"], track["frames"]
    J = {k: [V(p) for p in v] for k, v in track["joints"].items()}
    rig = Rig(RIG_FBX)

    # Pass 1: directions.
    frames, prev_n = [], {}
    for f in range(F):
        cap, mid_hip = capture({k: v[f] for k, v in J.items()}, prev_n)
        want, pelvis = want_from(rig, cap)
        frames.append({"cap": cap, "want": want, "hips": mid_hip + pelvis @ rig.hips_off})

    # Pass 2: ground, from the footfalls (process_motion.py: given, or found in the 2D track).
    # A planted foot's ankle sits at the rig's standing ankle height; the body is moved up or down
    # to put it there, and between footfalls the correction runs linearly (smoothed) to the next.
    contacts = [c for c in track.get("contacts", []) if c["start"] < F]
    side_name = dict(SIDES)
    ankle_h = rig.head("LeftFoot").z - min(rig.head("LeftToeBase").z, rig.tail("LeftToeBase").z)
    ankles = {S: [] for _, S in SIDES}
    for fr in frames:
        posed, _ = rig.pose(fr["want"], fr["hips"])
        for _, S in SIDES:
            ankles[S].append(rig.world_head(posed, f"{S}Foot"))
    dz = [None] * F
    for c in contacts:
        S = side_name[c["side"]]
        for f in range(c["start"], min(c["end"], F - 1) + 1):
            v = ankle_h - ankles[S][f].z
            dz[f] = v if dz[f] is None else (dz[f] + v) / 2
    known = [f for f in range(F) if dz[f] is not None]
    if not known:
        lows = sorted(a.z for S in ankles for a in ankles[S])
        known, dz = [0], [ankle_h - lows[len(lows) // 5]] + [None] * (F - 1)
    for f in range(F):
        if dz[f] is None:
            lo = max((k for k in known if k < f), default=None)
            hi = min((k for k in known if k > f), default=None)
            if lo is None or hi is None:
                dz[f] = dz[lo if hi is None else hi]
            else:
                t = (f - lo) / (hi - lo)
                dz[f] = dz[lo] * (1 - t) + dz[hi] * t
    dz = [sum(dz[max(0, f - 2):f + 3]) / len(dz[max(0, f - 2):f + 3]) for f in range(F)]
    for fr, d in zip(frames, dz):
        fr["hips"] = fr["hips"] + Vector((0, 0, d))
    for _, S in SIDES:
        ankles[S] = [a + Vector((0, 0, d)) for a, d in zip(ankles[S], dz)]
    print(f"ground: correction {min(dz):.3f}..{max(dz):.3f} m over {len(contacts)} footfalls")

    # Pass 3: feet. Each footfall's ankle is pinned where it landed, for the whole contact,
    # easing in and out over PLANT_BLEND frames either side.
    pins = {S: ([None] * F, [0.0] * F) for _, S in SIDES}
    for c in contacts:
        S = side_name[c["side"]]
        s0, s1 = c["start"], min(c["end"], F - 1)
        pin = ankles[S][s0].copy()
        pin.z = ankle_h
        target, weight = pins[S]
        for f in range(max(0, s0 - PLANT_BLEND), min(F, s1 + PLANT_BLEND + 1)):
            w = 1.0 if s0 <= f <= s1 else 1.0 - (min(abs(f - s0), abs(f - s1)) / (PLANT_BLEND + 1))
            if w > weight[f]:
                weight[f], target[f] = w, pin
    print("feet: " + ", ".join(f"{c['side']}:{c['start']}-{c['end']}" for c in contacts))

    for f, fr in enumerate(frames):
        posed, _ = rig.pose(fr["want"], fr["hips"])
        for s, S in SIDES:
            target, weight = pins[S]
            if target[f] is None:
                continue
            H = rig.world_head(posed, f"{S}UpLeg")
            K = rig.world_head(posed, f"{S}Leg")
            A = ankles[S][f]
            T = A.lerp(target[f], weight[f])
            k_new, a_new = two_bone(H, K, A, T)
            n = (k_new - H).cross(a_new - k_new)
            n = n if n.length > 1e-6 else fr["cap"][f"{s}_thigh"][1]
            fr["want"][f"{S}UpLeg"] = align(*rig.rest[f"{s}_thigh"], k_new - H, n)
            fr["want"][f"{S}Leg"] = align(*rig.rest[f"{s}_shin"], a_new - k_new, n)

    # Keys.
    scene = bpy.context.scene
    scene.render.fps = round(fps)
    scene.render.fps_base = round(fps) / fps
    scene.frame_start, scene.frame_end = 0, F - 1
    pose = rig.arm.pose.bones
    prev_q = {}
    for f, fr in enumerate(frames):
        _, basis = rig.pose(fr["want"], fr["hips"])
        for name, B in basis.items():
            pb = pose[name]
            q = B.to_quaternion()
            if name in prev_q:
                q.make_compatible(prev_q[name])
            prev_q[name] = q
            pb.rotation_quaternion = q
            pb.keyframe_insert("rotation_quaternion", frame=f)
            if rig.bones[name].parent is None:
                pb.location = B.to_translation()
                pb.keyframe_insert("location", frame=f)
    rig.arm.animation_data.action.name = os.path.splitext(os.path.basename(OUT_FBX))[0]

    if RENDER_DIR:
        render(rig.arm, F, track["release_frame"])

    # With the skinned meshes: they carry the bind pose. An armature alone has none, and a reader
    # (Blender, measured) takes whatever frame was current as its rest pose instead.
    for o in bpy.data.objects:
        o.select_set(o == rig.arm or (o.type == "MESH" and o.parent == rig.arm))
    bpy.context.view_layer.objects.active = rig.arm
    bpy.ops.export_scene.fbx(filepath=OUT_FBX, use_selection=True, object_types={"ARMATURE", "MESH"},
                             add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=False,
                             bake_anim_use_nla_strips=False, bake_anim_simplify_factor=0.0,
                             bake_anim_force_startend_keying=True)
    print(f"exported {OUT_FBX}: {F} frames @ {fps:.2f}")


def render(arm, F, release):
    """Workbench stills from the side and from behind, every frame, for checking by eye."""
    os.makedirs(RENDER_DIR, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x, scene.render.resolution_y = 360, 480
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    bpy.ops.mesh.primitive_plane_add(size=40, location=(0, -6, 0))
    cam_data = bpy.data.cameras.new("cam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = 3.2
    cam = bpy.data.objects.new("cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    hips = arm.pose.bones[P + "Hips"]
    for f in range(F):
        scene.frame_set(f)
        c = arm.matrix_world @ hips.head
        for view, offset, rot in (("side", Vector((8, 0, 0)), (math.pi / 2, 0, math.pi / 2)),
                                  ("back", Vector((0, 8, 0)), (math.pi / 2, 0, math.pi))):
            cam.location = Vector((c.x, c.y, 0.95)) + offset
            cam.rotation_euler = rot
            scene.render.filepath = os.path.join(RENDER_DIR, f"{view}_{f:03d}.png")
            bpy.ops.render.render(write_still=True)


main()
