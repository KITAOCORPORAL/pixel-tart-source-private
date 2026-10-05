# Color Studio 重构起点

- 核验时间：2026-10-05；本机时区 Asia/Shanghai。首次核验 UTC 02:40:09。
- 实际工作树：`N:/pixart/pixel-tart-source-private`。
- 分支：`integration/pixel-tart-developer-preview`。
- START_HEAD / fetch 后 REMOTE_HEAD：`5427406c4ef3c805ef56fbb814251df69365032d`。
- 起点工作区 CLEAN；没有用户未提交修改。未发现仓库或祖先 AGENTS.md。
- 与用户指定 5427406 相比：起点没有新增源码。后续修改以此为基线。
- 已打开原始参考图：`codex-clipboard-c5b9b11b-eeb1-4fb7-85bf-6a72b040a9ea.png`，1672×941。
- 参考图 SHA256：`82A1EAB8C4DEBDC278AAF2D6086A7FD6527C7362B9613AEBE9B6F4D24E34AC0E`。
- Capture One：`16.6.1.2962`；Windows 11 Pro 10.0.26200。屏幕实际缩放待实测。
- 用户已将授权测试目录导入 C1，并确认继续桌面验证。使用 DSC04831.ARW 的新变体 [2] 调查，原变体保留。曝光、阴影、RGB 曲线、色阶中间调已经分别操作并复位；其余观察程度见 C1_FEATURE_BEHAVIOR_MATRIX.md。
- 测试目录有 DSC04831–DSC04835 五组 ARW/JPEG，无用户提供的 TIFF。输入原件只读，SHA256 清单保存在本地 artifacts 的 input-manifest.json；测试生成的 TIFF 必须标明派生文件。
- DSC04831.ARW SHA256：558DA0141BBF67D153D3DA5523EBBD2CC4C304FF239061F94F97FE2BC5D4C6F3。
- DSC04831.JPG SHA256：0262124931AD899DE360312EB07EA019FF0A66C20DC00220D50F9129DD35D45E。
- 桌面 helper 曾有缩放坐标与导入模态窗口定位问题，用户协助原位导入后继续。仅记录实际完成的观察。
- 既有 r4 EXE SourceHead=63cd21e，不是当前源码，也不是本轮证据。
- 本阶段不制作安装包、不推进 Film Lab / Polaroid 等路线。
- USER_VISUAL_REVIEW=NOT_APPROVED；VisualApproved=false；UserVerified=false。

## 最近提交（起点）

```text
5427406 docs(review): record r4 runtime evidence and remaining verification gaps
1485e6d test(ui): verify shared escape routing for asset transients
63cd21e fix(asset): close local filter before shell escape navigation
6a52e83 fix(studio): detach decoded pixels before asynchronous preview
b5bd051 fix(studio): preserve target viewport in short runtime windows
f88cbc1 fix(test): parse BOM-bearing source blobs in evidence validator
d95a2f3 test(wpf): reuse suite application for visual phase matrix
e148caf fix(ui): address evening feedback across browser studio and canvas
```
