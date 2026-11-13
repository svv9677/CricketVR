"""Cricket bowling animation tools for Blender: a "Cricket" tab in the 3D view's side panel (N).

Hand-animate the CricketVR bowler (the game's Bowler rig) and send the result straight to Unity.
Set up by Tools/mocap/handanim/build_scene.py; see Tools/mocap/handanim/README.md for the workflow.

    Start from: the game's own actions (Steyn, Bumrah, Lyon) or the captured Anderson, as a base
                layer you do not touch, with an "Edits" layer on top where your keys go.
    Ghost:      another action played by a see-through skeleton beside yours, to compare.
    Reference:  an image sequence (make_reference.py) on a plane, in step with the timeline.
    Markers:    back foot, front foot, release - the release one is where the ball leaves.
    Export:     FBX + sidecar into the Unity project; Unity imports it, wires it into the bowling
                controller and works out his mark from where your front foot lands.
"""
bl_info = {
    "name": "Cricket bowling animation (CricketVR)",
    "author": "CricketVR",
    "version": (1, 0, 0),
    "blender": (4, 2, 0),
    "location": "View3D > Sidebar > Cricket",
    "category": "Animation",
}

import json
import os

import bpy
from mathutils import Vector

P = "mixamorig5:"
UNITY_MOCAP = ("Assets", "Resources", "Animations", "Bowling", "Mocap")


def project_root():
    """The CricketVR checkout: stored on the scene by build_scene.py."""
    return bpy.context.scene.get("cricket_project", "")


def handanim_dir():
    return os.path.join(project_root(), "Tools", "mocap", "handanim")


def rig(context):
    return bpy.data.objects.get("Bowler")


def ghost(context):
    return bpy.data.objects.get("Ghost")


def base_actions():
    return [a for a in bpy.data.actions if a.get("cricket_base")]


# ---------------------------------------------------------------- layers -------------------------

def start_from(arm, base, keep_edits=True):
    """Base action as a locked NLA strip, an additive 'Edits' action on top for the user's keys."""
    ad = arm.animation_data or arm.animation_data_create()
    edits = ad.action if (keep_edits and ad.action and ad.action.get("cricket_edits")) else None
    for t in list(ad.nla_tracks):
        ad.nla_tracks.remove(t)
    track = ad.nla_tracks.new()
    track.name = "Base (do not edit)"
    strip = track.strips.new(base.name, int(base.frame_range[0]), base)
    strip.blend_type = "REPLACE"
    track.lock = True
    if edits is None:
        edits = bpy.data.actions.new(f"Edits_{base.name}")
        edits["cricket_edits"] = True
    ad.action = edits
    ad.action_blend_type = "COMBINE"      # rotations multiply onto the base (quaternions stay unit)
    ad.action_influence = 1.0
    scene = bpy.context.scene
    scene.frame_start, scene.frame_end = int(base.frame_range[0]), int(base.frame_range[1])
    scene["cricket_base"] = base.name
    rel = base.get("cricket_release")
    if rel is not None:
        m = scene.timeline_markers.get("release") or scene.timeline_markers.new("release")
        m.frame = int(rel)
    return edits


class CRICKET_OT_start_from(bpy.types.Operator):
    bl_idname = "cricket.start_from"
    bl_label = "Start from this action"
    bl_description = "Use this action as the base layer; your keys go on the Edits layer above it"
    bl_options = {"REGISTER", "UNDO"}
    action: bpy.props.StringProperty()
    keep_edits: bpy.props.BoolProperty(name="Keep my edits", default=False)

    def execute(self, context):
        arm, act = rig(context), bpy.data.actions.get(self.action)
        if arm is None or act is None:
            self.report({"ERROR"}, "No Bowler rig or no such action")
            return {"CANCELLED"}
        start_from(arm, act, self.keep_edits)
        self.report({"INFO"}, f"Base: {act.name}. Key your changes on the Edits layer (it combines on top of the base).")
        return {"FINISHED"}


class CRICKET_OT_toggle_edits(bpy.types.Operator):
    bl_idname = "cricket.toggle_edits"
    bl_label = "Edits on/off"
    bl_description = "Compare your edits against the base action"

    def execute(self, context):
        ad = rig(context).animation_data
        ad.action_influence = 0.0 if ad.action_influence > 0.5 else 1.0
        return {"FINISHED"}


class CRICKET_OT_bake_flat(bpy.types.Operator):
    bl_idname = "cricket.bake_flat"
    bl_label = "Flatten to one action"
    bl_description = "Bake base + edits into a single new action (for heavy rework of the timing)"
    bl_options = {"REGISTER", "UNDO"}

    def execute(self, context):
        arm = rig(context)
        context.view_layer.objects.active = arm
        bpy.ops.object.mode_set(mode="POSE")
        bpy.ops.pose.select_all(action="SELECT")
        scene = context.scene
        bpy.ops.nla.bake(frame_start=scene.frame_start, frame_end=scene.frame_end, only_selected=False,
                         visual_keying=True, clear_constraints=False, use_current_action=False, bake_types={"POSE"})
        baked = arm.animation_data.action
        baked.name = f"Flat_{scene.get('cricket_base', 'action')}"
        baked["cricket_base"] = True
        baked["cricket_release"] = scene.timeline_markers["release"].frame if "release" in scene.timeline_markers else -1
        start_from(arm, baked, keep_edits=False)
        bpy.ops.object.mode_set(mode="OBJECT")
        self.report({"INFO"}, f"Baked to {baked.name}; it is now the base")
        return {"FINISHED"}


# ---------------------------------------------------------------- ghost ---------------------------

class CRICKET_OT_ghost(bpy.types.Operator):
    bl_idname = "cricket.ghost"
    bl_label = "Ghost plays this"
    bl_description = "A see-through skeleton beside yours plays this action (or hide it)"
    action: bpy.props.StringProperty()

    def execute(self, context):
        g = ghost(context)
        if g is None:
            return {"CANCELLED"}
        if not self.action:
            g.hide_viewport = not g.hide_viewport
            return {"FINISHED"}
        g.hide_viewport = False
        g.animation_data_create()
        g.animation_data.action = bpy.data.actions[self.action]
        return {"FINISHED"}


# ---------------------------------------------------------------- reference -----------------------

class CRICKET_OT_reference(bpy.types.Operator):
    bl_idname = "cricket.reference"
    bl_label = "Add reference images"
    bl_description = "An image sequence from make_reference.py on a plane beside the bowler, in step with the timeline"
    bl_options = {"REGISTER", "UNDO"}
    directory: bpy.props.StringProperty(subtype="DIR_PATH")
    view: bpy.props.EnumProperty(items=[("SIDE", "Side-on", ""), ("BEHIND", "From behind", ""), ("FRONT", "Front-on", "")])
    first_frame: bpy.props.IntProperty(name="Starts at frame", default=0)

    def invoke(self, context, event):
        context.window_manager.fileselect_add(self)
        return {"RUNNING_MODAL"}

    def execute(self, context):
        files = sorted(f for f in os.listdir(self.directory) if f.lower().endswith((".png", ".jpg")))
        if not files:
            self.report({"ERROR"}, "No images in that folder")
            return {"CANCELLED"}
        img = bpy.data.images.load(os.path.join(self.directory, files[0]))
        img.source = "SEQUENCE"
        name = f"Ref_{self.view.title()}"
        old = bpy.data.objects.get(name)
        if old:
            bpy.data.objects.remove(old)
        empty = bpy.data.objects.new(name, None)
        empty.empty_display_type = "IMAGE"
        empty.data = img
        empty.empty_display_size = 2.4
        empty.use_empty_image_alpha = True
        empty.color[3] = 0.6
        iu = empty.image_user
        iu.frame_duration = len(files)
        iu.frame_start = self.first_frame
        iu.use_auto_refresh = True
        # Beside the run, facing the camera that matches the footage.
        place = {"SIDE": ((3.5, -2.5, 1.2), (1.5708, 0, 1.5708)),
                 "BEHIND": ((-2.8, 3.5, 1.2), (1.5708, 0, 3.1416)),
                 "FRONT": ((2.8, -9.0, 1.2), (1.5708, 0, 0))}[self.view]
        empty.location, empty.rotation_euler = place
        context.scene.collection.objects.link(empty)
        self.report({"INFO"}, f"{name}: {len(files)} frames from frame {self.first_frame} (move it with Starts at frame)")
        return {"FINISHED"}


# ---------------------------------------------------------------- markers -------------------------

class CRICKET_OT_marker(bpy.types.Operator):
    bl_idname = "cricket.marker"
    bl_label = "Mark here"
    bl_description = "Put this marker at the current frame"
    name: bpy.props.StringProperty()

    def execute(self, context):
        scene = context.scene
        m = scene.timeline_markers.get(self.name) or scene.timeline_markers.new(self.name)
        m.frame = scene.frame_current
        return {"FINISHED"}


# ---------------------------------------------------------------- export --------------------------

def forward_distance_to_toe(arm, release, foot="LeftToeBase"):
    """Metres along the run (-Y) from the hips at the first frame to the front toe at the release, and
    the bowling hand's sideways offset (+X = his left) at the release."""
    scene = bpy.context.scene
    keep = scene.frame_current
    scene.frame_set(scene.frame_start)
    hips0 = arm.matrix_world @ arm.pose.bones[P + "Hips"].head
    scene.frame_set(release)
    toe = arm.matrix_world @ arm.pose.bones[P + foot].tail
    hand = arm.matrix_world @ arm.pose.bones[P + "RightHand"].head
    hips = arm.matrix_world @ arm.pose.bones[P + "Hips"].head
    scene.frame_set(keep)
    return -(toe.y - hips0.y), (hand.x - hips.x)


def next_action_number(folder):
    used = {5, 6}
    for f in os.listdir(folder) if os.path.isdir(folder) else []:
        if f.endswith(".mocap.json"):
            try:
                used.add(int(json.load(open(os.path.join(folder, f))).get("action", 0)))
            except Exception:
                pass
    return max(used | {6}) + 1


class CRICKET_OT_export(bpy.types.Operator):
    bl_idname = "cricket.export"
    bl_label = "Export to Unity"
    bl_description = "FBX + sidecar into the Unity project; Unity wires it into the bowling controller"
    name: bpy.props.StringProperty(name="Name in the game", default="HandPace")

    def invoke(self, context, event):
        return context.window_manager.invoke_props_dialog(self)

    def execute(self, context):
        scene, arm = context.scene, rig(context)
        if "release" not in scene.timeline_markers:
            self.report({"ERROR"}, "Put the 'release' marker on the frame the ball leaves the hand first")
            return {"CANCELLED"}
        folder = os.path.join(project_root(), *UNITY_MOCAP)
        os.makedirs(folder, exist_ok=True)
        name = "".join(ch for ch in self.name if ch.isalnum()) or "HandPace"
        fbx = os.path.join(folder, name + ".fbx")
        side_path = os.path.join(folder, name + ".mocap.json")
        old = json.load(open(side_path)) if os.path.exists(side_path) else {}
        release = scene.timeline_markers["release"].frame - scene.frame_start
        frames = scene.frame_end - scene.frame_start + 1
        fps = scene.render.fps / scene.render.fps_base

        # His mark, from where your front foot lands against Steyn's (whose mark is already right).
        steyn = scene.get("cricket_steyn_toe")
        d, wide = forward_distance_to_toe(arm, scene.timeline_markers["release"].frame)
        mark_forward = round(steyn[0] - d, 3) if steyn else 0.0
        mark_wide = round(steyn[1] - wide, 3) if steyn else 0.0

        for o in bpy.data.objects:
            o.select_set(o == arm or (o.type == "MESH" and o.parent == arm))
        context.view_layer.objects.active = arm
        bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True, object_types={"ARMATURE", "MESH"},
                                 add_leaf_bones=False, use_armature_deform_only=True, bake_anim=True,
                                 bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
                                 bake_anim_simplify_factor=0.0, bake_anim_force_startend_keying=True)
        blend_s = 0.25
        side = {
            "frames": frames, "fps": fps, "release_frame": release,
            "action": old.get("action", next_action_number(folder)),
            "state_speed": 1.0 / 1.24,      # real time at a pace ball's Time
            "run_exit": 0.537, "blend": 0.3, "clip_offset": 0.0,       # as Run -> Steyn
            "exit_to": "Follow Through", "exit_offset": 0.1, "exit_blend": blend_s,
            "exit_time": round(max(0.5, 1.0 - (blend_s * fps + 2) / max(1, frames - 1)), 4),
            "mark_forward": mark_forward, "mark_wide": mark_wide, "run_pace": 1.0, "priority": 10,
            "source": f"hand-animated in Blender ({bpy.data.filepath or 'unsaved'}), base {scene.get('cricket_base', '?')}",
        }
        for k in ("exit_time", "exit_blend", "exit_offset", "exit_to", "run_exit", "blend", "clip_offset", "priority"):
            if k in old:
                side[k] = old[k]          # tuned by hand in Unity: keep
        json.dump(side, open(side_path, "w"), indent=2)
        self.report({"INFO"}, f"Exported {name}: {frames} frames, release {release}, mark {mark_forward:+.2f} m on. "
                              "Switch to Unity; it imports and wires it.")
        return {"FINISHED"}


# ---------------------------------------------------------------- panel ---------------------------

class CRICKET_PT_panel(bpy.types.Panel):
    bl_label = "Cricket bowling"
    bl_space_type = "VIEW_3D"
    bl_region_type = "UI"
    bl_category = "Cricket"

    def draw(self, context):
        lay, scene, arm = self.layout, context.scene, rig(context)
        if arm is None:
            lay.label(text="Open BowlingAnimation.blend (build_scene.py)")
            return
        box = lay.box()
        box.label(text=f"Base: {scene.get('cricket_base', 'none')}", icon="ACTION")
        for a in base_actions():
            box.operator("cricket.start_from", text=f"Start from {a.name}").action = a.name
        ad = arm.animation_data
        if ad:
            row = box.row()
            row.label(text=f"Edits layer: {'on' if ad.action_influence > 0.5 else 'OFF'}")
            row.operator("cricket.toggle_edits", text="", icon="HIDE_OFF")
        box.operator("cricket.bake_flat", icon="NLA_PUSHDOWN")

        box = lay.box()
        box.label(text="Ghost", icon="GHOST_ENABLED")
        for a in base_actions():
            box.operator("cricket.ghost", text=f"Ghost: {a.name}").action = a.name
        box.operator("cricket.ghost", text="Show / hide ghost").action = ""

        box = lay.box()
        box.label(text="Reference video", icon="IMAGE_DATA")
        box.operator("cricket.reference", text="Add reference images…")

        box = lay.box()
        box.label(text="Markers", icon="MARKER_HLT")
        for n, label in (("back foot", "Back foot lands"), ("front foot", "Front foot lands"), ("release", "Ball leaves hand")):
            row = box.row()
            m = scene.timeline_markers.get(n)
            row.label(text=f"{label}: {m.frame if m else '-'}")
            row.operator("cricket.marker", text="", icon="MARKER_HLT").name = n

        lay.separator()
        lay.operator("cricket.export", icon="EXPORT")


CLASSES = (CRICKET_OT_start_from, CRICKET_OT_toggle_edits, CRICKET_OT_bake_flat, CRICKET_OT_ghost,
           CRICKET_OT_reference, CRICKET_OT_marker, CRICKET_OT_export, CRICKET_PT_panel)


def register():
    for c in CLASSES:
        bpy.utils.register_class(c)


def unregister():
    for c in reversed(CLASSES):
        bpy.utils.unregister_class(c)


if __name__ == "__main__":
    register()
