Gunmaggedon Runtime

Current helper: 1.8.4. Compatible installed asset pack: 0.0.18.
Workspace documentation: ../context/README.txt
History: ../context/CHANGELOG.txt

Runtime code is under src/, grouped by feature. Namespaces and runtime logic
are unchanged by the folder reorganization. Tests and tools are separate.

From this workspace's root:
  powershell -NoProfile -ExecutionPolicy Bypass -File GunmaggedonRuntime/build.ps1
  powershell -NoProfile -ExecutionPolicy Bypass -File GunmaggedonRuntime/tests/run-unit-tests.ps1

build.ps1 compiles only src/**/*.cs, including nested feature folders.
It writes build/1.8.4/Gunmaggedon.Runtime.dll; it does not install the DLL.
Use -OutputDirectory <path> for a separate validation build.

Unity integration fixtures: tests/integration/
Unity regression fixtures: tests/regression/
Standalone unit suites: tests/unit/
Native game IL inspection utility: tools/InspectGameIL.cs

The helper DLL and the Blueprinter .nobp asset pack must both accompany the mod.
See context/BUILD-AND-VALIDATION.txt for dependencies and validation limits.

Install: BepInEx/plugins/Gunmaggedon/Gunmaggedon.Runtime.dll. Remove older
GunmageddonAudio.dll copies from all plugin subfolders when upgrading.
The original plugin ID, configuration filename and C# namespace are retained
for compatibility with existing settings and version-pinned test baselines.
