# Color Studio 色彩科学审计

## 当前真实处理路径

源码复核显示，现有路径已具备 LibRaw 解码、浮点调整栈、预览渲染和 TIFF16 产品输出，但主要处理契约仍围绕 sRGB 显示编码结果：

- `LibRawDecoder.cs` 负责 RAW 解码入口；
- `ColorStudioRenderPipeline.cs` 和调整栈执行现有处理；
- `RawMatchTiff16ProductPipeline` 输出 TIFF16；
- `StudioQuickExport` 复用 Studio 导出入口；
- `ColorStudioSessionStore` 保存既有会话和版本。

现有管线不能据此称为相机特征化的场景线性宽色域管线。相机输入矩阵、DCP/ICC 输入特征化、工作空间白点管理、显示变换和显示器 ICC 仍需明确拆分。

## 本批新增基础

新增独立的 `LinearRgbColorScience`：

- 线性 sRGB（D65）；
- 线性 ProPhoto RGB（D50）；
- ACEScg/AP1（D60）；
- Bradford 白点适配；
- 浮点矩阵转换不自动裁切负值和超 1 值。

该基础目前没有接入历史 ProcessingVersion 1/2，因此不会未经用户选择改变旧照片外观。

## ProPhoto 与 ACEScg 技术结论

| 项目 | 线性 ProPhoto RGB | ACEScg |
|---|---|---|
| 白点 | D50 | D60 |
| 原色 | 极宽，含非物理原色 | AP1，面向场景线性制作 |
| 优势 | 摄影/ICC 工作流认知度高 | 交换规范清晰，适合节点式场景线性计算 |
| 风险 | 不适合直接作为显示编码；负值和映射必须定义 | 必须有相机输入变换及最终显示/输出变换 |
| 本批决策 | 仅提供可测转换 | 优先作为后续场景线性原型候选 |

尚未做生产空间最终选择。Little CMS 2、OpenColorIO、ICC/DCP 支持仍为 PROPOSED；引入前需核对许可证、Windows x64 分发和 .NET 互操作成本。

## 关键差距

- 相机黑白电平和线性化阶段契约需从解码结果中显式化；
- 相机到工作空间的特征化尚未统一；
- 白平衡/曝光、曲线、HSL、HDR 需要按数学域拆分；
- 显示器 ICC 与导出目标空间尚未形成统一输出变换；
- 旧 ProcessingVersion 必须继续可重放。

状态：新增颜色数学 CODE=PASS、TEST=PASS；生产管线迁移 CODE=PROPOSED、TEST=NOT_RUN、RELEASE_RUNTIME=NOT_RUN、USER_ACCEPTANCE=NOT_APPROVED。
