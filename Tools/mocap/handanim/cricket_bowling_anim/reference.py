"""Reference footage behind the bowler, seen through the camera that matches it, in step with the action.

A reference is an image sequence from make_reference.py (reference/<view>/0001.jpg... + meta.json) on an
image empty parented to its camera (side -> "Cam side-on", front -> "Cam front-on", back -> "Cam
behind"; a side-on clip he runs right to left in is filmed from his left, "Cam side-on left"; a clip
listed in PHONE_CAMERAS gets a camera where its phone stood, turning to follow his hips), drawn
behind everything. On every frame change:
  - timing: the scene's markers "back foot", "front foot", "release" are matched to the same events in
    the footage, and the footage is warped between them (straight lines; past the ends, the end
    segments' rate), so it follows the action being animated, not its own timing;
  - placing: the footage's bowler is put on the rig - his mid-hip on the rig's, at a size where his
    hip-to-shoulder length matches the rig's (one size for the whole clip, so the footage does not
    breathe). Turn off "Pin to bowler" to hold it where it is at the release instead.
Two views (CRICKET_OT_two_views) splits the 3D view into side-on | front-on, each looking through its
camera with the rig see-through.
"""
import bisect
import json
import os

import bpy
from bpy.app.handlers import persistent
from mathutils import Vector

P = "mixamorig5:"
EVENTS = ("back foot", "front foot", "release")
CAMERA_FOR = {"side": "Cam side-on", "front": "Cam front-on", "front_oblique": "Cam front-on", "back": "Cam behind"}
# A side-on clip in which he runs right to left was filmed from his left: "Cam side-on" is on his right.
SIDE_FROM_LEFT = "Cam side-on left"


# A clip filmed by someone standing still and turning to follow him gets its own camera: one at that
# spot, aimed at the rig's hips on every frame. The spot is in metres from the rig's hips at the
# "front foot" marker (x, y on the ground, then height). anderson_front's was found by projecting the
# rig through cameras all round him and keeping the one whose picture best matched the clip's tracked
# joints: behind him and to his right, about 50 degrees round from straight behind (mean 2D error in
# torso lengths, by where the camera is: his left 0.85, front-on 0.66, straight behind 0.61, his right
# side 0.52, there 0.46). How far away it stood the fit could not tell; 8 m with a 50 mm lens frames
# him about as the phone did.
PHONE_CAMERAS = {"anderson_front": ("Cam behind right", (-6.3, 5.8, 1.5), 50, "from behind his right")}


def phone_camera(scene, name, offset, lens):
    cam = bpy.data.objects.get(name)
    if cam is None:
        cam = bpy.data.objects.new(name, bpy.data.cameras.new(name))
        scene.collection.objects.link(cam)
    cam.data.lens = lens
    cam.data.clip_end = max(cam.data.clip_end, DEPTH * 2)
    arm = bpy.data.objects["Bowler"]
    keep = scene.frame_current
    m = scene.timeline_markers.get("front foot")
    scene.frame_set(m.frame if m else keep)
    hips, _ = rig_points(arm.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    scene.frame_set(keep)
    cam.location = (hips.x + offset[0], hips.y + offset[1], offset[2])
    con = cam.constraints.get("Follow hips") or cam.constraints.new("TRACK_TO")
    con.name = "Follow hips"
    con.target, con.subtarget = arm, P + "Hips"
    con.track_axis, con.up_axis = "TRACK_NEGATIVE_Z", "UP_Y"
    return cam


def camera_for(meta, scene=None):
    spec = PHONE_CAMERAS.get(meta.get("view"))
    if spec and scene is not None:
        return phone_camera(scene, *spec[:3])
    if meta["kind"] == "side" and meta.get("run_dir", 1) < 0:
        return bpy.data.objects.get(SIDE_FROM_LEFT) or bpy.data.objects.get("Cam side-on")
    return bpy.data.objects.get(CAMERA_FOR.get(meta["kind"], "Cam side-on"))
# No front-on delivery of Anderson's was found: anderson_front (named for the camera that films his
# run-up coming at it) is a phone in the nets that he runs past, so by the gather it is behind his
# right. anderson_back (straight behind) is still there to load by hand.
PAIRS = {"Steyn": ("steyn_side", "steyn_front"), "Anderson": ("anderson_side", "anderson_front")}
DEPTH = 60.0          # metres in front of its camera: past everything in the scene


def reference_dir():
    return os.path.join(bpy.context.scene.get("cricket_project", ""), "Tools", "mocap", "handanim", "reference")


def references():
    return [o for o in bpy.data.objects if o.get("cricket_ref")]


_meta_cache = {}


def meta_of(obj):
    path = obj.get("cricket_ref")
    if path not in _meta_cache:
        _meta_cache[path] = json.load(open(path))
    return _meta_cache[path]


# ---------------------------------------------------------------- timing ----------------------------

def footage_time(scene, meta, frame):
    """Real seconds into the footage that go with this scene frame."""
    fps = scene.render.fps / scene.render.fps_base
    times = meta["time"]
    pairs = sorted((scene.timeline_markers[n].frame, times[meta["events"][n]]) for n in EVENTS
                   if n in meta.get("events", {}) and n in scene.timeline_markers)
    ok = all(b[0] > a[0] and b[1] > a[1] for a, b in zip(pairs, pairs[1:]))
    if not pairs or not ok:
        rel = [p for p in pairs if p[0] == scene.timeline_markers["release"].frame] if "release" in scene.timeline_markers else []
        f0, t0 = (rel[0] if rel else (scene.frame_start, 0.0))
        return t0 + (frame - f0) / fps
    if len(pairs) == 1:
        return pairs[0][1] + (frame - pairs[0][0]) / fps
    i = 0
    while i < len(pairs) - 2 and frame >= pairs[i + 1][0]:
        i += 1
    (f0, t0), (f1, t1) = pairs[i], pairs[i + 1]
    return t0 + (frame - f0) * (t1 - t0) / (f1 - f0)


def image_at(meta, t):
    times = meta["time"]
    k = bisect.bisect_left(times, t)
    if k <= 0:
        return 0
    if k >= len(times):
        return len(times) - 1
    return k if times[k] - t < t - times[k - 1] else k - 1


# ---------------------------------------------------------------- placing ---------------------------

def rig_points(arm_eval):
    pb = arm_eval.pose.bones
    mw = arm_eval.matrix_world
    hips = (mw @ pb[P + "LeftUpLeg"].head + mw @ pb[P + "RightUpLeg"].head) / 2
    shoulders = (mw @ pb[P + "LeftArm"].head + mw @ pb[P + "RightArm"].head) / 2
    return hips, shoulders


def on_plane(cam, p):
    """A world point carried along its camera ray onto the reference plane (camera space x, y)."""
    c = cam.matrix_world.inverted() @ p
    if c.z > -1e-3:
        return None
    s = DEPTH / -c.z
    return Vector((c.x * s, c.y * s))


def image_scale(meta):
    """Plane units per normalised image unit, x and y (the empty's size is the image's longer side)."""
    w, h = meta["width"], meta["height"]
    m = max(w, h)
    return w / m, h / m


def place(obj, cam, meta, k, arm_eval):
    size = obj.empty_display_size
    sx, sy = image_scale(meta)
    if obj.get("cricket_pin", True):
        hips, _ = rig_points(arm_eval)
        H = on_plane(cam, hips)
        if H is None:
            return
        u, v = meta["hips"][k]
        obj.location = (H.x - (u - 0.5) * size * sx, H.y - (0.5 - v) * size * sy, -DEPTH)
    obj.rotation_euler = (0, 0, 0)


def fit_size(scene, obj, cam, meta, arm):
    """One size for the clip: the median over the scene's frames of rig torso / footage torso."""
    keep = scene.frame_current
    ratios = []
    for f in range(scene.frame_start, scene.frame_end + 1):
        scene.frame_set(f)
        dg = bpy.context.evaluated_depsgraph_get()
        hips, shoulders = rig_points(arm.evaluated_get(dg))
        a, b = on_plane(cam, hips), on_plane(cam, shoulders)
        k = image_at(meta, footage_time(scene, meta, f))
        t = meta["torso"][k]
        if a is not None and b is not None and t > 1e-3:
            ratios.append((a - b).length / t)
    scene.frame_set(keep)
    ratios.sort()
    return ratios[len(ratios) // 2] if ratios else 20.0


def update(scene, depsgraph=None):
    arm = bpy.data.objects.get("Bowler")
    if arm is None:
        return
    dg = depsgraph or bpy.context.evaluated_depsgraph_get()
    arm_eval = arm.evaluated_get(dg)
    for obj in references():
        try:
            meta = meta_of(obj)
        except (OSError, ValueError):
            continue
        k = image_at(meta, footage_time(scene, meta, scene.frame_current))
        iu = obj.image_user
        iu.frame_start = scene.frame_current - k      # shows image k+1 (0001.jpg is image 0) now
        iu.frame_offset = 0
        if obj.parent is not None:
            place(obj, obj.parent, meta, k, arm_eval)
        obj["cricket_image"] = k


@persistent
def on_frame(scene, depsgraph=None):
    update(scene, depsgraph)


@persistent
def on_load(_):
    _meta_cache.clear()


# ---------------------------------------------------------------- loading ---------------------------

def load_reference(context, view):
    """The footage of one view (a folder under reference/) behind its camera."""
    scene = context.scene
    folder = view if os.path.isabs(view) else os.path.join(reference_dir(), view)
    meta_path = os.path.join(folder, "meta.json")
    meta = json.load(open(meta_path))
    cam = camera_for(meta, scene)
    name = f"Ref {meta['kind']}"
    old = bpy.data.objects.get(name)
    if old:
        bpy.data.objects.remove(old)
    img = bpy.data.images.load(os.path.join(folder, "0001.jpg"), check_existing=True)
    img.source = "SEQUENCE"
    obj = bpy.data.objects.new(name, None)
    obj.empty_display_type = "IMAGE"
    obj.data = img
    obj.empty_image_depth = "BACK"
    obj.empty_image_side = "FRONT"
    obj.show_empty_image_orthographic = False
    obj.use_empty_image_alpha = True
    obj.color[3] = 1.0
    iu = obj.image_user
    iu.frame_duration = meta["frames"]
    iu.use_auto_refresh = True
    iu.use_cyclic = False
    obj.hide_select = True
    obj["cricket_ref"] = meta_path
    obj["cricket_pin"] = True
    scene.collection.objects.link(obj)
    if cam is not None:
        obj.parent = cam
        obj.matrix_parent_inverse.identity()
        obj.location = (0, 0, -DEPTH)
        _meta_cache.pop(meta_path, None)
        obj.empty_display_size = fit_size(scene, obj, cam, meta, bpy.data.objects["Bowler"])
    update(scene)
    return obj, meta


class CRICKET_OT_reference_pair(bpy.types.Operator):
    bl_idname = "cricket.reference_pair"
    bl_label = "Footage of this bowler"
    bl_description = "His side-on and front-on footage behind the side-on and front-on cameras, in step with the markers"
    bl_options = {"REGISTER", "UNDO"}
    bowler: bpy.props.StringProperty()

    def execute(self, context):
        missing = [v for v in PAIRS[self.bowler] if not os.path.exists(os.path.join(reference_dir(), v, "meta.json"))]
        if missing:
            self.report({"ERROR"}, f"No reference for {', '.join(missing)}: run handanim/make_reference.py {' '.join(missing)}")
            return {"CANCELLED"}
        for obj in references():                 # one bowler's footage at a time
            bpy.data.objects.remove(obj)
        metas = [load_reference(context, v)[1] for v in PAIRS[self.bowler]]
        context.scene["cricket_reference"] = self.bowler
        point_views(context)
        other = next((m for m in metas if m["kind"] != "side"), {"kind": "front"})
        where = (PHONE_CAMERAS[other["view"]][3] if other.get("view") in PHONE_CAMERAS
                 else "from behind him" if other["kind"] == "back" else other["kind"].replace("_", " "))
        self.report({"INFO"}, f"{self.bowler}: side-on, and {where}; timed to the markers")
        return {"FINISHED"}


class CRICKET_OT_reference(bpy.types.Operator):
    bl_idname = "cricket.reference"
    bl_label = "Add reference folder"
    bl_description = "A folder made by make_reference.py (it has a meta.json) behind its camera"
    bl_options = {"REGISTER", "UNDO"}
    directory: bpy.props.StringProperty(subtype="DIR_PATH")

    def invoke(self, context, event):
        context.window_manager.fileselect_add(self)
        return {"RUNNING_MODAL"}

    def execute(self, context):
        if not os.path.exists(os.path.join(self.directory, "meta.json")):
            self.report({"ERROR"}, "That folder has no meta.json: make it with handanim/make_reference.py")
            return {"CANCELLED"}
        obj, meta = load_reference(context, self.directory.rstrip("/"))
        self.report({"INFO"}, f"{obj.name}: {meta['frames']} frames of {meta['view']}")
        return {"FINISHED"}


class CRICKET_OT_refit(bpy.types.Operator):
    bl_idname = "cricket.refit_reference"
    bl_label = "Refit size"
    bl_description = "Size the footage's bowler to the rig again (after big changes to the action or the markers)"

    def execute(self, context):
        arm = bpy.data.objects["Bowler"]
        for obj in references():
            if obj.parent is not None:
                obj.empty_display_size = fit_size(context.scene, obj, obj.parent, meta_of(obj), arm)
        update(context.scene)
        return {"FINISHED"}


# ---------------------------------------------------------------- layout ----------------------------

def camera_view(space, cam):
    space.camera = cam
    space.use_local_camera = True
    space.region_3d.view_perspective = "CAMERA"
    space.region_3d.view_camera_zoom = 0.0
    space.region_3d.view_camera_offset = (0.0, 0.0)
    space.shading.type = "SOLID"
    space.shading.color_type = "MATERIAL"
    space.shading.show_xray = True
    space.shading.xray_alpha = 0.55
    space.overlay.show_bones = True
    space.lock_camera = False


def view_cameras():
    """(left, right) cameras for the two views: the side-on clip's camera, and the other clip's
    (front-on, or wherever that clip was filmed from)."""
    side = next((o.parent for o in references() if o.parent and meta_of(o)["kind"] == "side"), None)
    other = next((o.parent for o in references() if o.parent and meta_of(o)["kind"] != "side"), None)
    return (side or bpy.data.objects.get("Cam side-on"), other or bpy.data.objects.get("Cam front-on"))


def point_views(context):
    """The two left-most 3D views through view_cameras(), the right one with the Cricket tab."""
    screen = context.screen
    views = sorted((a for a in screen.areas if a.type == "VIEW_3D"), key=lambda a: a.x)[:2]
    if len(views) < 2:
        return
    for area, cam in zip(views, view_cameras()):
        if cam is None:
            continue
        sp = area.spaces.active
        camera_view(sp, cam)
        sp.region_3d.view_camera_zoom = 30.0      # the camera frame filling most of the view
        sp.show_region_ui = area == views[-1]
        if sp.show_region_ui:
            for r in area.regions:
                if r.type == "UI":
                    try:
                        r.active_panel_category = "Cricket"
                    except (TypeError, AttributeError):
                        pass
        area.tag_redraw()


class CRICKET_OT_two_views(bpy.types.Operator):
    bl_idname = "cricket.two_views"
    bl_label = "Two views"
    bl_description = "Split the 3D view in two: side-on, and front-on (or from behind), each through its camera with its footage"

    def execute(self, context):
        win, screen = context.window, context.screen
        views = [a for a in screen.areas if a.type == "VIEW_3D"]
        if len(views) < 2:
            big = max(views, key=lambda a: a.width * a.height)
            region = next(r for r in big.regions if r.type == "WINDOW")
            with context.temp_override(window=win, screen=screen, area=big, region=region):
                bpy.ops.screen.area_split(direction="VERTICAL", factor=0.5)
            views = [a for a in screen.areas if a.type == "VIEW_3D"]
        point_views(context)
        pitch = bpy.data.collections.get("Pitch")
        if pitch is not None:
            pitch.hide_viewport = True      # the footage has its own ground; the turf would cover his feet
        update(context.scene)
        return {"FINISHED"}


class CRICKET_OT_toggle_pitch(bpy.types.Operator):
    bl_idname = "cricket.toggle_pitch"
    bl_label = "Show / hide pitch"

    def execute(self, context):
        pitch = bpy.data.collections.get("Pitch")
        if pitch is not None:
            pitch.hide_viewport = not pitch.hide_viewport
        return {"FINISHED"}


def draw(box, context):
    box.label(text="Reference footage", icon="IMAGE_DATA")
    row = box.row()
    for b in PAIRS:
        row.operator("cricket.reference_pair", text=b).bowler = b
    box.operator("cricket.two_views", icon="SPLIT_VERTICAL")
    for obj in references():
        row = box.row(align=True)
        row.label(text=f"{obj.name}: image {obj.get('cricket_image', 0) + 1}")
        row.prop(obj, '["cricket_pin"]', text="Pin")
        row.prop(obj, "color", index=3, text="Opacity", slider=True)
    row = box.row(align=True)
    row.operator("cricket.refit_reference")
    row.operator("cricket.toggle_pitch", text="Pitch", icon="MESH_PLANE")
    box.operator("cricket.reference", text="Add reference folder…")


CLASSES = (CRICKET_OT_reference_pair, CRICKET_OT_reference, CRICKET_OT_refit, CRICKET_OT_two_views,
           CRICKET_OT_toggle_pitch)


def register():
    for c in CLASSES:
        bpy.utils.register_class(c)
    if on_frame not in bpy.app.handlers.frame_change_post:
        bpy.app.handlers.frame_change_post.append(on_frame)
    if on_load not in bpy.app.handlers.load_post:
        bpy.app.handlers.load_post.append(on_load)


def unregister():
    for h, lst in ((on_frame, bpy.app.handlers.frame_change_post), (on_load, bpy.app.handlers.load_post)):
        if h in lst:
            lst.remove(h)
    for c in reversed(CLASSES):
        bpy.utils.unregister_class(c)
