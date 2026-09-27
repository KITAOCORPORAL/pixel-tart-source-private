# WPS Office Version Baseline

Status: **PARTIAL — installation metadata verified; Writer/Presentation UI not verified**

## Scope and method

This baseline uses Windows installation metadata and file version metadata only. The requested WPS UI traversal and About dialogs were not performed because the active workspace rules prohibit Browser/WebView/Computer Use automation. No proprietary binary inspection was performed.

## Observed baseline

| Field | Value | Evidence | Confidence |
|---|---|---|---|
| Product | WPS Office | Windows uninstall registration | Verified |
| Product version | 12.1.0.28505 | DisplayVersion and executable metadata | Verified |
| Architecture | 64-bit Windows host; WPS executable architecture not independently inspected | `Win32_OperatingSystem` reports 64-bit OS | Partial |
| Windows | Microsoft Windows 11 专业版, 10.0.26200 | `Win32_OperatingSystem` | Verified |
| Writer executable | `D:\store\WPS Office\12.1.0.28505\office6\wps.exe` | Process and file metadata | Verified |
| Presentation executable | `D:\store\WPS Office\12.1.0.28505\office6\wpp.exe` | File metadata | Verified |
| Writer UI version | Not separately exposed by metadata | No UI traversal | Not verified |
| Presentation UI version | Not separately exposed by metadata | No UI traversal | Not verified |
| Build | 12,1,0,28505 | `wps.exe`, `wpp.exe` FileVersion/ProductVersion | Verified |

## Reproduction commands

```powershell
Get-Process wps | Select-Object Path
Get-Item 'D:\store\WPS Office\12.1.0.28505\office6\wps.exe' | Select-Object -ExpandProperty VersionInfo
Get-Item 'D:\store\WPS Office\12.1.0.28505\office6\wpp.exe' | Select-Object -ExpandProperty VersionInfo
Get-CimInstance Win32_OperatingSystem | Select-Object Caption,Version,BuildNumber,OSArchitecture
```

## Follow-up required for a complete baseline

Open Writer and Presentation manually, record the About dialog, edition/license channel, language, update channel, and the exact UI build shown by each application. Attach screenshots to the evidence manifest before marking UI-dependent fields `VERIFIED`.
