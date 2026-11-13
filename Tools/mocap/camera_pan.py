"""Camera rotation from the background, for footage where the camera pans to follow the bowler.

Frame to frame, background features (the bowler's box masked out, and the bottom strip where
broadcast captions sit) are tracked with Lucas-Kanade and a homography fitted with RANSAC. A
camera turning on a tripod or held in place moves the background by H = K R K^-1, so the rotation
is fitted between the inliers' viewing rays K^-1 x (Kabsch). Rotations accumulate to the first frame: `R[t]` takes
frame-0 camera axes to frame-t camera axes (p_t = R[t] p_0), so a point seen in frame t is
R[t].T @ p_t in frame-0 axes. A zoom shows up as scale in the homography; it is accumulated and
reported, not modelled.
"""
import cv2
import numpy as np


class PanTracker:
    def __init__(self, fx, width, height, caption_strip=0.12):
        self.K = np.array([[fx, 0, width / 2], [0, fx, height / 2], [0, 0, 1.0]])
        self.Ki = np.linalg.inv(self.K)
        self.w, self.h = width, height
        self.caption = caption_strip
        self.prev = None
        self.R = np.eye(3)
        self.zoom = 1.0
        self.rotations, self.zooms, self.inliers = [], [], []

    def _mask(self, box):
        m = np.full((self.h, self.w), 255, np.uint8)
        m[int(self.h * (1 - self.caption)):, :] = 0
        if box is not None:
            x0, y0, x1, y1 = box
            pw, ph = (x1 - x0) * 0.6, (y1 - y0) * 0.4
            m[max(0, int(y0 - ph)):int(y1 + ph), max(0, int(x0 - pw)):int(x1 + pw)] = 0
        return m

    def _rays(self, px):
        p = np.c_[px.reshape(-1, 2), np.ones(len(px.reshape(-1, 2)))] @ self.Ki.T
        return p / np.linalg.norm(p, axis=1, keepdims=True)

    def step(self, gray, box):
        """Feed the next frame (grayscale) and the bowler's pixel box (x0, y0, x1, y1) or None."""
        if self.prev is not None:
            pts = cv2.goodFeaturesToTrack(self.prev[0], 600, 0.01, 8, mask=self._mask(self.prev[1]))
            H, n = None, 0
            if pts is not None and len(pts) >= 12:
                nxt, st, _ = cv2.calcOpticalFlowPyrLK(self.prev[0], gray, pts, None, winSize=(21, 21), maxLevel=3)
                good = st.ravel() == 1
                if good.sum() >= 12:
                    H, inl = cv2.findHomography(pts[good], nxt[good], cv2.RANSAC, 2.0)
                    n = int(inl.sum()) if inl is not None else 0
            if H is not None and n >= 12:
                H = H / H[2, 2]
                self.zoom *= float(np.sqrt(abs(np.linalg.det(H[:2, :2]))))
                # The rotation between the inliers' viewing rays (Kabsch). Not K^-1 H K made
                # orthonormal: with a long lens H is nearly a pure shift, its perspective terms are
                # lost in the noise, and squaring that up kept only part of the turn (measured on
                # anderson_stands: 9 deg against the 13.5 the background's 923 px shift gives).
                m = inl.ravel() == 1
                a = self._rays(pts[good][m])
                b = self._rays(nxt[good][m])
                u, _, vt = np.linalg.svd(b.T @ a)
                Rr = u @ np.diag([1, 1, np.linalg.det(u @ vt)]) @ vt
                self.R = Rr @ self.R
            self.inliers.append(n)
        else:
            self.inliers.append(-1)
        self.rotations.append(self.R.copy())
        self.zooms.append(self.zoom)
        self.prev = (gray, box)
        return self.R

    def summary(self):
        R = np.array(self.rotations)
        yaw = np.degrees(np.arctan2(R[:, 0, 2], R[:, 2, 2]))
        pitch = np.degrees(np.arcsin(np.clip(-R[:, 1, 2], -1, 1)))
        inl = [n for n in self.inliers if n >= 0]
        return (f"pan: yaw {yaw.min():+.1f}..{yaw.max():+.1f} deg, pitch {pitch.min():+.1f}..{pitch.max():+.1f} deg, "
                f"zoom {min(self.zooms):.3f}..{max(self.zooms):.3f}, RANSAC inliers median "
                f"{int(np.median(inl)) if inl else 0}, frames under 12: {sum(1 for n in inl if n < 12)}")
