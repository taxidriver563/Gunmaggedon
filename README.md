# Gunmaggedon

A gun-based warfare expansion for Nuclear Option, covering coastal artillery, primitive anti-aircraft guns, searchlights, and experimental ammunition.

## Current build

**Runtime helper 1.8.1 + Blueprinter asset pack 0.0.16. Both are required.**

- Ordinary 203 mm coastal gun with HE/AP selection, moderate artillery arcs, colored ranging rounds/trails/splashes, and low-light parachute illumination.
- SPECIAL coastal gun with cluster, smoke, chaff, and parachute top-attack ammunition; physical slug impacts, discharge effects and spent-canister separation.
- L40 AA trailer with delayed optical aiming corrections, moving traverse/elevation handles, barrel recoil, 40 mm firing audio, and mild stock casing ejection.
- Air Defense Searchlight Trailer with night activation, gradual aircraft acquisition, sky sweeps, and improved search cues from friendly HLT/MSV radar trucks within 300 metres.
- Shared illumination tokens assign one friendly searchlight per aircraft, freeing others to sweep or acquire different aircraft. Claims release on lost illumination, shutdown, or destruction.
- Two-sided searchlight cone shading, native aircraft illumination, and enormous directional lens glare with off-axis fading and opaque-source occlusion.

Custom mission hitch/unhitch remains disabled pending repair of its vehicle integration. Remote clients currently show local searchlight sweeps rather than replicated host tracking. Mission balance, multiplayer presentation, and GPU performance still need player testing.

## Install

Requires a working Nuclear Option installation with compatible BepInEx and Blueprinter. Game/dependency assemblies are not included.

1. Close Nuclear Option and back up your current Gunmaggedon installation/config.
2. Copy [`dist/1.8.1/BepInEx`](dist/1.8.1/BepInEx) into the game's `BepInEx` folder.
3. Keep only `Gunmageddon_0.0.16.nobp` active in `BepInEx/plugins`; move older packs outside that folder or change their extensions.
4. Restart the game. BepInEx generates/updates `BepInEx/config/gunmageddon.203mm.audio.cfg`.

Verify files with [`SHA256SUMS.txt`](dist/1.8.1/SHA256SUMS.txt). The helper DLL and `.nobp` pack must be distributed together. The older `dist/1.6.0` snapshot is retained for history; use the current build above.

For ordinary coastal guns, `[Coastal ammunition] Mode` accepts `Auto`, `HE`, or `AP`. Auto selects AP against ships at least 65 m long or armor tier 3+, while ranging and illumination requirements still apply.

## Validation

All nine standalone suites passed. Connected Unity fixtures passed for searchlight activation/acquisition, radar support, coordinated tokens, and rendered cone/dazzler behavior. The latest installed helper/pack hashes match the staged files, and Nuclear Option startup passed with pack 0.0.16 loaded.

These checks do not certify full mission balance, actual CIWS interception, multiplayer behavior, or frame rates. Dazzler occlusion requires the camera's opaque depth texture; transparent glass does not block it. Detailed results and remaining limits are in [`context`](context).

## Repository layout

| Path | Contents |
|---|---|
| [`GunmageddonAudio/src`](GunmageddonAudio/src) | Runtime C# grouped by feature |
| [`GunmageddonAudio/tests`](GunmageddonAudio/tests) | Standalone suites and Unity integration/render regression fixtures |
| [`GunmageddonAudio/tools`](GunmageddonAudio/tools) | Asset setup, render verification and native IL inspection utilities |
| [`Assets/Blueprinter/Mods/Gunmageddon`](Assets/Blueprinter/Mods/Gunmageddon) | Custom prefabs, definitions, models, materials, shaders, textures, audio and Unity metadata |
| [`Assets/GunmaggedonEditor`](Assets/GunmaggedonEditor) | Editor-only searchlight obstruction preview |
| [`UnityVisuals/Searchlight`](UnityVisuals/Searchlight) | Working shader and editor-preview source copies |
| [`context`](context) | Current state, feature notes, validation evidence and limitations |
| [`dist/1.8.1`](dist/1.8.1) | Current packaged helper 1.8.1 and asset pack 0.0.16 |

## Build and test

On Windows, from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File GunmageddonAudio/build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File GunmageddonAudio/tests/run-unit-tests.ps1
```

Scripts use local defaults for Unity 2022.3.62f2 and the Steam Nuclear Option installation; adjust paths for your machine. Build output goes under `GunmageddonAudio/build/1.8.1/`. Building does not install or publish it.

Import the mod asset folder into the matching Blueprinter Editor project to rebuild the `.nobp`. This repository contains mod source, not a standalone replacement for that editor project. Native `_donotship` placeholders, game assemblies, BepInEx, and Blueprinter must come from your own installation.

Unity fixtures retain version-pinned DLL and local project paths. Review those before running them. Historical helper DLLs and rollback archives are not included; standalone tests do not require those baselines. Render fixtures run through the connected Unity scripting tool; run `RenderSearchlightVisual` before the cone/dazzler pixel checks.

Start with [`context/README.txt`](context/README.txt). Searchlight details: [behavior](context/SEARCHLIGHT-BEHAVIOR.txt), [tokens](context/SEARCHLIGHT-TOKENS.txt), [cone](context/SEARCHLIGHT-CONE.txt), and [dazzler](context/SEARCHLIGHT-DAZZLER.txt).
