# Global Close Button Audit

The pre-fix shell close was a 40×40 `SurfaceCloseButton`, right/top aligned at ZIndex 30000 over the entire route content. Reserved space was zero, so any right-aligned page action could share its hit rectangle. Reference Color “选择照片” provided the visible reproduction.

| Control | Owner | Size | Position | Layout | Reserved space before/after | Collision fix |
|---|---|---:|---|---|---|---|
| ShellSurfaceCloseButton | MainWindow closable routes | 40×40 | shell top-right | shell safe-area column | 0 / 56 DIP | structural third column |
| Settings close | Settings modal | 40×40 | header column | modal header | Auto / Auto | retained; Shell close hidden |
| Task details close | Task drawer | 40×40 | header column | drawer header | Auto / Auto | retained; Shell close hidden |
| Booking editors/details | booking modal/drawer | 40×40 | header column | local header | Auto / Auto | retained; Shell close hidden |
| Online create close | Online Selection modal | 40×40 | header column | modal header | Auto / Auto | retained; Shell close hidden |
| Finance editor close | Finance drawer | 40×40 | header column | drawer header | Auto / Auto | retained; Shell close hidden |
| Tutorial close | tutorial callout | 40×40 | callout top-right | local overlay | local title margin | retained; Shell close hidden |
| Planning image close | image preview | icon target | preview top-right | local preview grid | caption right reserve | retained |
| Tether quick preview / inspector | local overlays | button target | overlay top-right | local grid | explicit local control | local audit; not Shell-owned |

The production shell now owns a 56 DIP close-safe column whenever `IsCurrentSurfaceClosable` is true. All route content ends before that column. Modal triggers preserve the existing single-close-authority behavior. Runtime matrix tests cover 1180, 1366, 1600, 1920 and 2560 widths at 100%, 125%, 150% and 200% layout scales.
