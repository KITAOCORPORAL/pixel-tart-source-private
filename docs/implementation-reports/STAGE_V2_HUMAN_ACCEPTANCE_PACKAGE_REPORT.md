# Stage V.2 Human Acceptance Package Report

## 状态

`PARTIAL / READY FOR HUMAN VISUAL ACCEPTANCE`

本报告只记录可由源码、构建和包契约证明的结果。真实用户电脑的 DPI、多屏、真实相机、真实图片规模和截图仍需人工执行后回填；没有把这些项目标为已通过。

## 构建与包

- 版本：`2.3.0-RC12`（当前工程统一版本 `2.3.0`，RC12 为发布阶段标记）
- 产品源 SHA：`5aeb417991449578e6c83bba36cc92396dde2687`
- 分支：`integration/pixel-tart-developer-preview`
- 构建：`dotnet publish -c Release -r win-x64 --self-contained true -p:AcceptanceBuild=true`
- 编译结果：0 warnings / 0 errors
- Developer Preview：`artifacts/stage-v2-human-acceptance/PixelTart-DeveloperPreview/`
- 启动脚本：`START_UI_ACCEPTANCE.bat`
- Demo Workspace：包根 `DemoWorkspace/`；启动时强制隔离，直接双击 `PixelTart.exe` 也默认使用包内目录
- 真实产品入口：`PixelTart.exe`（复制自 AcceptanceBuild 的正式 WPF `App.xaml` / `MainWindow` 输出）

## Demo 内容

启动种子只写入隔离根：12 个镜头、Lighting/Pose/Storyboard/Styling 外部参考、项目摘要、灵感板、自由画布、调色板、11 区间色调目标和一个默认参考风格。没有连接真实相机，没有触碰生产 LocalAppData 数据。

## 自动化/静态验证

- AcceptanceBuild 发布：通过，0 warnings / 0 errors
- Planning Core contract tests：通过，8/8
- Planning WPF contract project：Release 编译通过；当前 runner 未返回匹配用例，未将其虚报为通过
- 真实入口契约：通过（启动后调用 `MainViewModel.OpenPlanningAsync`，不是假页面）
- Demo 隔离契约：通过（`PIXEL_TART_HUMAN_ACCEPTANCE`、`PIXEL_TART_ACCEPTANCE_ROOT`、包内默认 `DemoWorkspace`）
- 启动脚本契约：通过（脚本不含开发机绝对路径）
- UI 截图：`NO`（当前环境未进行人工桌面截图）
- 物理 DPI：`NOT TESTED`
- 物理多屏：`NOT TESTED`
- 真实相机：`NOT TESTED`

## 人工验收文档与审计

- `MANUAL_UI_ACCEPTANCE.md`：机器矩阵和完整 Checklist
- `USER_FEEDBACK_TEMPLATE.md`：第一次打开/导入/查看/整理的“需要猜”记录模板
- `BUTTON_UI_AUDIT.md`：常驻动作与低频菜单入口审计
- `ROUNDED_UI_AUDIT.md`：圆角 token 与历史组件例外审计
- `BUILD_PROVENANCE.json`：产品源、包哈希和构建命令
- `PackageMetadataSha`：`0ead337f723b262175b7f88589286c71dab62821fe344092f6121d744f7779d8`（将该字段置空后的规范 JSON UTF-8 SHA-256）
- `PixelTart.exe` SHA-256：`BDE0059CEEF20AA4757F53D9596581D2A5B4496BD0330E27E12642FEB69BDF77`

## 语言扫描

Planning/Tether 用户可见文案继续使用中文；构建/审计文档中的内部术语不作为产品 UI 通过依据。以下保留项仅用于技术契约或状态绑定，不应出现在用户可见文案：`TaskId`、`P3Query`、`SHA`、`LibRaw`、`AssetId`、`Viewer`、`Query`。`True` / `False` 仅作为 XAML 绑定值或序列化技术值检查，不作为界面标签。

## 真实用户回填

完成普通用户电脑测试后，请把截图、发现问题、修复记录和最终通过项追加到本报告；只有 Crash、错位、乱码或功能不可理解才允许在 RC12 范围内修复。

## 启动阻塞修复记录

2026-09-18 首次人工启动发现 Planning 页面加载失败：`ToggleButton` 错误引用了 `Button` 类型的 `GhostButton` 样式，触发 WPF `XamlParseException`。已新增仅适用于 `ToggleButton` 的本页筛选样式并重新完成 AcceptanceBuild/publish；随后出现的关闭阶段空引用属于启动失败后的连带清理，不是独立产品故障。

## 包文件清单（按扩展名）

当前包包含：`.exe` 3 个（含根入口与运行时辅助程序）、`.dll` 270 个、`.json` 4 个、`.md` 6 个、`.bat` 2 个、`.txt` 8 个，以及 `DemoWorkspace/.keep`。自包含运行时文件均位于 `PixelTart-DeveloperPreview` 根目录，用户无需进入 `bin/Release`。
