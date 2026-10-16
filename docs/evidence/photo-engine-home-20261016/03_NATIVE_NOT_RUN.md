# Stage3 native acceptance boundary

Current user restrictions continue to prohibit ComputerUse, Browser/WebView/CDP. No exclusive desktop booking/idle guarantee or native API access has been established. We did NOT activate the product, inject input, move foreground windows or change physicalDPI. Current monitor associations were queried read-only; their presence is not screen calibration or actual WPF color-compensation proof.

|Step|Actual status|
|---|---|
|Full release EXE normal native startup|NOT_RUN|
|Workbench→ColorStudio real navigation|NOT_RUN|
|Canon/Fuji foreground open/switch/inflight-cancel/version save-reopen|NOT_RUN|
|Sony real product FileUnsupported UI|NOT_RUN: exact authorized Sony original absent; old failure semantics/code retained|
|ForegroundTIFF/PNG export and dialog/readback|NOT_RUN; backend product APIs separately tested on authorized copies|
|Natural process memory after product workspace close|NOT_RUN; backend child10s series is not workspace/native-disposal proof|
|Physical100/150/200DPI set/check/restore|NOT_RUN; no settings changed, nothing to restore|
|WPS actual edit/import|NOT_RUN, not relevant to static-photo acceptance here|
|CleanVM native dependency load/full UI chain|NOT_RUN|

Backend audit reports DO NOT substitute for any line above. Use isolated authorized copies only after operator confirms exclusive desktop availability. Stop on foreground conflict/access denied. No claim that an old0x80070005 occurred again here.
