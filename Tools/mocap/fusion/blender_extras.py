"""Run after retarget_blender.py in the same Blender session: keep the result as a .blend and
render a three-view preview (side, front, three-quarter), every frame.

    Blender -b -P retarget_blender.py -P fusion/blender_extras.py -- track.json Bowler.fbx out.fbx \
        [--save-blend out.blend] [--preview-dir frames/]

Blender runs the -P scripts in order in one session, so this sees the rig with its action on it.
Track axes: X the bowler's left, -Y forward (the run), Z up.
"""
import math
import os
import sys

import bpy
from mathutils import Vector

ARGS = sys.argv[sys.argv.index("--") + 1:]
BLEND = ARGS[ARGS.index("--save-blend") + 1] if "--save-blend" in ARGS else None
PREVIEW = ARGS[ARGS.index("--preview-dir") + 1] if "--preview-dir" in ARGS else None

# (name, offset from the hips, rotation): side from his right, front from down the pitch, 3/4.
VIEWS = [("side", Vector((-5.0, 0.0, 0.0)), (math.pi / 2, 0, -math.pi / 2)),
         ("front", Vector((0.0, -5.0, 0.0)), (math.pi / 2, 0, 0)),
         ("q34", Vector((-3.5, -3.5, 0.0)), (math.pi / 2, 0, -math.pi / 4)),
         # for checking against the source clips (review.py): his left side, and from behind him
         ("lside", Vector((5.0, 0.0, 0.0)), (math.pi / 2, 0, math.pi / 2)),
         ("back", Vector((0.0, 5.0, 0.0)), (math.pi / 2, 0, math.pi))]


def armature():
    return next(o for o in bpy.data.objects if o.type == "ARMATURE")


def frame_range(arm):
    act = arm.animation_data.action
    return int(act.frame_range[0]), int(act.frame_range[1])


def save_blend(arm):
    scene = bpy.context.scene
    f0, f1 = frame_range(arm)
    scene.frame_start, scene.frame_end = f0, f1
    os.makedirs(os.path.dirname(os.path.abspath(BLEND)), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(BLEND), check_existing=False)
    print(f"extras: saved {BLEND} (frames {f0}-{f1}, action {arm.animation_data.action.name})")


def render_preview(arm):
    os.makedirs(PREVIEW, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x, scene.render.resolution_y = 480, 600
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    if "extras_ground" not in bpy.data.objects:
        bpy.ops.mesh.primitive_grid_add(x_subdivisions=40, y_subdivisions=40, size=40, location=(0, -10, 0))
        bpy.context.active_object.name = "extras_ground"
        bpy.context.active_object.display_type = "WIRE"
    cam_data = bpy.data.cameras.new("extras_cam")
    cam_data.lens = 45
    cam = bpy.data.objects.new("extras_cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    hips = arm.pose.bones["mixamorig5:Hips"]
    f0, f1 = frame_range(arm)
    for i, f in enumerate(range(f0, f1 + 1)):
        scene.frame_set(f)
        c = arm.matrix_world @ hips.head
        for name, off, rot in VIEWS:
            cam.location = Vector((c.x, c.y, 1.0)) + off
            cam.rotation_euler = rot
            scene.render.filepath = os.path.join(PREVIEW, f"{name}_{i:04d}.png")
            bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam)
    print(f"extras: preview frames {f0}-{f1} -> {PREVIEW}")


arm = armature()
if BLEND:
    save_blend(arm)
if PREVIEW:
    render_preview(arm)
