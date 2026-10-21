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
