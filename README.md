# Gunmaggedon

A gun-based warfare expansion for Nuclear Option, covering coastal artillery, primitive anti-aircraft guns, searchlights, and experimental ammunition.

## Current release

**[Alpha v0.0.26](https://github.com/taxidriver563/Gunmaggedon/releases/tag/v0.0.26): runtime 1.10.0 + asset pack 0.0.26. Both are required.**

- Standard 203 mm coastal gun with HE/AP ammunition, artillery arcs, colored ranging rounds, matching splashes, and low-light parachute illumination.
- SPECIAL coastal gun with cluster, smoke, chaff, and parachute top-attack ammunition; physical slugs, discharge effects, and spent-canister separation.
- L40 AA trailer with primitive optical correction, moving cranks, recoil, custom audio, and mild casing ejection.
- F88 heavy flak trailer with predicted-area barrages, timed airbursts, a broad gradual fragmentation envelope, and a working breech/loading/casing sequence. Friendly radar within 300 m improves knowledge without removing gun dispersion.
- L25 triple AA trailer with 3.5 km range, close-defense tracking, staggered barrels, independent 15-round clips, custom 25 mm audio, and one tracer every three total shots.
- Searchlight trailer with low-light activation, smooth aircraft tracking, rough 3–5 km engine-noise cues, 300 m radar support, and exclusive illumination claims. Coastal batteries within 2 km can request ship illumination; air defense takes priority. Beam/tracking reach extends to 13 km with a finite soft cloud cutoff.
- Three model LOD levels across all units, with reduced distant cosmetic animation. The runtime pools effects/audio and shares suitable query and bounds work without reducing targeting or scan cadence.

See the [patch notes](releases/v0.0.26.md) for changes and validation limits.

## Install

Requires compatible BepInEx and Blueprinter. Game/dependency assemblies are not included.

1. Close Nuclear Option and back up your current mod/configuration.
2. Download `Gunmaggedon.Runtime.zip` and `Gunmageddon_0.0.26.nobp` from the [release](https://github.com/taxidriver563/Gunmaggedon/releases/tag/v0.0.26).
3. Extract the runtime ZIP into the game folder, merging its `BepInEx` folder. Place the `.nobp` in `BepInEx/plugins`.
4. Remove old `GunmageddonAudio.dll` copies and keep only one Gunmaggedon runtime and the current asset pack active.
5. Start the game. Existing settings remain in `BepInEx/config/gunmageddon.203mm.audio.cfg`.

The standalone `Gunmaggedon.Runtime.dll` attachment is the same CI-built runtime for manual replacement. The ZIP contains the runtime only; the asset pack is downloaded separately. Older `dist/` folders are historical snapshots.

For ordinary coastal guns, `[Coastal ammunition] Mode` accepts `Auto`, `HE`, or `AP`. Auto selects AP against ships at least 65 m long or armor tier 3+, while ranging and illumination requirements still apply.

## Development and verification

[Release CI](ci/README.txt) runs the standalone suites, builds the tagged runtime source with private compile references, and attaches build-provenance attestations. Private game/Unity reference DLLs are not redistributed. The Blueprinter asset pack is built separately in Unity.

See [build instructions](context/BUILD-AND-VALIDATION.txt), [LOD policy](context/LOD-POLICY.txt), and [runtime optimization evidence](context/RUNTIME-OPTIMIZATION.txt). All 14 standalone suites and 1,568 checks across ten Unity groups passed locally. A separate staged L25 preview-rig tracer check remains mismatched; the operational trailer uses the intended ratio.

This remains an alpha. Mission balance, live multiplayer presentation, and representative combat FPS need further testing. Custom mission hitch/unhitch remains disabled. Remote searchlight clients still use local sweeps rather than replicated host pointing. Native allocation measurements and full combat benchmarks are not certified by editor microbenchmarks.
