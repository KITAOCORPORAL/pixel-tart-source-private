# 回传给 GPT：北尾鸲归片助手 1.4.2 顶部与侧栏精修报告

## 图一重叠原因

1.4.1 侧栏使用 `DockPanel + StackPanel`，主导航、应用导航、免费版卡片和收起按钮仍处在同一纵向内容流中。窗口高度降低或 DPI 提高时，导航内容不会优先滚动，免费版卡片也没有独立布局行，因此“授权与版本”、版本卡和底部区域产生视觉挤压。原状态栏高度 54，内部又包含文本和进度两行，也放大了底部压迫感。

## 左侧栏修复

侧栏改为稳定三行 `Grid`：

1. `*`：导航主区，使用垂直 `ScrollViewer`；
2. `Auto`：免费版信息卡，固定最小高度 80；
3. `Auto`：收起侧栏按钮。

导航区在小窗口下先滚动，不会压住版本卡。版本卡使用 14 内边距、正文换行、19 行高和独立上下间距。侧栏位于主窗口内容行，状态栏位于独立底行，两者不会发生布局交叉。

## 顶部大框取消

顶部应用区由完整 `Border` 容器改为平面 `Grid` 信息带，不再设置四边描边或圆角卡片。信息带只保留语义表面背景和底部 1px 分隔线，形成轻量层级。

## 免费版与升级入口重设计

- 索引：32 高中性 pill。
- 免费版：32 高、圆角 16 的浅强调色状态标签，不承担主按钮语义。
- 升级专业版：32 高透明 Ghost 文本按钮，Hover 才显示轻背景。
- 取消任务：32 高独立次级按钮，与版本组之间设置 24 高竖分隔线和 20 水平间距。
- 无任务时仍由原 `CancelCommand` 显示禁用状态。

## 顶部对齐

顶栏保持 72 高，采用左 / 中 / 右三段：左侧两行标题与副标题左对齐，间距 3；中间状态文字 13 号并垂直居中；右侧全部控件统一 32 高、13 号字、垂直居中、右对齐，并启用 `UseLayoutRounding` 和 `SnapsToDevicePixels`。

## 修改文件

- `src/RAWSelectionAssistant/MainWindow.xaml`
- `src/RAWSelectionAssistant/Resources/DesignSystem/Controls.Buttons.xaml`
- `src/RAWSelectionAssistant/Resources/DesignSystem/Controls.Status.xaml`
- `src/RAWSelectionAssistant/Views/HelpWindow.xaml`
- `src/RAWSelectionAssistant.Core/Models/Branding.cs`
- `src/RAWSelectionAssistant/app.manifest`
- `src/RAWSelectionAssistant/RAWSelectionAssistant.csproj`
- `Directory.Build.props`
- `installer/RAWSelectionAssistant.iss`
- `README.md`
- `docs/UI设计系统_1.4.0.md`
- `tests/RAWSelectionAssistant.Tests/UiPolish142Tests.cs`
- 既有 UI 版本测试

## 自动化测试

- 新增 21 项 1.4.2 布局测试，覆盖侧栏三段分层、可滚动导航、版本卡尺寸、独立状态栏、无外框信息带、右侧 32 高度体系、视觉分组、100% / 125% / 150% DPI 数学约束、浅色 / 深色资源和 WinExe。
- Debug 完整测试：251 / 251 通过。
- Release 完整测试：251 / 251 通过，0 失败、0 跳过。

## 编译与安装包

- Debug 与 Release 构建：0 警告、0 错误。
- 安装包：`artifacts/installer/北尾鸲归片助手_Setup_1.4.2_x64.exe`
- 安装包大小：46.46 MB
- SHA-256：`E7BB78C7B2448552551C861F35735E5D2EB2C771E5AC7C2F7D4B990589B1C668`
- 发布 EXE：1.4.2 / 1.4.2.0
- 发布授权配置：Provider=None

## 已知问题

本机 Inno Setup 7 编译器标记为 `Non-commercial use only`；正式商业分发前应使用具有相应商业许可的安装包编译环境。
