# CricketVR — Revival Plan (recommendations only)

**Status: P0 done · P4 done (one item deferred) · P3 partially done. Nothing committed — all
changes are staged or in the working tree for Rao to review and commit himself.**

Prepared 2026-09-20 against commit `e8e5bec` (master, clean). Last updated 2026-09-21.

## Progress

| Phase | State | Notes |
|---|---|---|
| P0 | **Done** | Baseline tagged `pre-revival`. The build-blocker premise was **disproved** — see the corrected P0.1 below. |
| P4 | **Done**, 1 deferred | `.gitattributes` added, `.gitignore` rewritten, 10 generated files untracked. `Assets/Resources/` relocation deferred to P2. |
| P3 | **Partial** | Dead files, unused field and log gating done. Bulk comment removal and structural items still open. |
| P2 | Not started | |
| P1 | Not started | |

**Verification standard used:** after each change, the full 172-source `Assembly-CSharp` set is
compiled with Roslyn against all 252 references with `UnityEditor.dll` deliberately excluded — the
player's view of the code. Currently **0 errors**. This is not a substitute for a real APK build
(it does not exercise IL2CPP, the linker or Gradle), which remains unverified.

Companion documents: [GameDesignDocument.html](GameDesignDocument.html) · [TechnicalDesignDocument.md](TechnicalDesignDocument.md)

---

## P0 — Blockers (do these first; both are small)

### 0.1 `HUD.cs:3` — unused `using UnityEditor;` — **DONE (but not a blocker)**

⚠️ **The original claim in this plan was wrong and has been corrected.** Two subagents independently
reported this as a player-build blocker; it is not.

`UnityEngine.CoreModule.dll` declares its own small `UnityEditor` namespace (`ExcludeFromPresetAttribute`,
`AssetFileNameExtensionAttribute` and similar), and CoreModule *is* referenced in player builds. So a
bare, unused `using UnityEditor;` compiles fine without `UnityEditor.dll`. Verified by probe:

| Probe | Result |
|---|---|
| `using UnityEditor;` with no editor types (what `HUD.cs` had) | compiles against CoreModule + mscorlib alone |
| `using UnityEditor;` + `AssetDatabase.Refresh()` | fails `CS0103`, as expected |

The import was still removed — it is dead, and it is a landmine, because the next person to reference
an editor type in that file gets a confusing player-only failure. But this belongs in **P3 cleanup,
not P0**.

Correspondingly: the absence of an `AndroidBuild/` directory and of build records in `Logs/` is
consistent with never having built, but equally consistent with a clean checkout. The two facts were
wrongly presented as causally linked. There is **no known compile blocker**; whether the project
produces a working APK is simply still unverified.

### 0.2 Baseline — **PARTIALLY DONE**

- ✅ `pre-revival` annotated tag created on `e8e5bec`.
  ⚠️ Its message repeats the disproved "known issue" wording. Retag if that matters:
  `git tag -f -a pre-revival e8e5bec -m "..."`.
- ❌ **APK not built.** Three independent blockers at the time: the Unity Editor was open on this
  project (batchmode would contend for the project lock), `adb devices` showed no headset attached,
  and there is no `BuildPlayer` entry point for a headless build (`CustomEditorScript.cs`, the only
  editor script, was entirely commented out — and has now been deleted).

  To get a real baseline: build from the open Editor (File → Build Settings → Build), **or** add a
  minimal `BuildPlayer` editor script so builds can run headlessly. The latter is worth doing
  regardless, but it is a new file and was outside the approved scope.

Toolchain is otherwise ready: Unity 2022.3.4f1 with the Android module (bundled SDK/NDK/OpenJDK) is
installed, and `adb` is on PATH.

---

## P1 — Engine and SDK

### Where things stand
| | Current | Notes |
|---|---|---|
| Unity | 2022.3.4f1 | 2022 LTS left Personal/Pro support ~May 2025; patches are Enterprise/Industry only. This is the *first* patch of the line. |
| Meta SDK | Vendored Oculus Integration, OVRPlugin wrapper **1.55.0** (~mid-2021) | Meta deprecated the all-in-one `.unitypackage` at v59; distribution moved to UPM. Current Meta XR All-in-One SDK is **205.0** (Jul 2026). |
| Devices | `quest\|quest2` in AndroidManifest | Quest 3 / 3S / Pro not declared. |
| Target API | `AndroidTargetSdkVersion: 0` (auto) | New Horizon Store apps created after 1 Mar 2026 must target API 34. |
| Pipeline | Built-in, Forward | `URPProjectSettings.asset` exists but references no pipeline asset; URP is not in the manifest. |

Android stereo rendering is already **Multiview** (`Oculus Settings.asset: m_StereoRenderingModeAndroid: 2`) — correct, no action.

### Recommendation: Unity 6 LTS + Meta XR SDK (UPM), staged in three separate commits

**The migration surface is far smaller than the 657 MB vendored SDK suggests.** Complete verified
list of Oculus SDK types referenced by first-party code (matched against all 978 public types
defined under `Assets/Oculus`):

| Type | Refs | Location | Migration |
|---|---|---|---|
| `DebugUIBuilder` | 39 | `Main.cs:298–416`, `639–640`, `942`, `1164–1171` | The entire settings/debug menu. **Not in Meta XR SDK** — see below. |
| `OVRInput` | 15 | `Main.cs` (all via one `GetButton()` helper at `:1219`), `Bat.cs:279–288` (haptics) | Survives unchanged in Meta XR Core SDK. |
| `OVRPlayerController` | 1 | `Main.cs:52` — serialized, never read | Delete the field. |

`OVRManager`, `OVRCameraRig`, `OVRPlugin` are never touched from first-party code.

**The `DebugUIBuilder` bridge.** It lives in `Assets/Oculus/SampleFramework/Core/DebugUI/` — 3 C#
files plus prefabs, 2 MB total — and depends only on `OVRCameraRig`, `OVRRaycaster` and
`OVRPlugin`, all of which still exist in Meta XR Core SDK. Recommendation: **copy that one folder
to `Assets/ThirdParty/DebugUI/` and delete the other ~655 MB.** Rebuilding the menu in uGUI
properly is a separate, optional, later task — not a revival blocker.

**Stage 1 — Meta XR SDK migration.** Replace the vendored SDK with `com.meta.xr.sdk.all` via UPM.
Keep the DebugUI bridge. ~5 real integration points to fix.

**Stage 2 — Unity 6 LTS.** Meta's current Unity setup docs specify 6000.0.66f2+ (6.1+ recommended)
for Quest. ⚠️ Confirm the minimum Unity version for whichever Meta XR SDK release you land on
before committing to an order — it may force Stage 2 ahead of Stage 1.

**Stage 3 — URP. Defer.** Meta and Unity both recommend URP for Quest now, and Unity 6.3 LTS added
XR post-processing that runs efficiently on tile GPUs. But converting 47 materials from Built-in
will cause visual regressions and is a real chunk of work. Do it after the game is playable again.

### Also in this bucket
- Set `AndroidTargetSdkVersion` explicitly to **34**. Raise `AndroidMinSdkVersion` from 24 to **32**.
- Update `Assets/Plugins/Android/AndroidManifest.xml` `com.oculus.supportedDevices` to include
  Quest 3 / 3S / Pro, and `targetDeviceTypes` in `Assets/Oculus/OculusProjectConfig.asset`.

---

## P2 — Package and asset removal

Zero usage, verified across all first-party scripts, both scenes, and every prefab. Remove from
`Packages/manifest.json`:

| Package | Evidence |
|---|---|
| `com.unity.ads` 4.4.2 | no references |
| `com.unity.purchasing` 4.9.3 | no references; also delete the `Assets/Resources/BillingMode.json` it left behind |
| `com.unity.analytics` 3.8.1 | no references |
| `com.unity.learn.iet-framework` 3.1.3 | tutorial framework, with `Assets/ExampleAssets/Tutorial/` |
| `com.unity.ai.navigation` 1.1.4 | zero `NavMeshAgent`; fielders use iTween + closed-form maths |
| `com.unity.timeline` 1.7.4 | zero `PlayableDirector` |
| `com.unity.2d.tilemap`, `com.unity.2d.sprite` | 2D packages in a VR game |

Worth noting: ads, analytics and purchasing are currently linked into a personal project. Removing
them shrinks the APK and drops data collection you never asked for.

**Review, don't auto-remove:** `com.unity.xr.legacyinputhelpers` (no first-party reference, but the
vendored SDK may need it — re-check after migration) · `com.unity.test-framework` (keep; you will
want tests) · `com.unity.collab-proxy` (remove unless using Unity Version Control) · the three IDE
packages (keep only the one you actually use).

**Assets to delete alongside** (all zero first-party references):
`Assets/Oculus/LipSync` (274 MB) · `Assets/Oculus/SampleFramework` minus DebugUI (269 MB) ·
`Assets/Oculus/Spatializer` (63 MB) · `Assets/Oculus/Avatar` · `Assets/Oculus/Platform` ·
`Assets/ExampleAssets/` (848 KB — confirm the XRController prefabs are not in the scene first).

**`iTween` stays.** It is genuinely used — `Fielder.cs:140,158,182` and `AnimatedFielder.cs:229`.

---

## P3 — Code cleanup

Do this **after** a green build baseline, and commit each category separately so any regression is
bisectable.

### Safe and mechanical
1. ✅ **DONE — Deleted `AnimatedFielderUnchanged.cs`** — 803 lines, a 9-line diff from `AnimatedFielder.cs`
   (class name plus one commented-out line), referenced by no scene and no prefab.
2. ✅ **DONE** — removed the dead `using UnityEditor;` from `HUD.cs` (was P0.1; see correction there).
3. ✅ **DONE** — deleted the never-read `OVRPlayerController theController` field at `Main.cs:52`.
   This removes the **last** `OVRPlayerController` reference in first-party code. The scene still
   carries an orphaned `theController:` entry, which Unity drops silently on the next scene save.
   ✅ **ALSO DONE** — deleted `Assets/Editor/CustomEditorScript.cs`, which was 100% commented out
   and defined nothing. `Assets/Editor/` and its orphaned `.meta` went with it.
4. **~985 commented-out lines across 21 files (~18% of first-party code).** Git history is the undo.
   Worst: `Main.cs` 195 · `AnimatedFielder.cs` 163 · `BowlingProfileManager.cs` 107 · `Bat.cs` 72 ·
   `CustomEditorScript.cs` 22/22 — the entire file is commented out, delete it.

### Logging — do NOT blanket-delete
Only **7** active `Debug.*` calls remain; everything else the raw grep finds is inside comments.

- **Keep (4):** `BoundaryCollider.cs:13`, `KeeperCollider.cs:24`, `Bat.cs:266`, `Fielder.cs:78` —
  genuine `LogError`/`LogWarning` assertions on illegal state transitions.
- ✅ **DONE — gated (1):** `Main.cs:1193`. ⚠️ **Correction:** this does *not* fire on every state
  transition. It lives only in `WaitAndSetGameState` (6 call sites), while 25 direct `gameState =`
  assignments log nothing and the property setter does not log at all — so it was a partial,
  inconsistent trace, not a firehose. Now gated behind a new `[SerializeField] bool
  verboseStateLogging` (default `false`) under the *Debug Tweaks* header. Kept rather than deleted
  because moving it into the `gameState` setter would give genuinely complete coverage.
- **Not logs — a missing feature (2):** `HUD.cs:354` `LogWarning("MATCH DONE!!!!")` and `HUD.cs:374`
  `LogWarning("GAME OVER!! ALL OUT")` are placeholders standing in for the match-result UI that
  NOTES lists as pending. After they fire, play simply continues and the batsman list wraps. Treat
  as a feature ticket, not cleanup.

All 7 are also mirrored into the in-world debug console by `Main.HandleLog`, so the debug pad
depends on them existing.

### Structural (recommended, higher effort)
5. **Add an asmdef for first-party code** (e.g. `CricketVR.Runtime`). Everything currently compiles
   into `Assembly-CSharp` alongside 488 vendor scripts, so every script edit recompiles the world.
   This is the single biggest iteration-speed win available, and it gets much cheaper once the
   vendored SDK is gone.
6. Move `BallSpeed.cs` and `TestDisplay.cs` from `Assets/` root into `Assets/Scripts/`.
7. **`Main.cs` at 1353 lines is the main maintainability risk — but do not refactor it yet.** Its
   settings system is ~40 hand-maintained quartets (`field` / `_field` / `_fieldText` /
   `_fieldSlider`), the convention NOTES documents. Replacing that with a ScriptableObject settings
   asset plus a small binding helper would delete several hundred lines. Post-revival work.

### Bugs found while documenting — tickets, not cleanup
- `Stumps.cs:42,49` both read `LegStump.GetComponent<Stump>()` for slots 1 and 2. Looks like a
  copy-paste bug leaving middle/off stump unregistered — would explain the "Fix wickets collision
  issues" item in NOTES.
- **Frame-rate dependent gameplay.** Shot power (`Bat.cs:294` — `trackerVelocity` is metres per
  *frame*, not per second) and fielder run speed (`AnimatedFielder.cs:672`). Balance therefore
  varies with device framerate, and this is very likely why `BatAmplifier` has to be 75.
- `Main.cs:1296` — `(height - startPos.y / time)` operator precedence looks wrong; two correct
  projectile solutions sit commented out beside it.
- `Main.Factorial()` computes a summation, not a factorial.
- `CameraReplay` allocates unbounded `Texture2D` frames.

---

## P4 — Repository hygiene

- ✅ **DONE — `.gitattributes` added** (133 lines): explicit `binary` markers for every binary
  extension present, `eol=lf` for source and Unity YAML, and `-merge` on `*.unity`/`*.prefab` so git
  cannot silently produce a corrupt auto-merge of a scene.
  ⚠️ **No `filter=lfs` rules were added — git-lfs is not installed on this machine**, and LFS
  attributes without the binary present break checkout. An LFS block sits commented at the bottom of
  the file, ready for after `git lfs install`.
  ⚠️ **Correction:** first-party sources are *not* meaningfully a "CRLF/LF mix". Exactly 4 files
  (`AnimatedFielder`, `AnimatedFielderManagement`, `Ball`, `KeeperCollider`) carry a **single stray
  CR each**; every other file is already pure LF. The rules are worth having to prevent future drift
  when editing from Windows, but the problem was overstated here originally.
  ⚠️ Adding `.gitattributes` does **not** rewrite the working tree. Normalisation happens only on an
  explicit `git add --renormalize .`, which was deliberately **not** run.
- **The 769 MB history question is still open.** LFS only affects future commits; shrinking existing
  history needs `git filter-repo`. **Decide before creating a GitHub remote** — far cheaper now.
- ✅ **DONE — untracked 10 generated files** with `git rm --cached` (all still present on disk):
  `Logs/*.log` (5) · `UserSettings/*` (3) · `InitCodeMarker` · `Assembly-CSharp-firstpass.csproj`.
  Tracked file count 3378 → 3368 from the untracking alone (3363 after the P3 file deletions).
- ✅ **DONE — `.gitignore` rewritten** (15 → 57 lines): folder-scoped Unity ignores, `*.csproj`/`*.sln`
  wildcards replacing the hand-listed names, plus `UserSettings/`, `Builds/`, `*.apk`/`*.aab`,
  `.DS_Store` and IDE dirs. Verified that **exactly** the 10 intended files match a positive ignore
  rule and nothing else does — `.vsconfig` correctly stays tracked.
- **`Assets/Resources/` is 426 MB.** Everything under `Resources/` is force-included in the build
  whether referenced or not. Textures are capped at 2048 with compression so runtime memory is fine,
  but 15 FBX (up to 50 MB each) and 17 animations all ship regardless of use.
  First-party code contains **zero `Resources.Load` calls**, so this content is almost certainly all
  movable into a normal folder with direct references. Verify, then move.

---

## Suggested order of work

1. P0.1 `using UnityEditor;` → build APK → deploy → **tag the baseline**.
2. P4 repo hygiene (`.gitattributes`, untrack generated files) — do it before the history grows.
   Settle the LFS/history-rewrite question here.
3. P3 safe cleanup: delete the dead fielder clone, the commented-out editor script, the dead field.
   Rebuild, redeploy, confirm no regression.
4. P2 package and asset removal. Rebuild, redeploy. **Expect this to be the noisiest step** — it
   removes ~655 MB of assets and the scene may hold references you have to repair.
5. P1 Stage 1: Meta XR SDK migration with the DebugUI bridge.
6. P1 Stage 2: Unity 6 LTS.
7. P3 structural: asmdef, script relocation.
8. Feature work resumes — match-end/results UI first, since it is the one thing standing between
   this and a completable match.
9. Optional, later: URP; `Main.cs` settings refactor; catch/throw fielder animations.

Steps 1–4 are low-risk and make everything after them faster. Steps 5–6 are where the real
uncertainty sits — do them on a branch, one at a time, with a deployable build after each.
