"""Cricket bowling animation tools for Blender: a "Cricket" tab in the 3D view's side panel (N).

Hand-animate the CricketVR bowler (the game's Bowler rig) and send the result straight to Unity.
Set up by Tools/mocap/handanim/build_scene.py; see Tools/mocap/handanim/README.md for the workflow.

    Start from: the game's own actions (Steyn, Bumrah, Lyon) or the captured Anderson, as a base
                layer you do not touch, with an "Edits" layer on top where your keys go.
    Ghost:      another action played by a see-through skeleton beside yours, to compare.
    Reference:  footage (make_reference.py) behind the side-on and front-on cameras, timed to the
                markers and pinned to the rig (reference.py); "Two views" shows both.
    Markers:    back foot, front foot, release - the release one is where the ball leaves.
    Export:     FBX + sidecar into the Unity project; Unity imports it, wires it into the bowling
                controller and works out his mark from where your front foot lands.
"""
bl_info = {
    "name": "Cricket bowling animation (CricketVR)",
    "author": "CricketVR",
    "version": (1, 1, 0),
    "blender": (4, 2, 0),
    "location": "View3D > Sidebar > Cricket",
    "category": "Animation",
}

import json
import os

import bpy
from mathutils import Vector

from . import reference

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

def bind(owner, act):
    """Play `act` on an AnimData or NLA strip. Blender 5 also needs the action's slot: an action
    brought in from another file is not bound to one, and then nothing moves."""
    if owner.action != act:
        owner.action = act
    if act is not None and hasattr(owner, "action_slot") and owner.action_slot is None and len(act.slots):
        suitable = list(getattr(owner, "action_suitable_slots", [])) or list(act.slots)
        owner.action_slot = suitable[0]


def start_from(arm, base, keep_edits=True):
    """Base action as a locked NLA strip, an additive 'Edits' action on top for the user's keys."""
    ad = arm.animation_data or arm.animation_data_create()
    edits = ad.action if (keep_edits and ad.action and ad.action.get("cricket_edits")) else None
    for t in list(ad.nla_tracks):
        ad.nla_tracks.remove(t)
    track = ad.nla_tracks.new()
    track.name = "Base (do not edit)"
    strip = track.strips.new(base.name, int(base.frame_range[0]), base)
    bind(strip, base)
    strip.blend_type = "REPLACE"
    track.lock = True
    if edits is None:
        edits = bpy.data.actions.new(f"Edits_{base.name}")
        edits["cricket_edits"] = True
    bind(ad, edits)
    ad.action_blend_type = "COMBINE"      # rotations multiply onto the base (quaternions stay unit)
    ad.action_influence = 1.0
    scene = bpy.context.scene
    scene.frame_start, scene.frame_end = int(base.frame_range[0]), int(base.frame_range[1])
    scene["cricket_base"] = base.name
    for name, key in (("back foot", "cricket_back_foot"), ("front foot", "cricket_front_foot"), ("release", "cricket_release")):
        f = base.get(key)
        if f is not None and f >= 0:
            m = scene.timeline_markers.get(name) or scene.timeline_markers.new(name)
            m.frame = int(f)
    reference.update(scene)
    return edits


def foot_contacts(arm, act, release, front="Left"):
    """(back foot landing, front foot landing) frames of a right-armer's action (front = his left foot):
    the first frame of the front foot's last stretch on the ground at or before the release, and the
    first frame of the back foot's stretch on the ground that began before that."""
    scene = bpy.context.scene
    ad = arm.animation_data
    keep_action, keep_tracks, keep_frame = ad.action, [t.mute for t in ad.nla_tracks], scene.frame_current
    for t in ad.nla_tracks:
        t.mute = True
    bind(ad, act)
    back = "Right" if front == "Left" else "Left"
    lo, hi = int(act.frame_range[0]), int(act.frame_range[1])
    z = {front: [], back: []}
    for f in range(lo, hi + 1):
        scene.frame_set(f)
        for side in z:
            pb = arm.pose.bones
            toe = arm.matrix_world @ pb[P + side + "ToeBase"].tail
            ankle = arm.matrix_world @ pb[P + side + "Foot"].head
            z[side].append(min(toe.z, ankle.z))
    ad.action = keep_action
    for t, m in zip(ad.nla_tracks, keep_tracks):
        t.mute = m
    scene.frame_set(keep_frame)
    ground = min(min(v) for v in z.values())
    # On the ground: low. (Not "and still": the game's own actions slide their planted feet at 1-4 m/s.)
    down = {s: [h < ground + 0.06 for h in v] for s in z for v in [z[s]]}

    def run_start(flags, at):
        while at > 0 and flags[at - 1]:
            at -= 1
        return at

    r = min(max(release - lo, 0), hi - lo)
    k = r
    while k > 0 and not down[front][k]:
        k -= 1
    ffc = run_start(down[front], k)
    k = ffc
    while k > 0 and not down[back][k]:
        k -= 1
    bfc = run_start(down[back], k) if down[back][k] else -1
    return (bfc + lo if bfc >= 0 else -1), ffc + lo


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
        bind(g.animation_data, bpy.data.actions[self.action])
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
        reference.update(scene)          # the footage is timed to the markers
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

        reference.draw(lay.box(), context)

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
           CRICKET_OT_marker, CRICKET_OT_export, CRICKET_PT_panel)


def register():
    for c in CLASSES:
        bpy.utils.register_class(c)
    reference.register()


def unregister():
    reference.unregister()
    for c in reversed(CLASSES):
        bpy.utils.unregister_class(c)


if __name__ == "__main__":
    register()
