"""Build the hand-animation scene: Tools/mocap/handanim/BowlingAnimation.blend (not in git: rebuild it).

    /Applications/Blender.app/Contents/MacOS/Blender -b --python-exit-code 1 -P build_scene.py [-- --no-addon]

First export the game's actions from Unity: CricketVR > Bowling > Export Bowling Actions For Blender
(writes actions/Steyn.json, Bumrah.json, Lyon.json). This then makes:
  - the Bowler rig (Assets/Resources/Models/Bowler.fbx) with those actions as starting points, plus the
    captured Anderson if Tools/mocap/out/anim/anderson_smooth.blend exists (reference only: fan footage);
  - "Ghost": a see-through skeleton for comparing against another action;
  - the bowler's end of the pitch to scale (stumps, popping and return creases), placed from Steyn's
    action as it plays in the game: his front toe lands 5 cm behind the popping crease, his mark is
    2.32 m to the side of middle stump;
  - cameras: side-on from his right and from his left, from behind, front-on (from the batter's end);
  - each action's back-foot and front-foot landings found from its feet (markers "back foot", "front foot");
  - Steyn's side-on and front-on footage behind those cameras, if handanim/reference/ has it
    (make_reference.py steyn_side steyn_front);
  - the "Cricket" tab (cricket_bowling_anim/), installed as a Blender add-on unless --no-addon.
Then in Blender: Cricket tab > "Side-on | front-on views".
"""
import math
import os
import shutil
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
sys.path.insert(0, HERE)
# The installed add-on is imported at start-up under the same name: use this checkout's copy instead.
for _m in [m for m in sys.modules if m == "cricket_bowling_anim" or m.startswith("cricket_bowling_anim.")]:
    del sys.modules[_m]
import load_action  # noqa: E402
import cricket_bowling_anim as tools  # noqa: E402

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
P = "mixamorig5:"
OUT = os.path.join(HERE, "BowlingAnimation.blend")
TOE_BEHIND_CREASE = 0.05   # Steyn's front toe at the release, behind the popping crease (in play, 2026-11-13)
HAND_OFF_STUMPS = 0.32     # Steyn's bowling hand at the release, to his left of middle stump (in play: z 0.32)


def material(name, rgb):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*rgb, 1.0)
    return m


def box(name, loc, size, mat, coll):
    bpy.ops.mesh.primitive_cube_add(location=loc)
    o = bpy.context.active_object
    o.name = name
    o.scale = (size[0] / 2, size[1] / 2, size[2] / 2)
    o.data.materials.append(mat)
    for c in o.users_collection:
        c.objects.unlink(o)
    coll.objects.link(o)
    o.hide_select = True
    return o


def pitch(crease_y, stump_x):
    """The bowler's end: run along -Y; popping crease at crease_y; stumps 1.22 m further on."""
    coll = bpy.data.collections.new("Pitch")
    bpy.context.scene.collection.children.link(coll)
    white, turf, wood = material("Crease", (1, 1, 1)), material("Pitch", (0.55, 0.5, 0.35)), material("Stump", (0.85, 0.75, 0.5))
    stumps_y = crease_y - 1.22
    box("Pitch", (stump_x, stumps_y + 5, -0.005), (3.05, 30, 0.01), turf, coll)
    box("Popping crease", (stump_x, crease_y, 0.001), (3.66, 0.05, 0.002), white, coll)
    box("Bowling crease", (stump_x, stumps_y, 0.001), (2.64, 0.05, 0.002), white, coll)
    for side in (-1, 1):
        box(f"Return crease {'L' if side > 0 else 'R'}", (stump_x + side * 1.32, stumps_y + 1.22, 0.001), (0.05, 3.0, 0.002), white, coll)
    for k, dx in enumerate((-0.095, 0.0, 0.095)):
        bpy.ops.mesh.primitive_cylinder_add(radius=0.019, depth=0.711, location=(stump_x + dx, stumps_y, 0.3555))
        s = bpy.context.active_object
        s.name = f"Stump {k}"
        s.data.materials.append(wood)
        for c in s.users_collection:
            c.objects.unlink(s)
        coll.objects.link(s)
        s.hide_select = True


def camera(name, loc, look_at, lens=35):
    cam = bpy.data.objects.new(name, bpy.data.cameras.new(name))
    cam.data.lens = lens
    cam.location = loc
    d = Vector(look_at) - Vector(loc)
    cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.collection.objects.link(cam)
    return cam


def fcurves_of(act):
    """Every F-curve of an action: Blender 5 keeps them in layers / strips / channel bags."""
    if hasattr(act, "fcurves"):
        return list(act.fcurves)
    out = []
    for layer in getattr(act, "layers", []):
        for strip in layer.strips:
            for bag in getattr(strip, "channelbags", []):
                out.extend(bag.fcurves)
    return out


def anderson(arm):
    """The captured Anderson action, retimed to the scene's frame rate (reference: fan footage)."""
    src = os.path.join(ROOT, "Tools", "mocap", "out", "anim", "anderson_smooth.blend")
    if not os.path.exists(src):
        return None
    with bpy.data.libraries.load(src) as (data_from, data_to):
        data_to.actions = [a for a in data_from.actions if "anderson" in a.lower()]
    act = next((a for a in data_to.actions if a), None)
    if act is None:
        return None
    ratio = bpy.context.scene.render.fps / 50.6224
    for fc in fcurves_of(act):
        for k in fc.keyframe_points:
            k.co.x *= ratio
            k.handle_left.x *= ratio
            k.handle_right.x *= ratio
    act.name = "Anderson (captured)"
    act["cricket_base"] = True
    act["cricket_release"] = int(round(25 * ratio))
    act.use_fake_user = True
    return act


def mark_feet(arm, act):
    bfc, ffc = tools.foot_contacts(arm, act, int(act["cricket_release"]))
    act["cricket_back_foot"], act["cricket_front_foot"] = bfc, ffc
    print(f"build_scene: {act.name}: back foot lands {bfc}, front foot {ffc}, release {act['cricket_release']}")


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene["cricket_project"] = ROOT
    scene.render.fps = 30
    bpy.ops.import_scene.fbx(filepath=os.path.join(ROOT, "Assets", "Resources", "Models", "Bowler.fbx"))
    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    arm.name = "Bowler"
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)

    acts = {}
    for n in ("Steyn", "Bumrah", "Lyon"):
        path = os.path.join(HERE, "actions", n + ".json")
        if not os.path.exists(path):
            print(f"build_scene: no {path} - export it from Unity first")
            continue
        act = load_action.load(arm, path, name=n)
        act["cricket_base"] = True
        act["cricket_release"] = scene.timeline_markers["release"].frame
        act.use_fake_user = True
        acts[n] = act
    for act in list(acts.values()):
        mark_feet(arm, act)
    if "Steyn" not in acts:
        raise SystemExit("build_scene: Steyn.json is needed (it places the pitch and the mark)")
    andy = anderson(arm)
    if andy is not None:
        mark_feet(arm, andy)

    # Where Steyn's front toe is at his release: the popping crease is just in front of it.
    tools.bind(arm.animation_data, acts["Steyn"])
    rel = acts["Steyn"]["cricket_release"]
    d, wide = tools.forward_distance_to_toe(arm, rel)
    scene["cricket_steyn_toe"] = (d, wide)
    scene.frame_set(rel)
    toe = arm.matrix_world @ arm.pose.bones[P + "LeftToeBase"].tail
    hand = arm.matrix_world @ arm.pose.bones[P + "RightHand"].head
    scene.frame_set(0)
    hips0 = arm.matrix_world @ arm.pose.bones[P + "Hips"].head
    crease_y = toe.y - TOE_BEHIND_CREASE
    stump_x = hand.x - HAND_OFF_STUMPS         # his left is +X: the stumps are just to his right
    pitch(crease_y, stump_x)

    ghost = bpy.data.objects.new("Ghost", arm.data)
    ghost.matrix_world = arm.matrix_world.copy()
    ghost.show_in_front = True
    ghost.display_type = "WIRE"
    scene.collection.objects.link(ghost)
    ghost.animation_data_create()
    tools.bind(ghost.animation_data, andy or acts.get("Bumrah"))
    ghost.hide_viewport = True
    arm.data.display_type = "OCTAHEDRAL"

    camera("Cam side-on", (hips0.x - 9.0, crease_y + 2.0, 1.1), (hips0.x, crease_y + 2.0, 1.0), lens=35)       # his right
    camera("Cam side-on left", (hips0.x + 9.0, crease_y + 2.0, 1.1), (hips0.x, crease_y + 2.0, 1.0), lens=35)  # his left
    camera("Cam behind", (hips0.x, hips0.y + 7.0, 1.6), (hips0.x, crease_y, 1.0), lens=40)
    camera("Cam front-on", (stump_x, crease_y - 19.0, 1.6), (hips0.x - 0.5, crease_y, 1.2), lens=85)
    scene.camera = bpy.data.objects["Cam side-on"]

    tools.start_from(arm, acts["Steyn"], keep_edits=False)
    if all(os.path.exists(os.path.join(HERE, "reference", v, "meta.json")) for v in tools.reference.PAIRS["Steyn"]):
        for v in tools.reference.PAIRS["Steyn"]:
            obj, _ = tools.reference.load_reference(bpy.context, v)
            print(f"build_scene: reference {v} behind {obj.parent.name}, size {obj.empty_display_size:.1f}")
        scene["cricket_reference"] = "Steyn"
    scene.frame_set(0)
    bpy.ops.wm.save_as_mainfile(filepath=OUT)
    print(f"build_scene: saved {OUT}; actions {list(acts) + ([andy.name] if andy else [])}; "
          f"popping crease at y {crease_y:.2f}, middle stump x {stump_x:.2f}; Steyn toe {d:.2f} m on at his release")

    if "--no-addon" not in ARGS:
        install_addon()


def install_addon():
    """The Cricket tab as a user add-on, so it is there whenever the .blend is opened."""
    dest_dir = bpy.utils.user_resource("SCRIPTS", path="addons", create=True)
    old = os.path.join(dest_dir, "cricket_bowling_anim.py")          # the add-on before it was a package
    if os.path.exists(old):
        os.remove(old)
    dest = os.path.join(dest_dir, "cricket_bowling_anim")
    shutil.rmtree(dest, ignore_errors=True)
    shutil.copytree(os.path.join(HERE, "cricket_bowling_anim"), dest, ignore=shutil.ignore_patterns("__pycache__"))
    bpy.ops.preferences.addon_refresh()
    bpy.ops.preferences.addon_enable(module="cricket_bowling_anim")
    bpy.ops.wm.save_userpref()
    print(f"build_scene: add-on installed and enabled ({dest_dir})")


main()
