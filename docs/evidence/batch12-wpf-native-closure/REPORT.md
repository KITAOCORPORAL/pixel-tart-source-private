# WPF full-suite termination and native acceptance — PARTIAL

Base716be5741c9fb19a3e3e4e81d0a7036f16cb0712, independent named worktree.
Repair commitdc18b14e72fac341a1227364d91d6c5ba9809674. No product src changes.

## Investigation and actual end-to-end test runs

Prior batch12 runs were manually stopped by the controller after132/70completed tests; TRX reported
aborted/testhost unexpected termination. No crash dump or contemporaneous stacks from those two
runs existed. A Sequence XML named loading-error-retry-recovered/v1 incomplete, not proven cause.
We cannot reconstruct historical threads after process death. This follow-up captures actual live
reproduction, rather than attributing a new stack to the old dead process.

Focused9scenario reproduction:9pass and terminated in55.93seconds. Baseline full WPF, without
product/test edits, genuinely ended:1542pass/0fail/21skip,8.2719minutes. Thus a fixed scenario deadlock
was not reproduced; the earlier wait budget and silent minimal logger caused premature termination.

Managed stack PID22784: thread0x8018 in OSFileStreamStrategy.Read -> Stream.CopyTo -> GitBlob ->
SourceAudit -> EvidenceFixture constructor. This was a transient sampling phase, not demonstrated deadlock.
Full reproduction PID28008: test thread0x87A8 in Process.WaitForExit -> RunValidator -> fault scenario.
Other threads service pipe reads/testhost transport/thread-pool waits. Parent/child evidence shows
owned PowerShell validator. Windows WCT for34728/0x87A8 succeeds, cycle=false, thread stateWait/UserRequest,
only one thread node: WCT does not expand this process-handle wait to a complete child chain. The
logical parent->validator relationship comes from process metadata+managed stack, not a fabricated
WCT dependency. Independent tests progressed5-7seconds each and completed. No cyclic wait proved.

## Fix scope

Test-only BoundedTestProcess supervisor concurrently drains binary stdout and stderr and bounds
exit plus pipe EOF together. Timeout kills only the exact owned subprocess tree, emits diagnostic
identity/status and throws TimeoutException; never reports partial output as successful exit.
P1 Git blob/text/ancestry and PowerShell calls and P2 PowerShell calls adopt it. No validator criteria,
expected negative-evidence rejection, UI or renderer semantics changed. Added two tests:256KiB each
pipe with exit7; timeout process termination with no residual owned PID. Focused11/0/0.

Final whole Core1676pass/0fail/7skip,1683discovered, ended normally. Final whole WPF1544pass/0fail/21skip,
1565discovered,8.4585minutes, ended normally. Logs/TRX identities and skipped names retained.
Known opt-in skips still are not physical desktop/device acceptance. Runs tested these patch contents
before commit; production Release remains the exact original frozen payload, not a rebuild relabelled old.

## Native acceptance

Original batch12 Release290files hashes verified, copied and reverified; real isolated Windows process
PID16612, title2.3.0-dev.0fec3c6, STARTUP_OK. No fixture command-line arguments. Initial incomplete copy
attempt is separately retained as setup failure, superseded by complete sealed copy.

Real ColorStudio navigation and real source-read-only file picker observed. Paths entered via Computer
Use but no successful image render observed. Desktop was repeatedly covered/moved by unrelated user
windows, controls/focus changed, input and screenshots timed out. No private unrelated screenshots are
saved/distributed. No unsupported UIAutomation/FlaUI/custom message injection fallback used.

Physical GetDpiForWindow144; DPI awareness2. Helper monitor API returned96 from its querying process
context; not interpreted as monitor physical DPI or screenshot scaling truth. No display setting change,
no100/125/150/200percent transition, no logical DPI simulation. Pixel1:1 and hit testing still unverified.

Open-photo is BLOCKED; switch/cancel-active-task/save-reopen/error-message NOT_RUN. Opening/cancelling
picker is not task cancellation. Product failure count is not manufactured from tool failures. Physical
Escape stop in prior turn is USER_INTERRUPTED, then direct authorization resumed work. Current next
step waits for an exclusive desktop interval. Sony decoder/Polaroid measurement remain separate.

## Preservation

All other worktrees inspected read-only. Full suite regenerated tracked ui-guardian audit reports in this
new initially clean checkout; generated outputs archived in local artifacts and base blobs restored after
runs. No other user changes reset/stashed/merged/cleaned. User authorized Git publication on 2026-10-13. Publish this PARTIAL evidence without claiming native acceptance complete.
