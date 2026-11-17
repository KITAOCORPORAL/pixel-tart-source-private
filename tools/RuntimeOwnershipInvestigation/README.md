# Runtime ownership diagnostic (Windows)

This investigation links the unchanged production SingleInstanceManager and Branding sources. It uses a unique apphost alias, a unique mutex namespace and a preflight that permits only child PIDs/executables created by the fixture. It never starts a product EXE or opens a user runtime.

The two synthetic owners hold different instance names, have no ready UI/activation ACK and age past15seconds. recoverA requests only ownerA. The current production recovery then terminates both owners. This is a source-linked conditional counterexample, not proof that a real user's data was lost and not a product regression pass.

Build the project using the existing .NET10 SDK with Release/Platform=x64. Set DOTNET_ROOT and DOTNET_ROOT_X64 only for the current process. Do not install an SDK or change persistent system configuration for this probe.

Run run-owned-probe.py with two arguments: the built bin/x64/Release/net10.0 folder and a new owned output root. The runner copies the diagnostic apphost under a unique name and retains the result, PIDs, resource samples and logs. Each child has a2GiB guard; forcedGC and RAW are not used. The fixture's process exit0 only means observation completed: inspect RESULT.status and otherRootTerminated. The default probe demonstrates the current defect; it is not an assertion that the defect has been fixed.

Source files in src/tests are not changed. Current evidence is in docs/validation/development-root-cause-review-20261117/RUNTIME_OWNERSHIP_REPRODUCTION.json. Product changes require review of OPTIONS_AND_MINIMAL_SCOPE.md.
