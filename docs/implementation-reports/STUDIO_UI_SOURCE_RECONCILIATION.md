# Studio UI source reconciliation

This audit starts at branch HEAD `4c7eaa8fbd870ac2fa67584609fe84deb5e29b28`.
It preserves historical evidence rather than changing an earlier failure to PASS.

| Commit | Identity | Evidence scope |
| --- | --- | --- |
| `993854ce0fd2437bd1fc05dc36c6bac316305a48` | Historical four-page Studio sample freeze | Partial global typography, loading, geometry and scroll coverage |
| `9bcce6098ce876096bdd444030f554b968384fa0` | Current Studio / Reference / Film / Close product freeze | Reference responsive composition, procedural texture thumbnails, Film proxy and cancellation; structural Shell close reserve |
| `ac26e026accfc5928d791fb2c2d7a373832fda17` | Evidence commit | Reference/Film sheets and per-effect images; 33 closable runtime states; scoped text audit |
| `4c7eaa8fbd870ac2fa67584609fe84deb5e29b28` | Test infrastructure HEAD | Opt-in VisualEvidence excluded from functional WPF gate |

## Historical issue disposition

| Earlier issue | Current disposition | Required global work |
| --- | --- | --- |
| Reference narrow / high-DPI density | Closed by Reference responsive layout and 100–200% captures | Regression only |
| Reference failure / retry evidence | Closed by Loading/Error/Retry/Recovered sheet | Shared style regression |
| Film texture / individual effect evidence | Closed by per-effect output and true texture previews | Regression only |
| Shell close collision | Closed by 56 DIP Shell reserve and 40×40 target | Re-run across rollout |
| Global typography / legacy symbols | Open rollout scope | Audit actual rendered routes and migrate by purpose |
| Global border density | Open rollout scope | Workbench, Workflow, Finance, Settings and tools |
| Global loading and empty/error | Open rollout scope | Audit operation bindings, totals, feedback and recovery |
| Text / scroll reachability | Open rollout scope | Expand beyond four-page unwrapped TextBlocks; interact with scroll and expanders |
| Complete full-size screenshot review | Open rollout scope | Record individual review status after opening each required image |
| Physical DPI / camera / installed dialogs | NOT TESTED | Not part of this in-process UI rollout |

The old Studio BLOCKED report describes its historical freeze. Reference/Close P1=0
describes narrower, later closures. Neither claims that whole-app rollout was complete.
Current work and status continue in [Global Rollout](PIXEL_TART_STUDIO_UI_GLOBAL_ROLLOUT.md).
