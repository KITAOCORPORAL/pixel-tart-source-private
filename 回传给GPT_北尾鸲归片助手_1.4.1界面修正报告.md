# 回传给 GPT：北尾鸲归片助手 1.4.1 界面修正报告

## 完成情况

1.4.1 已完成截图所示菜单裁切、侧栏图标乱码、右上角版本区域拥挤、报告默认行为和操作区品牌重复问题的针对性修正。编号解析、照片索引、匹配、冲突选择、复制、授权和教程链路未重写。

## 顶部菜单

问题原因是原 `MenuItem` 仅设置了较小内边距，没有为顶层菜单提供稳定的高度、最小宽度、对齐和像素取整；WPF 默认模板在高 DPI 和微软雅黑 UI 字体度量下会压缩文字热区。

修正方式：菜单栏固定为 36 高，顶层菜单项最小宽度 76、最小高度 34、内边距 14×7，统一 13 号 Microsoft YaHei UI，并启用 `UseLayoutRounding`、`SnapsToDevicePixels` 和垂直居中。原 `_F/_P/_E/_V/_T/_H` AccessText 结构保留，Alt 菜单访问不变。

## 侧栏图标

乱码原因是旧界面使用 `Segoe Fluent Icons` 私有区 Unicode 字形；不同 Windows 版本或字体缺失时会回退成空方块。

修正方式：新增 `Icons.Navigation.xaml`，用项目内打包的 `Geometry` / `Path` 提供项目中心、归片工作区、项目历史、授权、设置、帮助和收起侧栏图标。图标统一 18×18、1.6 线宽、圆角线帽，颜色使用主题语义资源。导航按钮保留 ToolTip 和 AutomationProperties.Name，展开 / 收起及浅色 / 深色均不依赖字体。

## 右上角区域

删除了两个基于字体字形的方框按钮。外观与侧栏操作仍可通过“视图”菜单和设置页访问。新布局为：索引状态 → 免费版胶囊标签 → 低调升级文字按钮 → 分隔线 → 独立取消任务按钮。“取消任务”继续由原命令控制，无任务时显示禁用状态。

## 报告导出设置

`settings.json` 新增兼容字段：

```json
"reportSettings": {
  "defaultExportEnabled": false,
  "defaultExportCsv": true,
  "defaultExportJson": false,
  "defaultExportLog": false
}
```

旧设置缺少字段时自动补默认值。设置页新增“输出与报告”；输出交付页新增当前项目总开关及 CSV / JSON / 操作日志选项。新建项目读取全局默认，当前项目修改不会反写全局。默认关闭时复制不自动写报告；手动导出始终可用。免费版强制安全回落到基础 CSV，JSON 和日志沿用 `AdvancedReports` 专业版权限门控。

## 操作区标题

窗口标题栏继续保留“北尾鸲归片助手”。主操作区删除重复品牌大标题，改为“归片工作区 / 添加照片来源、导入客户选片并完成归片”。帮助、关于、授权和安装信息仍保留品牌名称。

## 修改文件

- `src/RAWSelectionAssistant/MainWindow.xaml`
- `src/RAWSelectionAssistant/ViewModels/MainViewModel.cs`
- `src/RAWSelectionAssistant.Core/Models/AppSettings.cs`
- `src/RAWSelectionAssistant.Core/Models/ReportSettings.cs`
- `src/RAWSelectionAssistant.Core/Models/ProjectModels.cs`
- `src/RAWSelectionAssistant.Core/Services/SettingsService.cs`
- `src/RAWSelectionAssistant.Core/Services/MediaReportService.cs`
- `src/RAWSelectionAssistant/Resources/DesignSystem/Controls.Menu.xaml`
- `src/RAWSelectionAssistant/Resources/DesignSystem/Controls.Navigation.xaml`
- `src/RAWSelectionAssistant/Resources/DesignSystem/Icons.Navigation.xaml`
- `tests/RAWSelectionAssistant.Tests/UiFix141Tests.cs`
- 版本与安装脚本相关文件

## 测试结果

- Debug 自动测试：230 / 230 通过。
- 新增针对性测试：17 项，覆盖菜单高 DPI 度量、矢量图标、右上角布局、设置迁移、报告格式选择、免费版权限回落和 Provider=None。
- Release 构建：成功，0 警告、0 错误。
- Release 完整测试：230 / 230 通过，0 失败、0 跳过。

## 安装包

最终路径：`artifacts/installer/北尾鸲归片助手_Setup_1.4.1_x64.exe`

- 大小：46.44 MB
- SHA-256：`2AD9DEF4191D8A6507B99D660C2A66D40DCD9231E1EE2347C04261D23B7DD56A`
- 发布 EXE 版本：1.4.1
- 发布授权 Provider：None

## 已知问题

本机 Inno Setup 7 编译器标记为 `Non-commercial use only`；技术构建可用，正式商业分发前需使用具备相应商业许可的安装编译环境。

最终隔离 UI 自动复验时，Windows 交互助手读取鼠标状态被系统拒绝（`GetCursorPos 0x80070005`）。未伪造截图结果；菜单尺寸、矢量图标、布局结构、主题资源和可访问性名称由新增测试覆盖。
