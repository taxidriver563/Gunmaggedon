Gunmageddon Coastal Artillery

Current helper: 1.6.0. Compatible installed asset pack: 0.0.7.
Workspace documentation: ../context/README.txt
History: ../context/CHANGELOG.txt

Runtime code is under src/, grouped by feature. Namespaces and runtime logic
are unchanged by the folder reorganization. Tests and tools are separate.

From this workspace's root:
  powershell -NoProfile -ExecutionPolicy Bypass -File GunmageddonAudio/build.ps1
  powershell -NoProfile -ExecutionPolicy Bypass -File GunmageddonAudio/tests/run-unit-tests.ps1

build.ps1 compiles only src/**/*.cs, including nested feature folders.
It writes build/1.6.0/GunmageddonAudio.dll; it does not install the DLL.
Use -OutputDirectory <path> for a separate validation build.

Unity integration fixtures: tests/integration/
Unity regression fixtures: tests/regression/
Standalone unit suites: tests/unit/
Native game IL inspection utility: tools/InspectGameIL.cs

The helper DLL and the Blueprinter .nobp asset pack must both accompany the mod.
See context/BUILD-AND-VALIDATION.txt for dependencies and validation limits.
