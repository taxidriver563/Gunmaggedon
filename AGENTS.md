# Gunmaggedon project rules

Read `context/README.txt` and the relevant feature documentation before changing units. Update the context documentation when behavior or setup requirements change.

## Mandatory unit LOD setup

User requirement, established 2026-10-22: integrate LOD components as a mandatory part of unit setup from now on.

- Every new unit prefab must ship with an enabled, correctly configured Unity `LODGroup` and meaningful distance-based reductions in model detail. A single full-detail level followed by culling does not satisfy this requirement.
- When modifying an existing unit, audit its LOD setup and bring it into compliance as part of that work. Do not silently remove an inherited LODGroup without providing a replacement appropriate to the new model.
- Include a near-detail level and at least one cheaper distant level, using simplified meshes and/or removing small details that no longer contribute visibly. Choose screen-size thresholds and final culling for the asset's size and role; do not apply one fixed distance to every unit.
- Account for every model renderer, including articulated barrels, handles, breeches and temporary loading-shell visuals. Preserve animation, recoil, material assignments and reload visibility across transitions; inactive loading parts must not be made visible by LOD switching.
- Manage searchlight beams, lens glare, particles and trails separately where their useful visibility differs from the physical model. Document these exceptions; do not leave full-detail model parts outside LOD management accidentally.
- LOD changes must affect presentation only: targeting, detection, firing, damage, physics and networking must continue independently of render detail.
- Skip unnecessary cosmetic animation at distant LODs, as requested by the user. Keep reload clocks and readiness gates running; preserve native animation that also controls a muzzle or another gameplay transform unless it has been safely separated from presentation.
- Validate renderer references, reduced distant geometry, transitions, final culling and bounds on the exported prefab. Check relevant animations across LOD transitions. Record what was checked and any unverified in-game behavior in `context/`.

See `context/LOD-POLICY.txt` for the baseline audit and rollout status.
