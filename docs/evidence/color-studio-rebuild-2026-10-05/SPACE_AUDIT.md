# 3D 色彩空间与像素映射：源码审计

- 审计日期：2026-10-05。
- 读取源码：`5427406c4ef3c805ef56fbb814251df69365032d`。
- 范围：只读源码、已有测试和已知测试样本目录；本文件不是本次修复或 Release 实测证据。
- CODE：现有点云、相机与硬阈值映射为 PARTIAL；连续球面和本次要求的显示控制尚未实现。
- TEST：本次未运行；已有测试覆盖范围如下，不升级为本次通过。
- RELEASE_RUNTIME：NOT_RUN；USER_VISUAL_REVIEW：NOT_APPROVED。

## 1. 实际调用链与缺口

| 能力 | UI / ViewModel / 算法 | 状态与风险 | 拟修改范围 |
|---|---|---|---|
| 当前照片分析 | `ReferenceColorWorkspaceViewModel.Analysis.cs:RefreshPreviewAnalysisAsync`，120 ms 合并、取消令牌、revision 校验，四帧缓存 | 使用当前原片/结果的实际 BitmapSource；缓存按图对象和取样等级。暂无透明像素规则。检查本次高频拖动时最终收敛，不能仅凭 revision 代码宣称通过 | Analysis partial、像素分析输入契约、行为测试 |
| 分析采样 | 主 VM `ToVisualBuffer`，最长边 768、转 RGB24；`ColorSpaceProxyBuilder.Build` 网格抽样 1024/4096/16384 点 | 图片坐标是分析代理坐标，不是全分辨率坐标。RGB24 丢弃 alpha；透明像素隐含 RGB 会污染直方图/点云/区域。网格抽样可能漏稀有颜色。界面须标注代理统计 | 统一含 alpha 的分析快照；不另造处理栈 |
| 真实点云 | `ColorStudioColorSpace.cs:ColorSpacePoint/ColorSpaceCloud`，RGB→`OklabColorSpace.FromSrgb`，保留 SourceX/Y | 坐标与源像素关系真实；实际取样来自显示用 sRGB 8-bit 代理，并非 RAW 传感器或高位深工作缓冲原值 | 保留模型，增显式分析代次/显示变换 |
| 相机与投影 | `ColorSpaceRendererContract.cs:ColorSpaceProjection`；`ColorSpace3DViewport` Orbit/Pan/Zoom/Reset/Fit | 正交投影；x=a/.4、y=2(L−.5)、z=b/.4；有 bounds-aware Fit。相机只有 yaw/pitch，缺独立 Z 旋转。`ColorSpaceCoordinate.FromLab` 钳制显示坐标，超范围值会重叠 | 同一相机管线增加 roll；显示变换与逆变换共享；避免渲染/拾取分别计算 |
| 球形表现 | `ColorSpace3DViewport.SphereGuide/DrawSphere` | 只有半径 √3 的经纬线外壳和散点；没有连续着色表面。此球用于包住归一化立方体，顶/底的 OKLab L 超过 0..1，直接上色会产生大块白/黑，不适合简单填色 | 新 Core 球形显示数学 + CPU 动态表面；实测平滑和旋转 |
| 绘制性能 | `ColorSpace3DViewport.OnRender` 每次重建经纬线、点投影、每点 Brush/Pen，点不按深度排序 | 多云最高约 49152 点；拖动可能受分配/排序压力。Analysis 当前用相同 buffer 建 source/ref/matched 和零变换，但 UI 仅 source 模式，做了多余计算 | 单快照检查模式直接复用一个 cloud；冻结缓存画刷/几何；受界面尺寸约束的表面 raster；profiling |
| 图片→点云 | View codebehind `TrySample`→`HighlightDisplayedImageSampleAsync`→`HighlightImageSample`→`FindNearest` | 取色对象与比较视口几何一致。但区域中心替换为“最近抽样点”，不一定是点击的精确颜色；稀有色可高亮错误区域，甚至不包括点击像素 | 精确选色作为 selection center，nearest 仅标识可见点；加入稀有色测试 |
| 点云→图片 | `OnMouseUp`→`ColorSpaceSelection(PointIndex)`→`HighlightCloudSelection`→`ToPixelMembership` | 遍历 RGB24 全代理，OKLab 欧氏距离硬阈值，默认 .06；没有羽化。多云扁平化后 PointIndex 无 cloud 身份，跨云会错误映射；当前 source-only 掩盖此问题 | 携带颜色中心、cloud/代次身份；加连续权重；源/结果切换清除 |
| 球面→图片 | 没有表面、逆投影或表面拾取 | NOT_IMPLEMENTED；不能通过 nearest point 冒称球面区域选择 | 同显示变换的逆相机 ray/sphere 交点，映射为精确 OKLab，再对当前快照求颜色距离 |
| 临时遮罩 | `ImageHighlightOverlay`，`HighlightedPixels`，`ColorStudioSampleMapping.Viewport` + 同一 ZoomPanState | 只绘制 UI 图层，未入 AdjustmentStack；比较视图裁切路径已存在。当前二值 alpha=150，缺软权重、显示开关；清除已由 Esc/切图/Tone mouseleave 接入 | 保留预览专用层；新增 byte 权重 mask 或受控结构；不改导出输入 |
| 影调映射 | `HighlightToneZone`→`VisualAnalysisEngine.ToneZoneMembers` | 线性 Y 量化等分 11 区，非摄影曝光 Zone System；统计/成员复用同一分区公式。仍需 alpha 与分析对象一致 | 同一个分析快照、统计对象文字、透明像素规则 |
| 表面/背景/网格/轴/色域轮廓 | 仅点尺寸、点 opacity、容差、密度和 Pan 已绑定 | 本次要求的 surface opacity、背景、显示开关、chroma/L 切片均无完整 UI 链。已有 `ColorSlice` 只是基础类型，未接渲染 | 同一 transient VisualizationSettings，两侧控制共用；全部仅 AffectsRender/analysis，不入照片栈 |

## 2. 建议的可测球形显示契约

### 推荐：OKLab 色域归一化球形显示

保留每个样本的真实 OKLab D65 值及颜色距离。仅显示坐标采用显式可逆的非线性变换：

1. `h = atan2(b,a)`，`C = hypot(a,b)`，`y = 2L−1`。
2. 用已有 `OklabColorSpace.ToLinear` 求固定 L/h 上的 sRGB 边界 `Cmax(L,h)`；采用确定性的二分求界或经验证的查找表。
3. `r = C/Cmax * sqrt(max(0,1−y²))`，`x = r*cos(h)`，`z = r*sin(h)`。
4. 球壳上 `C=Cmax`，球内为照片真实颜色分布。逆变换使用同一个 Cmax：从球坐标还原 L/h/C，再得到 a/b。近黑/近白与中性轴须显式处理除零、未知 hue。
5. 模式标签应为“OKLab · sRGB 色域归一化显示”；轴标记“L”“−a/+a 方向”“−b/+b 方向”。a/b 数值读数显示真实采样值，不能把归一化显示刻度当成原始 a/b。
6. 增加“原始 OKLab 点云”模式用于直接坐标检查。原始模式与球形模式共享样本/选区/相机意图，不复制永久像素或调整状态。
7. 显示空间目前只能提供真实支持的 sRGB。不能把 Rec.709 与 sRGB 的传递函数当成完全相同、或暴露没有输入/输出变换的任意 ICC 下拉项。

这样球面连续颜色来自真实数学和相机状态，具有明确逆映射，不用静态渐变图、不把照片点强推到球壳。它与参考图中“方向容易识别”的视觉目标接近，但不能宣称是 C1 内部算法。

可选低变更方案是保留仿射 OKLab 球形参考壳，表面像素转 `ToSrgbGamutMapped`；必须标注色域外压缩。半径 √3 的上下半球产生大范围 L 越界，颜色连续性/可读性预计较差，需实测后决定，不应直接沿用作最终设计。

### CPU 实现与交互

- Core 提供正/逆显示变换、相机正/逆旋转、sphere hit、gamut boundary、显示切片谓词、柔和选区权重；WPF 单一表面负责绘制。
- 连续表面可以使用 CPU 射线-球体求交动态生成有深度的 BGRA 帧（尺寸上限约 384–512 px），或足够密的三角网格。它随相机/参数重新生成，并在同一坐标系拾取；不是静态贴图。禁止每个片面只填一种粗糙颜色。
- 缓存与调度按 `(camera, settings, size, analysis generation)` 标识；拖动允许较小帧，停止后生成清晰帧；发布前校验 generation，取消旧任务。设置 visual redraw 与重新分析照片分离。
- 绘制顺序/遮挡应明确：半透明球面用于观察内部样本；点应按深度排序，选中标识最后绘制。表面 opacity 和 point opacity 分开。
- 饱和度控件若使用 `C/Cmax`，名称应为“归一化色度”，不能与 HSL S 混用；或明确提供 HSL display conversion。L 切片中心和半厚度需 UI 定义一致。
- Fit 应包括当前可见真实样本与球壳，而非永远依据 Source cloud；空云仍可显示/操作参考球。旋转、显示开关和背景操作不得调用照片渲染链。
- 单击真点优先；没有点命中时球面交点生成颜色中心。照片吸管使用精确采样颜色。区域 mask 基于当前分析快照的真实 OKLab 距离，而非样本点索引。
- 羽化建议 `weight = 1−smoothstep(innerRadius, outerRadius, dOKLab)`，明确范围/柔和度单位。透明像素 alpha=0 权重必须 0；部分透明像素权重乘 alpha，避免隐藏 RGB 产生假分布。
- 缓存代理的 OKLab 数组一次，选区滑动只算距离，不重复 RGB→OKLab。最大 768² 代理遍历可取消；像素身份和 mask 尺寸按同一 snapshot 固定。

## 3. 已有测试与本次必要增量

已读（本次未执行）：

- Core `ColorSpaceDataModelTests`：确定抽样、图片/点云成员、V4 migration、0% migration。
- Core `ColorSpaceProxyTests`：投影、hit、相机边界、坐标、cache、取消、大图点数上限。
- Core `ColorSpaceBoundsFitTests`：bounds-aware Fit/投影行为。
- Core `EveningFeedbackCoreTests`：线性 Y 分区边界/完整划分和取消。
- WPF `ColorSpace3DViewportTests`：完整 mask、resize Fit/Reset、真实模型接口；非本次连续球面效果测试。
- WPF `ColorStudioSampleMappingTests`、`ColorStudioZoomPanTests`：原片/结果/比较/letterbox 和视口几何。
- WPF `RuntimeCorrectionWpfTests`：红/蓝图切换、分析 cache、inspection 不改栈、Esc、现有点设置；其中布局测试断言旧中央 histogram，需由布局工作更新。
- WPF `EveningFeedbackWpfTests`：影调成员与 clear；`BatchExportProcessedPixelsTests.ColorInspectionOnlyChangesPreviewAndClearsOnTargetSwitch` 保护输出参数。

必要新测试（不能用 XAML 字符串存在替代）：

1. 球形正逆变换：RGB primaries、中性、near black/white、越界有限值；误差和 hue 未定义处处理；网格边界的连续性。
2. 旋转前后 ray/sphere pick 与投影点还原，roll/pan/zoom/Fit；显示 filter 与 pick 用同一可见性谓词。
3. 稀有取色不落在抽样网格上，mask 仍包含点击像素；半透明/全透明像素规则。
4. 软边权重端点、连续/单调、零/NaN/Inf 容差；C/L slice 边界。
5. 同名不同图、rapid target/processed toggle、分析任务乱序；revision stale selection 不得发布；清除后 mask 不返回。
6. 调相机/背景/切片/选区前后，同 target 的导出 frozen input 与像素校验不变。
7. WPF 按相同 Release 实测连续球面/controls 双向绑定、点/表面/图片双向高亮、窗口变化与性能。截图只证实那一步，不推断所有路径 PASS。

## 4. 已知样本位置与现实边界

只检查仓库明示的样本路径及 manifests，没有搜索私人照片目录。

| 来源 | 当前核查 | 用途与限制 |
|---|---|---|
| `tests/fixtures-local/raw/manifest.json`、`RAW_FIXTURE_MANIFEST.json` | `FIXTURE_PENDING`，`fixtures=[]`；该目录只有 manifest/README | 当前目录没有 RAW 二进制，不能据此宣称 real RAW 通过 |
| `PIXEL_TART_RAW_CORPUS_ROOT` | 当前进程未设置 | 不自动推断外部根路径 |
| `docs/color-studio/raw-compatibility/RAW_CORPUS_RESULTS.json` | 历史第三方本地兼容性 corpus manifest，根仅写 `Real`，无绝对路径；350 RAW/7 raster 等旧记录 | 证明历史存在，不证明当前机器存在或当前 Release 通过；禁止把其旧 PASS 作为本次证据 |
| `docs/color-studio/real-camera-company/COMPANY_RAW_FIXTURE_MANIFEST.json` | 5 个外部 company-owned RAW 的文件名/hash/metadata；绝对路径按设计省略 | 文件名/hash可用作人工提供样本后的核验；未发现实际文件位置 |
| `tools/MatchV4ParityForensics/boundary-fixtures.json` | 历史四 RAW 边界数值；runner 要 `--root` 或环境变量 | 数据不是原始 RAW，不能冒充实机输入 |
| `artifacts/evening-feedback/samples/` | `tone-landscape.png`、`tone-portrait.png`、`tone-square.png`、`tone-jpeg.jpg`、`tone-tiff16.tif` 可用，README 明确 synthetic | 可复用影调/映射/透明度与编码技术验证，不是肤色/风景/RAW 摄影质量 corpus |

已核验可用合成文件 SHA256：

- `tone-jpeg.jpg`：`BBF97C3D6E0A1EE3538401C9F39C559F0CDE7B65CBC7590BA0FDF73EBFBCA41E`
- `tone-landscape.png`：`FCFC12B997B7C013050A9D870F84DCE1B0F46391D72C2FA6532E05285F419410`
- `tone-tiff16.tif`：`2E6A20FC8DCB7A0D4C1F2FEEA61DDE53CAC935A60957D649D06FC16AC9438F70`

旧 `artifacts/round3-final/preview-export-parity` 明确 INVALIDATED，旧 fixture 曾错误列出未实测的 TIFF16/高精度行。此包不可作为本次 parity 基线通过证据。

## 5. 相关历史提交（仅定位）

- `6a52e83`：detached decoder pixels / 零尺寸 3D 防护。
- `e148caf`：2026-10-04 evening feedback 的球形网格、影调等改动。
- `4315bc9`：inspection 时保留主图可见。
- `674bed8`：短面板和源格式保留。

以上为源码历史，不是当前任务的实机验收结果。
