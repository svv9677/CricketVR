"""Joint track (.json from process_motion.py) -> the game rig's animation (FBX), run inside Blender.

    /Applications/Blender.app/Contents/MacOS/Blender -b --python-exit-code 1 -P retarget_blender.py -- \
        out/img9676.json ../../Assets/Resources/Models/Bowler.fbx out/Delivery.fbx [--render out/frames]

Bones are held as local pose rotations (relative to the parent and the rig's T-pose), so limits
and smoothing act on what a joint actually does. Passes:
1. Directions: per bone, the rotation carrying its rest frame (bone direction + hinge axis, or
   hip-to-hip / shoulder-to-shoulder) onto the captured one; only directions transfer, not
   proportions. Spine and neck blend pelvis, chest and head. Hands follow the forearm: taking their
   roll from MediaPipe's hand points corkscrewed it (Unity Forearm Twist jitter 0.61, 2x its range).
2. Limits: elbow, knee and ankle are hinges (no hyperextension, no twist); the rest swing cones
   and twist ranges (LIMITS).
3. Smoothing in rotation space per bone (light on the bowling arm, so it still whips over at the
   release), then the limits again.
4. Ground: the body moves up/down so each planted ankle is at standing height.
5. Feet: each footfall pinned where it landed (two-bone IK, limits again); a planted foot lies flat,
   otherwise the ankle takes the captured pitch, clamped.
"""
import json
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector

ARGS = sys.argv[sys.argv.index("--") + 1:]
TRACK, RIG_FBX, OUT_FBX = ARGS[0], ARGS[1], ARGS[2]
RENDER_DIR = ARGS[ARGS.index("--render") + 1] if "--render" in ARGS else None
P = "mixamorig5:"
SIDES = (("l", "Left"), ("r", "Right"))

# Frames either side of a footfall over which its pin eases in and out.
PLANT_BLEND = 2

# Per bone: ("hinge", axis, lo, hi) in degrees about the joint's hinge, positive = flexion (for the
# ankle, toes down); ("cone", swing, twist) = largest swing off the T-pose and twist about the bone;
# ("lock",) = the T-pose relative to its parent. Hips (the root) is free.
LIMITS = {
    "Spine": ("cone", 30, 30), "Spine1": ("cone", 30, 30), "Spine2": ("cone", 30, 30),
    "Neck": ("cone", 35, 40), "Head": ("cone", 40, 50),
    "LeftShoulder": ("lock",), "RightShoulder": ("lock",),
    "LeftArm": ("cone", 170, 75), "RightArm": ("cone", 170, 75),
    "LeftForeArm": ("hinge", "l_elbow", -5, 150), "RightForeArm": ("hinge", "r_elbow", -5, 150),
    "LeftHand": ("lock",), "RightHand": ("lock",),
    "LeftUpLeg": ("cone", 110, 32), "RightUpLeg": ("cone", 110, 32),
    "LeftLeg": ("hinge", "l_knee", 0, 150), "RightLeg": ("hinge", "r_knee", 0, 150),
    "LeftFoot": ("hinge", "l_ankle", -25, 40), "RightFoot": ("hinge", "r_ankle", -25, 40),
}
# Savitzky-Golay window (frames at 30 fps) and order per bone; the bowling arm is set in main().
SMOOTH_DEFAULT, SMOOTH_ARM, SMOOTH_FOOT = (9, 2), (5, 3), (7, 2)


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


def wrap(a):
    return (a + math.pi) % (2 * math.pi) - math.pi


def hinge_angle(q, axis):
    """Signed angle of q's rotation about `axis` (the rest, off-axis part dropped)."""
    return wrap(2 * math.atan2(q.x * axis.x + q.y * axis.y + q.z * axis.z, q.w))


def clamp_rotation(q, limit, axes):
    """q (a bone's local pose rotation) held inside its limit."""
    if limit[0] == "lock":
        return Quaternion()
    if limit[0] == "hinge":
        axis = axes[limit[1]]
        a = max(math.radians(limit[2]), min(math.radians(limit[3]), hinge_angle(q, axis)))
        return Quaternion(axis, a)
    swing_max, twist_max = math.radians(limit[1]), math.radians(limit[2])
    tw = Quaternion((q.w, 0.0, q.y, 0.0))            # twist about the bone's own axis (local Y)
    tw = tw.normalized() if tw.magnitude > 1e-9 else Quaternion()
    swing = q @ tw.inverted()
    t = max(-twist_max, min(twist_max, wrap(2 * math.atan2(tw.y, tw.w))))
    s = wrap(swing.angle)
    if abs(s) > swing_max:
        swing = Quaternion(swing.axis, math.copysign(swing_max, s))
    return swing @ Quaternion((0.0, 1.0, 0.0), t)


def savgol(x, window, order):
    """Savitzky-Golay along axis 0 (numpy only: Blender has no scipy); the ends get a one-sided fit."""
    F = len(x)
    window = min(window, F if F % 2 else F - 1)
    if window <= order + 1:
        return x.copy()
    k = window // 2
    out = np.empty_like(x)
    for i in range(F):
        s = min(max(0, i - k), F - window)
        t = np.arange(s, s + window) - i
        A = np.vander(t, order + 1, increasing=True)
        coef = np.linalg.lstsq(A, x[s:s + window], rcond=None)[0]
        out[i] = coef[0]
    return out


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

    def to_local(self, n, world_dir):
        """A world direction in bone n's own rest axes."""
        return ((self.mw_rot @ self.bones[P + n].matrix_local.to_3x3()).transposed() @ world_dir).normalized()

    def _rest(self):
        """Rest-pose frames (world), built exactly as the captured ones are, and the hinge axes."""
        h = self.head
        mid_hip = (h("LeftUpLeg") + h("RightUpLeg")) / 2
        mid_sh = (h("LeftArm") + h("RightArm")) / 2
        fwd, back = Vector((0, -1, 0)), Vector((0, 1, 0))     # the rig faces -Y
        rest = {
            "pelvis": (mid_sh - mid_hip, h("LeftUpLeg") - h("RightUpLeg")),
            "chest": (mid_sh - mid_hip, h("LeftArm") - h("RightArm")),
            "head": (self.tail("Head") - h("Head"), Vector((1, 0, 0))),
        }
        axes = {}
        for s, S in SIDES:
            up = h(f"{S}ForeArm") - h(f"{S}Arm")
            fo = h(f"{S}Hand") - h(f"{S}ForeArm")
            rest[f"{s}_upperarm"] = (up, up.cross(fwd))
            rest[f"{s}_forearm"] = (fo, up.cross(fwd))
            th = h(f"{S}Leg") - h(f"{S}UpLeg")
            sh = h(f"{S}Foot") - h(f"{S}Leg")
            foot = self.tail(f"{S}ToeBase") - h(f"{S}Foot")
            rest[f"{s}_thigh"] = (th, th.cross(back))
            rest[f"{s}_shin"] = (sh, th.cross(back))
            # Flexion is positive about these: forearm toward the chest, shin back, toes down.
            axes[f"{s}_elbow"] = self.to_local(f"{S}ForeArm", up.cross(fwd))
            axes[f"{s}_knee"] = self.to_local(f"{S}Leg", th.cross(back))
            axes[f"{s}_ankle"] = self.to_local(f"{S}Foot", th.cross(back))
            self.rest_ankle = math.atan2(sh.cross(foot).dot(th.cross(back).normalized()), sh.dot(foot))
            self.rest_foot_dir = foot.normalized()
        self.rest, self.axes = rest, axes
        self.hips_off = h("Hips") - mid_hip

    def local_from_world(self, want, hips_world):
        """Local pose rotations for world bone rotations `want` (bones not in it: their rest)."""
        local, posed = {}, {}
        for b in self.bones:                     # parents come before children
            name = b.name[len(P):]
            if b.parent is None:
                rot = self.to_arm @ want["Hips"] @ self.mw_rot @ b.matrix_local.to_3x3()
                posed[b.name] = Matrix.Translation(self.mw.inverted() @ hips_world) @ rot.to_4x4()
                local[name] = (b.matrix_local.to_3x3().inverted() @ rot).to_quaternion()
                continue
            base = posed[b.parent.name] @ (b.parent.matrix_local.inverted() @ b.matrix_local)
            if name in want:
                rot = self.to_arm @ want[name] @ self.mw_rot @ b.matrix_local.to_3x3()
                q = (base.to_3x3().inverted() @ rot).to_quaternion()
            else:
                q = Quaternion()
            local[name] = q
            posed[b.name] = base @ q.to_matrix().to_4x4()
        return local

    def posed(self, local, hips_world):
        """Armature-space posed matrices for local rotations and a world hips position."""
        out = {}
        for b in self.bones:
            q = local.get(b.name[len(P):], Quaternion())
            if b.parent is None:
                rot = b.matrix_local.to_3x3() @ q.to_matrix()
                out[b.name] = Matrix.Translation(self.mw.inverted() @ hips_world) @ rot.to_4x4()
            else:
                out[b.name] = (out[b.parent.name] @ (b.parent.matrix_local.inverted() @ b.matrix_local)
                               @ q.to_matrix().to_4x4())
        return out

    def world_head(self, posed, n):
        return self.mw @ posed[P + n].to_translation()

    def world_rot(self, posed, n):
        return self.mw_rot @ posed[P + n].to_3x3()

    def world_local_for(self, posed, n, world_rot):
        """The local rotation that gives bone n this world rotation under its posed parent."""
        b = self.bones[P + n]
        base = posed[b.parent.name] @ (b.parent.matrix_local.inverted() @ b.matrix_local)
        rot = self.to_arm @ world_rot @ self.mw_rot @ b.matrix_local.to_3x3()
        return (base.to_3x3().inverted() @ rot).to_quaternion()


def capture(g, prev_n):
    """This frame's captured (primary, secondary) per segment, and the ankles' captured pitch."""
    mid_hip = (g["l_hip"] + g["r_hip"]) / 2
    mid_sh = (g["l_sh"] + g["r_sh"]) / 2
    up_t = mid_sh - mid_hip
    lat_hip = g["l_hip"] - g["r_hip"]
    cap = {
        "pelvis": (up_t, lat_hip),
        "chest": (up_t, g["l_sh"] - g["r_sh"]),
        "head": ((g["l_ear"] + g["r_ear"]) / 2 - mid_sh, g["l_ear"] - g["r_ear"]),
    }
    ankle = {}
    for s, _ in SIDES:
        up = g[f"{s}_el"] - g[f"{s}_sh"]
        fo = g[f"{s}_wr"] - g[f"{s}_el"]
        n = up.cross(fo)
        bend = min(1.0, n.length / (up.length * fo.length + 1e-9) / 0.3)    # 1 once bent ~17 deg
        fallback = prev_n.get(s) or up.cross(cap["chest"][1].cross(up_t)).normalized()
        # A nearly straight arm's hinge is noise: lean on the last one, the more so the straighter.
        n_arm = (n.normalized() * bend + fallback * (1 - bend)) if n.length > 1e-9 else fallback
        n_arm = n_arm.normalized()
        prev_n[s] = n_arm
        cap[f"{s}_upperarm"] = (up, n_arm)
        cap[f"{s}_forearm"] = (fo, n_arm)
        th = g[f"{s}_knee"] - g[f"{s}_hip"]
        sh = g[f"{s}_ank"] - g[f"{s}_knee"]
        nk = th.cross(sh)
        kb = min(1.0, nk.length / (th.length * sh.length + 1e-9) / 0.3)
        n_leg = (nk.normalized() * kb + lat_hip.normalized() * (1 - kb)) if nk.length > 1e-9 else lat_hip
        cap[f"{s}_thigh"] = (th, n_leg)
        cap[f"{s}_shin"] = (sh, n_leg)
        foot = g[f"{s}_toe"] - g[f"{s}_ank"]
        ankle[s] = math.atan2(sh.cross(foot).dot(n_leg.normalized()), sh.dot(foot))
    return cap, mid_hip, ankle


def want_from(rig, cap):
    A = {k: align(*rig.rest[k], *cap[k]) for k in cap}
    q = {k: m.to_quaternion() for k, m in A.items()}

    def sl(a, b, t):
        return q[a].slerp(q[b], t).to_matrix()

    want = {"Hips": A["pelvis"], "Spine": sl("pelvis", "chest", 0.35), "Spine1": sl("pelvis", "chest", 0.7),
            "Spine2": A["chest"], "Neck": sl("chest", "head", 0.5), "Head": A["head"]}
    for s, S in SIDES:
        for seg, bone in (("upperarm", "Arm"), ("forearm", "ForeArm"), ("thigh", "UpLeg"), ("shin", "Leg")):
            want[f"{S}{bone}"] = A[f"{s}_{seg}"]
    return want, A["pelvis"]


def limit_all(rig, local):
    for name, lim in LIMITS.items():
        if name in local:
            local[name] = clamp_rotation(local[name], lim, rig.axes)


def smooth_locals(frames, bowling_side):
    """Rotation-space smoothing per bone (sign-aligned quaternion components, renormalised)."""
    arm = {f"{bowling_side}{b}" for b in ("Shoulder", "Arm", "ForeArm", "Hand")}
    for name in frames[0]["local"]:
        qs = [fr["local"][name].copy() for fr in frames]
        for i in range(1, len(qs)):
            qs[i].make_compatible(qs[i - 1])
        w, p = SMOOTH_ARM if name in arm else SMOOTH_DEFAULT
        sm = savgol(np.array([tuple(q) for q in qs]), w, p)
        for fr, row in zip(frames, sm):
            fr["local"][name] = Quaternion(row).normalized()


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


def ground(rig, frames, contacts):
    """Move the body up or down so each planted ankle is at standing height (linear between)."""
    F = len(frames)
    side_name = dict(SIDES)
    ankle_h = rig.head("LeftFoot").z - min(rig.head("LeftToeBase").z, rig.tail("LeftToeBase").z)
    ankles = {S: [] for _, S in SIDES}
    for fr in frames:
        posed = rig.posed(fr["local"], fr["hips"])
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
    dz = np.interp(np.arange(F), known, [dz[k] for k in known])     # held flat past the ends
    dz = [float(np.mean(dz[max(0, f - 2):f + 3])) for f in range(F)]
    for fr, d in zip(frames, dz):
        fr["hips"] = fr["hips"] + Vector((0, 0, d))
    for _, S in SIDES:
        ankles[S] = [a + Vector((0, 0, d)) for a, d in zip(ankles[S], dz)]
    print(f"ground: correction {min(dz):.3f}..{max(dz):.3f} m over {len(contacts)} footfalls")
    return ankles, ankle_h


def pins_for(contacts, ankles, ankle_h, F):
    """Per side and frame: the pinned ankle position and how much of it applies."""
    side_name = dict(SIDES)
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
    return pins


def pin_feet(rig, frames, pins):
    for f, fr in enumerate(frames):
        for s, S in SIDES:
            target, weight = pins[S]
            if target[f] is None:
                continue
            posed = rig.posed(fr["local"], fr["hips"])
            H = rig.world_head(posed, f"{S}UpLeg")
            K = rig.world_head(posed, f"{S}Leg")
            A = rig.world_head(posed, f"{S}Foot")
            k_new, a_new = two_bone(H, K, A, A.lerp(target[f], weight[f]))
            n = (k_new - H).cross(a_new - k_new)
            n = n if n.length > 1e-6 else fr["cap"][f"{s}_thigh"][1]
            thigh = align(*rig.rest[f"{s}_thigh"], k_new - H, n)
            fr["local"][f"{S}UpLeg"] = clamp_rotation(rig.world_local_for(posed, f"{S}UpLeg", thigh),
                                                      LIMITS[f"{S}UpLeg"], rig.axes)
            posed = rig.posed(fr["local"], fr["hips"])
            shin = align(*rig.rest[f"{s}_shin"], a_new - k_new, n)
            fr["local"][f"{S}Leg"] = clamp_rotation(rig.world_local_for(posed, f"{S}Leg", shin),
                                                    LIMITS[f"{S}Leg"], rig.axes)


def set_ankles(rig, frames, pins):
    """Ankle flexion: a planted foot lies flat; otherwise the captured pitch. Clamped, smoothed."""
    for s, S in SIDES:
        axis = rig.axes[f"{s}_ankle"]
        lo, hi = (math.radians(v) for v in LIMITS[f"{S}Foot"][2:4])
        weight, angles = pins[S][1], []
        for f, fr in enumerate(frames):
            captured = fr["ankle"][s] - rig.rest_ankle
            a = captured
            if weight[f] > 0:
                posed = rig.posed(fr["local"], fr["hips"])
                flat = flat_ankle(rig, posed, S, axis, lo, hi)
                a = captured * (1 - weight[f]) + flat * weight[f]
            angles.append(max(lo, min(hi, a)))
        sm = savgol(np.array(angles)[:, None], *SMOOTH_FOOT)[:, 0]
        for fr, a in zip(frames, sm):
            fr["local"][f"{S}Foot"] = Quaternion(axis, float(max(lo, min(hi, a))))
            fr["local"][f"{S}ToeBase"] = Quaternion()


def flat_ankle(rig, posed, S, axis, lo, hi):
    """The ankle flexion that gives the foot its standing pitch (sole on the ground)."""
    b = rig.bones[P + f"{S}Foot"]
    base = posed[b.parent.name] @ (b.parent.matrix_local.inverted() @ b.matrix_local)
    best, err = 0.0, 9.0
    for i in range(int(math.degrees(hi - lo)) + 1):
        a = lo + math.radians(i)
        rot = rig.mw_rot @ (base.to_3x3() @ Quaternion(axis, a).to_matrix())
        d = (rot @ Vector((0, 1, 0))).normalized()           # bone axis: ankle toward the toes
        e = abs(d.z - rig.rest_foot_dir.z)
        if e < err:
            best, err = a, e
    return best


def solve(rig, track):
    J = {k: [V(p) for p in v] for k, v in track["joints"].items()}
    F = track["frames"]
    frames, prev_n = [], {}
    for f in range(F):
        cap, mid_hip, ankle = capture({k: v[f] for k, v in J.items()}, prev_n)
        want, pelvis = want_from(rig, cap)
        hips = mid_hip + pelvis @ rig.hips_off
        frames.append({"cap": cap, "ankle": ankle, "hips": hips, "local": rig.local_from_world(want, hips)})
    for fr in frames:
        limit_all(rig, fr["local"])
    smooth_locals(frames, "Right" if track.get("arm", "right") == "right" else "Left")
    for fr in frames:
        limit_all(rig, fr["local"])
    contacts = [c for c in track.get("contacts", []) if c["start"] < F]
    ankles, ankle_h = ground(rig, frames, contacts)
    pins = pins_for(contacts, ankles, ankle_h, F)
    pin_feet(rig, frames, pins)
    set_ankles(rig, frames, pins)
    print("feet: " + ", ".join(f"{c['side']}:{c['start']}-{c['end']}" for c in contacts))
    return frames


def key(rig, frames, fps):
    scene = bpy.context.scene
    scene.render.fps = round(fps)
    scene.render.fps_base = round(fps) / fps
    scene.frame_start, scene.frame_end = 0, len(frames) - 1
    pose = rig.arm.pose.bones
    prev_q = {}
    for f, fr in enumerate(frames):
        posed = rig.posed(fr["local"], fr["hips"])
        for b in rig.bones:
            name, pb = b.name[len(P):], pose[b.name]
            q = fr["local"].get(name, Quaternion()).copy()
            if b.name in prev_q:
                q.make_compatible(prev_q[b.name])
            prev_q[b.name] = q
            pb.rotation_quaternion = q
            pb.keyframe_insert("rotation_quaternion", frame=f)
            if b.parent is None:
                pb.location = (b.matrix_local.inverted() @ posed[b.name]).to_translation()
                pb.keyframe_insert("location", frame=f)


def main():
    track = json.load(open(TRACK))
    rig = Rig(RIG_FBX)
    frames = solve(rig, track)
    key(rig, frames, track["fps"])
    rig.arm.animation_data.action.name = os.path.splitext(os.path.basename(OUT_FBX))[0]
    if RENDER_DIR:
        render(rig.arm, len(frames))
    # With the skinned meshes: they carry the bind pose. An armature alone has none, and a reader
    # (Blender, measured) takes whatever frame was current as its rest pose instead.
    for o in bpy.data.objects:
        o.select_set(o == rig.arm or (o.type == "MESH" and o.parent == rig.arm))
    bpy.context.view_layer.objects.active = rig.arm
    bpy.ops.export_scene.fbx(filepath=OUT_FBX, use_selection=True, object_types={"ARMATURE", "MESH"},
                             add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=False,
                             bake_anim_use_nla_strips=False, bake_anim_simplify_factor=0.0,
                             bake_anim_force_startend_keying=True)
    print(f"exported {OUT_FBX}: {len(frames)} frames @ {track['fps']:.2f}")


def render(arm, F):
    """Workbench stills from the side and from behind, every frame, for checking by eye."""
    os.makedirs(RENDER_DIR, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x, scene.render.resolution_y = 360, 480
    bpy.ops.mesh.primitive_plane_add(size=40, location=(0, -6, 0))
    cam_data = bpy.data.cameras.new("cam")
    cam_data.type, cam_data.ortho_scale = "ORTHO", 3.2
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
