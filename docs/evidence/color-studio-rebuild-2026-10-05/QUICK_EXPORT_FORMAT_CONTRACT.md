# 快速导出格式选择

## 产品入口与状态

顶部 `StudioExportFormatSelector` 绑定 `ReferenceColorWorkspaceViewModel.QuickExportFormat`，稳定枚举值 `Source/Jpeg/Png/Tiff` 与显示文本分离。默认 Source 沿用 JPEG/PNG/TIFF；其他 raster 以 JPEG 兼容输出；RAW 默认处理为 TIFF16，不伪称输出原 RAW。

顶部和胶片栏都使用既有 `ExportSelectedCommand` / `ExportAllCommand`→同一 `ExportAsync`。发布配方保留独立职责。目录选择标题显示数量和实际格式；状态显示目录/扩展名；工具提示说明位深、sRGB 和透明度。

调用开始冻结格式，目录选择后按既有协议冻结每张目标的 Look/Film/AdjustmentStack/Engine/ExecutionMode。正在导出期间改变格式 selector 不影响该次输出；失败重试使用上次冻结格式。逐张失败、取消、源文件只读及无覆盖 File.Move 语义保留。

## 编码契约

| 选择 | 位深 | Alpha | 颜色与来源 |
|---|---|---|---|
| 源格式 | JPEG8；PNG8/16；TIFF16；RAW→TIFF16 | 由下面格式决定 | 明确输出 sRGB |
| JPEG | 8 位 RGB，质量95 | 透明像素在编码前与白背景合成 | 输出 sRGB ICC；RAW使用完整处理结果，不回解传感器数据 |
| PNG | 输入处理结果≤32bpp使用8位RGBA；>32bpp使用16位RGBA | 保留 straight alpha | 输出 sRGB ICC |
| TIFF | 16位RGBA（非RAW）；RAW管线16位RGB | 非RAW保留alpha；RAW无alpha | 均嵌sRGB ICC；8位输入转16位容器不声称增加源精度 |

RAW选择PNG/JPEG时，仍先调用既有冻结RAW高位深处理链和原子TIFF writer写受控中间文件，再读回同一处理结果后编码最终格式。中间TIFF与.tmp在成功、失败、取消均清理；不修改原RAW。此方案为正确性保留一次额外磁盘往返，未宣称最快。

JPEG/TIFF 载入在 ICC 转换后读取 EXIF Orientation（274），按八种方向物理归正像素，再分离 decoder 与源 metadata。这样预览与导出共享的 raster 已是显示方向；后续不得再按源方向旋转。JPEG/TIFF 快速编码明确写 Orientation=1，PNG 输出物理归正的像素且不携带旧方向。方向变换保留位深、alpha 与颜色转换结果；不以改变宽高代替真实镜像/旋转。RAW 中间 TIFF 也由同一 loader 处理，不叠加第二次方向变换。

## 验证

新 `StudioQuickExportFormatTests`：16位编码/解码逐值比较、PNG/TIFF alpha与ICC、JPEG透明白底、取消不产生文件、RAW仅一次decode并跨格式导出、临时文件清理、语言和格式选择器使用真实深色ComboBox模板。补充真实 WIC JPEG/TIFF 八方向样本，以未转向的解码 raster 和独立坐标映射核对每个通道；TIFF16 精度及 alpha、JPEG/TIFF 输出方向为1、再次载入不重复旋转。

测试结果由统一阶段TRX记录；合成RAW解码器场景不是实际相机兼容性证据。实际格式选择、目录、结果文件及像素对比需同一最终Release运行证据。

`artifacts/color-studio-rebuild-2026-10-05/tests/studio-format-locale-layout-final.trx` 中本类三项均 Passed；同一 TRX 还有独立 JPEG 像素误差测试 Failed，不将整个组合标记为通过。后续 `studio-final-changes-focused.trx` 中本类三项继续 Passed；该组合仍有工具布局测试失败，不能当作整个阶段通过。

最新 `studio-latest-final-focus.trx` 中本类四项均 Passed，包含八方向真实 WIC 编解码测试。它验证数据合同，不替代用户 Release 中的方向、格式选择及结果体验；后续最终构建与完整测试以统一阶段证据为准。原始 TRX 仅保留在本机 artifacts（可能带测试机路径），不作为脱敏仓库文档内容粘贴。

RELEASE_RUNTIME：NOT_RUN（由最终证据更新）；USER_VISUAL_REVIEW：NOT_APPROVED。

## r1 发布 DLL 独立核查入口

最终 r1 的 `SourceHead=31d3ac3ae56d6ccd1d9b4c608bf522dbb735d591`；manifest 287项长度/hash只读核对全部一致。`artifacts/color-studio-rebuild-2026-10-05/verify-runtime-export/` 新增独立核查器，仅引用并加载该发布目录DLL，不构建或改写产品/测试。运行参数为 release-root、导出前 session、target ID或source、实际输出actual、JSON report；使用方法见同目录README。

核查器读取保存的冻结Look/Stack/Film；普通图重放发布版完整处理入口，RAW重放真实LibRaw归正及完整栈。PNG/TIFF按8/16位连alpha精确比较；JPEG独立WIC质量95编码完整处理结果后逐像素比较。另核对容器、位深、方向、ICC、输入前后hash，并记录真实加载的应用/Core DLL hash。它不能证明session保存时间与UI导出时间相同，须由运行会话提供来源证据。

工具自检使用旧开发候选的 `module-runtime-evidence/studio-batch.ptstudio.json`，明确不是最终r1 UI工作文件。授权JPEG 6224×4672→PNG8/JPEG8、RAW 7028×4688→TIFF16，各自产物与oracle最大差0；ICC/位深/源hash检查通过。故意改变session副本曝光后，同JPEG文件核查正确失败（最大差62，退出1）。报告位于核查器 `smoke/`，均固定 `releaseRuntime=NOT_EVALUATED`、`userVisualReview=NOT_APPROVED`。最终三份self-smoke明确 `smokeCreatedActual=true`，不能当成真实UI导出证据。核查器DLL SHA256为 `5DFFCF37A3F80D0793840AC5C28476A54889EEDDBAF18186F59CE498FAD2702B`。

现有5RAW/5JPEG/空间尺度TRX为发布前程序化数值证据（JSON记录 `5427406…+WORKTREE`）；本段r1 DLL自检不替换其历史身份，也不升级任何Release实机状态。后续实际UI导出可用同工具重新核对，禁止带 `--smoke-create-actual`。
