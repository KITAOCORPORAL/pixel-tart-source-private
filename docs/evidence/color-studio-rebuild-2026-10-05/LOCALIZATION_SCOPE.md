# 中文优先与语言切换范围

## 已实现机制

- `StudioLocalizationService`：默认 zh-CN；zh-TW/en-US；稳定 key→界面文本；中文 fallback；未知 key 原样返回。
- `Resources/Studio/*.json`：作为主程序集 embedded resource，简体中文、繁体中文、英文同一 key 集合（包含快速导出格式）。
- `StudioTextExtension`：XAML 动态 Binding，例如 `Content="{views:StudioText Reference}"`；无需改变按钮 Tag、ViewMode、处理参数键或用户文件名。
- `StudioLanguageSelector`：轻量界面入口；保存到 `AppDataPaths.Root/studio-language.json`；尊重隔离 runtime 根；写入采用临时文件再替换。
- 不修改线程 CultureInfo，也不让 UI 语言改变数值解析、颜色数学、项目内容或自定义节点名。
- 本次 3D 显示设置、球面标签、双直方图读数接动态资源。Color Studio 标题、四模式、七页签、双直方图标题、顶部与底部主要动作共约 60 处 XAML 引用同一资源 key。

## 真实边界

追加：新 `StudioToolPanel` 的工具名、父组名、参数名、复位和数值操作提示，以及 `StudioCurveEditor` 操作按钮/提示/坐标读数，均改为同资源服务动态绑定，补齐英文与繁体中文。切语言不重建参数控件、不改参数 key 或用户节点名；新增 `ParameterAndCurveLabelsSwitchWithoutMutatingStackKeysOrUserNodeName` 行为回归。最终结果待本阶段最后构建验证。

旧工程没有完整统一的全软件语言机制；本次建立可扩展的 Studio 语言机制。旧工具参数说明、既有菜单/弹窗、库/联机/项目等其他模块仍存在中文硬编码。不得以本次选择器声称整个 Pixel Tart 已完整翻译。CultureInfo 不变意味着某些旧格式化信息仍按系统地区显示。

语言切换不能更改永久字段：`Reference/Color/Film` 模式 ID、`ColorStudioNodeType`、参数 JSON key、项目名称、文件名、标签、路径、EXIF。

## 验证

`StudioLocalizationTests` 三项测试覆盖三种语言资源、持久化/重载、拒绝非法 locale、稳定 Tag 与 Binding 文本变更，及真实 ReferenceColorWorkspaceView 三语言切换后 Levels 页仍正常选择。最终测试结果以本阶段统一 TRX 为准；本说明不作为 RELEASE_RUNTIME 通过证据。

后续补充：当前工具参数与曲线标签也使用稳定 key 绑定；节点自定义名仍保持用户原文。`studio-format-locale-layout-final.trx` 中四项语言行为测试 Passed（含参数与曲线切换），新语言/格式派生 ComboBox 的深色模板行为测试 Passed。实际可读性仍需最终 Release 截图确认。

CODE：IMPLEMENTED（上述局部范围）；完整全软件翻译：PARTIAL；RELEASE_RUNTIME：NOT_RUN；USER_VISUAL_REVIEW：NOT_APPROVED。
