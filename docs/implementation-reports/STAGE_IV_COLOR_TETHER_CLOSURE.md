# Stage IV — 快速仿色与联机拍摄产品闭环

状态：**PARTIAL**

本阶段在 `integration/pixel-tart-developer-preview` 上完成代码与自动化验收闭环。由于当前自动化会话无法可靠捕获“当前源码构建”的真实 App 画面，且没有物理 DPI、多屏和相机设备，本报告不把视觉或真实硬件验收标记为完成。

## 已完成

- 用户界面中文化：落地参考仿色、项目色彩方案、仿色强度、保持原片影调等产品术语；加入用户可见字符串扫描和窄 allowlist。
- 设计系统：补齐产品级圆角别名，统一输入控件、菜单与 Tooltip 的深色主题契约；新增控件补全中文无障碍名称。
- 参考来源：项目色彩方案、灵感板、自由画布、素材库、最近使用和本地参考图入口；外部参考只保留原路径并分析代理图。
- 多参考：权重滑块与数值编辑、删除、排序和自动归一化。
- 项目关联：启动联机会话时支持项目或无项目；解析优先级为当前拍摄、项目默认、会话、无。
- ColorCore v2：D65 OKLab、单调有界分位影调映射、全局与平滑暗/中/亮分区、低样本置信度、感知色域压缩、严格 0% identity。
- 参数：影调、色彩、对比度、饱和度、肤色、高光、中性色保护与保持原片影调。
- 差异提醒：只基于亮度和色彩统计给出客观、无评分提示。
- LUT：33³ 预览 LUT、三线性插值、65³ `.cube` 独立导出和明确的 display-referred sRGB 契约。
- 联机拍摄：取消/修订号 latest-wins；新照片优先显示，后台失败回退原片。
- 曝光评估：Zone 0–X 使用预计算代理帧悬停，不在悬停时重新分析。
- 拍摄参考：Quick Preview、Esc 关闭、固定参考栏与基础第二屏参考窗口。
- 用户偏好：右栏宽度和各折叠组状态使用独立用户偏好文件持久化。
- 下一张拍摄：项目名、日期、计数器、自定义前缀预览，目标目录、剩余空间和有限调整规则。
- Source Safety：参考方案与拍摄段落只存引用；`.cube` 独立写入；自动化 SHA-256 测试证明被引用源文件不变。

## 自动化验收

- Core 全量：1357 passed，0 failed，0 skipped。
- Stage IV Core 定向：14 passed，0 failed，0 skipped。
- Stage IV / 相关门禁定向 WPF：25 passed，0 failed，0 skipped（含语言、视觉契约、无障碍和工作流）。
- 全量 WPF：1245 passed，0 failed，1 skipped；跳过项为既有 `ThreeSamplesOfPublicBatchCommandsAgainstFresh10128Fixture`。由于附件要求 0 skipped，正式门禁不能标为完整 PASS。结果记录在 `artifacts/stage-iv-color-tether-ui/stage-iv-wpf-final.trx`。
- Release 全解决方案构建：0 warnings，0 errors，使用 `Release` / win-x64 应用目标和 `-warnaserror`。
- 证据目录：`artifacts/stage-iv-color-tether-ui/`。

## 真实 UI 状态

- 尝试启动当前 Release 可执行文件时，应用单实例机制把请求重定向到已经运行的已安装版本。
- Windows UI 自动化能读取已安装版中文素材库的可访问性树，但截图接口返回平台不支持错误，且该窗口不能证明是当前源码构建。
- 因此未生成或伪造 Stage IV PNG，真实 App Screenshot 为 **NO**。
- 已输出 `artifacts/stage-iv-color-tether-ui/MANUAL_UI_ACCEPTANCE.md`，覆盖 12 组 DPI/分辨率组合及附件要求的全部截图与交互项目。

## 未完成 / 限制

- 真实 App 逐屏视觉验收与附件列出的 PNG：NOT TESTED。
- 100% / 125% / 150% / 200% 物理 DPI：NOT TESTED。
- 物理多屏与混合 DPI：NOT TESTED。
- 物理相机和 45MP RAW：NOT TESTED；本阶段仍只提供文件夹监看，不声称厂商 SDK 能力。
- 基础第二屏参考窗口可拖到其他显示器，但未提供专用显示器选择器。
- 下一张拍摄已提供规则与持久化基础版；不在相机写入期间重命名，也不声称拥有完整调色栈复制。
- 当前统计仿色没有语义分割、人物识别、ONNX 或 GPU 后端，前台没有 AI 声称。
- Model Display、厂商 SDK、远程快门、远程自动对焦、完整 ACES、XMP、COSTYLE 均未实现，符合本阶段禁止范围。

## 结论

代码、颜色数学、语言门禁、Source Safety 和基础产品闭环已达到本阶段实现目标；真实视觉和物理设备证据尚缺，因此总体结论必须保持 **PARTIAL**，不得据此推进 RC13 或合并 `main`。
