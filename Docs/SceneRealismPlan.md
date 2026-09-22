# CricketVR — Scene Realism Plan

**Goal:** an ultra-realistic look for `Assets/Scenes/CricketVR.unity`, running on **Meta Quest 3
standalone**.

Prepared 2026-09-21 against commit `046d910` (master). Companion to
[RevivalPlan.md](RevivalPlan.md) — and see **Amendments to RevivalPlan** below, because this plan
reverses one of its decisions.

**Status: design approved. Implementation plan written — see
[Plans/2026-09-21-scene-realism.md](Plans/2026-09-21-scene-realism.md). No project files changed yet.**

---

## Decisions taken

| Decision | Choice | Rationale |
|---|---|---|
| Performance target | **Quest 3 standalone only** | No PC VR uplift path. Hard mobile-GPU ceiling: no real-time GI, no SSR, effectively one shadow-casting light. |
| Art budget | **Purchased / CC0 PBR sets + HDRI skies** | Highest realism per hour. Existing project has 9 texture files and zero normal maps; authoring from scratch is not the bottleneck worth paying. |
| Render pipeline | **Migrate to URP 17 first, then relight** | The lightmap bake is the expensive artifact; produce it once, in the final pipeline. |
| Adaptive Probe Volumes | **Not baseline. Upgrade gated on a measured spike.** | APV is production-ready in URP 17 and Unity shipped a per-vertex quality mode aimed at mobile VR, but Quest-specific behaviour is unconfirmed. Hand-placed light probe groups are the baseline. |
| Scope | **All phases 0–7** | Confirmed by Rao. |
| Unity Editor MCP | **Yes — research and install** | Runs as a parallel track in phase 0.5. |

---

## Baseline findings (verified 2026-09-21)

Read directly from the project files. These are the facts the plan is built on.

### Engine and platform config — already correct, no action

| Setting | Value | Source |
|---|---|---|
| Unity | **6000.3.24f1** | `ProjectSettings/ProjectVersion.txt` |
| Color space | **Linear** (`m_ActiveColorSpace: 1`) | `ProjectSettings.asset:53` |
| Android graphics API | **Vulkan only** (`m_APIs: 0b000000`) | `ProjectSettings.asset:477` |
| Stereo mode (Android) | **Multiview** (`m_StereoRenderingModeAndroid: 2`) | `Assets/XR/Settings/Oculus Settings.asset` |
| GPU skinning | On (`gpuSkinning: 1`) | `ProjectSettings.asset:108` |
| Mobile rendering path | Forward (`m_MobileRenderingPath: 1`) | `ProjectSettings.asset:825` |
| XR loader | OpenXR (Oculus loader retained but inactive) | `Assets/XR/Loaders/` |

The foundation is sound. Nothing here is why the scene looks unrealistic.

### What is actually wrong

| Finding | Evidence |
|---|---|
| **No render pipeline package.** Built-in RP; `URPProjectSettings.asset` exists but references no pipeline asset. | `Packages/manifest.json` — no `com.unity.render-pipelines.*` |
| **No baked lighting at all.** Nothing is lightmapped. | `m_LightingDataAsset: {fileID: 0}` — `CricketVR.unity:101` |
| **No lightmap UVs on any model.** Nothing *can* bake today. | `generateSecondaryUV: 0` on all 11 `.fbx.meta` in `Assets/Resources/Models/` |
| **Zero reflection probes. Zero light probe groups.** | scene scan of `CricketVR.unity` |
| **Zero normal maps across all ~60 materials.** | material scan: no `_BumpMap` with an assigned texture |
| **Ambient is flat grey, not sky-derived.** | `m_AmbientMode: 3`, `m_AmbientSkyColor: 0.585` grey — `CricketVR.unity:24,28` |
| **No sun assigned to RenderSettings.** | `m_Sun: {fileID: 0}` — `CricketVR.unity:41` |
| **Fog off.** | `m_Fog: 0` — `CricketVR.unity:18` |
| **Baked AO off.** | `m_AO: 0` — `CricketVR.unity:62` |
| **Default reflection resolution 128.** | `m_DefaultReflectionResolution: 128` — `CricketVR.unity:37` |
| **🐞 All THREE skybox references are dangling, and day/night is broken at runtime.** The P2 purge deleted the Oculus SampleFramework skybox materials. `m_SkyboxMaterial` in both `CricketVR.unity:30` and `Nets.unity` → `SkyboxForLightbaking.mat` (`b95594ff…`, gone). `Main.NightMaterial` (`CricketVR.unity:11167`) → same, gone. `Main.DayMaterial` (`CricketVR.unity:11168`) → `SkyboxForRealtime.mat` (`799432b5…`, gone). Since `Main.updateStadiumMode()` assigns `RenderSettings.skybox = NightMaterial` at `Main.cs:582` and `= DayMaterial` at `:590`, **every day/night switch currently sets the skybox to null**. | `git log --diff-filter=D` + GUID search |
| **🐞 The stadium geometry is NOT lightmap-static.** 17 of 32 scene objects carry full static flags, but those are lights, colliders and wrapper transforms. `Stadium.prefab` has **zero** `m_StaticEditorFlags` overrides, so the FullStadium renderers nested inside it default to non-static — Unity does not propagate the root's flags into a nested model-prefab instance. Nothing would bake even with lightmap UVs present. Also carries 12 `m_IsActive` and 3 `m_CastShadows` overrides worth auditing. | `m_StaticEditorFlags` distribution + `Stadium.prefab` scan |
| Lights: 2 directional (intensity 1, soft shadows) + 5 spot floodlights (intensity 4, **no shadows**). | scene scan |
| `Assets/Resources/` is 426 MB and force-included. Models 176 MB, Animations 136 MB, Textures 111 MB. `FullStadium.fbx` 44 MB, `Bowler.fbx` 50 MB, `David.fbx` 48 MB, `Ball.fbx` 27 MB. | `du` |
| Only 9 texture files + 3 character texture folders exist. | `Assets/Resources/Textures/` |
| Foveated rendering subsampled layout disabled. | `enableSubsampledLayout: 0` — `OpenXR Package Settings.asset` |
| Fixed Timestep **0.007 s (~143 Hz)** — unusually high, CPU-expensive. | `TimeManager.asset:6` |

### Materials and shaders

49 of ~60 materials reference the Built-in **Standard** shader (`fileID: 46`), which Unity 6.3's
Render Pipeline Converter handles mechanically.

The four non-stock shaders are **all vendor unlit/transparent**, which do not depend on the lighting
model and therefore carry low migration risk:

| GUID | Shader |
|---|---|
| `b95caf64…` | `Assets/Oculus/VR/Shaders/OVRColorRampAlpha.shader` |
| `38ad33c1…` | `Assets/Oculus/VR/Resources/OVRMRUnlitTransparent.shader` |
| `05b53b47…` | `Assets/Oculus/VR/Shaders/Unlit Crosshair.shader` |
| `fe393ace…` | `Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile.shader` |

---

## Ordering principle

**Lighting before texturing.** Good lighting on untextured geometry reads better than flat lighting
on good textures — and materials authored under wrong lighting will be authored wrong. This is why
phase 4 sits after phases 2 and 3.

---

## Phases

Each phase ends at a gate. Do not start the next phase until its gate passes.

### Phase 0 — Baseline and measurement

Nothing in this plan is verifiable until the project builds to a device. RevivalPlan records that
**no APK has ever been produced**.

1. Branch off `master`.
2. Close out the outstanding RevivalPlan P1 items: `AndroidTargetSdkVersion` 0 → **34**,
   `AndroidMinSdkVersion` 25 → **32**.
3. Produce a working APK and run it on the Quest 3.
4. Record with OVR Metrics Tool: FPS, GPU ms, CPU ms, draw calls, triangle count.
5. Capture reference screenshots from **three fixed camera positions** — batsman stance, mid-pitch,
   boundary. These are the before/after control for every later phase.
6. **Verify `FullStadium.fbx` UV0 is usable.** If the source UVs are unusable, phase 4 requires a
   Blender re-UV first. Checking now rather than discovering it at phase 4 is the entire point.

**Gate:** APK runs on device; baseline numbers and three screenshots recorded.

### Phase 0.5 — Unity Editor MCP (parallel track)

Research which community Unity Editor MCP server is currently maintained, confirm what it exposes
(scene graph, component edits, menu items, console, play mode), and install it.

Not a blocker for phases 0–2, which are largely file surgery and asset acquisition. It becomes the
bottleneck from phase 3 onward, where the work is iterative and visual.

**Gate:** MCP connected and able to read the scene hierarchy, or an explicit decision to proceed
without it.

### Phase 1 — URP migration

1. Add `com.unity.render-pipelines.universal` (URP 17).
2. Create a mobile-tuned URP Asset + Renderer: **Forward**, **MSAA 4x**, depth texture off, opaque
   texture off, minimal shadow cascades, HDR on (measure the bandwidth cost — tonemapping in
   phase 5 depends on it).
3. Run **Window → Rendering → Render Pipeline Converter** (Built-in → URP) over the 49 Standard
   materials.
4. Verify the four vendor unlit shaders still render; hand-port only if they do not.
5. Re-check ball-trail particle systems and TMP materials.
6. Rebuild to device.

**Gate:** APK runs; framerate at or above the phase-0 baseline.

### Phase 2 — Baked lighting *(the single biggest win)*

1. Set `generateSecondaryUV: 1` on **static geometry only** — `FullStadium`, `Pitch`, `Stump`,
   `BowlingMachine`. **Not** the skinned characters (`Bowler`, `David`, `Animated Fielder`,
   `Fielder`). Tune hard angle and pack margin per model.
2. Audit the 15 unflagged scene objects and set Contribute GI where appropriate.
3. Create a LightingSettings asset: Progressive GPU, 2 bounces, **AO on** (currently off),
   denoising on.
4. Set the sun directional light to **Mixed** with **Shadowmask**: static geometry gets baked
   shadows, dynamic objects (batsman, bowler, ball, fielders) get real-time.
5. Budget lightmap resolution via per-object scale — high on the pitch and near geometry, very low
   on distant stands.
6. Bake.

**Gate:** visible improvement at the three reference cameras; total lightmap set fits Quest texture
memory; framerate held.

### Phase 3 — Environment and ambient

1. **Fix the dangling skybox reference** (see findings). Author or import a real HDRI sky — day and
   night variants. Poly Haven is CC0 and has suitable skies. Fix `Nets.unity` too.
2. `m_AmbientMode: 3` → **0** (skybox-derived ambient).
3. Assign `m_Sun` to the primary directional light.
4. Reflection probes: one large **box-projected** probe covering the bowl at **256**, smaller
   box-projected probes under the stands and around the pitch.
5. Light probe lattice over the play area so dynamic objects pick up bounce off the turf. This is
   the APV baseline; APV itself remains a gated upgrade.
6. Enable subtle exponential fog. Across a 150 m ground, aerial perspective is a large and nearly
   free depth cue.

**Gate:** bat and ball visibly pick up green bounce light from the outfield.

### Phase 4 — PBR materials

1. Acquire CC0 or purchased PBR sets (ambientCG, Poly Haven): turf, pitch soil, concrete, seating
   plastic, painted steel, willow, leather, cricket whites, advertising boards.
2. **Channel-pack ORM** (occlusion / roughness / metallic) into one texture per material — mobile
   bandwidth.
3. ASTC compression, mipmaps, anisotropic 2–4 on ground planes.
4. Author smoothness and metallic properly per material.
5. **Outfield, specifically:** base albedo + a **detail normal at a second tiling rate** + a
   **mown-stripe mask**. Stripes are the most recognisable "real cricket ground" cue and cost one
   texture.
6. **Pitch, specifically:** a wear mask scuffing and darkening the good-length area.

**Gate:** side-by-side improvement at the three reference cameras; texture memory within budget.

### Phase 5 — Grading

1. Global Volume with: Tonemapping, Color Adjustments, White Balance, Bloom (high threshold, low
   intensity).
2. **Test ACES vs Neutral tonemapping in the headset** — ACES can crush shadows on Quest panels.
   This is a device decision, not a monitor decision.
3. Day and night grading profiles, wired to the existing day/night setting in `Main.cs`.
4. **Explicitly excluded:** motion blur, depth of field, strong vignette, chromatic aberration, lens
   distortion. All are either nausea-inducing or wrong in VR.

**Gate:** post pass cost measured in GPU ms on device, not assumed free.

### Phase 6 — Crowd and life

Empty stands read as fake instantly.

1. GPU-instanced animated impostor crowd — texture-sheet animated quads.
2. A handful of higher-detail figures near the boundary where the player looks most.
3. Flags and bunting with cheap vertex-shader wind.

**Gate:** crowd added within the draw-call headroom measured in phase 0; framerate held.

### Phase 7 — Performance reclaim and VR polish

1. Decimate `FullStadium.fbx` (44 MB) and add LOD groups to the stands.
2. Move content out of `Assets/Resources/` — 426 MB force-included (already RevivalPlan P4; zero
   `Resources.Load` calls exist in first-party code).
3. Enable foveated rendering; `enableSubsampledLayout: 0` → **1**.
4. GPU instancing on repeated seating geometry.
5. Rebake occlusion culling — the existing data will be stale after geometry changes.
6. Revisit Fixed Timestep 0.007 s. 143 Hz physics is expensive and may be starving the render
   thread. **Gameplay-affecting** — `Bat.cs` does manual collision resolution and shot power is
   already frame-rate dependent (RevivalPlan P3). Change only with a gameplay regression test.

**Gate:** hold 72 Hz on Quest 3 with headroom.

---

## Risks

**Stadium UVs (high impact, checked in phase 0).** If `FullStadium.fbx` has unusable UV0, phase 4
requires a Blender re-UV pass first. This is why the check is in phase 0 and not phase 4.

**Lightmap memory (high likelihood).** A cricket stadium is spatially enormous and Quest texture
memory is not. This is the most likely place the plan must compromise. Mitigation: aggressive
per-object lightmap scale; accept low-resolution bakes on distant stands.

**Scope.** Phases 0–3 deliver most of the perceptual gain. Phases 4–6 are where the hours are. If
time runs short, stopping after phase 3 is a legitimate outcome.

**Physics regression in phase 7.** Item 6 touches gameplay balance, not just performance. It is
listed last deliberately and may be dropped.

---

## Amendments to RevivalPlan.md

This plan changes two things RevivalPlan currently states:

1. **URP is promoted from "Stage 3 — still deferred" to phase 1 here.** RevivalPlan deferred it on
   the grounds that converting 47 materials would cause visual regressions for no gameplay benefit.
   That reasoning holds for a *revival*; it does not hold for a *realism* goal, where the lightmap
   bake is the expensive artifact and must be produced in its final pipeline. The migration is also
   cheaper than RevivalPlan assumed: 49 of ~60 materials are stock Standard, and all four non-stock
   shaders are vendor unlit.
2. **RevivalPlan's P2 asset purge introduced a regression** — it deleted both Oculus skybox
   materials, leaving four dangling references across two scenes and breaking the runtime
   day/night sky switch entirely. Recorded in the findings above; fixed in phase 3, task 18.

RevivalPlan's outstanding P1 items (Android SDK levels, device build) are absorbed into phase 0 as
prerequisites.
