# Opening Menu — Design

**Date:** 2026-09-27
**Status:** Approved, ready for implementation planning

An opening menu that makes Splash the game's front door, with a pause menu in CricketVR and
Nets. Every piece is a prefab built by an editor tool, reused across all three scenes.

---

## 1. Goals

- Splash becomes the start scene and houses the main menu.
- Main menu offers: Quick Match (loads CricketVR), Practice in Nets (loads Nets), Settings.
- Quick Match runs a 5-over game by default, with the overs count adjustable on the button itself.
- CricketVR and Nets gain a pause menu leading to Settings and back to the main menu.
- The menu reads as vibrant and "big match" rather than as the current dark utility panels.

### Non-goals

- No change to ball physics, fielding, bowling profiles or the between-balls flow.
- No save slots, profiles, statistics or progression.
- No restyle of the existing in-match panels (`NextBallMenu`, `GripCalibrationPanel`,
  `ShotCard`, `ReplayControls`). They keep their current appearance.

---

## 2. Architecture

One `MenuRoot` prefab hosts a stack of interchangeable `MenuScreen` prefabs. `MenuRoot` absorbs
the boilerplate that `SettingsPanel` and `NextBallMenu` currently duplicate: world-space canvas,
raycasters, head-relative placement, `GrabbablePanel` restore, and registration with
`OpenXRMenuInputModule`. Screens know nothing about placement or input.

```
MenuRoot  (prefab, in all three scenes)
├── MenuBackdrop        curved quad + floodlit plate; enabled in Splash only
└── Screens             exactly one active at a time
    ├── MainMenuScreen  Splash only
    ├── PauseScreen     CricketVR and Nets only
    └── SettingsScreen  all three scenes, identical prefab
```

`MenuRoot` exposes `Push(screen)`, `Pop()` and `Root` — so Back works uniformly and Settings needs
no knowledge of who opened it. Splash starts with `MainMenuScreen` pushed and the backdrop shown;
CricketVR and Nets start with `PauseScreen` as root, the whole `MenuRoot` inactive until `B`.

### Why a stack rather than a router

A router keyed on "who opened Settings" produces one branch per caller and a second copy of the
placement code in every panel. The stack makes Settings context-free, which is what lets the same
prefab serve all three scenes — the requirement that motivated this work.

---

## 3. Components

### New runtime

| Type | Responsibility |
|---|---|
| `MenuRoot` | Screen stack, head-relative placement, input registration, grab-to-move. |
| `MenuScreen` | Base: `OnShow`/`OnHide`, a `Title`, and whether Back is available. |
| `MainMenuScreen` | Quick Match (with overs steppers), Practice in Nets, Settings. |
| `PauseScreen` | Resume, Settings, Main Menu. Owns the mid-match confirm. |
| `MenuBackdrop` | Displays a baked plate; enabled per scene. |
| `SceneFader` | Fade to black, `LoadSceneAsync`, fade in. |
| `GameSettings` | Static, PlayerPrefs-backed settings model. See §4. |

### New editor

| Tool | Responsibility |
|---|---|
| `MenuPrefabBuilder` | Builds `MenuRoot` and the three screens in the `UIStyle` look. |
| `SplashSceneBuilder` | Populates Splash: XR rig, pointer, EventSystem, `MenuRoot`. |
| `BackdropBaker` | Captures clean plates from CricketVR and applies the floodlit grade. |

All follow the existing convention: prefabs under `Assets/Resources/Prefabs/UI`, controls wired
with persistent listeners, **nothing created or hooked up at runtime**.

### Refactored

- **`SettingsPanel` → `SettingsScreen`.** Handlers write to `GameSettings` instead of calling
  `Main.Instance`. Match-only rows hide when there is no `Main`.
- **`Main`.** Reads tunables from `GameSettings` on `Start` and on `Changed` rather than owning
  them. `B` opens `PauseScreen` instead of `ToggleUI(!menuToggle)`. `HUD.Reset` takes its overs
  count from `GameSettings`.

### Untouched

`NextBallMenu`, `GripCalibrationPanel`, `ShotCard`, `ReplayControls`, ball physics, fielding,
bowling profiles, `UIControls`, `WorldPanelBuilder`.

---

## 4. `GameSettings`

`Main.Start` (Main.cs:234–254) already reads every persisted setting directly from `PlayerPrefs`.
This is consolidation, not invention: those reads move into `GameSettings`, a plain static class
over the existing `Constants.PP_*` keys plus a new `PP_Overs`.

It exposes typed properties — `BattingStyle`, `Difficulty`, `BatPower`, `FielderSpeed`,
`OverlayVisible`, `BowlerType`, `Overs` — writes through to `PlayerPrefs` on set, and raises
`Changed`. `SettingsScreen` talks only to `GameSettings`; `Main` subscribes and applies. Having no
`MonoBehaviour` dependency, it is unit-testable in the existing EditMode suite.

`Overs` defaults to 5, matching `HUD.Reset`'s current default, and is what carries the Quick Match
stepper value into CricketVR.

### Which settings appear where

The bowling **ranges** — `MinX/MaxX`, swing, turn, line — are not PlayerPrefs. They come from
`reloadTweakables()` per bowler type out of `Constants.paceCfg` and siblings, and live in
`BowlingProfileManager`. There is no meaningful value for them in Splash, where no profile is
loaded.

The dividing line: **pre-match you choose what you will face; in-match you tune how it behaves.**

| Section | Splash | In match |
|---|---|---|
| Batting — hand, difficulty, bat power | yes | yes |
| Fielding — fielder speed | yes | yes |
| Match — overs | yes | read-only |
| Bowling — bowler type | yes | yes |
| Bowling — speed / swing / turn / line ranges, Reset Bowling | no | yes |
| Grip calibration, Change bowler, Just Restart, debug overlay | no | yes |

Rows absent from Splash are hidden, not disabled: a greyed control the player can never reach in
that context is noise.

**Deliberately excluded:** persisting per-type range overrides in `GameSettings` so the full
bowling section could work in Splash. It conflicts with the existing "Reset bowling returns to
this type's built-in values" semantics, and the pre-match value is low. Revisit only if asked.

---

## 5. Scene wiring

### Build settings

`EditorBuildSettings` becomes:

| Index | Scene | Enabled |
|---|---|---|
| 0 | `Assets/Scenes/Splash.unity` | yes |
| 1 | `Assets/Scenes/CricketVR.unity` | yes |
| 2 | `Assets/Scenes/Nets.unity` | yes |

Existing disabled Oculus sample entries stay disabled. Splash must be index 0 so it is the
launch scene.

### Splash

Splash currently holds only a camera and a directional light. `SplashSceneBuilder` adds:

- An XR rig with `XRRigSetup`, so eye height is floor-relative as in the other scenes.
- The `UIHelpers` pointer, `EventSystem`, `InputSystemUIInputModule` and
  `OpenXRMenuInputModule` — by calling `CricketVRSceneBuilder.SetUpPointer` rather than
  duplicating it.
- `MenuRoot`, with `MainMenuScreen` as the root screen and the backdrop shown.

No `Main`, no bat, no pitch. `GameSettings` is standalone, so the settings screen works without
gameplay objects present.

### Transitions

All scene loads go through `SceneFader`: fade to black, `LoadSceneAsync`, fade in. A hard cut
between VR scenes is unpleasant, and the fade also covers the one-frame pop while `XRRigSetup`
finds its floor origin.

### Pause

`Main`'s `B` handler (Main.cs:942) changes from `ToggleUI(!menuToggle)` to opening `PauseScreen`.
`A` still bowls; `X` keeps its temporary reset-to-ready.

Pause sets `Time.timeScale = 0`. This freezes an in-flight ball mid-air rather than letting it
complete — accepted deliberately, as it is the correct behaviour for a paused match.

`Main Menu` from Pause asks to confirm in CricketVR, since the score is lost. Nets uses the same
prefab without the confirm: there is nothing to lose.

---

## 6. Backdrops

Direction chosen: **balanced floodlit grade** — a real long-shot render of the project's own
`ModiStadium` at roughly 45% under a 55% vibrant gradient. The ground stays legible as real
geometry and crowd texture, while the night palette carries the mood.

`BackdropBaker` is an editor tool that:

1. Opens CricketVR and hides the HUD, players and debug boards, so plates are clean.
2. Parks a camera at three long-shot vantage points — high behind the bowler's arm, square of
   the wicket, deep midwicket.
3. Captures at 2048×1024.
4. Applies the floodlit grade: indigo → magenta → coral gradient, floodlight bloom, side
   vignette sized to seat the menu card.
5. Writes to `Assets/Resources/UI/Backdrops`, ASTC-compressed and mipped for Quest.

Main Menu, Settings and Pause each use a different vantage point, so the set reads as one ground
seen from three places rather than one repeated image. Regenerable whenever the stadium changes.

`Docs/AssetSources.md` gains a row recording these as tier 2, self-authored, no third-party
licence.

### Palette

`UIStyle` gains the vibrant accents as **additional** named colours — magenta primary, coral
secondary, alongside the existing blue. Every existing value keeps its current definition, so no
in-match panel changes appearance unless deliberately restyled. The near-opaque dark card
(`Card`, alpha 0.97) is retained: it is what keeps menu text legible over a bright backdrop, and
the mockups confirmed it reads well against the floodlit grade.

---

## 7. Testing

**EditMode unit tests** (plain C#, no editor fixtures needed):

- `GameSettings` — defaults, round-tripping through `PlayerPrefs`, `Changed` firing exactly once
  per real change and not at all on a no-op set.
- Menu stack — push/pop ordering, Back availability at the root, and that popping the last screen
  closes `MenuRoot` rather than leaving an empty canvas.

**Build verification**, per this project's established practice: headless Roslyn player-view
compile plus a GUID reference scan, to catch broken serialized references that a clean compile
would not.

**Editor checks:** each scene opened and its `MenuRoot` exercised; a reference scan confirming the
new prefabs resolve and the four known pre-existing dangling `m_Script` GUIDs are still the only
ones.

**Device:** a Quest build is the final gate. The laser pointer, the `B` button, `Time.timeScale`
behaviour under pause, and the ASTC backdrop textures are only truly proven on device. If
something misbehaves there, read Unity's log over USB with `adb logcat` rather than the in-world
debug board.

---

## 8. Risks

| Risk | Mitigation |
|---|---|
| The `Main` refactor regresses live tuning. `Main` uses a shadow-field pattern (`_fielderSpeed = -1000` to force first update) that is easy to break. | Move settings one section at a time, preserving the shadow-field idiom rather than replacing it. Verify each on device. |
| `Time.timeScale = 0` interacts badly with coroutines in `Main`'s delivery loop, which uses `WaitForSeconds` and a per-frame state machine. | `B` opens Pause from any state, so every state must survive it. Test resuming from each of `InGame_Ready`, mid-run-up and mid-flight, confirming the delivery completes correctly rather than stalling or teleporting the ball. |
| Backdrop textures push Quest memory. | 2048×1024 ASTC, three plates, loaded only in the scene that shows them; `MenuBackdrop` is disabled in match scenes. |
| Splash has never been built or run — unknown gaps in its scene setup. | `SplashSceneBuilder` reuses the proven `CricketVRSceneBuilder` pointer/rig path rather than new code. |

---

## 9. Decisions

| Decision | Choice |
|---|---|
| Settings reuse across scenes | Extract a `GameSettings` model; one `SettingsScreen` prefab everywhere |
| Backdrop source | Real `ModiStadium` render under a vibrant gradient, balanced floodlit grade |
| Pause entry | `B` opens Pause; Settings becomes a sub-screen of Pause |
| Overs | Chosen inline on the Quick Match button with `‹ ›` steppers, default 5 |
| Menu architecture | `MenuRoot` + screen stack |
| Bowling ranges in Splash | Hidden — in-match tuning only |
| Pause and time | `Time.timeScale = 0`, freezing an in-flight ball |
