"""Two approximate cameras and the 3D joints they agree on.

World axes: F along the run (forward), L to the bowler's left, U up (right-handed).

Neither camera is calibrated, but each is described well enough by what is known about it: which
way it looks at the bowler (side / front / back, with an optional yaw if it is not square), how far
it tilts down, and its focal length. Its distance comes from the bowler's own size in the picture
(the leg at its longest near the anchor frame against MediaPipe's metric leg), so the far broadcast
lens and the close phone fall out of the same pinhole model. Each joint is then the point closest
to both viewing rays, weighted by visibility.
"""
import numpy as np

U = np.array([0.0, 0.0, 1.0])


def rot_u(deg):
    a = np.radians(deg)
    return np.array([np.cos(a), np.sin(a), 0.0])


def cam_axes(h, tilt_deg):
    """World-from-camera rotation, columns = the camera's x (picture right), y (picture down) and
    z (ahead) in world axes, for a camera whose picture-right is the horizontal h, tilted down."""
    h = h / np.linalg.norm(h)
    z0 = np.cross(U, h)
    a = np.radians(tilt_deg)
    z = np.cos(a) * z0 - np.sin(a) * U
    y = np.cross(z, h)
    return np.stack([h, y, z], 1)


def picture_right(kind, side_sign=1.0, yaw=0.0):
    """The world direction of a camera's picture-right. Side: +-F (+ when it stands on the bowler's
    right, so his run goes left to right). Front: +L (facing him); back: -L. yaw turns it about U."""
    if kind == "side":
        return side_sign * rot_u(yaw)
    return rot_u((90.0 if kind == "front" else -90.0) + yaw)


def place_camera(view, rays_cam0, Rwc_ref, ref, leg_m, scale_px):
    """Camera centre and world rays. The camera's axes at the anchor frame are Rwc_ref; the hips at
    the anchor frame are the origin, at the depth where the leg would measure scale_px pixels."""
    Rw0 = Rwc_ref @ view.R[ref]                       # world <- frame-0 camera (pan undone)
    rays_w = np.einsum("ij,fkj->fki", Rw0, rays_cam0)
    z_axis = Rwc_ref[:, 2]
    depth = view.fx * leg_m / scale_px
    hip_ray = (rays_w[ref, 23] + rays_w[ref, 24]) / 2
    hip_ray /= np.linalg.norm(hip_ray)
    C = -depth * hip_ray / float(hip_ray @ z_axis)
    return C, rays_w


def triangulate(Cs, rs, ws):
    """Least-squares point closest to several rays (centres C (3,), rays (..., 3), weights (...)).
    Returns points (..., 3) and the weighted mean perpendicular distance."""
    A_sum = 0.0
    b_sum = 0.0
    for C, r, w in zip(Cs, rs, ws):
        A = np.eye(3) - r[..., :, None] * r[..., None, :]
        A_sum = A_sum + w[..., None, None] * A
        b_sum = b_sum + w[..., None] * np.einsum("...ij,j->...i", A, C)
    p = np.linalg.solve(A_sum + 1e-9 * np.eye(3), b_sum[..., None])[..., 0]
    res = 0.0
    wsum = 0.0
    for C, r, w in zip(Cs, rs, ws):
        d = p - C
        perp = d - (d * r).sum(-1, keepdims=True) * r
        res = res + w * np.linalg.norm(perp, axis=-1)
        wsum = wsum + w
    return p, res / np.maximum(wsum, 1e-9)


def chain_swap_cost(Cs, Cf, rs, rf, ws, wf, pair):
    """(S, 2): triangulation residual (m) of a left/right point set with the side view's labels as
    they are, and swapped, the front view's labels fixed."""
    left, right = pair
    idx = left + right
    out = []
    for s in (0, 1):
        order = idx if s == 0 else right + left
        _, res = triangulate([Cs, Cf], [rs[:, order], rf[:, idx]], [ws[:, order], wf[:, idx]])
        out.append(np.nansum(res, axis=1))
    return np.stack(out, 1)


# Bones, parent before child: (parent, child). -1 = the hips' midpoint, -2 = the shoulders'.
EDGES = [(-1, 23), (-1, 24), (-1, -2), (-2, 11), (-2, 12), (-2, 0), (-2, 7), (-2, 8),
         (11, 13), (13, 15), (15, 17), (15, 19), (15, 21), (12, 14), (14, 16), (16, 18), (16, 20), (16, 22),
         (23, 25), (25, 27), (27, 29), (27, 31), (24, 26), (26, 28), (28, 30), (28, 32)]


def _pt(P, i):
    if i == -1:
        return (P[:, 23] + P[:, 24]) / 2
    if i == -2:
        return (P[:, 11] + P[:, 12]) / 2
    return P[:, i]


def fix_bone_lengths(P):
    """Every bone at its median length, keeping its direction, parents first."""
    lengths = {e: float(np.nanmedian(np.linalg.norm(_pt(P, e[1]) - _pt(P, e[0]), axis=-1))) for e in EDGES}
    Q = P.copy()
    mids = {-1: _pt(P, -1).copy()}
    for (a, b), L in lengths.items():
        old_dir = _pt(P, b) - _pt(P, a)
        old_dir /= np.linalg.norm(old_dir, axis=-1, keepdims=True) + 1e-9
        start = mids[a] if a < 0 else Q[:, a]
        new = start + old_dir * L
        if b == -2:
            mids[-2] = new
        else:
            Q[:, b] = new
    return Q, lengths
