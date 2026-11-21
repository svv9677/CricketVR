# What you see in the editor is what plays

Since 2026-11-15 the game no longer builds objects, components, meshes, materials or textures in code when a scene loads. They are saved in the scenes, the prefabs and `Assets/Art/Generated/`. Open `CricketVR.unity` or `Nets.unity` and the Hierarchy shows everything that runs.

The commands under **CricketVR > Scene Objects** made those objects once. You only run one again to rebuild its object from code, for example after changing its builder. Each one writes into the same assets, so references stay. Each refuses to switch scenes while the open scene has unsaved changes.

## Where things are

| You want to change | Look at |
|---|---|
| A game system (match flow, over break, ball ledger, career match, nets trial...) | The scene's root **Systems** object, one child per system. Children saved switched off are listed on `Systems > SceneSystems` with when they run: career match, Test match and so on. |
| Where a fielder stands by default | `Main.prefab > Fielders > AFielder_0N`: drag him. `FieldCaptain` moves him between balls for each field. |
| The quick-play team names | `Assets/Resources/Data/QuickPlayTeams.asset` |
| Lighting for evening, night or the club | CricketVR's root **Lighting** (Club Sun, Evening Sun, Floodlight Key) and the two **Floodlight Glows** roots. Their sky materials are in `Art/Generated/Lighting`. |
| Pitch bounce and colour per pitch type | `Systems > Pitch Surfaces` and `Art/Generated/PitchSurfaces` |
| The Test-match look | `Systems > Test Look` and `Resources/TestLook/TestLookAssets` |
| The umpire, the partner batter, the team-mates | `Prefabs/Scene/Umpire`, `Partner` and `Batting Side`, all placed in CricketVR. Partner is saved switched off and comes on in a career match. |
| Cards (next over, result, nets trial, career match) | `Resources/Prefabs/UI/*Card.prefab` |
| Bat sounds and marks, the seamed ball | `Resources/Prefabs/Bat.prefab` and `Ball.prefab` |
| Values on `Main` that are greyed out with "(Play)" | They come from the player's settings when Play starts (the tooltip says from where). Change them in Play to try a value. |

## The commands

| Command | What it writes |
|---|---|
| Place Sight Screen, Place Ball Result Text | CricketVR root objects |
| Build Umpire | `Prefabs/Scene/Umpire.prefab` and its materials, placed in CricketVR |
| Place Partner Batter, Place Batting Side | `Partner.prefab` and `Batting Side.prefab` with kit materials, placed in CricketVR |
| Place Procedural Clouds, Place Pitch Lines | The shared `ProceduralClouds` and `Pitch Lines` prefabs, placed in both scenes. Each scene keeps its own lines mesh. |
| Build Card Prefabs, Build Nets Trial Prefabs | The card prefabs, the nets progress line and the drill marker |
| Place Replay Overlay, Place HUD Rates | The overlay under Main's replay camera, and the rates bar in CricketVR's HUD |
| Place Bat Audio And Marks, Place Seamed Ball, Place Blink Quad | `Bat.prefab`, `Ball.prefab`, and the Blink quad on the XR camera in `Controllers.prefab` |
| Place Club Ground, Build Pitch Surfaces, Build Stadium Lighting, Build Test Look | CricketVR's club ground (switched off), pitch surfaces, lighting presets and Test look |
| Bake Player Setup, Set Up Crowd Audio | The players' components, ragdoll colliders and look in `Main.prefab` and the player prefabs, and the crowd's AudioSource |

Not in this menu:
- **CricketVR > Models > Bake Fixed Bat Mesh**: `Models/BatFixed.asset`, which the bat prefabs use.
- **CricketVR > Sky > Apply Cloud Plate Import Settings**: for a new cloud texture.
- **CricketVR > Bowling > Set Up Mocap Deliveries**: after a Blender export.

None of them runs by itself.

## What still happens at runtime, on purpose

- Things made per event:
  - one-shot sounds;
  - a replay's copies of the players;
  - cards and drill markers made from their prefabs when shown;
  - clouds laid out at random each time under the placed ProceduralClouds;
  - the bowler joining the field after a hit.
- Values from the player's settings: the greyed-out fields on `Main`, the bowler's arm and mark, the keeper standing up or back for each kind of bowler.
- Device-only setup on the headset: the display refresh rate and foveation (`DisplayRate`), and frame logging (`PerfLog`).
- One Android build step (`RestoreLauncherCategory`). It does not touch assets.
- Menus hide their panels when Play starts, so you can keep them visible while editing.
- Not yet converted:
  - `ClubGround.NightCopy` still dims material copies at night.
  - The Test look still recolours the ball's leather texture in code.
  - The fielder look's gloves and helmet are moved onto their bones at start (`FielderLook.MoveHeadKit`).
