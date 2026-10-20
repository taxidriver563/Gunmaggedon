# Gunmaggedon

A gun-based warfare expansion for Nuclear Option.

Gunmaggedon explores conventional and experimental gun platforms. The current build focuses on 203 mm coastal defenses, ranging fire, illumination, and specialized ammunition. Future concepts include heavy anti-aircraft guns, railgun platforms, and the Old Guard expansion; those concepts are not included in this build.

## Current build

**Runtime helper 1.6.0 + Blueprinter asset pack 0.0.7.** Both are required.

- Ordinary coastal gun: HE and AP, with automatic AP selection against large or heavily armored ships.
- SPECIAL coastal gun: cluster, smoke, chaff, and parachute top-attack payloads.
- Colored, non-damaging ranging rounds and persistent trails.
- Moderate artillery arcs, moving-target lead, and low-light parachute illumination.
- Travelling audio, native water splashes, and compact top-attack/AP effects.
- Cached targeting/audio lookups and reusable cluster collision-query buffers.

This is an experimental build. Compilation and six standalone test suites passed. AP damage/balance, the latest query changes, multiplayer presentation, and actual defensive interception still need in-game validation. Native bomb registration alone does not prove CIWS engagement.

## Install

Start with a working Nuclear Option mod setup using BepInEx and the compatible Blueprinter loader.

1. Close Nuclear Option and back up your existing Gunmaggedon installation/config.
2. Copy the contents of [`dist/1.6.0/BepInEx`](dist/1.6.0/BepInEx) into the game's `BepInEx` folder.
3. Keep only the intended current `Gunmageddon_*.nobp` in `BepInEx/plugins`; move older pack versions outside that folder.
4. Restart the game. BepInEx generates or updates `BepInEx/config/gunmageddon.203mm.audio.cfg`.

For the ordinary gun, `[Coastal ammunition] Mode` accepts `Auto`, `HE`, or `AP`. Auto uses AP against ships at least 65 m long or armor tier 3+, retaining HE for lighter ships and other targets. Ranging and illumination requirements still apply.

The helper DLL is separate from the `.nobp`; distributing one without the other is insufficient. File checksums are in [`dist/1.6.0/SHA256SUMS.txt`](dist/1.6.0/SHA256SUMS.txt).

## Repository layout

| Path | Contents |
|---|---|
| [`GunmageddonAudio/src`](GunmageddonAudio/src) | Runtime C# grouped by feature |
| [`GunmageddonAudio/tests`](GunmageddonAudio/tests) | Standalone unit suites and Unity integration/regression fixtures |
| [`GunmageddonAudio/tools`](GunmageddonAudio/tools) | Native implementation inspection utility |
| [`Assets/Blueprinter/Mods/Gunmageddon`](Assets/Blueprinter/Mods/Gunmageddon) | Mod prefabs, definitions, models, materials, textures, audio, and Unity metadata |
| [`context`](context) | Current state, change history, build notes, and validation limits |
| [`dist/1.6.0`](dist/1.6.0) | Latest packaged helper and asset pack |

## Build and test

On Windows, from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File GunmageddonAudio/build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File GunmageddonAudio/tests/run-unit-tests.ps1
```

The scripts currently use local defaults for Unity 2022.3.62f2 and the Steam Nuclear Option installation. Adjust those paths for your machine. Build output goes under `GunmageddonAudio/build/1.6.0/`; building does not install or publish it.

Open/import the mod asset folder in the matching Blueprinter Editor project to edit or rebuild the `.nobp`. This repository is the mod source, not a standalone replacement for that editor project. Native placeholder assets, the game assemblies, BepInEx, and Blueprinter must come from your own installation.

Unity fixtures contain version-pinned DLL and local project paths. Review those before running them. Historical comparison DLLs and local rollback archives are not included; the standalone unit runner does not require them. Query tests use deterministic transport stubs and do not substitute for native physics testing.

See [`context/README.txt`](context/README.txt) for detailed project context and [`context/ARMOR-PIERCING.txt`](context/ARMOR-PIERCING.txt) for the AP implementation and pending checks.
