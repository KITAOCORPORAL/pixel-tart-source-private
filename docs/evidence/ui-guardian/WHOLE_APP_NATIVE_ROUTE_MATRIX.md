# Whole-app native route matrix — BLOCKED

Generated from `a28f4d39ab701c02dcaa4c7ce792b989a549bf3d`. Detailed ledger: `WHOLE_APP_NATIVE_ROUTE_MATRIX.json`.

| Requested route | Product mapping | 1180×720 | 1600×920 | 1920×1080 |
|---|---|---|---|---|
| Workbench | Workbench | NOT RUN | NOT RUN | NOT RUN |
| Asset Library | AssetLibrary | NOT RUN | NOT RUN | NOT RUN |
| Tether / Photography | Tether | NOT RUN | NOT RUN | NOT RUN |
| Planning | Planning | NOT RUN | NOT RUN | NOT RUN |
| Online Selection | OnlineSelection | NOT RUN | NOT RUN | NOT RUN |
| Color Studio | ReferenceColor | NOT RUN | NOT RUN | NOT RUN |
| Publishing | Publishing | NOT RUN | NOT RUN | NOT RUN |
| Settings | Settings modal | NOT RUN | NOT RUN | NOT RUN |

Production EXE startup and fixture navigation succeeded at 1600×1000 DIP / 2400×1500 physical pixels, Windows scaling 150%. This is not one of the required native route matrix captures and is not counted.

Exact blocker: Computer Use screenshot capture reports `SetIsBorderRequired failed: 不支持此接口 (0x80004002)`; after one fresh-window retry the failure is unchanged. Text-only observation works; coordinate click reports `coordinate input geometry is unavailable`. No fallback command invocation, new UI automation backend, global scroll container, route exemption or Guardian exemption was introduced.

Current native P0/P1 are unknown (null), not zero. Historical calibrated P0=0/33 states remains historical evidence. Screenshots, resize and route pointer navigation have not been verified this run.
