# 2026-10-04 晚间：R10 参数与处理节点

用户未批准产品验收。本表描述实现与自动验证，不能代替同一 Release 的实机重放。

| 父组 / 子参数 | 原有能力 | 本轮真实处理节点与数学 | 保存 / 同步 / 输出 |
|---|---|---|---|
| 明度：曝光 EV、亮度 | Preset 的曝光仅作用于 OKLab L；不是独立摄影曝光控件 | 新 Develop：sRGB 解码到线性 RGB，以线性 Y 比例施加 2^EV；亮度为 ±1 EV 的独立偏移 | 同一个 AdjustmentStack 数值字典，Scheme v2 序列化、撤销、SyncSelectedByTypeFrom；Preview/Export 共用 ColorStudioDevelop |
| 明度：阴影、中调、高光 | 以前只有参考匹配强度和保护参数，不能当作此能力 | Develop：阴影与高光 smoothstep 权重，中调 4Y(1−Y)，独立 ±1 EV | 同上；不改变 ReferenceMatch 参数含义 |
| 对比与端点：对比、白场、黑场 | Preset 有全局 OKLab 对比；没有独立黑白端点 | Develop：围绕线性 Y=0.18 对比，白场/黑场分别加权平滑端点偏移，限制到显示范围 | 同上；按父组复位仅删除本组键 |
| 结构、细节 | 缺少实际节点 | Develop：线性 Y 局部反差；结构窗口为短边 1%，细节半径 1 像素；可正负调整；分离运行和 O(pixels) | 同上；代理重采样/细节半径允许分辨率差异，不声称代理与全分辨率逐像素相等 |
| 局部颜色 | ColorRange 已有吸管、正负样本、范围/柔和度、色相、明度、饱和度、色度 | 复用 ColorRange 的 OKLab a/b 距离选区；本轮补齐浮点路径的真实选区权重和相同 gamut mapping | 不新增另一套永久状态；范围外像素保持原值；不是画笔或几何蒙版 |
| 仿色 / 预设 / 胶片 / 输出 | ReferenceMatch、Preset、Film、Publishing 已有 | 保留实际能力及职责。Film 复用同一浮点实现，8 位仅为输入/输出适配 | RAW/TIFF16 不再把局部颜色强行作用到全图；经调整 RAW 输出 TIFF16，不是原始 RAW |

## 处理边界

- Develop 全零精确 identity；禁用节点跳过；实际参数范围在 UI 和数学中约束。
- 输入 ICC 由 StudioQuickExport.Load 统一转换到 sRGB；未带 profile 按 sRGB 解释。输出嵌入 sRGB；不是任意用户可选工作空间/intent 系统。
- JPEG 输出 8 位有损；PNG、TIFF 保存实际处理位深与 alpha。PNG/TIFF 编码由 WIC 负责。
- 影调区间是量化线性亮度 0–255 的 11 等分；不是摄影曝光 Zone System。
- 球形只是归一化 OKLab 参考网格：x=a/0.4、y=2(L−0.5)、z=b/0.4；样本、颜色距离和像素坐标不投到球壳。
- 本轮未实现新的任意区域画笔蒙版、GPU、Film Lab、Variant 或 Installer。

## 有意义的自动验证

`EveningFeedbackCoreTests`：每一 Develop 参数改变真实像素、全零 identity、8 位/浮点数学一致、存储重新加载、选择性同步、线性 Y 区间端点与成员、ColorRange 选区外不变、Film 两条适配路径一致。

`EveningFeedbackWpfTests`：真实 TIFF16 文件解码→同一处理栈 Preview/Export 像素比较、组复位与快照恢复、PNG alpha/TIFF16 精度、影调预览不写编辑栈、切图/Esc 清除。测试不是用户视觉批准。
