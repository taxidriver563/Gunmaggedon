# Gunmaggedon

A gun-based warfare expansion for Nuclear Option, covering coastal artillery, primitive anti-aircraft guns, searchlights, and experimental ammunition.

## Current release

**[Alpha v0.0.26](https://github.com/taxidriver563/Gunmaggedon/releases/tag/Releases): runtime 1.10.0 + asset pack 0.0.26. Both are required.**

- Standard 203 mm coastal gun with HE/AP ammunition, artillery arcs, colored ranging rounds, matching splashes, and low-light parachute illumination.
- SPECIAL coastal gun with cluster, smoke, chaff, and parachute top-attack ammunition; physical slugs, discharge effects, and spent-canister separation.
- L40 AA trailer with primitive optical correction, moving cranks, recoil, custom audio, and mild casing ejection.
- F88 heavy flak trailer with predicted-area barrages, timed airbursts, a broad gradual fragmentation envelope, and a working breech/loading/casing sequence. Friendly radar within 300 m improves knowledge without removing gun dispersion.
- L25 triple AA trailer with 3.5 km range, close-defense tracking, staggered barrels, independent 15-round clips, custom 25 mm audio, and one tracer every three total shots.
- Searchlight trailer with low-light activation, smooth aircraft tracking, rough 3–5 km engine-noise cues, 300 m radar support, and exclusive illumination claims. Coastal batteries within 2 km can request ship illumination; air defense takes priority. Beam/tracking reach extends to 13 km with a finite soft cloud cutoff.
- Three model LOD levels across all units, with reduced distant cosmetic animation. The runtime pools effects/audio and shares suitable query and bounds work without reducing targeting or scan cadence.

See the [patch notes](releases/v0.0.26.md) for changes and validation limits.

## Old Guard: what to expect

Old Guard brings primitive gun crews and fire control into Nuclear Option. Their limited accuracy is intentional: these guns need time to work out where you're going, and changing direction gives them something new to get wrong.

- **L40: walking the clips onto target.** The crew observes how a clip misses, then makes a delayed aim correction for the next one. Flying a predictable course gives them time to improve their solution. Hard turns and changing speed can throw that work away; they do not instantly snap to perfect lead.
- **L25: the close-range last line.** The triple mount is more accurate against aircraft that get dangerously close, with a 3.5 km maximum range. Its three barrels fire in sequence and each has its own 15-round clip and independent reload. It still relies on imperfect optical tracking.
- **F88: fill the sky, not chase the cockpit.** Heavy flak fires timed bursts into the region where the crew expects an aircraft to be. Nearby radar improves their information, not the gun's dispersion. Expect area suppression and accumulating fragment damage rather than pinpoint hits.
- **Give them support.** Searchlights help crews engage at night, while nearby radar can improve supported units' awareness. Neither turns an old gun into a modern SPAAG.

**Do not expect these units to survive against anything above Rank 2 aircraft.** You're bringing hand cranks to a jet fight. The crews appreciate your optimism, but optimism is not a countermeasure.

## Install

Requires compatible BepInEx and Blueprinter. Game/dependency assemblies are not included.

1. Close Nuclear Option and back up your current mod/configuration.
2. Download `Gunmaggedon.Runtime.zip` and `Gunmageddon_0.0.26.nobp` from the [release](https://github.com/taxidriver563/Gunmaggedon/releases/tag/Releases).
3. Extract the runtime ZIP into the game folder, merging its `BepInEx` folder. Place the `.nobp` in `BepInEx/plugins`.
4. Remove old `GunmageddonAudio.dll` copies and keep only one Gunmaggedon runtime and the current asset pack active.
5. Start the game. Existing settings remain in `BepInEx/config/gunmageddon.203mm.audio.cfg`.

The standalone `Gunmaggedon.Runtime.dll` attachment is the same CI-built runtime for manual replacement. The ZIP contains the runtime only; the asset pack is downloaded separately. Older `dist/` folders are historical snapshots.

For ordinary coastal guns, `[Coastal ammunition] Mode` accepts `Auto`, `HE`, or `AP`. Auto selects AP against ships at least 65 m long or armor tier 3+, while ranging and illumination requirements still apply.

## Disclaimer

This project is an unofficial community modification and is not affiliated with, sponsored by, or endorsed by Shockfront Studios Pty Ltd. Original Nuclear Option assets, vehicle designs, audio, and code are Copyright (c) 2026 Shockfront Studios Pty Ltd. All rights reserved. Nuclear Option and Shockfront Studios are trademarks or registered trademarks of Shockfront Studios Pty Ltd. Original mod content and all other trademarks belong to their respective owners.
