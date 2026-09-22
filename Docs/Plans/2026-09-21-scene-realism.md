# CricketVR Scene Realism Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give `Assets/Scenes/CricketVR.unity` an ultra-realistic look, targeting Meta Quest 3 standalone.

**Architecture:** Set up Unity and Blender MCP tooling first so the agent can work in the Editor directly. Then migrate Built-in RP → URP 17 so the lightmap bake is produced once in its final pipeline. Then build realism in dependency order: baked lighting → environment/ambient → PBR materials → grading → crowd → performance reclaim.

**Tech Stack:** Unity 6000.3.24f1 · URP 17 · OpenXR + Multiview · Vulkan · Linear color space · Quest 3 (Android, arm64) · Blender · C# editor tooling under `Assets/Editor/`

---

## Operating constraints

These four were set by Rao and override the defaults this plan would otherwise assume.

**1. No branching.** All work happens on `master`.

**2. Commits are Rao's.** Every task ends with a commit step showing the exact command. **Do not run them** — they are there so Rao can commit at a sensible boundary.

**3. No device builds in this plan.** Android builds and on-headset measurement are deferred to a later effort. See **Phase D** at the end, which collects the build tooling and every deferred check in one place.

> ⚠️ **Consequence, stated plainly:** this removes every real performance gate. Editor Game-view statistics and the editor profiler are **not** predictive of Quest 3 — the tile GPU, memory bandwidth ceiling and fixed 72 Hz budget are precisely what an editor measurement cannot see. Phases 5–7 in particular are being tuned blind. Each affected step below carries a `🔺 DEFERRED` marker and is repeated in Phase D, so nothing is silently skipped.

**4. Asset sourcing waterfall.** For every model, texture and material this plan needs, work down this order and stop at the first that works:

| Order | Approach | Notes |
|---|---|---|
| 1 | **Free from the internet** | CC0 preferred, CC-BY acceptable with attribution. Must permit commercial use. |
| 2 | **Author in Blender via MCP** | Set up in Phase 0 task 2 specifically so this is viable. |
| 3 | **Purchase** | Last resort. Flag to Rao and get approval before spending. |

Any task that acquires an asset must state which tier it used and why the tiers above it were
rejected. Provenance and licences are recorded in [AssetSources.md](../AssetSources.md).

> ⚠️ **Legal note, separate from copyright.** Sketchfab's cricket stadium models are almost all
> recognisable real venues (Narendra Modi Stadium, the SCG, Gaddafi Stadium, Ekana Lucknow). A
> CC-BY licence covers the *uploader's copyright in the model* — it does **not** cover the venue
> operator's trademarks, trade dress or name. Shipping a recognisable branded real stadium
> commercially is a distinct exposure. **Keep the stadium generic**; the existing
> `FullStadium.fbx` already is.

---

## Conventions for this plan

**No fake tests.** This is asset and config work; there is no unit test for "looks realistic." Each task's verification step is a concrete observable: a `grep` assertion on serialized state, an Editor console state, an Editor statistic, or a screenshot diff against the phase-0.5 control set. Where a task produces real C#, it gets a genuine test.

**Close the Unity Editor before any task that edits `.meta`, `.prefab`, `.unity` or `ProjectSettings/` files on disk.** The Editor owns those files while open and will silently overwrite your edits. Tasks that require this say so explicitly. Once the Unity MCP server is connected (Phase 0), prefer doing the change *through* the Editor instead — it is safer and avoids the whole class of problem.

**Paths used throughout:**
- Unity: `/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity`
- Project: `/Users/rao/Git/CricketVR`

---

## File structure

Files this plan creates or modifies, and what each owns.

### Created

| Path | Responsibility |
|---|---|
| *(none — MCP config lives in `~/.claude.json` under the project, written by `claude mcp add`)* | Machine-specific absolute paths, so deliberately not committed to the repo |
| `Assets/Editor/CricketVR.Editor.asmdef` | Assembly definition so editor tooling compiles separately from `Assembly-CSharp` |
| `Assets/Editor/LightmapUVSetup.cs` | Idempotently enables `generateSecondaryUV` + tuned params on the four static models |
| `Assets/Editor/StaticFlagSetup.cs` | Walks the Stadium/Pitch hierarchies and applies lightmap-static flags to renderers |
| `Assets/Editor/LightmapScaleBudget.cs` | Applies per-renderer `scaleInLightmap` from a distance-banded table |
| `Assets/Editor/OrmPacker.cs` | Packs occlusion/roughness/metallic greyscale maps into one RGB texture |
| `Assets/Editor/ReferenceShots.cs` | Renders the three control cameras to PNG for before/after diffs. **The primary verification mechanism in this plan.** |
| `Assets/Editor/SceneStats.cs` | Dumps editor-side renderer/triangle/material counts to a text file for phase comparison |
| `Assets/Settings/URP-Quest.asset` | URP pipeline asset, mobile-tuned |
| `Assets/Settings/URP-Quest-Renderer.asset` | Forward renderer for the above |
| `Assets/Settings/CricketVR-Lighting.lighting` | LightingSettings asset (Progressive GPU, AO on, 2 bounces) |
| `Assets/Settings/Volumes/Global-Day.asset` | Day grading profile |
| `Assets/Settings/Volumes/Global-Night.asset` | Night grading profile |
| `Assets/Art/Sky/` | HDRI cubemaps + skybox materials (day, night) |
| `Assets/Art/Materials/` | New PBR materials replacing the untextured Standard set |
| `Assets/Scripts/GradingSwitcher.cs` | Runtime: swaps Volume profile when `stadiumMode` changes |
| `Docs/Baseline.md` | Editor-side metrics per phase; the record every later gate compares against |
| `Docs/AssetSources.md` | Provenance and licence for every acquired asset |

### Modified

| Path | Change |
|---|---|
| `Packages/manifest.json` | Add `com.unity.render-pipelines.universal` |
| `ProjectSettings/GraphicsSettings.asset` | Point at `URP-Quest.asset` |
| `ProjectSettings/QualitySettings.asset` | Per-tier URP asset assignment |
| `Assets/Resources/Models/{FullStadium,Pitch,Stump,BowlingMachine}.fbx.meta` | `generateSecondaryUV: 0` → `1` |
| `Assets/Resources/Prefabs/Stadium.prefab` | Static flags on renderers; audit `m_IsActive` / `m_CastShadows` overrides |
| `Assets/Scenes/CricketVR.unity` | Skybox refs, ambient mode, sun, fog, probes, Volume |
| `Assets/Scenes/Nets.unity` | Skybox ref only (same dangling GUID) |
| `Assets/Scripts/Main.cs:579-592` | Call into `GradingSwitcher` alongside the existing day/night light toggle |

---

# Phase 0 — Tooling

**Done first, deliberately.** Every later phase is iterative Editor work — bake, look, adjust,
rebake. Without MCP the agent is editing YAML blind and Rao is the only one who can see the
result. This phase pays for itself by phase 2.

## Task 1: Unity Editor MCP server

**Files:**
- Create: `.mcp.json`

**Chosen: [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp)** ("MCP for Unity"),
v10.2.0. Selected on evidence, not popularity:

- **The only candidate with source-level evidence of Unity 6000.3 support** — it carries an
  explicit `#elif UNITY_6000_3_OR_NEWER → EditorUtility.EntityIdToObject` branch for the API
  change that landed in 6000.3, and its CI matrix runs 6000.4.8f1, which compiles that same
  branch. Every other candidate offers only a version badge.
- **`manage_graphics` is purpose-built for phases 2–3**: `bake_start`, `bake_status`,
  `bake_cancel`, `bake_create_light_probe_group`, `bake_create_reflection_probe`,
  `bake_set_probe_positions`, `bake_reflection_probe`. No other candidate has probe tooling.
- 14,387 stars, commits landing daily, v10.2.0 released 2026-09-01.

> ⚠️ **Two open macOS issues you will hit.** [#1207](https://github.com/CoplayDev/unity-mcp/issues/1207)
> and [#1265](https://github.com/CoplayDev/unity-mcp/issues/1265): over the default HTTP
> transport the bridge drops its session on domain reload and only re-registers when the Editor
> window regains focus — reported specifically with Claude Code driving a backgrounded macOS
> Editor. **Baking and reimporting cause exactly this reload churn**, which is most of phases 2
> and 3. Mitigation: keep the Unity Editor focused and foregrounded during long operations, or
> switch to the stdio transport. Expect to re-establish the session occasionally; it is not a
> sign anything is broken.

- [ ] **Step 1: Install prerequisites**

```bash
brew install uv && uv --version
```

If the Editor's setup wizard later cannot see `uv`, launch Unity Hub **from a terminal** — GUI
apps do not inherit your shell `PATH`, which is a documented cause of this.

- [ ] **Step 2: Install the Unity-side bridge package**

Window → Package Manager → **+** → *Add package from git URL*:

```
https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#v10.2.0
```

Pin the version tag rather than `#main`. The EntityId migration is an active source of breakage
in this package — 6000.6 is currently broken by it — so a pinned version and a re-test on any
Editor upgrade is the cautious posture.

- [ ] **Step 3: Register the MCP server — on stdio, not HTTP**

A setup wizard opens on import. It may register the client for you on the **HTTP** transport.

> ⚠️ **Use stdio instead.** ✅ *Resolved in practice 2026-09-21.* The Editor-side bridge starts
> on **stdio, port 6400** — the log line is
> `MCP-FOR-UNITY: StdioBridgeHost started on port 6400`, which Unity prints *with a stack trace
> attached* because it goes through `McpLog.Info`. It looks alarmingly like an exception and is
> not one. Meanwhile nothing serves HTTP 8080, so an HTTP client registration silently fails to
> connect. Register stdio, which also sidesteps macOS issues #1207/#1265 entirely.

```bash
claude mcp remove UnityMCP
claude mcp add UnityMCP -- /usr/local/bin/uvx --from mcpforunityserver mcp-for-unity --transport stdio
claude mcp list | grep -i unity
```

Expected: `✔ Connected`.

**Use the absolute `/usr/local/bin/uvx` path**, not bare `uvx` — a GUI-launched client does not
inherit your shell `PATH` and fails with `spawn uvx ENOENT`. The health check above passes either
way because the CLI *does* have PATH; the desktop app is where it breaks.

- [ ] **Step 4: Restart the Claude Code session**

MCP servers added after a session starts are not hot-loaded, and `reconnect_session_connector`
only re-dials servers the session already knows about. **The session must be restarted** before
any Unity tool is callable.

Confirm the bridge is up first:

```bash
lsof -nP -iTCP:6400 -sTCP:LISTEN
```

Expected: a `Unity` process listening.

- [ ] **Step 5: Verify the bridge reads live Editor state**

With the Unity Editor open on `CricketVR.unity`, call
`manage_scene(action="get_active")` and `manage_scene(action="get_hierarchy")`.

Expected: **`rootCount: 4`** — `Day Lights`, `Main`, `Controllers`, `GameObject`.

> ⚠️ **Corrected 2026-09-21.** An earlier draft of this plan said to expect **32**. That was
> wrong: 32 is the number of *named `GameObject:` blocks in the scene YAML*, which counts every
> loose GameObject in the file, not the roots. Only 4 are roots. Do not use the YAML block count
> as a hierarchy check.

**Cross-check the bridge against the file rather than trusting it alone.** A good independent
assertion: `Day Lights` must report `activeSelf: false` while `Night Lights` is active.

```bash
grep -A30 "m_Name: Day Lights" Assets/Scenes/CricketVR.unity | grep -m1 m_IsActive
```

Expected: `m_IsActive: 0`, matching what the bridge reports.

> **Note for later phases: the scene ships in NIGHT mode.** `Day Lights` is disabled and
> `Night Lights` enabled. Every reference-shot capture must use the *same* mode or the
> comparisons are meaningless. Pick **Day** for the canonical set, since that is the primary
> look, and capture Night separately.

- [ ] **Step 6: Verify console access**

Introduce a deliberate compile error — add a line reading `syntax error` to any file under
`Assets/Scripts/` — let the Editor recompile, and confirm the agent can read the error from the
console. Then revert:

```bash
git checkout Assets/Scripts/
```

- [ ] **Step 7: Verify menu invocation**

Have the agent invoke a harmless menu item, for example `Assets/Refresh`. If this fails, phases 2
and 3 lose most of their value — resolve it before continuing.

- [ ] **Step 8: Commit**

Only the Unity package reference is committable — the MCP client config holds machine-specific
absolute paths and lives in `~/.claude.json`, outside the repo.

```bash
git add Packages/manifest.json Packages/packages-lock.json && git commit -m "tooling: add Unity Editor MCP bridge package"
```

## Task 2: Blender MCP server

**Files:**
- Modify: `.mcp.json`

Needed for tier 2 of the asset sourcing waterfall. Phases 4, 6 and 7 all depend on being able to
author or process geometry and textures without buying anything.

**Chosen: [ahujasid/mcp-for-blender](https://github.com/ahujasid/mcp-for-blender)** (29,139
stars, commit 2026-09-21). Note the project was **renamed** from `blender-mcp` on 2026-09-16 —
the old PyPI package is now a frozen shim, so install under the new name.

Why this one:

- **`execute_blender_code` runs unrestricted Python in Blender's process.** That is the whole
  point — decimation, `bmesh`, `smart_project`, `bpy.ops.object.bake` and `export_scene.fbx` are
  all just bpy, and a fixed-tool server would box us in on exactly this work.
- **It directly serves tier 1 of the sourcing waterfall.** Built-in Poly Haven (CC0 textures and
  HDRIs), Poly Pizza (free low-poly) and Sketchfab search/download tools mean "look for it free"
  happens inside the same loop as authoring. No other candidate has this.
- `export_scene(filepath, format="glb"|"fbx", apply_modifiers=True)` is the Unity round-trip, and
  `apply_modifiers` bakes the Decimate for you.
- `bpy_api_lookup` lets the agent check real operator signatures instead of hallucinating them.

> ⚠️ **Hard-coded 180-second socket timeout** (`sock.settimeout(180.0)`, both send and receive).
> A normal/AO bake off the high-poly stadium, or decimating the 44 MB FBX, can plausibly exceed
> that — the MCP call fails even though Blender keeps working. Mitigations, in order: chunk the
> work; bake at lower resolution first; or for the heaviest jobs have the agent write a `.py` to
> disk and run `blender --background --python`, bypassing MCP entirely.

- [ ] **Step 1: Install Blender**

**Not currently installed** on this machine — verified.

```bash
brew install --cask blender && ls -d /Applications/Blender.app
```

> ⚠️ **This Mac is Intel** (i9-9980HK, x86_64 — verified). Blender dropped macOS Intel builds at
> 5.0, so **4.5 LTS is the last version that runs here**, supported to July 2027. The Homebrew
> cask is arch-aware and will serve 4.5.x automatically. The server supports Blender 3.0+, so
> this is fine — but you are permanently capped at 4.5 on this hardware.

- [ ] **Step 2: Register the MCP server**

```bash
claude mcp add blender -- /usr/local/bin/uvx mcp-for-blender
```

Use the **absolute** `uvx` path (verified present at `/usr/local/bin/uvx`, v0.11.21) — a
GUI-launched client that does not inherit your shell `PATH` otherwise fails with
`spawn uvx ENOENT`. As with Unity, the session must be **restarted** before the tools appear.

- [ ] **Step 3: Install and enable the Blender addon**

```bash
uvx mcp-for-blender install-addon
```

Then in Blender: **Edit → Preferences → Add-ons** → enable **Interface: MCP for Blender**. Then
in the 3D viewport press **N** → **MCP for Blender** tab → **Start MCP Server**.

Telemetry pings per tool call by default. To disable, set `DISABLE_TELEMETRY=true` in the server
env. Run only one MCP client against Blender at a time.

- [ ] **Step 4: Verify round-trip capability**

Have the agent, through Blender: import `Assets/Resources/Models/Stump.fbx`, report its polygon
count, and export it unchanged to `/tmp/stump-roundtrip.fbx`.

```bash
ls -l /tmp/stump-roundtrip.fbx
```

Expected: the file exists and is non-zero. `Stump.fbx` is chosen deliberately — at 18 KB it is
the smallest model in the project, so this tests the pipeline rather than Blender's patience.

- [ ] **Step 5: Nothing to commit**

The Blender MCP config is machine-specific and lives in `~/.claude.json`. Record in
[AssetSources.md](../AssetSources.md) that Blender **4.5.14 LTS** is the pinned version on this
machine — the Intel cap means it cannot move to 5.x.

**PHASE 0 GATE:** agent can read the Unity scene hierarchy, read the Editor console, and invoke a
menu item · Blender round-trip verified — or an explicit recorded decision to proceed without one
of them, and an understanding of what that costs.

---

## ⚠️ Known trap: installing the MCP breaks compilation until URP is added

*Encountered and resolved 2026-09-21. Recorded because anyone repeating this setup will hit it.*

The MCP for Unity setup adds three packages it does **not** require — its real dependencies are
only Unity modules plus `newtonsoft-json` and `test-framework`:

| Package | Why it appeared |
|---|---|
| `com.unity.cloud.gltfast` 6.20.0 | optional integration for `import_model` |
| `com.unity.probuilder` 6.1.2 | optional, for `manage_probuilder` |
| `com.unity.cinemachine` 3.1.7 | optional, for `manage_camera` |

glTFast pulls in `com.unity.shadergraph`, which defines `UNITY_SHADER_GRAPH`. On a project with
**Shader Graph but no SRP**, this hits a genuine glTFast packaging bug:

```
com.unity.cloud.gltfast/Runtime/Scripts/Export/MaterialExport.cs(44,29):
error CS0246: The type or namespace name 'LitMaterialExport' could not be found
```

`LitMaterialExport.cs:4` is wrapped in `#if USING_URP || USING_HDRP`, but its call site in
`MaterialExport.cs:44` sits under `case RenderPipeline.Universal:` guarded only by
`#if UNITY_SHADER_GRAPH`. Shader Graph present + URP absent = the call site compiles, the class
does not exist.

**Resolution taken here: install URP immediately (task 6), which defines `USING_URP` and makes
the class appear.** The alternative — removing glTFast to restore a clean Built-in compile — was
considered and rejected, since URP was happening anyway.

**Consequence for the baseline:** phase 0.5's reference shots are captured **after** the URP
migration, not on Built-in. A Built-in Scene View capture was taken first and preserved at
`Docs/Baseline/phase0-sceneview.png` as the true "before".

---

# Phase 0.5 — Baseline

No device build here (constraint 3). The baseline is editor-side plus the reference screenshots,
which become the primary way to judge every later phase.

## Task 3: Baseline record and editor stats tool

**Files:**
- Create: `Docs/Baseline.md`
- Create: `Assets/Editor/SceneStats.cs`

- [ ] **Step 1: Write the stats dumper**

Create `Assets/Editor/SceneStats.cs`:

```csharp
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CricketVR.EditorTools
{
    /// <summary>
    /// Dumps editor-side scene statistics for phase-to-phase comparison.
    /// These are NOT a substitute for on-device measurement - they cannot see tile-GPU
    /// cost, bandwidth or stereo overhead. They catch gross regressions only.
    /// </summary>
    public static class SceneStats
    {
        [MenuItem("CricketVR/Dump Scene Stats")]
        public static void Dump()
        {
            var sb = new StringBuilder();
            var renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            var skinned = Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None);

            var tris = 0L;
            foreach (var r in renderers)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3;
            }

            var materials = renderers
                .SelectMany(r => r.sharedMaterials)
                .Where(m => m != null)
                .Distinct()
                .ToList();

            sb.AppendLine($"MeshRenderers:        {renderers.Length}");
            sb.AppendLine($"SkinnedMeshRenderers: {skinned.Length}");
            sb.AppendLine($"Triangles (sum):      {tris}");
            sb.AppendLine($"Unique materials:     {materials.Count}");
            sb.AppendLine($"Materials w/ normal:  {materials.Count(m => m.HasProperty("_BumpMap") && m.GetTexture("_BumpMap") != null)}");
            sb.AppendLine($"Lights:               {Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length}");
            sb.AppendLine($"Reflection probes:    {Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None).Length}");
            sb.AppendLine($"Light probe groups:   {Object.FindObjectsByType<LightProbeGroup>(FindObjectsSortMode.None).Length}");
            sb.AppendLine($"Lightmaps:            {LightmapSettings.lightmaps.Length}");
            sb.AppendLine($"Static (ContributeGI):{renderers.Count(r => (GameObjectUtility.GetStaticEditorFlags(r.gameObject) & StaticEditorFlags.ContributeGI) != 0)}");

            Directory.CreateDirectory("Docs/Baseline");
            var path = "Docs/Baseline/stats-current.txt";
            File.WriteAllText(path, sb.ToString());
            Debug.Log($"[SceneStats] wrote {path}\n{sb}");
        }
    }
}
```

- [ ] **Step 2: Run it and snapshot as phase 0.5**

Menu: CricketVR → Dump Scene Stats.

```bash
cp Docs/Baseline/stats-current.txt Docs/Baseline/stats-phase0.txt && cat Docs/Baseline/stats-phase0.txt
```

- [ ] **Step 3: Sanity-check the output against known values**

Expected, from the verified baseline scan: **Lights: 7**, **Reflection probes: 0**, **Light probe
groups: 0**, **Lightmaps: 0**, **Materials w/ normal: 0**.

If any of those five differ, the tool is not reading the scene you think it is — fix that before
trusting any later comparison.

- [ ] **Step 4: Create the baseline record**

Create `Docs/Baseline.md`:

```markdown
# CricketVR Scene Realism Baseline

Editor-side measurements only. **On-device measurement is deferred** — see Phase D of the
implementation plan for the list of checks that still need running on a Quest 3.

## Editor stats per phase

Snapshots live in `Docs/Baseline/stats-<phase>.txt`, produced by CricketVR → Dump Scene Stats.

| Phase | Renderers | Triangles | Materials | w/ normal | Probes | Lightmaps | Notes |
|---|---|---|---|---|---|---|---|
| 0.5 baseline | | | | 0 | 0 | 0 | Built-in RP, nothing baked |

## Reference screenshots

`Docs/Baseline/phase0-{batsman,midpitch,boundary}.png` and one set per later phase.

## Lightmap memory

| Phase | Baked set size on disk |
|---|---|
| 0.5 | 0 (nothing baked) |

## Decisions recorded

| Question | Decision | Evidence |
|---|---|---|
| FullStadium UV0 usable? | | task 5 |
| Tonemapper: ACES or Neutral? | | DEFERRED to Phase D - must be judged in the headset |
| APV or hand-placed probes? | | task 20 spike |
```

- [ ] **Step 5: Fill the phase 0.5 row from the stats dump, then commit**

```bash
git add Docs/Baseline.md Docs/Baseline Assets/Editor/SceneStats.cs && git commit -m "tooling: add scene stats dumper and baseline record"
```

## Task 4: Reference screenshot rig

**Files:**
- Create: `Assets/Editor/ReferenceShots.cs`
- Create: `Docs/Baseline/` (output directory)

Three fixed cameras, rendered identically every phase. **With device builds deferred, this is the
primary way to judge whether anything actually improved** — treat it as load-bearing, not a nicety.

- [ ] **Step 1: Write the capture tool**

Create `Assets/Editor/ReferenceShots.cs`:

```csharp
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CricketVR.EditorTools
{
    /// <summary>
    /// Renders three fixed viewpoints to PNG for before/after comparison across phases.
    /// Positions are in world space and must never change once phase 0 shots are taken.
    /// </summary>
    public static class ReferenceShots
    {
        const int Width = 1920;
        const int Height = 1080;
        const string OutDir = "Docs/Baseline";

        struct Shot
        {
            public string Name;
            public Vector3 Position;
            public Vector3 Euler;
        }

        // Batsman stance is at the striker's end looking down the pitch toward the bowler.
        // Calibrate these once against the scene in step 2, then never again.
        static readonly Shot[] Shots =
        {
            new Shot { Name = "batsman",  Position = new Vector3(0f, 1.7f, -10.0f), Euler = new Vector3(2f,   0f, 0f) },
            new Shot { Name = "midpitch", Position = new Vector3(0f, 1.7f,   0.0f), Euler = new Vector3(0f,  90f, 0f) },
            new Shot { Name = "boundary", Position = new Vector3(0f, 3.0f, -60.0f), Euler = new Vector3(5f,   0f, 0f) }
        };

        [MenuItem("CricketVR/Capture Reference Shots")]
        public static void Capture()
        {
            var choice = EditorUtility.DisplayDialogComplex(
                "Reference Shots", "Which phase are these?", "phase0", "current", "cancel");
            if (choice == 2) return;
            var tag = choice == 0 ? "phase0" : "current";

            Directory.CreateDirectory(OutDir);

            var go = new GameObject("__RefCam");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 90f;      // approximates the Quest 3 horizontal FOV
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 2000f;

            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 4
            };
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);

            foreach (var shot in Shots)
            {
                go.transform.position = shot.Position;
                go.transform.eulerAngles = shot.Euler;

                cam.targetTexture = rt;
                cam.Render();

                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;

                var path = Path.Combine(OutDir, $"{tag}-{shot.Name}.png");
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Debug.Log($"[ReferenceShots] wrote {path}");
            }

            cam.targetTexture = null;
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            AssetDatabase.Refresh();
        }
    }
}
```

- [ ] **Step 2: Calibrate the camera positions**

Open `Assets/Scenes/CricketVR.unity`. Find the batsman spawn position and read its world transform
from the Inspector. Update `Shots[0].Position` to match at eye height. Do the same for mid-pitch
and boundary. **This is the only time these values may change** — after the phase-0 capture they
are frozen, or every later comparison is meaningless.

- [ ] **Step 3: Capture the phase-0 set**

Menu: CricketVR → Capture Reference Shots → choose `phase0`.

- [ ] **Step 4: Verify three files exist and are non-trivial**

```bash
ls -l Docs/Baseline/phase0-*.png && find Docs/Baseline -name "phase0-*.png" -size -10k
```

Expected: three files listed by `ls`, and the `find` returns **nothing** — a sub-10 KB PNG means
the camera rendered an empty frame and the positions in step 2 are wrong.

- [ ] **Step 5: Commit**

```bash
git add Assets/Editor/ReferenceShots.cs Docs/Baseline && git commit -m "tooling: add fixed reference shot capture and phase 0 control set"
```

## Task 5: Verify the stadium mesh has usable UVs

**Files:** none modified — this is a gate check.

If `FullStadium.fbx` has unusable UV0, the PBR pass needs a Blender re-UV first (tier 2 of the
sourcing waterfall, using the Blender MCP from task 2). Finding that out now rather than at
phase 4 is the entire reason this task exists.

- [ ] **Step 1: Inspect UV0 in the Editor**

Select `Assets/Resources/Models/FullStadium.fbx`. In the Inspector's model preview, switch the
display dropdown to **UV Checker**. Rotate the model.

- [ ] **Step 2: Judge against these criteria**

| Observation | Verdict |
|---|---|
| Checker squares roughly square, not smeared into long rectangles | **usable** |
| Checker density varies wildly between stands and ground | **usable-with-density-compensation** — fix per-material when authoring |
| Overlapping UVs, absent UVs, or no checker appears at all | **unusable** — blocks phase 4 |

- [ ] **Step 3: If unusable, scope the Blender re-UV now**

Do not defer this to phase 4. Using the Blender MCP from task 2, import `FullStadium.fbx`, run a
Smart UV Project on the stand geometry, and confirm a clean unwrap is achievable. Record roughly
how long it took — the phase 4 estimate depends on it.

- [ ] **Step 4: Record the verdict in `Docs/Baseline.md`**

Fill the "FullStadium UV0 usable?" row of the Decisions table with the verdict and one line of
evidence.

> ✅ **Done 2026-09-21 — verdict: UNUSABLE.** Verified twice independently (Blender per-face UV
> analysis, then confirmed against the live Unity meshes; both agree). **`Stand1`, which carries
> the entire visible stadium at 43,630 triangles, has no UV0 at all.** So do `Boundry`, `Plane`,
> `Sidebar` and the `30Yrds` rings. Only `Inner Circle` and `Outer Circle` (202 tris between
> them) are unwrapped.
>
> Its current checkerboard look must come from vertex colours or a UV-independent material —
> there is nothing to attach an albedo or normal map to.
>
> **Two consequences, and they differ by phase:**
> - **Phase 4 is blocked on the stadium** and needs a Blender re-UV of `Stand1` first (tier 2).
>   At 43.6k triangles this is tractable — a Smart UV Project, not a rebuild. Added as task 21a.
> - **Phase 2 is NOT blocked.** Unity's `generateSecondaryUV` builds UV2 from mesh topology and
>   does not need an existing UV0. Lightmapping can proceed immediately.
>
> Separately: **`Cube.001` is 1,735,672 triangles and disabled.** It is the bulk of the 44 MB
> FBX, ships in the build anyway via `Resources/`, and is dead weight. Filed against task 29.

- [ ] **Step 5: Commit**

```bash
git add Docs/Baseline.md && git commit -m "docs: record FullStadium UV0 verdict"
```

**PHASE 0.5 GATE:** editor stats snapshot matches the five known baseline values · three phase-0
reference screenshots captured and non-empty · UV verdict recorded.

---

# Phase 1 — URP migration

**Why now:** the lightmap bake is the expensive artifact in this plan. Producing it in Built-in and
then migrating means baking twice. 49 of ~60 materials are stock Standard, and all four non-stock
shaders are vendor *unlit* (`OVRColorRampAlpha`, `OVRMRUnlitTransparent`, `Unlit Crosshair`,
`TMP_SDF-Mobile`) which do not depend on the lighting model — so migration risk here is low.

## Task 6: Add the URP package

**Files:**
- Modify: `Packages/manifest.json`

> ✅ **Done 2026-09-21 — resolved to URP 17.3.0.** Installed live through the MCP rather than by
> hand-editing `manifest.json` with the Editor closed. That is the better path and supersedes the
> original instructions: no editor restart, and Unity resolves the editor-matched version itself.

- [ ] **Step 1: Install through the MCP**

```
manage_packages(action="add_package", package="com.unity.render-pipelines.universal")
```

Omit the version — Unity resolves the one matching the editor. The call is async; poll with
`manage_packages(action="status", job_id=...)`.

- [ ] **Step 2: Verify the resolved versions line up**

Read `Packages/packages-lock.json` and check these three entries.

Expected — and confirmed on this machine: **`com.unity.render-pipelines.universal`,
`com.unity.render-pipelines.core` and `com.unity.shadergraph` all at 17.3.0**. A mismatch between
core and shadergraph is a known source of subtle breakage; they should move together.

- [ ] **Step 3: Confirm the glTFast compile error cleared**

Installing URP defines `USING_URP`, which makes `LitMaterialExport` exist and resolves the CS0246
documented in the phase 0 trap section.

```
read_console(action="get", types=["error"])
```

Expected: no `LitMaterialExport` error. Unity will be mid-domain-reload for a while first — the
MCP returns `Unity is reloading; please retry`, which is normal, not a failure.

- [ ] **Step 4: Commit**

```bash
git add Packages/manifest.json Packages/packages-lock.json && git commit -m "build: add URP 17 package"
```

## Task 7: Create the mobile-tuned URP asset

**Files:**
- Create: `Assets/Settings/URP-Quest.asset`
- Create: `Assets/Settings/URP-Quest-Renderer.asset`
- Modify: `ProjectSettings/GraphicsSettings.asset`
- Modify: `ProjectSettings/QualitySettings.asset`

- [ ] **Step 1: Create the renderer**

Project window → `Assets/Settings/` → Create → Rendering → URP Universal Renderer.
Name it `URP-Quest-Renderer`.

Set on it:

| Setting | Value | Why |
|---|---|---|
| Rendering Path | **Forward** | Forward+ costs more on tile GPUs; revisit only if the 5 floodlights prove limiting |
| Depth Texture | **Off** | Extra pass; nothing in this plan needs it |
| Opaque Texture | **Off** | Extra resolve; nothing needs it |
| Depth Priming Mode | **Disabled** | Counterproductive on tile-based deferred GPUs |
| Native RenderPass | **On** | Lets URP merge passes into one tile pass on Vulkan |

- [ ] **Step 2: Create the pipeline asset**

Create → Rendering → URP Asset (without Renderer). Name it `URP-Quest`. Assign
`URP-Quest-Renderer` to its Renderer List.

Set on it:

| Setting | Value | Why |
|---|---|---|
| Anti Aliasing (MSAA) | **4x** | MSAA is cheap on tile GPUs and the single biggest VR clarity win |
| HDR | **On** | Tonemapping in phase 5 is meaningless without it — but measure the bandwidth cost in task 10 |
| Render Scale | **1.0** | Leave headroom tuning to foveation in phase 7 |
| Main Light | **Per Pixel**, shadows **On** | The sun |
| Main Light Shadow Resolution | **2048** | |
| Shadow Distance | **25** | The batsman only needs shadows in the near field; 150 in the old Ultra tier is waste |
| Cascade Count | **1** | One cascade at 25 m is sufficient and cheapest |
| Additional Lights | **Per Pixel**, max **4**, shadows **Off** | The 5 floodlights; they already cast no shadows today |
| SRP Batcher | **On** | The big draw-call win on repeated stadium geometry |
| Dynamic Batching | **Off** | Redundant with, and slower than, the SRP Batcher |

- [ ] **Step 3: Assign the pipeline**

Project Settings → Graphics → Default Render Pipeline → `URP-Quest`.
Project Settings → Quality → for **every** tier, set Render Pipeline Asset → `URP-Quest`.

Assigning all tiers matters: the project ships 6 tiers and an unassigned one silently falls back
to Built-in.

- [ ] **Step 4: Verify both settings files point at the asset**

```bash
grep -c "URP-Quest" ProjectSettings/GraphicsSettings.asset ProjectSettings/QualitySettings.asset 2>/dev/null; grep -o 'm_CustomRenderPipeline: {fileID: [0-9]*, guid: [0-9a-f]*' ProjectSettings/GraphicsSettings.asset
```

Expected: a non-zero guid on `m_CustomRenderPipeline`. A guid of all zeros means the pipeline is
not assigned and you are still on Built-in.

- [ ] **Step 5: Commit**

```bash
git add Assets/Settings ProjectSettings/GraphicsSettings.asset ProjectSettings/QualitySettings.asset && git commit -m "render: add mobile-tuned URP asset and assign to all quality tiers"
```

## Task 8: Convert materials

**Files:**
- Modify: ~49 `.mat` files across `Assets/Resources/Materials/` and elsewhere

- [ ] **Step 1: Record the pre-conversion shader census**

```bash
grep -rh "m_Shader:" --include="*.mat" Assets | sort | uniq -c | sort -rn
```

Expected: 49 references to `{fileID: 46, guid: 0000000000000000f000000000000000}` (Built-in
Standard). Save this output — step 4 compares against it.

- [ ] **Step 2: Run the converter — manually**

Window → Rendering → Render Pipeline Converter. Select **Built-in to URP**. Tick
**Material Upgrade** and **Rendering Settings**. Initialize, then Convert Assets.

> ⚠️ **This step cannot be automated through the Unity MCP.** `execute_menu_item` can *open* the
> converter window, but "Initialize And Convert" is an IMGUI button, not a `[MenuItem]`, so it is
> not invokable. **Rao clicks this one.** The agent can verify the result in step 4 but cannot
> trigger the conversion.

- [ ] **Step 3: Read the converter report**

Every row must be green. Note any material it refuses — those get hand-conversion in task 9.

- [ ] **Step 4: Verify no Standard materials remain**

```bash
grep -rh "m_Shader:" --include="*.mat" Assets | grep -c "fileID: 46, guid: 0000000000000000f000000000000000"
```

Expected: **0**. A non-zero count names materials the converter skipped — convert those by hand
(set shader to `Universal Render Pipeline/Lit` and re-assign the albedo).

- [ ] **Step 5: Commit**

```bash
git add -A -- '*.mat' && git commit -m "render: convert Standard materials to URP Lit"
```

## Task 9: Verify vendor shaders and particles

**Files:**
- Modify: only what is found broken

- [ ] **Step 1: Open the scene and look for magenta**

Open `Assets/Scenes/CricketVR.unity`. Magenta means a shader failed to compile under URP.

- [ ] **Step 2: Check each vendor shader explicitly**

| Shader | Where it shows | Expected |
|---|---|---|
| `Assets/Oculus/VR/Shaders/Unlit Crosshair.shader` | crosshair/reticle | renders, unlit |
| `Assets/Oculus/VR/Shaders/OVRColorRampAlpha.shader` | Oculus debug visuals | renders |
| `Assets/Oculus/VR/Resources/OVRMRUnlitTransparent.shader` | mixed-reality capture only | renders |
| `Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile.shader` | all HUD/score text | text crisp, not magenta |

Unlit shaders normally survive URP untouched because they never reference lighting. If one is
magenta, the fix is to replace its `Tags { "RenderType"="..." }` / pass structure with URP's
unlit template — not to rewrite it against the Lit model.

- [ ] **Step 3: Check the ball trail particles**

Enter play mode, bowl a delivery, watch the trail. URP uses
`Universal Render Pipeline/Particles/Unlit`; the converter usually handles this but transparent
particle sort order is a common casualty.

- [ ] **Step 4: Verify TMP text renders**

Look at the HUD score display. Unity 6 has known TMP font-rendering regressions after upgrade
(RevivalPlan P1 flags this). If text is invisible or garbled, re-import TMP Essentials via
Window → TextMeshPro → Import TMP Essential Resources.

- [ ] **Step 5: Record findings and commit any fixes**

```bash
git add -A && git commit -m "render: fix shader and particle regressions from URP conversion"
```

## Task 10: Rebuild and confirm no performance regression

**Files:**
- Modify: `Docs/Baseline.md`

- [ ] **Step 1: Confirm the project compiles clean**

Open the Editor and check the console. Zero errors. URP conversion commonly leaves shader
warnings — those are fine; errors are not.

- [ ] **Step 2: Dump editor stats**

Menu: CricketVR → Dump Scene Stats, then:

```bash
cp Docs/Baseline/stats-current.txt Docs/Baseline/stats-phase1.txt && diff Docs/Baseline/stats-phase0.txt Docs/Baseline/stats-phase1.txt
```

Expected: renderer, triangle and material counts **unchanged**. URP conversion should change how
things are drawn, not what exists. Any change in those counts means the converter removed or
duplicated something.

- [ ] **Step 3: Check batching in the Frame Debugger**

Window → Analysis → Frame Debugger → Enable, with the Game view on the batsman viewpoint. Look
for `SRPBatcher` batches rather than long runs of individual draws.

This is the one URP benefit visible without a device, and worth confirming — if the SRP Batcher
is not engaging, something is misconfigured on `URP-Quest`.

- [ ] **Step 4: 🔺 DEFERRED — on-device frame timing**

Whether URP costs or saves GPU time on a tile GPU **cannot be answered in the Editor**.
Specifically unverified: whether HDR on `URP-Quest` is affordable at Quest 3 bandwidth. Logged in
Phase D.

- [ ] **Step 5: Capture current reference shots**

Menu: CricketVR → Capture Reference Shots → `current`. Rename the outputs:

```bash
cd Docs/Baseline && for f in current-*.png; do mv "$f" "phase1-${f#current-}"; done && cd -
```

- [ ] **Step 6: Commit**

```bash
git add Docs/Baseline.md Docs/Baseline && git commit -m "docs: record phase 1 URP metrics and reference shots"
```

**PHASE 1 GATE:** APK builds and runs · median FPS ≥ phase 0 · no magenta materials · HUD text
renders.

---

# Phase 2 — Baked lighting

**The single biggest win in this plan.** A cricket stadium is overwhelmingly static geometry and
currently has no baked lighting at all.

**Three prerequisites, all currently unmet:**

1. No model has lightmap UVs (`generateSecondaryUV: 0` on all 11 FBXs) — task 11.
2. The stadium geometry is **not lightmap-static**. `Stadium.prefab` has zero
   `m_StaticEditorFlags` overrides, so the FullStadium renderers inside it default to non-static.
   The 17 "static" objects in the scene are lights, colliders and wrapper transforms — task 12.
3. No LightingSettings asset exists and baked AO is off — task 13.

## Task 11: Enable lightmap UV generation on static models

**Files:**
- Create: `Assets/Editor/LightmapUVSetup.cs`
- Modify: `Assets/Resources/Models/{FullStadium,Pitch,Stump,BowlingMachine}.fbx.meta`

Static geometry only. The skinned characters (`Bowler`, `David`, `Animated Fielder`, `Fielder`)
must **not** get lightmap UVs — they are dynamic and will be lit by probes.

- [ ] **Step 1: Write the setup tool**

Create `Assets/Editor/LightmapUVSetup.cs`:

```csharp
using UnityEditor;
using UnityEngine;

namespace CricketVR.EditorTools
{
    /// <summary>
    /// Idempotently enables secondary (lightmap) UV generation on the project's static models.
    /// Deliberately excludes skinned characters, which are lit by probes, not lightmaps.
    /// </summary>
    public static class LightmapUVSetup
    {
        static readonly string[] StaticModels =
        {
            "Assets/Resources/Models/FullStadium.fbx",
            "Assets/Resources/Models/Pitch.fbx",
            "Assets/Resources/Models/Stump.fbx",
            "Assets/Resources/Models/BowlingMachine.fbx"
        };

        [MenuItem("CricketVR/Lighting/Enable Lightmap UVs on Static Models")]
        public static void Run()
        {
            var changed = 0;

            foreach (var path in StaticModels)
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                {
                    Debug.LogError($"[LightmapUVSetup] not a model: {path}");
                    continue;
                }

                if (importer.generateSecondaryUV &&
                    Mathf.Approximately(importer.secondaryUVHardAngle, 60f))
                {
                    Debug.Log($"[LightmapUVSetup] already configured: {path}");
                    continue;
                }

                importer.generateSecondaryUV = true;

                // 88 (the current value) treats almost every edge as smooth, which produces
                // lighting bleed across the hard architectural creases of a stadium.
                importer.secondaryUVHardAngle = 60f;

                // 4 px margin at the low lightmap resolutions this plan uses lets adjacent
                // charts bleed into each other. 8 is the safe floor for large sparse atlases.
                importer.secondaryUVPackMargin = 8;

                importer.secondaryUVAngleDistortion = 8f;
                importer.secondaryUVAreaDistortion = 15f;

                importer.SaveAndReimport();
                changed++;
                Debug.Log($"[LightmapUVSetup] configured {path}");
            }

            Debug.Log($"[LightmapUVSetup] done, {changed} model(s) changed");
        }
    }
}
```

- [ ] **Step 2: Run it**

Menu: CricketVR → Lighting → Enable Lightmap UVs on Static Models.
Expect a slow reimport — `FullStadium.fbx` is 44 MB.

- [ ] **Step 3: Verify the meta files changed**

```bash
for m in FullStadium Pitch Stump BowlingMachine; do printf "%-16s %s %s\n" "$m" "$(grep -m1 'generateSecondaryUV:' "Assets/Resources/Models/$m.fbx.meta" | tr -d ' ')" "$(grep -m1 'secondaryUVHardAngle:' "Assets/Resources/Models/$m.fbx.meta" | tr -d ' ')"; done
```

Expected: all four show `generateSecondaryUV:1` and `secondaryUVHardAngle:60`.

- [ ] **Step 4: Verify the characters were NOT touched**

```bash
for m in Bowler David "Animated Fielder" Fielder; do printf "%-18s %s\n" "$m" "$(grep -m1 'generateSecondaryUV:' "Assets/Resources/Models/$m.fbx.meta" | tr -d ' ')"; done
```

Expected: all four show `generateSecondaryUV:0`.

- [ ] **Step 5: Commit**

```bash
git add Assets/Editor/LightmapUVSetup.cs 'Assets/Resources/Models/*.fbx.meta' && git commit -m "lighting: generate lightmap UVs for static stadium geometry"
```

## Task 12: Make the stadium geometry lightmap-static

**Files:**
- Create: `Assets/Editor/StaticFlagSetup.cs`
- Modify: `Assets/Resources/Prefabs/Stadium.prefab`, `Assets/Resources/Prefabs/Pitch.prefab`

Without this, task 15's bake produces nothing regardless of UVs.

- [ ] **Step 1: Write the tool**

Create `Assets/Editor/StaticFlagSetup.cs`:

```csharp
using UnityEditor;
using UnityEngine;

namespace CricketVR.EditorTools
{
    /// <summary>
    /// Applies lightmap-static flags to every MeshRenderer under the static environment prefabs.
    /// Unity does not propagate m_StaticEditorFlags into nested model-prefab instances, which is
    /// why the Stadium root being flagged does not make its geometry bakeable.
    /// </summary>
    public static class StaticFlagSetup
    {
        static readonly string[] StaticPrefabs =
        {
            "Assets/Resources/Prefabs/Stadium.prefab",
            "Assets/Resources/Prefabs/Pitch.prefab"
        };

        const StaticEditorFlags Flags =
            StaticEditorFlags.ContributeGI |
            StaticEditorFlags.OccluderStatic |
            StaticEditorFlags.OccludeeStatic |
            StaticEditorFlags.BatchingStatic |
            StaticEditorFlags.ReflectionProbeStatic;

        [MenuItem("CricketVR/Lighting/Flag Static Environment Geometry")]
        public static void Run()
        {
            foreach (var path in StaticPrefabs)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                if (root == null)
                {
                    Debug.LogError($"[StaticFlagSetup] could not load {path}");
                    continue;
                }

                var count = 0;
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject, Flags);
                    r.receiveGI = ReceiveGI.Lightmaps;
                    count++;
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
                Debug.Log($"[StaticFlagSetup] flagged {count} renderer(s) in {path}");
            }
        }
    }
}
```

- [ ] **Step 2: Run it**

Menu: CricketVR → Lighting → Flag Static Environment Geometry.

Expected console output: a non-zero renderer count for `Stadium.prefab`. **If it reports 0, stop**
— the geometry is not where this tool expects it and the rest of phase 2 will silently no-op.

- [ ] **Step 3: Verify the prefab now carries static flags**

```bash
grep -c "m_StaticEditorFlags" Assets/Resources/Prefabs/Stadium.prefab
```

Expected: greater than 1 (it was exactly 1 before — the root wrapper only).

- [ ] **Step 4: Audit the pre-existing overrides**

```bash
grep -B4 "propertyPath: m_IsActive" Assets/Resources/Prefabs/Stadium.prefab | grep -c "value: 0"
```

Twelve `m_IsActive` and three `m_CastShadows` overrides exist in this prefab. Open it and confirm
each disabled part is deliberately hidden (unused stand sections, roof pieces) rather than
accidentally switched off. Anything accidentally disabled is free geometry you are not rendering.

- [ ] **Step 5: Commit**

```bash
git add Assets/Editor/StaticFlagSetup.cs Assets/Resources/Prefabs/Stadium.prefab Assets/Resources/Prefabs/Pitch.prefab && git commit -m "lighting: flag stadium and pitch geometry as lightmap static"
```

## Task 13: Lighting settings and mixed sun

**Files:**
- Create: `Assets/Settings/CricketVR-Lighting.lighting`
- Modify: `Assets/Scenes/CricketVR.unity`

- [ ] **Step 1: Create the LightingSettings asset**

Window → Rendering → Lighting → Scene tab → New Lighting Settings. Save as
`Assets/Settings/CricketVR-Lighting.lighting`.

Set:

| Setting | Value | Why |
|---|---|---|
| Lightmapper | **Progressive GPU**, falling back to CPU | See the hardware note below before trusting this |
| Lightmap Resolution | **8** texels/unit | Deliberately low. A stadium is ~150 m across; start low and raise only where task 14 says to |
| Max Lightmap Size | **2048** | |
| Compress Lightmaps | **On** | |
| Ambient Occlusion | **On**, Max Distance **1.0** | Currently off. This is a large share of the perceived realism gain |
| Directional Mode | **Directional** | Preserves normal-map response from phase 4 |
| Lightmap Bounces | **2** | |
| Filtering | **Auto** | |
| Lighting Mode | **Shadowmask** | Static gets baked shadows, dynamic gets real-time |

> ⚠️ **Hardware note.** This Mac is an Intel i9-9980HK with an **AMD Radeon Pro 5300M, 4 GB
> VRAM**. Unity's GPU lightmapper needs the scene to fit in VRAM and silently falls back to CPU
> when it does not — and macOS/AMD is historically its least-exercised path. Expect one of: it
> works, it falls back to CPU and takes far longer, or it errors. **All three are acceptable
> outcomes** — if GPU baking misbehaves, switch to Progressive CPU and accept the wall-clock
> cost. Do not spend time fighting it, and do not lower lightmap quality to make GPU baking fit;
> the 4 GB ceiling is also why task 14's scale budget matters more here than on a typical dev box.

- [ ] **Step 2: Configure the sun**

Select `Sun Light 1` (under `Day Lights`). Set Mode to **Mixed**, Shadow Type **Soft Shadows**.
Do the same for `Sun Light 2` under `Night Lights`.

The five `Spot Light*` floodlights stay **Realtime** with shadows off, as today — they are the
night-mode look and shadowed spots on a tile GPU are not affordable.

- [ ] **Step 3: Verify the scene references the lighting asset**

```bash
grep -n "m_LightingSettings:" Assets/Scenes/CricketVR.unity
```

Expected: a non-zero `guid`. `{fileID: 0}` means it is not assigned.

- [ ] **Step 4: Verify the sun is Mixed**

```bash
grep -A22 '^Light:' Assets/Scenes/CricketVR.unity | grep -c "m_Lightmapping: 1"
```

Unity encodes Mixed as `m_Lightmapping: 1`. Expected: **2** (both suns).

- [ ] **Step 5: Commit**

```bash
git add Assets/Settings/CricketVR-Lighting.lighting Assets/Scenes/CricketVR.unity && git commit -m "lighting: add lighting settings asset, set suns to mixed shadowmask"
```

## Task 14: Budget lightmap resolution per object

**Files:**
- Create: `Assets/Editor/LightmapScaleBudget.cs`

A uniform texel density over a 150 m stadium will blow past Quest texture memory. Near geometry
needs density; distant stands do not.

- [ ] **Step 1: Write the tool**

Create `Assets/Editor/LightmapScaleBudget.cs`:

```csharp
using UnityEditor;
using UnityEngine;

namespace CricketVR.EditorTools
{
    /// <summary>
    /// Sets per-renderer lightmap scale from distance to the pitch centre. Texel density is a
    /// memory budget, and on a stadium almost all of the geometry is far away and unimportant.
    /// scaleInLightmap has no public setter, so it is written through SerializedObject.
    /// </summary>
    public static class LightmapScaleBudget
    {
        // Distance from pitch centre (m) -> lightmap scale multiplier.
        static readonly (float MaxDistance, float Scale)[] Bands =
        {
            (12f,   1.0f),   // pitch and immediate surrounds - the player stares at this
            (35f,   0.5f),   // inner ring outfield
            (75f,   0.25f),  // outfield to boundary
            (float.MaxValue, 0.1f) // stands, roof, everything beyond
        };

        [MenuItem("CricketVR/Lighting/Apply Lightmap Scale Budget")]
        public static void Run()
        {
            var pitch = GameObject.Find("Pitch");
            if (pitch == null)
            {
                Debug.LogError("[LightmapScaleBudget] no GameObject named 'Pitch' in the open scene");
                return;
            }

            var origin = pitch.transform.position;
            var applied = 0;

            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var flags = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
                if ((flags & StaticEditorFlags.ContributeGI) == 0) continue;

                var d = Vector3.Distance(r.bounds.center, origin);
                var scale = 0.1f;
                foreach (var band in Bands)
                {
                    if (d <= band.MaxDistance) { scale = band.Scale; break; }
                }

                var so = new SerializedObject(r);
                so.FindProperty("m_ScaleInLightmap").floatValue = scale;
                so.ApplyModifiedProperties();
                applied++;
            }

            Debug.Log($"[LightmapScaleBudget] applied to {applied} renderer(s)");
        }
    }
}
```

- [ ] **Step 2: Run it with `CricketVR.unity` open**

Menu: CricketVR → Lighting → Apply Lightmap Scale Budget.

Expected: a renderer count matching task 12's flagged count. **Zero means task 12 did not take** —
go back and fix that before baking.

- [ ] **Step 3: Verify scales were written**

```bash
grep -o "m_ScaleInLightmap: [0-9.]*" Assets/Scenes/CricketVR.unity | sort | uniq -c
```

Expected: a spread across `1`, `0.5`, `0.25`, `0.1` — not all one value.

- [ ] **Step 4: Commit**

```bash
git add Assets/Editor/LightmapScaleBudget.cs Assets/Scenes/CricketVR.unity && git commit -m "lighting: apply distance-banded lightmap scale budget"
```

## Task 15: Bake and measure

**Files:**
- Create: `Assets/Scenes/CricketVR/` (Unity's bake output directory)
- Modify: `Assets/Scenes/CricketVR.unity`, `Docs/Baseline.md`

- [ ] **Step 1: Bake**

Window → Rendering → Lighting → Generate Lighting.

This will take a long time on first run. Watch for "Clustering" and "Light Transport" progress —
if it finishes in seconds, nothing was marked static and you must revisit task 12.

- [ ] **Step 2: Verify a LightingDataAsset now exists**

```bash
grep -n "m_LightingDataAsset:" Assets/Scenes/CricketVR.unity
```

Expected: a non-zero guid. `{fileID: 0}` means the bake produced nothing.

- [ ] **Step 3: Check the lightmap memory cost**

```bash
ls -lhS Assets/Scenes/CricketVR/*.exr Assets/Scenes/CricketVR/*.png 2>/dev/null | head; du -sh Assets/Scenes/CricketVR/
```

Budget: keep the total baked set **under 64 MB**. Over that, halve the band scales in task 14 and
rebake rather than reducing the global resolution — distant stands are where the waste is.

- [ ] **Step 4: Capture reference shots**

Menu: CricketVR → Capture Reference Shots → `current`, then:

```bash
cd Docs/Baseline && for f in current-*.png; do mv "$f" "phase2-${f#current-}"; done && cd -
```

Compare `phase2-*.png` against `phase0-*.png`. You should see contact darkening under the stands,
AO in the seating, and green bounce from the outfield onto nearby surfaces.

- [ ] **Step 5: Record stats and lightmap memory**

Menu: CricketVR → Dump Scene Stats, then:

```bash
cp Docs/Baseline/stats-current.txt Docs/Baseline/stats-phase2.txt && grep Lightmaps Docs/Baseline/stats-phase2.txt && du -sh Assets/Scenes/CricketVR/
```

Expected: `Lightmaps:` greater than 0 — it was 0 at baseline. Record the baked set size in the
lightmap memory table in `Docs/Baseline.md`.

- [ ] **Step 6: 🔺 DEFERRED — on-device lightmap memory headroom**

The 64 MB budget in step 3 is a reasoned estimate, not a measurement. Whether this baked set fits
alongside everything else in Quest 3 memory is unverified. Logged in Phase D.

- [ ] **Step 7: Commit**

```bash
git add Assets/Scenes Docs/Baseline Docs/Baseline.md && git commit -m "lighting: bake stadium lightmaps with AO and shadowmask"
```

**PHASE 2 GATE:** `m_LightingDataAsset` non-zero · baked set under 64 MB · visible improvement at
all three reference cameras · framerate held.

---

# Phase 3 — Environment and ambient

## Task 16: Fix the dangling skybox and add real HDRI skies

**Files:**
- Create: `Assets/Art/Sky/Sky-Day.mat`, `Assets/Art/Sky/Sky-Night.mat` + their cubemaps
- Modify: `Assets/Scenes/CricketVR.unity:30`, `Assets/Scenes/Nets.unity`

**This is a bug fix, not just an art change — and it is worse than one dangling reference.**
The P2 purge deleted the Oculus SampleFramework skybox materials, and **three** references still
point at them:

| Reference | Pointed at | GUID |
|---|---|---|
| `CricketVR.unity:30` `m_SkyboxMaterial` | `SkyboxForLightbaking.mat` | `b95594ff…` |
| `Nets.unity` `m_SkyboxMaterial` | `SkyboxForLightbaking.mat` | `b95594ff…` |
| `Main.NightMaterial` (`CricketVR.unity:11167`) | `SkyboxForLightbaking.mat` | `b95594ff…` |
| `Main.DayMaterial` (`CricketVR.unity:11168`) | `SkyboxForRealtime.mat` | `799432b5…` |

`Main.updateStadiumMode()` assigns `RenderSettings.skybox = NightMaterial` at
`Assets/Scripts/Main.cs:582` and `= DayMaterial` at `:590`. **Both fields are null**, so every
day/night switch currently sets the skybox to nothing. The runtime day/night sky is not merely
generic — it is broken.

- [ ] **Step 1: Confirm all three references are dangling**

```bash
grep -n "m_SkyboxMaterial" Assets/Scenes/CricketVR.unity Assets/Scenes/Nets.unity; grep -nE "NightMaterial:|DayMaterial:" Assets/Scenes/CricketVR.unity; for g in b95594ffeaf238c46a8d575dcd72705a 799432b58386306488cfd7a19e7ff4d7; do r=$(grep -rl "$g" --include='*.meta' Assets Packages 2>/dev/null | head -1); echo "$g -> ${r:-MISSING}"; done
```

Expected: both GUIDs report `MISSING`.

- [ ] **Step 2: Acquire two HDRIs (tier 1 — free, CC0)**

Both verified CC0 and reachable on 2026-09-21. Full provenance in
[AssetSources.md](../AssetSources.md).

```bash
mkdir -p Assets/Art/Sky && cd Assets/Art/Sky
curl -L -o sky-day.hdr "https://dl.polyhaven.org/file/ph-assets/HDRIs/hdr/4k/kloofendal_48d_partly_cloudy_puresky_4k.hdr"
curl -L -o sky-night.hdr "https://dl.polyhaven.org/file/ph-assets/HDRIs/hdr/4k/dikhololo_night_4k.hdr"
ls -lh *.hdr && cd -
```

Expected: `sky-day.hdr` around 20.7 MB, `sky-night.hdr` around 28.4 MB.

**Day — Kloofendal 48d Partly Cloudy (Pure Sky).** Midday, partly cloudy, crisp clouds. *Pure
Sky* means the ground is removed, which is what you want when only sky shows above the stands. A
clear blue gradient would give you nothing to bounce and read as fake.

**Night — Dikhololo Night.** Clear, low contrast, starlight. Deliberately *not* Moonless Golf,
which includes warm clubhouse lamps that would fight your own floodlight rig. For a floodlit
match you want the night HDRI contributing near zero.

- [ ] **Step 3: Import with correct settings**

Place in `Assets/Art/Sky/`. For each `.hdr`, in the Inspector set:

| Setting | Value |
|---|---|
| Texture Shape | **Cube** |
| Mapping | **Latitude-Longitude (Cylindrical)** |
| Convolution Type | **None** |
| Max Size | **2048** |
| Compression | **High Quality** |

- [ ] **Step 4: Create the skybox materials**

For each: Create → Material, shader **Skybox/Panoramic** (or **Skybox/Cubemap**). Assign the
cubemap. Name `Sky-Day` / `Sky-Night`.

- [ ] **Step 5: Assign to both scenes**

Open each scene → Lighting window → Environment → Skybox Material → `Sky-Day`.

- [ ] **Step 6: Re-wire the runtime day/night fields**

Open `CricketVR.unity`, select the `Main` GameObject. In the Inspector set **Day Material** →
`Sky-Day` and **Night Material** → `Sky-Night`.

Without this, `updateStadiumMode()` keeps nulling the skybox on every mode switch and the fix in
step 5 only survives until the player first toggles day/night.

- [ ] **Step 7: Verify all four references resolve to real assets**

```bash
for s in CricketVR Nets; do g=$(grep -m1 "m_SkyboxMaterial" "Assets/Scenes/$s.unity" | grep -o "guid: [0-9a-f]*" | cut -d' ' -f2); echo "$s skybox -> $(grep -rl "$g" --include='*.meta' Assets | head -1)"; done; for f in NightMaterial DayMaterial; do g=$(grep -m1 "$f:" Assets/Scenes/CricketVR.unity | grep -o "guid: [0-9a-f]*" | cut -d' ' -f2); echo "$f -> $(grep -rl "$g" --include='*.meta' Assets | head -1)"; done
```

Expected: all four lines resolve to a real `.mat.meta` path. Any empty right-hand side is still
dangling.

- [ ] **Step 8: Verify the switch works at runtime**

Enter play mode and toggle stadium mode between Day and Night in the settings menu. The sky must
change between the two HDRIs and must never go black or default-blue.

- [ ] **Step 9: Commit**

```bash
git add Assets/Art/Sky Assets/Scenes && git commit -m "fix: restore all three skybox refs deleted in P2 purge, add day and night HDRI skies"
```

## Task 17: Sky-derived ambient, sun, and fog

**Files:**
- Modify: `Assets/Scenes/CricketVR.unity` lines 18, 28, 41

Three one-line changes with disproportionate impact.

- [ ] **Step 1: Set ambient source to Skybox**

Lighting window → Environment → Environment Lighting → Source → **Skybox**, Intensity **1.0**.

This replaces the current flat grey (`m_AmbientMode: 3`, `m_AmbientSkyColor` 0.585 grey), which is
why nothing currently looks like it belongs in its environment.

- [ ] **Step 2: Assign the sun**

Lighting window → Environment → Sun Source → **Sun Light 1**.

Currently `m_Sun: {fileID: 0}`. With it assigned, the skybox sun disc tracks the directional light
instead of contradicting it.

- [ ] **Step 3: Enable fog**

Lighting window → Environment → Fog → **On**, Mode **Exponential Squared**, Density **0.0015**,
Colour sampled from the horizon of the day HDRI.

Across a 150 m ground, aerial perspective is a large and nearly free depth cue. Density is
deliberately low — visible haze at the boundary, none at the pitch.

- [ ] **Step 4: Verify all three**

```bash
grep -nE "m_Fog: |m_FogMode: |m_FogDensity: |m_AmbientMode: |m_Sun: " Assets/Scenes/CricketVR.unity
```

Expected: `m_Fog: 1`, `m_FogMode: 3`, `m_FogDensity: 0.0015`, `m_AmbientMode: 0`, and `m_Sun` with
a non-zero fileID.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scenes/CricketVR.unity && git commit -m "lighting: sky-derived ambient, assigned sun, exponential fog"
```

## Task 18: Reflection probes

**Files:**
- Modify: `Assets/Scenes/CricketVR.unity`

The scene has zero reflection probes. Without them the bat, helmet, stumps and ball have no
specular grounding and read as plastic.

- [ ] **Step 1: Raise the default reflection resolution**

Lighting window → Environment → Reflections → Resolution **256** (currently 128).

- [ ] **Step 2: Create the main bowl probe**

GameObject → Light → Reflection Probe. Name `RP_Bowl`. Set:

| Setting | Value |
|---|---|
| Type | **Baked** |
| Box Projection | **On** |
| Position | pitch centre, **y = 8** |
| Box Size | enclose the whole playing area and the lower stands |
| Resolution | **256** |
| HDR | On |

- [ ] **Step 3: Create near-pitch and under-stand probes**

`RP_Pitch` — Baked, box projected, tight box over the pitch strip at y = 2, resolution **128**.
This is the one the bat and ball actually sample during play.

`RP_Stand_N/E/S/W` — Baked, box projected, one per stand section, resolution **64**. Under-stand
areas are dark and enclosed; the bowl probe will give them sky reflections they should not have.

- [ ] **Step 4: Bake reflections**

Lighting window → Generate Lighting (reflection probes bake with the lightmaps).

- [ ] **Step 5: Verify probes exist and baked**

```bash
grep -c "^ReflectionProbe:" Assets/Scenes/CricketVR.unity; ls Assets/Scenes/CricketVR/ | grep -ci reflection
```

Expected: **6** probes, and at least one baked reflection texture in the bake directory.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scenes && git commit -m "lighting: add baked box-projected reflection probes"
```

## Task 19: Light probes for dynamic objects

**Files:**
- Modify: `Assets/Scenes/CricketVR.unity`

The batsman, bowler, ball and 19 fielders are all dynamic and currently receive **only** flat
ambient. This is the task that makes them look like they are standing on grass.

- [ ] **Step 1: Create the probe group**

GameObject → Light → Light Probe Group. Name `LPG_PlayArea`.

- [ ] **Step 2: Place probes over the play area**

Delete the four default probes. Place a lattice covering where dynamic objects actually go:

- Pitch strip and inner ring: probes every **4 m**, at heights **0.3 m, 1.2 m, 2.2 m**
- Outfield to the boundary: probes every **12 m**, at heights **0.3 m, 1.8 m**
- A ring just inside the boundary rope at **0.3 m and 1.8 m**

Denser near the pitch because that is where the player looks and where the ball spends its
most-scrutinised moments.

- [ ] **Step 3: Rebake**

Lighting window → Generate Lighting.

- [ ] **Step 4: Verify probes baked**

```bash
grep -c "^LightProbeGroup:" Assets/Scenes/CricketVR.unity
```

Expected: **1**. Then in the Editor, move the Ball prefab around the outfield in scene view and
watch its shading change — that is the probe data working.

- [ ] **Step 5: Capture phase 3 reference shots**

Menu: CricketVR → Capture Reference Shots → `current`, then:

```bash
cd Docs/Baseline && for f in current-*.png; do mv "$f" "phase3-${f#current-}"; done && cd -
```

Compare `phase3-*.png` against `phase2-*.png`. The sky is now a real HDRI, surfaces pick up its
colour, and fog should separate the boundary from the stands behind it.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scenes Docs/Baseline && git commit -m "lighting: add light probe lattice over the play area"
```

## Task 20: APV spike (gated upgrade — may be abandoned)

**Files:** throwaway branch only

Adaptive Probe Volumes are production-ready in URP 17 and Unity shipped a per-vertex quality mode
aimed at mobile VR, but **Quest-specific behaviour is unconfirmed**. Task 19 is the baseline;
this task either beats it measurably or is discarded.

- [ ] **Step 1: Make sure the tree is clean first**

No branching (constraint 1), so this spike is a **revertible working-tree experiment**. Commit or
stash anything outstanding before starting, or you will not be able to cleanly undo it.

```bash
git status --short
```

Expected: empty.

- [ ] **Step 2: Enable APV**

Project Settings → Graphics → Lighting → **Light Probe System: Adaptive Probe Volumes**.
On `URP-Quest`, set APV Memory Budget **Low** and **Probe Blending on, per-vertex sampling**.
Add an Adaptive Probe Volume covering the playing area. Disable `LPG_PlayArea`. Rebake.

- [ ] **Step 3: 🔺 DEFERRED — the measurement this decision needs**

APV versus hand-placed probes is fundamentally a **performance** question, and with device builds
deferred it cannot be answered. Do not guess.

**Recommended action now:** capture reference shots with APV enabled, compare against the task-19
probe lattice, record both the images and the open question — then **discard the branch and keep
the probe lattice** as the shipping baseline. It is the known-safe option. Revisit in Phase D.

- [ ] **Step 4: Discard the spike**

Unless the visual difference is dramatic, revert everything the spike touched:

```bash
git checkout -- Assets/Settings Assets/Scenes ProjectSettings && git status --short
```

Expected: empty. Then re-enable `LPG_PlayArea` and rebake if the revert disturbed it.

- [ ] **Step 5: Record the decision in `Docs/Baseline.md` either way**

Write down the measured numbers and the verdict. A negative result is a real result and stops
this question being reopened later.

- [ ] **Step 6: Commit the record**

Do this whichever way the spike went, so the finding survives the experiment being reverted.

```bash
git add Docs/Baseline.md && git commit -m "docs: record APV spike measurement and verdict"
```

**PHASE 3 GATE:** skybox references resolve in both scenes · bat and ball visibly pick up green
bounce from the outfield · framerate held · APV decision recorded.

---

# Phase 4 — PBR materials

Zero of the project's ~60 materials currently have a normal map, and only 9 texture files exist.
This is the surface-detail half of "ultra-realistic."

## Task 21a: Re-UV the stadium in Blender

**Files:**
- Create: `RAW-Assets/Stand1-uv.fbx` (re-unwrapped), then reimported over the stadium mesh

**Blocking for the rest of phase 4.** Task 5 established that `Stand1` — the whole visible
stadium, 43,630 triangles — has **no UV0**. Nothing in this phase can texture it until it does.

Tier 2 of the sourcing waterfall: authored in Blender, since no free replacement stadium exists
that is both generic and non-trademarked (see AssetSources.md gap #5).

- [ ] **Step 1: Import and isolate**

Through the Blender MCP, import `Assets/Resources/Models/FullStadium.fbx` and keep only
`Stand1`. The import takes about 5 seconds; well inside the 180 s socket timeout.

- [ ] **Step 2: Unwrap**

Use **Smart UV Project** with an island margin of about 0.02. A stadium stand is a repetitive
extruded form, so a projection unwrap is appropriate — hand-seaming buys nothing here.

Check afterwards that UV area per face is roughly proportional to world area; wildly uneven
texel density means the tiling rate in task 23 cannot serve the whole mesh.

- [ ] **Step 3: Export**

`export_scene(format="fbx", object_names=["Stand1"], apply_modifiers=True)` to `RAW-Assets/`.

- [ ] **Step 4: Verify UV0 now exists in Unity**

After reimporting, confirm through the MCP that the mesh reports a non-empty `uv` array. The
exact check that produced the original verdict:

```
mesh.uv != null && mesh.uv.Length > 0
```

Expected: a vertex count's worth of UVs where there were none.

- [ ] **Step 5: Confirm nothing else broke**

`read_console(action="get", types=["error"])` — expected empty. Then capture reference shots and
confirm the stadium still renders in the boundary view; a bad reimport silently loses materials.

- [ ] **Step 6: Commit**

```bash
git add RAW-Assets Assets/Resources/Models && git commit -m "art: re-UV stadium Stand1 mesh for PBR texturing"
```

## Task 21: Acquire and import PBR source sets

**Files:**
- Create: `Assets/Art/Textures/<material>/`

- [ ] **Step 1: Download the sets (tier 1 — free, CC0)**

All verified CC0 and reachable on 2026-09-21. Full table with licences and caveats in
[AssetSources.md](../AssetSources.md).

| Surface | Asset | URL |
|---|---|---|
| Outfield turf | ambientCG **Grass005** | https://ambientcg.com/a/Grass005 |
| Turf variation | ambientCG **Grass001** / **Grass004** | https://ambientcg.com/a/Grass001 |
| Pitch strip | Poly Haven **Brown Mud Dry** | https://polyhaven.com/a/brown_mud_dry |
| Concrete | ambientCG **Concrete034** / **Concrete047A** | https://ambientcg.com/a/Concrete034 |
| Seating plastic | ambientCG **Plastic015A** (+013A/016A/017A for colour variety) | https://ambientcg.com/a/Plastic015A |
| Painted metal | ambientCG **PaintedMetal005**, **Metal027** | https://ambientcg.com/a/PaintedMetal005 |
| Willow (bat) | ambientCG **Wood090A** | https://ambientcg.com/a/Wood090A |
| Leather (ball) | cgbookcase **Red Leather 01** | https://www.cgbookcase.com/textures/red-leather-01 |
| Cotton (whites) | cgbookcase **blue-cotton-01**, desaturated | https://www.cgbookcase.com/textures/blue-cotton-01 |

**Download rules — these matter more than the choice of asset:**

- **2K JPG variants, not 4K.** Every one of these tiles; resolution buys nothing and costs Quest
  texture memory. Downloading 2K directly also skips a Unity reimport at reduced max size.
- **Take `nor_gl`, never `nor_dx`.** Unity uses the OpenGL normal convention. Both sites ship
  both files and the names are one character apart.
- **Discard every displacement and height map.** There is no tessellation budget on Quest.
- Two known compromises, accepted: **Wood090A is birch, not willow** (warmer and more figured —
  desaturate and push grain contrast), and **no free source has a cotton twill weave** (invisible
  at Quest viewing distance). Both recorded in AssetSources.md.
- **Plastic015A ships no AO map.** That is fine — `OrmPacker` defaults occlusion to 1.0.

- [ ] **Step 2: Import with mobile-correct settings**

For every **albedo**: sRGB **on**, Max Size **2048**, Compression **High Quality**, Format
**ASTC 6x6**, Generate Mip Maps **on**, Aniso Level **4** on ground textures / **1** elsewhere.

For every **normal map**: Texture Type **Normal map**, sRGB **off** (Unity forces this), Max Size
**2048**, Format **ASTC 6x6**.

For every **occlusion / roughness / metallic** greyscale input: sRGB **off**. These are data, not
colour — leaving sRGB on is the single most common PBR authoring bug and it will make every
surface subtly wrong.

- [ ] **Step 3: Verify sRGB is off on all data maps**

```bash
for f in $(find Assets/Art/Textures -name "*orm*" -o -name "*rough*" -o -name "*metal*" -o -name "*ao*" | grep -v meta); do printf "%-60s sRGB=%s\n" "$f" "$(grep -m1 'sRGBTexture:' "$f.meta" | tr -d ' ')"; done
```

Expected: every line shows `sRGBTexture:0`.

- [ ] **Step 4: Commit**

```bash
git add Assets/Art/Textures && git commit -m "art: import CC0 PBR source texture sets"
```

## Task 22: Channel-pack ORM maps

**Files:**
- Create: `Assets/Editor/OrmPacker.cs`

URP's Lit shader reads metallic from **R** and smoothness from **A** of the Metallic map, and
occlusion from **G** of the Occlusion map. Three separate greyscale textures is three fetches;
one packed texture is one.

> ⚠️ **Do not shortcut this with Poly Haven's `arm` file.** Poly Haven ships an `arm` texture
> packing **AO / Roughness / Metallic** into R/G/B. That is the glTF and Unreal convention and is
> **not** what URP Lit reads — feeding it straight in gives you AO-as-metallic,
> roughness-as-occlusion, and no smoothness at all.
>
> Verified against the Poly Haven API: `brown_mud_dry` exposes `AO`, `Rough`, `nor_gl`, `Diffuse`,
> `Displacement`, `arm`, `spec`, `Bump`. **Use the separate `AO` and `Rough` maps as packer
> inputs and ignore `arm`.** Most of these surfaces are dielectric with no metal map at all,
> which the packer handles by defaulting metallic to 0.

- [ ] **Step 1: Write the packer**

Create `Assets/Editor/OrmPacker.cs`:

```csharp
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CricketVR.EditorTools
{
    /// <summary>
    /// Packs three greyscale maps into one RGBA texture in the layout URP/Lit expects:
    /// R = metallic, G = occlusion, A = smoothness (1 - roughness). B is unused.
    /// Select exactly three textures named *_metallic, *_ao (or *_occlusion), *_roughness.
    /// </summary>
    public static class OrmPacker
    {
        [MenuItem("CricketVR/Art/Pack Selected ORM Textures")]
        public static void Pack()
        {
            Texture2D metallic = null, occlusion = null, roughness = null;

            foreach (var obj in Selection.objects)
            {
                var tex = obj as Texture2D;
                if (tex == null) continue;
                var n = tex.name.ToLowerInvariant();
                if (n.Contains("metal")) metallic = tex;
                else if (n.Contains("ao") || n.Contains("occlusion")) occlusion = tex;
                else if (n.Contains("rough")) roughness = tex;
            }

            if (roughness == null)
            {
                Debug.LogError("[OrmPacker] need at least a *_roughness texture selected");
                return;
            }

            EnsureReadable(metallic);
            EnsureReadable(occlusion);
            EnsureReadable(roughness);

            var w = roughness.width;
            var h = roughness.height;
            var outTex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            var px = new Color[w * h];

            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var i = y * w + x;
                    var m = metallic  != null ? metallic.GetPixel(x, y).r  : 0f;
                    var o = occlusion != null ? occlusion.GetPixel(x, y).r : 1f;
                    var r = roughness.GetPixel(x, y).r;
                    px[i] = new Color(m, o, 0f, 1f - r);   // A = smoothness
                }
            }

            outTex.SetPixels(px);
            outTex.Apply();

            var dir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(roughness));
            var baseName = roughness.name.ToLowerInvariant()
                .Replace("_roughness", "").Replace("roughness", "");
            var path = Path.Combine(dir, $"{baseName}_orm.png");
            File.WriteAllBytes(path, outTex.EncodeToPNG());
            Object.DestroyImmediate(outTex);

            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.sRGBTexture = false;          // data, not colour
            imp.alphaSource = TextureImporterAlphaSource.FromInput;
            imp.alphaIsTransparency = false;
            imp.SaveAndReimport();

            Debug.Log($"[OrmPacker] wrote {path}");
        }

        static void EnsureReadable(Texture2D tex)
        {
            if (tex == null) return;
            var path = AssetDatabase.GetAssetPath(tex);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null && !imp.isReadable)
            {
                imp.isReadable = true;
                imp.SaveAndReimport();
            }
        }
    }
}
```

- [ ] **Step 2: Pack each material set**

Select the three source maps for one material, then CricketVR → Art → Pack Selected ORM Textures.
Repeat for all eight sets.

- [ ] **Step 3: Verify the outputs**

```bash
ls Assets/Art/Textures/*/*_orm.png | wc -l
```

Expected: **8**.

- [ ] **Step 4: Turn the source maps' read/write back off**

The packer enables Read/Write to sample pixels, which doubles their memory at runtime. Select all
`*_metallic`, `*_ao`, `*_roughness` sources and untick Read/Write Enabled. The packed `_orm`
outputs are the ones actually shipped; consider excluding the sources from the build entirely.

- [ ] **Step 5: Commit**

```bash
git add Assets/Editor/OrmPacker.cs Assets/Art/Textures && git commit -m "art: add ORM channel packer and pack PBR source sets"
```

## Task 23: Author the materials

**Files:**
- Create: `Assets/Art/Materials/*.mat`
- Modify: `Assets/Resources/Prefabs/Stadium.prefab`, `Assets/Resources/Prefabs/Pitch.prefab`, and the bat/ball/stump prefabs

- [ ] **Step 1: Create one URP Lit material per surface**

For each of the eight sets, Create → Material, shader **Universal Render Pipeline/Lit**. Assign:

- **Base Map** → albedo
- **Metallic Map** → the packed `_orm`, with **Source: Metallic Alpha** so smoothness reads from A
- **Normal Map** → normal, strength **1.0**
- **Occlusion** → the same `_orm` (URP reads occlusion from G)
- Workflow **Metallic**, Surface **Opaque**

- [ ] **Step 2: Set tiling to real-world scale**

Get this wrong and everything reads as a toy. Set tiling so one texture repeat covers roughly its
real-world size: turf ~2 m, concrete ~4 m, seating ~0.5 m.

If task 5 returned `usable-with-density-compensation`, tiling per-material is where you
compensate.

- [ ] **Step 3: Assign materials to renderers**

Replace the converted URP Lit materials on the Stadium, Pitch, Bat, Ball and Stump prefabs.

- [ ] **Step 4: Verify normal maps are actually assigned**

```bash
c=0; for f in $(find Assets/Art/Materials -name "*.mat"); do if grep -A3 -- "- _BumpMap:" "$f" | grep -q "m_Texture: {fileID: 2800000"; then c=$((c+1)); fi; done; echo "materials with normal maps: $c"
```

Expected: **8**. The project-wide count was **0** before this task — that delta is the point of
phase 4.

- [ ] **Step 5: Commit**

```bash
git add Assets/Art/Materials Assets/Resources/Prefabs && git commit -m "art: author PBR materials with normal and ORM maps"
```

## Task 24: Outfield mown stripes and pitch wear

**Files:**
- Modify: `Assets/Art/Materials/Turf.mat`, `Assets/Art/Materials/PitchSoil.mat`

Two specifics that carry disproportionate weight. Mown stripes are the most recognisable "real
cricket ground" cue that exists, and they cost one texture.

> **Design decision: the stripes are a shader function, not a texture.** A CC0 striped-turf
> texture does exist (TextureCan [Ground 0040](https://www.texturecan.com/details/469/),
> including a parametric `.sbsar`) and it is tempting. **Do not bake it into the albedo.** Two
> reasons:
>
> 1. **Baking couples band width to the grass tiling rate.** Tile the grass enough to keep blade
>    detail crisp at VR near-field and the bands become a corduroy floor rather than a cricket
>    ground.
> 2. **Mowing stripes are a reflectance effect, not an albedo effect.** The blades are physically
>    bent toward or away from the viewer, flipping their specular response — which is why a real
>    ground's stripes *invert* as you walk to the other end. An albedo stripe cannot do that, and
>    in VR, where the player turns their head constantly, the difference is obvious.
>
> Driving it from world position also costs **zero extra texture samples** on the tile GPU, and
> lets a parameter switch between straight bands and the concentric rings some grounds use.
> Keep the TextureCan sbsar as a visual reference for band contrast.

- [ ] **Step 1: Create a turf Shader Graph**

Create → Shader Graph → URP → Lit Shader Graph. Name it `TurfStriped`. Wire the phase-4 turf
maps into Base Color, Normal and the ORM inputs as normal, then add the stripe on top.

- [ ] **Step 2: Build the stripe node chain**

| Step | Nodes |
|---|---|
| Get world position | `Position` (space: **World**) → `Split` |
| Pick the mow axis | take the **X** channel (bands run across the square; swap to Z to rotate 90°) |
| Band frequency | `Divide` by a `Float` property **BandWidth**, default **3.0** (metres per band) |
| Square wave | `Fraction` → `Step` (edge 0.5) — gives a hard 0/1 alternation |
| Soften the edge | `Smoothstep` (0.45 → 0.55) on the `Fraction` output instead of `Step` |

Call the result **StripeMask** (0 or 1 per band).

- [ ] **Step 3: Drive reflectance, not colour**

This is the part that makes it read correctly:

- **Smoothness** → `Lerp(smoothnessAway, smoothnessToward, StripeMask)`. Expose both as
  properties; start **0.25** and **0.45**.
- **Normal** → flip the green channel per band. `Split` the sampled normal, `Multiply` Y by
  `Lerp(-1, 1, StripeMask)`, `Combine` back. This is what physically tilts the blades.
- **Base Color** → a *very* slight `Lerp` between two greens, no more than ~4% luminance apart.
  Resist making this large; if the stripes are obvious in albedo you have overdone it.

- [ ] **Step 4: Apply and orient**

Assign `TurfStriped` to the outfield renderer. Set the mow axis so bands run **perpendicular to
the pitch**, as a groundsman mows.

- [ ] **Step 5: Add a detail normal to the turf**

Feed the turf normal map a second time through a `Sample Texture 2D` with tiling **8×** the base,
`Blend` it with the primary normal (mode: Reoriented), and route the result into the stripe
green-flip from step 3. The outfield is a huge surface right in front of the player; a single
tiling rate reads flat at close range and this is the cheapest fix.

- [ ] **Step 6: Author the pitch wear mask**

A 1024×1024 greyscale with darker, scuffed regions at each end's good-length area and at the
batsman's crease. Multiply into the pitch material's base map, and raise roughness there.

- [ ] **Step 7: Capture and compare phase 4 reference shots**

Menu: CricketVR → Capture Reference Shots → `current`, then:

```bash
cd Docs/Baseline && for f in current-*.png; do mv "$f" "phase4-${f#current-}"; done && cd -
```

Compare `phase4-*.png` against `phase3-*.png`. The mid-pitch shot should clearly show both the
mown stripes and the worn good length.

- [ ] **Step 8: Record texture memory and commit**

```bash
du -sh Assets/Art/Textures/ && cp Docs/Baseline/stats-current.txt Docs/Baseline/stats-phase4.txt
```

🔺 **DEFERRED** — whether this texture set fits Quest 3 memory alongside the phase 2 lightmaps is
unverified. Logged in Phase D.

```bash
git add Assets/Art Docs/Baseline Docs/Baseline.md && git commit -m "art: add mown stripes, turf detail normal, and pitch wear"
```

**PHASE 4 GATE:** 8 materials carry normal maps (from 0) · texture memory within budget on device
· visible improvement at all three cameras · framerate held.

---

# Phase 5 — Grading

Where "looks like Unity" becomes "looks graded." Also the phase with the most potential to make
people sick, so the exclusions below are not stylistic preferences.

## Task 25: Global volume and tonemapping

**Files:**
- Create: `Assets/Settings/Volumes/Global-Day.asset`
- Modify: `Assets/Scenes/CricketVR.unity`

- [ ] **Step 1: Enable post-processing on the camera**

Select the main camera under `OVRCameraRig` (`CenterEyeAnchor`). In its Camera component's
Rendering section, tick **Post Processing**.

- [ ] **Step 2: Create the global volume**

GameObject → Volume → Global Volume. Name `PostVolume`. Create a new profile saved as
`Assets/Settings/Volumes/Global-Day.asset`.

- [ ] **Step 3: Add the overrides**

| Override | Settings | Why |
|---|---|---|
| **Tonemapping** | Mode **Neutral** initially | See step 4 — this is a device decision |
| **Color Adjustments** | Post Exposure **0**, Contrast **+10**, Saturation **+8** | Gentle. Cranking saturation is the classic mobile-VR mistake |
| **White Balance** | Temperature **+5** | Warms the daylight slightly; pure-neutral daylight reads clinical |
| **Bloom** | Threshold **1.1**, Intensity **0.15**, Scatter **0.6** | High threshold so only genuine highlights bloom |
| **Shadows Midtones Highlights** | lift shadows very slightly toward blue | Approximates sky fill in shadow, which the bake will not fully give you |

- [ ] **Step 4: Choose the tonemapper on the device, not the monitor**

Build with **Neutral**, measure and look. Then build with **ACES** and compare in the headset.

ACES has more filmic highlight rolloff but crushes shadows, which on Quest panels can turn the
under-stand areas into black mush. **Whichever looks better in the headset wins** — do not decide
this on a desktop monitor. Record the choice in `Docs/Baseline.md`.

- [ ] **Step 5: Confirm the exclusions**

Do **not** add: Motion Blur, Depth of Field, Vignette above trivial strength, Chromatic
Aberration, Lens Distortion, Film Grain. Each is either nausea-inducing in VR, fights the
headset's own lens correction, or wastes bandwidth on a tile GPU.

- [ ] **Step 6: Verify the volume exists and post is on**

```bash
grep -c "m_RenderPostProcessing: 1" Assets/Scenes/CricketVR.unity; ls -l Assets/Settings/Volumes/Global-Day.asset
```

Expected: at least 1 camera with post enabled, and the profile asset present.

- [ ] **Step 7: 🔺 DEFERRED — the cost of the post pass, and the tonemapper choice**

The post pass is **not** free on a tile GPU, and its cost is exactly what the Editor cannot show
you. Budget for when you can measure: if it exceeds ~1.0 ms, drop Bloom first — most expensive
override here, least load-bearing.

Also deferred: **the tonemapper choice itself.** Step 4 says judge ACES against Neutral in the
headset, and that is not soft advice — Quest panels crush shadows differently from a desktop
monitor. **Ship Neutral until it can be judged on device**; it is the safer default and far less
likely to turn the under-stand areas into black mush. Logged in Phase D.

- [ ] **Step 8: Commit**

```bash
git add Assets/Settings/Volumes Assets/Scenes/CricketVR.unity Docs/Baseline.md && git commit -m "render: add global post volume with tonemapping and grading"
```

## Task 26: Day and night grading profiles

**Files:**
- Create: `Assets/Settings/Volumes/Global-Night.asset`
- Create: `Assets/Scripts/GradingSwitcher.cs`
- Modify: `Assets/Scripts/Main.cs` around line 590

- [ ] **Step 1: Create the night profile**

Duplicate `Global-Day.asset` → `Global-Night.asset`. Adjust: Post Exposure **-0.3**, Temperature
**-10** (floodlights read cooler), Bloom Intensity **0.35** (floodlights should bloom; daylight
should not).

- [ ] **Step 2: Write the switcher**

Create `Assets/Scripts/GradingSwitcher.cs`:

```csharp
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Swaps the global Volume profile when the stadium changes between day and night.
/// Owned separately from Main so the grading assets are not another hand-maintained
/// quartet in Main's settings block.
/// </summary>
public class GradingSwitcher : MonoBehaviour
{
    [SerializeField] Volume globalVolume;
    [SerializeField] VolumeProfile dayProfile;
    [SerializeField] VolumeProfile nightProfile;

    public void SetDay()
    {
        Apply(dayProfile);
    }

    public void SetNight()
    {
        Apply(nightProfile);
    }

    void Apply(VolumeProfile profile)
    {
        if (globalVolume == null || profile == null)
        {
            Debug.LogWarning("[GradingSwitcher] volume or profile not assigned");
            return;
        }
        globalVolume.profile = profile;
    }
}
```

- [ ] **Step 3: Add the field to Main**

In `Assets/Scripts/Main.cs`, alongside the existing `theDayLights` / `theNightLights` fields
(lines 58 and 60), add:

```csharp
    [SerializeField]
    protected GradingSwitcher theGradingSwitcher;
```

- [ ] **Step 4: Call it from the existing switch**

In `updateStadiumMode()`, add one line to each existing branch. The night branch (currently
`Main.cs:579-585`) becomes:

```csharp
        if (stadiumMode == eStadiumMode.Night && !theNightLights.activeSelf)
        {
            changed = true;
            RenderSettings.skybox = NightMaterial;
            theNightLights.SetActive(true);
            theDayLights.SetActive(false);
            if (theGradingSwitcher != null) theGradingSwitcher.SetNight();
            PlayerPrefs.SetInt(Constants.PP_StadiumMode, (int)stadiumMode);
        }
```

and the day branch (currently `Main.cs:587-594`):

```csharp
        else if(stadiumMode == eStadiumMode.Day && !theDayLights.activeSelf)
        {
            changed = true;
            RenderSettings.skybox = DayMaterial;
            theNightLights.SetActive(false);
            theDayLights.SetActive(true);
            if (theGradingSwitcher != null) theGradingSwitcher.SetDay();
            PlayerPrefs.SetInt(Constants.PP_StadiumMode, (int)stadiumMode);
        }
```

The null guard keeps the scene working if the reference is not wired, which matters because this
project has a history of dangling serialized references.

- [ ] **Step 5: Wire it in the scene**

Add `GradingSwitcher` to the `PostVolume` GameObject. Assign its three fields. Then assign that
component to `Main`'s new `theGradingSwitcher` field.

- [ ] **Step 6: Verify at runtime**

Enter play mode, toggle day/night. Both the skybox (task 16) and the grading must change. Check
the console for the `[GradingSwitcher] volume or profile not assigned` warning — if you see it,
step 5 is incomplete.

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/GradingSwitcher.cs Assets/Scripts/Main.cs Assets/Settings/Volumes Assets/Scenes/CricketVR.unity && git commit -m "render: add day and night grading profiles wired to stadium mode"
```

**PHASE 5 GATE:** post pass measured in GPU ms on device · tonemapper chosen in the headset ·
day/night switches both sky and grading · none of the excluded effects present.

---

# Phase 6 — Crowd and life

Empty stands read as fake instantly, and no amount of lighting fixes that.

## Task 27: Impostor crowd

**Files:**
- Create: `Assets/Art/Crowd/` (sprite sheet, material)
- Create: `Assets/Scripts/CrowdSpawner.cs`
- Modify: `Assets/Scenes/CricketVR.unity`

> **Tier 2 — this one must be authored.** Verified: **no free spectator impostor atlas exists.**
> OpenGameArt returns crowd *audio* only. The Unity Asset Store's Stadium Crowd Generator is
> $19.99 (tier 3). Nothing CC0 or CC-BY surfaced on any source checked. This is the single
> biggest authoring item in the plan — budget a day or two.

- [ ] **Step 1: Author the crowd atlas in Blender (via the task 2 MCP)**

Source characters: [Quaternius Ultimate Modular Men](https://quaternius.com/packs/ultimatemodularcharacters.html)
— **CC0**, 11 modular characters with swappable parts.

Pipeline, all driveable through `execute_blender_code`:

1. Import the Quaternius pack; build ~12 body variants by mixing the modular parts.
2. Pose them **seated** — stands need seated spectators, and this is the one thing the free
   posed-human packs do not reliably provide.
3. Randomise shirt colour per variant via material variants.
4. Render **8–16 yaw angles × ~12 variants** orthographic, transparent background, to a 2K atlas.
5. Export the atlas PNG to `Assets/Art/Crowd/`.

⚠️ Watch the **180-second MCP socket timeout** flagged in task 2. A 192-cell render will exceed
it. Render in batches, or write the script to disk and run
`blender --background --python crowd_atlas.py`.

- [ ] **Step 2: Import the atlas**

2048², ASTC 6x6, sRGB **on**, **Alpha Is Transparency on**, mipmaps on.

- [ ] **Step 3: Write the spawner**

Create `Assets/Scripts/CrowdSpawner.cs`:

```csharp
using UnityEngine;

/// <summary>
/// Spawns GPU-instanced billboard quads across a stand section. One draw call per section
/// via instancing; each instance picks a random atlas cell and a small scale/offset jitter.
/// </summary>
public class CrowdSpawner : MonoBehaviour
{
    [SerializeField] Mesh quad;
    [SerializeField] Material crowdMaterial;   // must have Enable GPU Instancing ticked

    [Header("Section layout")]
    [SerializeField] int rows = 20;
    [SerializeField] int seatsPerRow = 60;
    [SerializeField] float rowSpacing = 0.9f;
    [SerializeField] float seatSpacing = 0.55f;
    [SerializeField] float rowRise = 0.45f;
    [SerializeField, Range(0f, 1f)] float occupancy = 0.75f;
    [SerializeField] int atlasCells = 4;       // 4x4 atlas

    Matrix4x4[] matrices;
    MaterialPropertyBlock block;

    void Start()
    {
        var list = new System.Collections.Generic.List<Matrix4x4>();
        var offsets = new System.Collections.Generic.List<Vector4>();

        for (var r = 0; r < rows; r++)
        {
            for (var s = 0; s < seatsPerRow; s++)
            {
                if (Random.value > occupancy) continue;

                var local = new Vector3(
                    (s - seatsPerRow * 0.5f) * seatSpacing,
                    r * rowRise,
                    r * rowSpacing);

                var pos = transform.TransformPoint(local);
                var scale = Vector3.one * Random.Range(0.92f, 1.08f);
                list.Add(Matrix4x4.TRS(pos, transform.rotation, scale));

                var cell = Random.Range(0, atlasCells * atlasCells);
                var u = (cell % atlasCells) / (float)atlasCells;
                var v = (cell / atlasCells) / (float)atlasCells;
                offsets.Add(new Vector4(1f / atlasCells, 1f / atlasCells, u, v));
            }
        }

        matrices = list.ToArray();
        block = new MaterialPropertyBlock();
        block.SetVectorArray("_MainTex_ST_Array", offsets.ToArray());
    }

    void Update()
    {
        if (matrices == null || matrices.Length == 0) return;

        // DrawMeshInstanced caps at 1023 per call.
        for (var i = 0; i < matrices.Length; i += 1023)
        {
            var count = Mathf.Min(1023, matrices.Length - i);
            Graphics.DrawMeshInstanced(quad, 0, crowdMaterial, matrices, count, block);
        }
    }
}
```

- [ ] **Step 4: Create the crowd material**

Shader **Universal Render Pipeline/Unlit**, Surface **Transparent**, Alpha Clipping **on**
threshold 0.5, **Enable GPU Instancing ticked**, Render Face **Both**.

Unlit is deliberate: the crowd is in shadow under the stands and lighting it correctly costs more
than it returns at that screen size.

- [ ] **Step 5: Place one spawner per stand section**

Empty GameObject at the base of each section, rotated to face the pitch, with `CrowdSpawner`
attached. Tune `rows` / `seatsPerRow` / `rowRise` to match that section's geometry.

- [ ] **Step 6: Verify instancing is actually engaging**

Window → Analysis → Frame Debugger, Game view on the boundary viewpoint. Find the crowd draws.

Expected: a small number of instanced draws, **not** hundreds of individual ones. Hundreds means
GPU instancing is not engaging — confirm **Enable GPU Instancing** is ticked on the crowd
material. Worth checking here because the failure mode is a silent 100× draw-call increase.

🔺 **DEFERRED** — actual crowd cost at Quest 3 fill rate. Transparent billboards are
fill-rate-hungry and a stand full of them is precisely the case a tile GPU dislikes. Phase D.

- [ ] **Step 7: Commit**

```bash
git add Assets/Art/Crowd Assets/Scripts/CrowdSpawner.cs Assets/Scenes/CricketVR.unity && git commit -m "art: add GPU-instanced impostor crowd"
```

## Task 28: Flags and wind

**Files:**
- Create: `Assets/Art/Shaders/WindBanner.shader`
- Modify: `Assets/Scenes/CricketVR.unity`

- [ ] **Step 1: Write the shader**

Create `Assets/Art/Shaders/WindBanner.shader`:

```shaderlab
Shader "CricketVR/WindBanner"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _WindSpeed ("Wind Speed", Float) = 2.0
        _WindStrength ("Wind Strength", Float) = 0.08
        _WaveScale ("Wave Scale", Float) = 3.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float _WindSpeed;
                float _WindStrength;
                float _WaveScale;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                // Displace along X, scaled by UV.x so the hoist edge stays pinned.
                float wave = sin(_Time.y * _WindSpeed + IN.positionOS.y * _WaveScale);
                float3 pos = IN.positionOS.xyz;
                pos.x += wave * _WindStrength * IN.uv.x;
                OUT.positionCS = TransformObjectToHClip(pos);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
            }
            ENDHLSL
        }
    }
}
```

- [ ] **Step 2: Create banner geometry**

Subdivided quads (at least 8 segments vertically) — the vertex displacement needs vertices to move.
Place along the boundary and on the stand roofline.

- [ ] **Step 3: Verify it animates and costs nothing measurable**

Enter play mode; the banners should ripple. 🔺 **DEFERRED** — GPU cost relative to task 27.

- [ ] **Step 4: Commit**

```bash
git add Assets/Art/Shaders Assets/Scenes/CricketVR.unity && git commit -m "art: add wind-animated boundary banners"
```

**PHASE 6 GATE:** crowd adds fewer than 20 draw calls · framerate held · stands no longer read as
empty at the boundary reference camera.

---

# Phase 7 — Performance reclaim and VR polish

Everything up to here spends budget. This phase buys it back.

## Task 29: LODs on the stadium

**Files:**
- Modify: `Assets/Resources/Models/FullStadium.fbx.meta`, `Assets/Resources/Prefabs/Stadium.prefab`

`FullStadium.fbx` is 44 MB and renders at full detail from 150 m away.

> 🔍 **Found during task 5: `Cube.001` is 1,735,672 triangles and is disabled in the scene.**
> It is the bulk of the 44 MB `FullStadium.fbx`. Because everything under `Assets/Resources/` is
> force-included, it ships in the build despite never rendering, and it is re-imported on every
> reimport. Deleting it from the source model is the single cheapest size win available.
>
> Active stadium geometry is only **5 renderers / 44,346 triangles** — so the decimation and LOD
> work below matters far less than simply removing the dead mesh. Do step 0 first and re-measure
> before investing in LODs.

- [ ] **Step 0: Delete the dead 1.7M-triangle mesh**

In Blender, import `FullStadium.fbx`, delete `Cube.001` (and the other disabled meshes confirmed
unused: `Sidebar`, `Sidebar.001`, `30Yrds`, `30Yrds.001`, `Cylinder`, `Cylinder.001`), and
re-export. Verify in Unity that the boundary reference shot is unchanged.

- [ ] **Step 1: Measure what you are actually drawing**

In the Editor, select the Stadium instance and read the triangle count from the Statistics panel
in Game view.

- [ ] **Step 2: Enable mesh compression on import**

Select `FullStadium.fbx` → Mesh Compression **Medium**, Optimize Mesh **on**, Read/Write
**off**.

Read/Write off is the important one: it halves the mesh's runtime memory by not keeping a CPU
copy, and nothing in this project reads stadium vertices at runtime.

- [ ] **Step 3: Verify the import settings took**

```bash
grep -nE "meshCompression:|isReadable:|optimizeMeshForGPU:|optimizeMeshVertices:" Assets/Resources/Models/FullStadium.fbx.meta
```

Expected: `meshCompression: 2` (Medium) and `isReadable: 0`.

- [ ] **Step 4: Add LOD groups to the stand sections**

Produce decimated LOD1 (50%) and LOD2 (20%) meshes in Blender from `RAW-Assets/`. Add an
`LODGroup` per stand section with transitions at 60% / 25% / 2% screen height.

The far stands are the whole point — they are large, numerous, and never inspected closely.

- [ ] **Step 5: Verify the triangle reduction**

Compare the Statistics panel figure against step 1 with the camera at the batsman position.
Target: at least a **40%** reduction in rendered triangles.

- [ ] **Step 6: Commit**

```bash
git add Assets/Resources/Models Assets/Resources/Prefabs && git commit -m "perf: compress stadium mesh and add stand LOD groups"
```

## Task 30: Move content out of Resources

**Files:**
- Modify: everything under `Assets/Resources/`

`Assets/Resources/` is **426 MB** and every byte is force-included in the build whether referenced
or not. RevivalPlan P4 already established that first-party code contains **zero** `Resources.Load`
calls.

- [ ] **Step 1: Re-confirm no runtime Resources.Load**

```bash
grep -rn "Resources.Load" Assets/Scripts/ Assets/*.cs 2>/dev/null || echo "CONFIRMED: no first-party Resources.Load"
```

Expected: `CONFIRMED`. If anything appears, that asset must stay in `Resources/`.

- [ ] **Step 2: Move the folders with git so GUIDs and meta files survive**

```bash
mkdir -p Assets/Art && git mv Assets/Resources/Models Assets/Art/Models && git mv Assets/Resources/Animations Assets/Art/Animations && git mv Assets/Resources/Textures Assets/Art/SourceTextures && git mv Assets/Resources/Prefabs Assets/Art/Prefabs && git mv Assets/Resources/Materials Assets/Art/SourceMaterials
```

`git mv` moves the `.meta` files too, so GUIDs are preserved and every scene and prefab reference
keeps resolving. Do **not** move these in Finder.

- [ ] **Step 3: Open the Editor and check for broken references**

Open `CricketVR.unity`. Console must show no "missing" warnings, and nothing in the scene may be
magenta or absent.

- [ ] **Step 4: Verify what remains in Resources**

```bash
du -sh Assets/Resources/ && ls Assets/Resources/
```

Expected: only the Oculus config assets (`OVRPlatformToolSettings.asset`, `OVRBuildConfig.asset`)
and `UI/`. Total well under 5 MB.

- [ ] **Step 5: Verify the size reduction at the source**

```bash
du -sh Assets/Resources/ Assets/Art/
```

Expected: `Assets/Resources/` under 5 MB, down from 426 MB. That 421 MB was force-included in
every build regardless of reference, so the APK saving follows directly — but the APK figure
itself is 🔺 **DEFERRED** to Phase D.

- [ ] **Step 6: Commit**

```bash
git add -A && git commit -m "perf: move 426MB of assets out of Resources so they are no longer force-included"
```

## Task 31: Foveated rendering and subsampling

**Files:**
- Modify: `Assets/XR/Settings/OpenXR Package Settings.asset`

- [ ] **Step 1: Enable subsampled layout**

```bash
grep -n "enableSubsampledLayout" "Assets/XR/Settings/OpenXR Package Settings.asset"
```

Expected before: `enableSubsampledLayout: 0`. Set both the Android and Standalone entries to `1`
in Project Settings → XR Plug-in Management → OpenXR → Foveated Rendering.

Subsampling makes foveation actually save bandwidth rather than just shading rate — without it,
foveated rendering on Quest leaves most of its benefit on the table.

- [ ] **Step 2: Set the foveation level at runtime**

In `Main.Start()`, or wherever XR init happens, request a foveation level. Start at **Medium** and
raise only if task 34's measurement demands it — high foveation is visible in peripheral vision
and cricket involves tracking a small fast object across the field of view.

- [ ] **Step 3: Verify**

```bash
grep -c "enableSubsampledLayout: 1" "Assets/XR/Settings/OpenXR Package Settings.asset"
```

Expected: **2** (Android and Standalone).

- [ ] **Step 4: 🔺 DEFERRED — the entire benefit of this task**

Foveated rendering saves GPU time and nothing else. It has **no** observable effect in the
Editor. The settings change is correct and safe to make now, but the payoff is unmeasurable until
device builds exist. Logged in Phase D.

```bash
git add "Assets/XR/Settings/OpenXR Package Settings.asset" Docs/Baseline.md && git commit -m "perf: enable foveated rendering with subsampled layout"
```

## Task 32: GPU instancing and occlusion rebake

**Files:**
- Modify: `Assets/Art/Materials/*.mat`, `Assets/Scenes/CricketVR.unity`

- [ ] **Step 1: Enable instancing on repeated-geometry materials**

Tick **Enable GPU Instancing** on the seating, railing and concrete materials. Leave it off for
one-off surfaces — it costs a constant-buffer path for no benefit there.

- [ ] **Step 2: Verify**

```bash
grep -l "m_EnableInstancingVariants: 1" Assets/Art/Materials/*.mat | wc -l
```

Expected: at least 3.

- [ ] **Step 3: Rebake occlusion culling**

The scene carries occlusion data from before any of this work
(`m_OcclusionCullingData` guid `b940ebc5…`), and it is now stale after the LOD and geometry
changes in task 29.

Window → Rendering → Occlusion Culling → Bake. Keep Smallest Occluder at **5** — the stands are
large occluders and that is what this scene has to work with.

- [ ] **Step 4: Verify the data regenerated**

```bash
ls -l Assets/Scenes/CricketVR/OcclusionCullingData.asset 2>/dev/null || find Assets -name "OcclusionCullingData*" -newermt "-1 hour"
```

Expected: a file modified within the last hour.

- [ ] **Step 5: Commit**

```bash
git add Assets/Art/Materials Assets/Scenes && git commit -m "perf: enable GPU instancing on repeated geometry, rebake occlusion"
```

## Task 33: Fixed timestep (gameplay-gated — may be abandoned)

**Files:**
- Modify: `ProjectSettings/TimeManager.asset`

**This is a gameplay change, not a perf tweak.** Fixed Timestep is 0.007 s (~143 Hz), which is
unusually high and expensive. But RevivalPlan P3 records that `Bat.cs` does manual collision
resolution and that shot power (`Bat.cs:294`) is **frame-rate dependent** — `trackerVelocity` is
metres per *frame*, not per second. Changing the timestep will change how the game plays.

Do this last, or not at all.

- [ ] **Step 1: Record current batting behaviour as the control**

Play 20 deliveries. Record the distance reading from the in-game display for each. This is your
regression baseline — without it you cannot tell whether the change broke balance.

- [ ] **Step 2: Measure the CPU cost of physics**

Unity Profiler on device, Physics module. Record `FixedUpdate` cost per frame. If it is under
1 ms, **stop here** — there is nothing to win and real risk to lose. Record that and close the
task.

- [ ] **Step 3: If it is expensive, change it**

```bash
sed -i '' 's/^  Fixed Timestep: 0.007$/  Fixed Timestep: 0.011111111/' ProjectSettings/TimeManager.asset
```

90 Hz, matching a Quest 3 display rate rather than free-running above it.

- [ ] **Step 4: Re-run the batting regression**

Play the same 20 deliveries. Compare distances against step 1.

**If shot distances changed by more than 10%, revert:**

```bash
git checkout ProjectSettings/TimeManager.asset
```

The correct fix is to make shot power time-independent in `Bat.cs`, which is a gameplay ticket
outside this plan's scope — not something to smuggle in under a performance change.

- [ ] **Step 5: Record the outcome either way and commit**

```bash
git add ProjectSettings/TimeManager.asset Docs/Baseline.md && git commit -m "perf: reduce fixed timestep to 90Hz after batting regression check"
```

## Task 34: Final gate

**Files:**
- Modify: `Docs/Baseline.md`

- [ ] **Step 1: Final editor stats and console check**

Menu: CricketVR → Dump Scene Stats, then:

```bash
cp Docs/Baseline/stats-current.txt Docs/Baseline/stats-final.txt && diff Docs/Baseline/stats-phase0.txt Docs/Baseline/stats-final.txt
```

Console must be error-free. That diff is the whole story of this plan on one screen — expect
materials-with-normal-maps 0 → 8+, reflection probes 0 → 6, light probe groups 0 → 1,
lightmaps 0 → many.

- [ ] **Step 2: 🔺 DEFERRED — the actual performance gate**

The real gate — *72 FPS held through a full over with fielders moving and the ball in flight* —
**has not been run**. Everything above was tuned against editor proxies.

Go to **Phase D** and work the deferred list before treating this as finished.

- [ ] **Step 3: Capture the final reference shots**

Menu: CricketVR → Capture Reference Shots → `current`, then:

```bash
cd Docs/Baseline && for f in current-*.png; do mv "$f" "final-${f#current-}"; done && cd -
```

- [ ] **Step 4: Check the editor-side gate**

**Pass:** console error-free · `final-*.png` clearly better than `phase0-*.png` at all three
cameras · stats diff shows the expected deltas · SRP Batcher and crowd instancing both engaging
in the Frame Debugger.

**This is not the real gate.** The performance gate lives in Phase D and remains unrun.

- [ ] **Step 5: Commit**

```bash
git add Docs/Baseline Docs/Baseline.md && git commit -m "docs: record final performance gate and reference shots"
```

**PHASE 7 GATE:** 72 FPS held through a full over · APK substantially smaller than phase 0 ·
`final-*.png` shows clear improvement over `phase0-*.png` at all three cameras.

---

# Appendix: what to do when a gate fails

Gates exist to stop compounding. When one fails:

1. **Do not proceed to the next phase.** The next phase's measurements become meaningless.
2. **Check the phase table in `Docs/Baseline.md`** to isolate which change cost the budget.
3. **Reduce in the phase that caused it**, not globally. Dropping render scale to compensate for
   an over-budget lightmap is how projects end up blurry *and* slow.
4. **If a phase cannot fit the budget, cut it.** Phases 0–3 deliver most of the perceptual gain.
   Phases 4–6 are where the hours are. Stopping after phase 3 with a solid 72 FPS is a better
   outcome than shipping all eight phases at 60.

---

# Phase D — Deferred device work

Everything constraint 3 removed from the main plan, collected in one place. **None of this has
been run.** Work this list before treating the realism work as finished.

## D1: Android SDK levels

**Files:** `ProjectSettings/ProjectSettings.asset`

RevivalPlan P1 leaves these outstanding. Horizon Store requires API 34 for new apps.

- [ ] Close the Unity Editor.
- [ ] Record current values:

```bash
grep -nE "AndroidTargetSdkVersion|AndroidMinSdkVersion" ProjectSettings/ProjectSettings.asset
```

Expected: `AndroidTargetSdkVersion: 0`, `AndroidMinSdkVersion: 25`.

- [ ] Apply:

```bash
sed -i '' -e 's/^  AndroidTargetSdkVersion: 0$/  AndroidTargetSdkVersion: 34/' -e 's/^  AndroidMinSdkVersion: 25$/  AndroidMinSdkVersion: 32/' ProjectSettings/ProjectSettings.asset
```

- [ ] Verify:

```bash
grep -nE "AndroidTargetSdkVersion: 34|AndroidMinSdkVersion: 32" ProjectSettings/ProjectSettings.asset
```

Both lines must be present. If either is missing the original value differed — set them in Player
Settings in the Editor instead.

## D2: Headless build script

**Files:** `Assets/Editor/BuildScript.cs`, `Assets/Editor/Tests/BuildScriptTests.cs`

No `BuildPlayer` entry point exists — RevivalPlan deleted the only editor script. This is the one
genuinely TDD-shaped artifact in the whole effort.

- [ ] Write the failing test, `Assets/Editor/Tests/BuildScriptTests.cs`:

```csharp
using NUnit.Framework;
using UnityEditor;
using CricketVR.EditorTools;

public class BuildScriptTests
{
    [Test]
    public void AndroidOptions_TargetsAndroidArm64()
    {
        var opts = BuildScript.AndroidOptions("out/test.apk");
        Assert.AreEqual(BuildTarget.Android, opts.target);
        Assert.AreEqual(BuildTargetGroup.Android, opts.targetGroup);
    }

    [Test]
    public void AndroidOptions_UsesGivenOutputPath()
    {
        var opts = BuildScript.AndroidOptions("out/test.apk");
        Assert.AreEqual("out/test.apk", opts.locationPathName);
    }

    [Test]
    public void AndroidOptions_IncludesAtLeastOneEnabledScene()
    {
        var opts = BuildScript.AndroidOptions("out/test.apk");
        Assert.IsNotNull(opts.scenes);
        Assert.Greater(opts.scenes.Length, 0, "No enabled scenes in Build Settings");
    }

    [Test]
    public void AndroidOptions_IsNotADevelopmentBuildByDefault()
    {
        var opts = BuildScript.AndroidOptions("out/test.apk");
        Assert.AreEqual(BuildOptions.None, opts.options);
    }
}
```

Test asmdef, `Assets/Editor/Tests/CricketVR.Editor.Tests.asmdef`:

```json
{
    "name": "CricketVR.Editor.Tests",
    "rootNamespace": "",
    "references": ["CricketVR.Editor", "UnityEngine.TestRunner", "UnityEditor.TestRunner"],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": ["nunit.framework.dll"],
    "autoReferenced": false,
    "defineConstraints": ["UNITY_INCLUDE_TESTS"],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] Run Test Runner → EditMode → Run All. Expect 4 failures: `BuildScript` not found.
- [ ] Write `Assets/Editor/BuildScript.cs`:

```csharp
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CricketVR.EditorTools
{
    /// <summary>
    /// Headless Android player build entry point. Invoked from CI or the command line via
    /// -executeMethod CricketVR.EditorTools.BuildScript.BuildAndroid
    /// </summary>
    public static class BuildScript
    {
        const string DefaultOutput = "Builds/Android/CricketVR.apk";

        /// <summary>Assembles build options. Pure and unit-testable.</summary>
        public static BuildPlayerOptions AndroidOptions(string outputPath)
        {
            return new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes
                    .Where(s => s.enabled)
                    .Select(s => s.path)
                    .ToArray(),
                locationPathName = outputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };
        }

        public static void BuildAndroid()
        {
            var output = ArgValue("-output") ?? DefaultOutput;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));

            var report = BuildPipeline.BuildPlayer(AndroidOptions(output));
            var summary = report.summary;

            Debug.Log($"[BuildScript] result={summary.result} " +
                      $"size={summary.totalSize / (1024 * 1024)}MB " +
                      $"errors={summary.totalErrors} time={summary.totalTime}");

            if (summary.result != BuildResult.Succeeded)
            {
                EditorApplication.Exit(1);
            }
        }

        static string ArgValue(string flag)
        {
            var args = System.Environment.GetCommandLineArgs();
            var i = System.Array.IndexOf(args, flag);
            return (i >= 0 && i < args.Length - 1) ? args[i + 1] : null;
        }
    }
}
```

- [ ] Run Test Runner again. Expect 4 passed. If `AndroidOptions_IncludesAtLeastOneEnabledScene`
  fails, no scene is enabled in Build Settings — add `Assets/Scenes/CricketVR.unity` and rerun.
- [ ] Close the Editor and build:

```bash
/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath /Users/rao/Git/CricketVR -executeMethod CricketVR.EditorTools.BuildScript.BuildAndroid -logFile - 2>&1 | tail -40
```

Expected: `[BuildScript] result=Succeeded ... errors=0`. **This is the first APK this project has
ever produced — expect to iterate on IL2CPP, linker and Gradle errors** that no amount of
editor-side verification could have surfaced.

- [ ] Install:

```bash
/Users/rao/Library/Android/sdk/platform-tools/adb devices && /Users/rao/Library/Android/sdk/platform-tools/adb install -r Builds/Android/CricketVR.apk
```

## D3: The deferred verification checklist

Every `🔺 DEFERRED` marker in the main plan, in one list. Each needs OVR Metrics Tool on a
Quest 3.

| # | From | What is unverified | Why it matters | If it fails |
|---|---|---|---|---|
| 1 | Task 10 | Does URP cost or save GPU time vs Built-in? Is **HDR** on `URP-Quest` affordable at Quest 3 bandwidth? | HDR is required for tonemapping to mean anything | Turn HDR off on `URP-Quest`; grading gets flatter but survives |
| 2 | Task 15 | Does the baked lightmap set fit in Quest 3 memory alongside everything else? | 64 MB was a reasoned estimate, never measured | Halve the band scales in task 14 and rebake — cut distant stands first |
| 3 | Task 20 | APV vs hand-placed probes, on GPU time | Purely a perf question; plan ships the safe option | N/A — probe lattice is already the shipping default |
| 4 | Task 24 | Does the PBR texture set fit memory alongside the lightmaps? | Two large memory consumers added independently | Drop to 1K on everything except turf and pitch |
| 5 | Task 25 | Cost of the post pass in GPU ms | Not free on a tile GPU | Drop Bloom first |
| 6 | **Task 25** | **ACES vs Neutral tonemapping, judged in the headset** | Quest panels crush shadows differently from a monitor. **Plan ships Neutral as the safe default — this choice was never actually made** | Switch to ACES if the headset says so |
| 7 | Task 27 | Crowd cost at Quest 3 fill rate | Transparent billboards are fill-rate-hungry; a full stand is the worst case for a tile GPU | Reduce `occupancy` on `CrowdSpawner`, then `rows` |
| 8 | Task 30 | Actual APK size reduction | 421 MB left `Resources/`, so the saving should follow — unconfirmed | N/A |
| 9 | Task 31 | Foveated rendering benefit | Saves GPU time and nothing else; **zero** observable effect in the Editor | Raise foveation level, accepting peripheral blur |
| 10 | Task 33 | Is physics actually expensive enough to justify touching Fixed Timestep? | Gameplay-affecting; plan says skip if under 1 ms | N/A — default is to leave it alone |
| 11 | **Task 34** | **The real gate: 72 FPS held through a full over, fielders moving, ball in flight** | Everything was tuned against editor proxies | See the gate-failure appendix |

- [ ] Work items 1–11 in order. Record each result in `Docs/Baseline.md`.
- [ ] Item 11 is the one that matters. Until it passes, the realism work is unvalidated.
