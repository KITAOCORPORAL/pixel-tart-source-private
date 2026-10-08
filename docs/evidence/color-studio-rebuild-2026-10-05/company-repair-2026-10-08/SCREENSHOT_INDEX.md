# Company runtime images and missing evidence

Both published captures are full actual production-window PrintWindow captures,
not Designer, test-host, generated layout images or old home screenshots.
SourceHead: 1a9a53913665490298e6de1b5656a33d0efc934b; PID 45896; HWND 18154890.
Physical dimensions 2400×1380; GetDpiForWindow=144; dimensions in DIP 1600×920.
Captured at the observed scale only. No claim about 100%, 125% or 200%.

| File | UTC | SHA256 | What it proves |
|---|---|---|---|
| [01-company-release-start.png](screenshots/01-company-release-start.png) | 2026-10-08T02:57:57.3198548Z | 0EF4204F17926386E5F69CCFC43A7A7651CA2D7246D44B3D3CC4699F4EE60969 | New Release launched into real onboarding; startup log matches source |
| [02-company-studio-observed.png](screenshots/02-company-studio-observed.png) | 2026-10-08T03:05:18.1120205Z | CE8DAF1F212B05795A9C7A2B8A519F14916A37AAD6801AFF6A40EEC90012E871 | New Studio empty state was observed after un-attributed native interaction; tone entry and wrapped tabs visible; not a pointer test |

Neither image contains private originals, user workfiles, personal paths or account
details. They were visually opened and checked before copying into this directory.

## Required paired evidence — not completed

| Issue | Company baseline before | Repaired after |
|---|---|---|
| CU-01 | Automated bounds failure only; no complete native three-size/four-DPI series | Empty-state observation only; no complete reachability series |
| CU-05 | No reproducible company numeric-key sequence; home failure retained | No full native abc/empty/range/decimal/Enter/Tab/blur/Esc/history series |
| CU-02 | Home floating-window failure retained; no company public paired capture | Dock is implemented/tested; native rotate/pan/zoom/pick not completed |
| CU-03 | Home 9-versus-8 mismatch retained | Frozen-set tests only, native boundary sequence pending |
| CU-04 | Home English/Traditional mixed-text failure retained | Resource tests only; full native switch/restart and long-text series pending |
| CU-06 | Home inner-scroll failure retained | New pinned entry visible in 02; hover/leave/Esc/export invariant pending |

Baseline PID 36044 was independently built from a9f61bab and launched before source
edits. Local private-captures holds onboarding/home/studio/file-dialog attempts;
some include private material already selected in the app, so they are not published.
No home images were relabeled company-before or company-after. No company export
file was produced and verified via the normal UI; export comparison remains pending.

Capture/input diagnostics: bundled capture reported FrameArrived/window-capture
timeout; click fallback reported coordinate geometry unavailable. Alternate tool
returned unrelated screenshot content (discarded), while native dialog Invoke /
Return / coordinate attempt did not reliably submit (one 30s runtime timeout).
Later it could not disambiguate two same-name processes by executable path.
Bundled input also detected user input; no further input was sent after that signal.
Read-only observations and screenshots continued. This is not an app test PASS.
