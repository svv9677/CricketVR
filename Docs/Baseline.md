# CricketVR Scene Realism Baseline

Editor-side measurements only. **On-device measurement is deferred** — see Phase D of
[the implementation plan](Plans/2026-09-21-scene-realism.md) for the checks that still need
running on a Quest 3.

Captured 2026-09-21 against `Assets/Scenes/CricketVR.unity`.

## Editor stats per phase

Snapshots live in `Docs/Baseline/stats-<phase>.txt`, produced by CricketVR → Dump Scene Stats.

| Metric | Phase 0.5 baseline |
|---|---|
| Render pipeline | **Built-in** (URP package installed, pipeline not yet switched) |
| MeshRenderers | 25 |
| SkinnedMeshRenderers | 61 |
| Triangles (sum) | 292,570 |
| Unique materials | 32 |
| **Materials w/ normal map** | **0** |
| Lights (active) | 5 |
| Reflection probes | **0** |
| Light probe groups | **0** |
| Lightmaps | **0** |
| Static (ContributeGI) | **4 of 25** |
| Ambient mode | **Flat** |
| Skybox material | **`<NONE>`** |
| Sun assigned | **no** |
| Fog | False |

**On the light count:** the scene file contains 7 lights, but 2 sit inside the inactive
`Day Lights` group, so only 5 are live. The scene ships in **night mode**.

## Reference screenshots

`Docs/Baseline/phase0-{batsman,midpitch,boundary}.png`, 1920×1080, produced by
CricketVR → Capture Reference Shots. Framing is frozen in `Assets/Editor/ReferenceShots.cs`
and must not change.

| Shot | Position | Looks at | FOV | Purpose |
|---|---|---|---|---|
| batsman | (11.5, 1.7, 0) | (-9, 1.2, 0) | 90° | Closest flat approximation of the player's view |
| midpitch | (1, 1.3, 4) | (6, 0.3, 0) | 70° | Pitch surface, creases, stumps — judges turf and pitch materials |
| boundary | (-55, 9, 22) | (5, 1, 0) | 65° | Whole ground — judges stands, crowd and sky |

`phase0-sceneview.png` is an additional Scene View capture taken before the URP package was
installed, kept as the untouched Built-in reference.

**What the baseline shots show:** pure white sky (the dangling skybox, rendered), zero shadows
anywhere, flat uniform outfield, untextured block stands, entirely empty seating.

## Lightmap memory

| Phase | Baked set size on disk |
|---|---|
| 0.5 | 0 (nothing baked) |

## Decisions recorded

| Question | Decision | Evidence |
|---|---|---|
| FullStadium UV0 usable? | **UNUSABLE for the meshes that matter** — see below | task 5 |
| Tonemapper: ACES or Neutral? | *pending* — **DEFERRED to Phase D**, must be judged in the headset. Ship Neutral until then | task 25 |
| APV or hand-placed probes? | *pending* | task 20 spike |

---

## Task 5 — FullStadium UV0 verdict: **UNUSABLE**

Verified twice, independently: analysed in Blender (per-face UV area vs world area) and then
confirmed against the live meshes in Unity. Both agree.

### Stadium meshes as they actually are

| Mesh | Triangles | UV0 | Active in scene |
|---|---|---|---|
| **Stand1** | 43,630 | **NONE** | **yes** — this is the visible stadium |
| Boundry | 512 | NONE | yes |
| Plane | 2 | NONE | yes |
| Inner Circle | 40 | yes | yes |
| Outer Circle | 162 | yes | yes |
| **Cube.001** | **1,735,672** | yes | **no** |
| Sidebar | 107,384 | NONE | no |
| Sidebar.001 | 10,104 | NONE | no |
| 30Yrds ×2 | 6,656 each | NONE | no |
| Cylinder | 2,108 | NONE | no |
| Cylinder.001 | 496 | NONE | no |

Active stadium geometry is **5 renderers, 44,346 triangles**.

### What this means

**Phase 4 (PBR materials) is blocked on the stadium and needs a Blender re-UV.** `Stand1` carries
essentially the whole visible structure and has no texture coordinates at all. Its current
checkerboard appearance must come from vertex colours or a UV-independent material, not from a
texture map — so there is nothing to attach an albedo or normal map to.

The good news is scale: at **43,630 triangles** `Stand1` is very tractable for a Smart UV Project
in Blender. This is tier 2 of the sourcing waterfall and is a bounded job, not a rebuild.

**Phase 2 (lightmapping) is NOT blocked.** Unity's `generateSecondaryUV` builds UV2 from mesh
topology and does not require an existing UV0. The bake can proceed before any re-UV work.

### Separate finding: 1.74 million disabled triangles

`Cube.001` is **1,735,672 triangles** and disabled in the scene. It is the bulk of the 44 MB
`FullStadium.fbx`, and it ships in the build regardless because everything under
`Assets/Resources/` is force-included. Deleting it from the source model is a large, cheap win.

Filed against phase 7 (task 29, LODs / mesh cleanup), not phase 2 — it costs nothing at runtime
today since it is disabled, but it costs download size and import time on every reimport.
