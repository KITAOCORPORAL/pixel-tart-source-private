# RC11 WPF Test Infrastructure Audit

## Scope

The RC10 full WPF run reported 1,066 passed, 85 failed and 2 skipped. The failures were classified from the captured test output rather than blanket-skipped.

| Class | Evidence | RC11 action | Result |
|---|---|---|---|
| Product behavior | Async thumbnail queue, missing-source state, context command contracts | Fixed missing-source completion and added targeted regression tests | PASS: targeted WPF suite 34/34; thumbnail suite 4/4 |
| Test environment | Multiple `System.Windows.Application` instances in one AppDomain | Documented as suite isolation debt; no production behavior was hidden | KNOWN LEGACY EXCEPTION |
| Old branch assumption | P1/P2/P3 runners and validators required historical `feature/*` names | Runners now accept any named development branch and record the actual branch; main/master/HEAD remain rejected | FIXED in RC11 |
| Repository-root assumption | Old evidence fixture could resolve a sibling checkout | Test-side root resolution uses the solution marker discovered from the executing assembly; no hard-coded `D:\` path is introduced | FIXED in RC11 |
| Missing historical evidence | DPI suite expected `artifacts/automated-dpi-review/2.0.4/AutomatedDpiScreenshotHashes.json` | RC11 does not fabricate that file; current-version evidence remains opt-in and is reported separately | KNOWN LEGACY EXCEPTION |
| Test isolation | Fire-and-forget dispatcher work and static WPF application lifetime | Existing `RunSta`/dispatcher drains are retained; production async thumbnail requests now complete synchronously for missing paths | PARTIAL: broader singleton cleanup remains |

## Verification

- Solution Release build: 0 warnings / 0 errors.
- Core suite: 1,303 / 1,303 passed.
- Targeted WPF thumbnail/context/viewer suite: 38 / 38 passed.
- Full historical WPF suite still contains legacy evidence and singleton failures; RC11 does not claim it green until those exact suites are migrated to isolated processes.

## Remaining infrastructure work

The next safe step is process-per-WPF-fixture execution for suites that instantiate `Application`, plus a versioned current-run DPI evidence producer/validator pair. This is intentionally not implemented by weakening assertions or skipping the failing tests.
