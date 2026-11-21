"""Pose2Sim output (two or more synced cameras, triangulated) -> the joint track retarget_blender.py reads.

The multi-camera counterpart of process_motion.py. Pose2Sim triangulates the bowler from every
camera at once, so depth is measured rather than guessed and the near and far legs cannot swap;
its OpenSim step then fits a scaled human skeleton (inverse kinematics) to those points, so the
joint centres taken from it obey bone lengths and joint ranges by construction.

Run with the Pose2Sim venv (it needs opensim):

    ~/Documents/Git/pose2sim-env/.venv/bin/python trc_to_track.py <trial dir> <out.json> \
        [--arm right] [--start N --end N] [--release N] [--contacts "l:a-b,r:c-d"] [--source ik|trc]

<trial dir> is the Pose2Sim project folder (holds pose-3d/ and kinematics/). By default the joints
come from the IK result (kinematics/*.mot on its scaled *.osim): joint centres from the model's
joint frames, heel, toe, hand and face points from its markers. --source trc uses the filtered
triangulated keypoints instead (no skeleton fit; hands and ears are synthesised).

Output, exactly as process_motion.py writes it: Blender axes for the game rig (Z up, the run toward
-Y, X to the bowler's left), metres, the ground at Z = 0, the first frame's hips over the origin,
joints scaled to the rig's legs; plus fps, frames, release_frame, arm, start_frame and contacts.
Contacts come from the feet in true 3D (low and slow), which single-camera footage could not give.
"""
import argparse
import glob
import json
import os

import numpy as np

RIG_LEG = 0.928 - 0.036   # as process_motion.py: the game rig's hip height above its toe base
NAMES = ["nose", "l_ear", "r_ear", "l_sh", "r_sh", "l_el", "r_el", "l_wr", "r_wr", "l_pinky", "r_pinky",
         "l_index", "r_index", "l_hip", "r_hip", "l_knee", "r_knee", "l_ank", "r_ank", "l_heel", "r_heel",
         "l_toe", "r_toe"]

# From the IK'd OpenSim model (Pose2Sim's scaled model): joint child frames and model markers.
IK_JOINTS = {"l_hip": "hip_l", "r_hip": "hip_r", "l_knee": "walker_knee_l", "r_knee": "walker_knee_r",
             "l_ank": "ankle_l", "r_ank": "ankle_r", "l_sh": "acromial_l", "r_sh": "acromial_r",
             "l_el": "elbow_l", "r_el": "elbow_r", "l_wr": "radius_hand_l", "r_wr": "radius_hand_r"}
IK_MARKERS = {"l_heel": "L_calc_study", "r_heel": "r_calc_study", "l_toe": "L_toe_study",
              "r_toe": "r_toe_study", "l_index": "LIndex", "r_index": "RIndex", "l_pinky": "LPinky",
              "r_pinky": "RPinky", "nose": "Nose", "l_eye": "LEye", "r_eye": "REye"}
# From the triangulated keypoints (Pose2Sim's Body_with_feet / HALPE_26 names).
TRC = {"l_hip": "LHip", "r_hip": "RHip", "l_knee": "LKnee", "r_knee": "RKnee", "l_ank": "LAnkle",
       "r_ank": "RAnkle", "l_heel": "LHeel", "r_heel": "RHeel", "l_sh": "LShoulder", "r_sh": "RShoulder",
       "l_el": "LElbow", "r_el": "RElbow", "l_wr": "LWrist", "r_wr": "RWrist", "nose": "Nose", "head": "Head"}


def read_trc(path):
    with open(path) as fh:
        lines = fh.read().splitlines()
    rate = float(lines[2].split("\t")[0])
    names = [n for n in lines[3].split("\t")[2:] if n.strip()]
    rows = [r.split("\t") for r in lines[5:] if r.strip()]
    data = np.array([[float(v) for v in r[2:2 + 3 * len(names)]] for r in rows])
    return rate, {n: data[:, 3 * i:3 * i + 3] for i, n in enumerate(names)}


def read_mot(path):
    with open(path) as fh:
        lines = fh.read().splitlines()
    end = next(i for i, l in enumerate(lines) if l.strip().lower() == "endheader")
    degrees = any("indegrees=yes" in l.replace(" ", "").lower() for l in lines[:end])
    cols = lines[end + 1].split("\t")
    data = np.array([[float(v) for v in l.split()] for l in lines[end + 2:] if l.strip()])
    return cols, data, degrees


def from_ik(trial):
    """Joint centres and markers of the scaled model posed by every IK frame, in OpenSim ground."""
    import opensim as osim
    osim.Logger.removeFileSink()          # otherwise opensim.log lands in the working directory
    mot = sorted(glob.glob(os.path.join(trial, "kinematics", "*.mot")))[-1]
    model_path = os.path.splitext(mot)[0] + ".osim"
    if not os.path.exists(model_path):
        model_path = sorted(glob.glob(os.path.join(trial, "kinematics", "*.osim")))[-1]
    cols, data, degrees = read_mot(mot)
    model = osim.Model(model_path)
    state = model.initSystem()
    coords = model.getCoordinateSet()
    # Degrees in the .mot for everything but translations (rotational and coupled, e.g. knee beta).
    rotational = {coords.get(i).getName(): coords.get(i).getMotionType() != 2 for i in range(coords.getSize())}
    joints = {k: model.getJointSet().get(v).getChildFrame() for k, v in IK_JOINTS.items()}
    markers = {k: model.getMarkerSet().get(v) for k, v in IK_MARKERS.items()}
    out = {k: [] for k in list(IK_JOINTS) + list(IK_MARKERS)}
    for row in data:
        for name, value in zip(cols[1:], row[1:]):
            if name in rotational:
                coords.get(name).setValue(state, np.radians(value) if degrees and rotational[name] else value, False)
        model.realizePosition(state)
        for k, fr in joints.items():
            p = fr.getPositionInGround(state)
            out[k].append([p.get(0), p.get(1), p.get(2)])
        for k, m in markers.items():
            p = m.getLocationInGround(state)
            out[k].append([p.get(0), p.get(1), p.get(2)])
    times = data[:, 0]
    fps = 1.0 / np.median(np.diff(times))
    print(f"IK: {os.path.basename(mot)} on {os.path.basename(model_path)}, {len(data)} frames @ {fps:.1f}")
    return fps, {k: np.array(v) for k, v in out.items()}


def from_trc(trial):
    trcs = sorted(glob.glob(os.path.join(trial, "pose-3d", "*_filt_*.trc")))
    trcs = [t for t in trcs if "LSTM" not in t] or sorted(glob.glob(os.path.join(trial, "pose-3d", "*.trc")))
    fps, raw = read_trc(trcs[0])
    out = {k: raw[v] for k, v in TRC.items() if v in raw}
    out["l_toe"] = (raw["LBigToe"] + raw["LSmallToe"]) / 2
    out["r_toe"] = (raw["RBigToe"] + raw["RSmallToe"]) / 2
    print(f"TRC: {os.path.basename(trcs[0])}, {len(out['l_hip'])} frames @ {fps:.1f}")
    return fps, out


def complete(j, up):
    """Ears from the eyes (or the head point), hand points from the forearm, where the source lacks them."""
    lat = j["l_sh"] - j["r_sh"]
    lat /= np.linalg.norm(lat, axis=1, keepdims=True)
    if "l_eye" in j:
        mid = (j["l_eye"] + j["r_eye"]) / 2
        back = mid - j["nose"]
        back /= np.linalg.norm(back, axis=1, keepdims=True)
        for s in ("l", "r"):
            j[f"{s}_ear"] = j[f"{s}_eye"] + back * 0.08          # ears ~8 cm behind the eyes
    else:
        head = j.pop("head")
        j["l_ear"], j["r_ear"] = head + lat * 0.075, head - lat * 0.075
    for s in ("l", "r"):
        if f"{s}_index" in j:
            continue
        fo = j[f"{s}_wr"] - j[f"{s}_el"]
        fo /= np.linalg.norm(fo, axis=1, keepdims=True)
        side = np.cross(fo, up)
        side /= np.linalg.norm(side, axis=1, keepdims=True) + 1e-9
        j[f"{s}_index"] = j[f"{s}_wr"] + fo * 0.09 + side * 0.03
        j[f"{s}_pinky"] = j[f"{s}_wr"] + fo * 0.08 - side * 0.03
    return j


def first_peak(x, frac=0.85):
    top = np.nanmax(x)
    for i in range(1, len(x) - 1):
        if x[i] >= frac * top and x[i] >= x[i - 1] and x[i] >= x[i + 1]:
            return i
    return int(np.nanargmax(x))


def parse_contacts(text):
    out = []
    for item in text.split(","):
        side, rng = item.strip().split(":")
        f0, f1 = rng.split("-")
        out.append({"side": side, "start": int(f0), "end": int(f1)})
    return out


def auto_contacts(out, fps, min_frames=3, slow=0.5, low=0.05):
    """Footfalls from the feet in 3D: within `low` m of that foot's lowest level and slower than
    `slow` m/s along the ground, for at least `min_frames` frames."""
    res = []
    for s in ("l", "r"):
        p = (out[f"{s}_heel"] + out[f"{s}_toe"]) / 2
        h = p[:, 2]
        sp = np.r_[np.linalg.norm(np.diff(p[:, :2], axis=0), axis=1), np.inf] * fps
        planted = (h < np.percentile(h, 5) + low) & (sp < slow)
        f = 0
        while f < len(planted):
            if planted[f]:
                e = f
                while e + 1 < len(planted) and planted[e + 1]:
                    e += 1
                if e - f + 1 >= min_frames:
                    res.append({"side": s, "start": int(f), "end": int(e)})
                f = e + 1
            else:
                f += 1
    return sorted(res, key=lambda c: c["start"])


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("trial")
    ap.add_argument("out")
    ap.add_argument("--source", choices=["ik", "trc"], default="ik")
    ap.add_argument("--arm", choices=["right", "left"], default="right")
    ap.add_argument("--start", type=int, default=0)
    ap.add_argument("--end", type=int, default=10 ** 9)
    ap.add_argument("--release", type=int, default=-1, help="relative to --start; -1 = auto")
    ap.add_argument("--contacts", default="", help="'l:a-b,r:c-d' (relative to --start); default: from 3D feet")
    ap.add_argument("--up", default="auto", help="world up axis of the Pose2Sim output: auto, x, y, z, -x, -y, -z")
    a = ap.parse_args()

    have_ik = bool(glob.glob(os.path.join(a.trial, "kinematics", "*.mot")))
    fps, j = from_ik(a.trial) if a.source == "ik" and have_ik else from_trc(a.trial)
    sl = slice(a.start, a.end + 1)
    j = {k: v[sl].astype(float) for k, v in j.items()}

    # Up: the calibration's vertical. OpenSim output (Pose2Sim) is Y-up; auto picks the axis the
    # body stands along, so a Z-up calibration also works.
    mid_sh = (j["l_sh"] + j["r_sh"]) / 2
    mid_ank = (j["l_ank"] + j["r_ank"]) / 2
    if a.up == "auto":
        tall = np.nanmean(mid_sh - mid_ank, axis=0)
        k = int(np.argmax(np.abs(tall)))
        up = np.eye(3)[k] * np.sign(tall[k])
    else:
        up = np.eye(3)["xyz".index(a.up[-1])] * (-1 if a.up.startswith("-") else 1)
    j = complete(j, up)

    side = "r" if a.arm == "right" else "l"
    lift = ((j[f"{side}_wr"] - j[f"{side}_sh"]) @ up)
    release = a.release if a.release >= 0 else first_peak(lift)

    # Forward: the hips' travel to the release along the ground; if he barely moves (a standing
    # demo), the way the pelvis faces.
    hips = (j["l_hip"] + j["r_hip"]) / 2
    travel = hips[release] - hips[0]
    travel -= (travel @ up) * up
    if np.linalg.norm(travel) < 0.3:
        lat = np.nanmean(j["l_hip"] - j["r_hip"], axis=0)
        travel = np.cross(lat, up)
        print("forward: pelvis facing (hips travel < 0.3 m)")
    fwd = travel / np.linalg.norm(travel)
    right = np.cross(fwd, up)

    centre = hips[0]
    out = {}
    for k in NAMES:
        rel = j[k] - centre
        out[k] = np.stack([-(rel @ right), -(rel @ fwd), rel @ up], -1)
    feet_z = np.concatenate([out[k][:, 2] for k in ("l_heel", "r_heel", "l_toe", "r_toe")])
    floor = np.percentile(feet_z, 5)
    for k in out:
        out[k][:, 2] -= floor
    leg = np.median(np.linalg.norm(j["l_hip"] - j["l_ank"], axis=1) +
                    np.linalg.norm(j["r_hip"] - j["r_ank"], axis=1)) / 2
    scale = RIG_LEG / (leg + 0.05)
    for k in out:
        out[k] *= scale

    contacts = parse_contacts(a.contacts) if a.contacts else auto_contacts(out, fps)
    res = {"fps": float(fps), "frames": int(len(hips)), "start_frame": a.start, "release_frame": int(release),
           "arm": a.arm, "scale": float(scale), "source": a.source if have_ik else "trc", "contacts": contacts,
           "joints": {k: v.round(5).tolist() for k, v in out.items()}}
    with open(a.out, "w") as fh:
        json.dump(res, fh)
    hb = (out["l_hip"] + out["r_hip"]) / 2
    print(f"{len(hb)} frames @ {fps:.1f}, up {up.tolist()}, release frame {release}, scale {scale:.3f}, "
          f"hips travel {np.linalg.norm(hb[-1, :2] - hb[0, :2]):.2f} m, contacts "
          + ", ".join(f"{c['side']}:{c['start']}-{c['end']}" for c in contacts))


if __name__ == "__main__":
    main()
