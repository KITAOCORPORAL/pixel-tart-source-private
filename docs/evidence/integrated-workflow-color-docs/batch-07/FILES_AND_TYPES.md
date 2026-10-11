# 文件与类型

根：N:/pixart/pixel-tart-integrated-workflow-color-docs。只列本批真实修改，旧LibRawDecoder/PV1/2/3数学未改。

- Core/Services/Export/TiffExport.cs：RGB16 rows接口、保持旧baseline布局、去掉假整图/ushort/byte副本。
- Core/Services/Export/TiffStreamValidator.cs：有界IFD/profile与逐行样本/hash验证；TiffReadBack旧完整API保留为测试oracle。
- Core/Services/Export/AtomicTiffWriter.cs：流式验证/expected pixel digest比对、取消临时、原子move。
- Core/Services/Projects/ProfessionalRawPipeline.cs：历史full与DisplayV1不改；RenderPreview显示线性面积积分/4线程；RenderPixel/FillRgb16Row/RenderRegion与EXIF映射。
- Core/Services/Projects/ProfessionalRawResourcePolicy.cs：512MiBmaster/64MiBcache/4Mi tile/4096wide/并发1策略。
- Core/Services/RawToJpeg/ProfessionalRawDecoder.cs：预算/重native任务gate/可选分阶段观测；原参数与域不改变。
- Core/Services/Projects/ExportRecovery.cs：ExportRecipeSnapshot可选OutputValidation，旧文件可读。
- Core/Services/Publishing/PublishingExportService.cs：继承RAW定义/input/fingerprint、非sRGB/假PNG16拒绝、IPublishingPixelVerifier检查及记录。
- Services/ProfessionalRawExport.cs：实际三格式及private staging/回读/容差；pixel oracle/sRGBprofile/朝向检查。
- Services/ProfessionalWicRowEncoder.cs：系统WIC RGB16PNG/JPEG444 row COM适配器、质量/DPI/profile、资源释放。
- Services/Publishing/WpfPublishingRenderer.cs：PV4original TIFF行转写/JPEG444/PNG8行写、DPI；pixel verifier；resized/watermarked明确metadata-only。
- RAWSelectionAssistant.csproj：允许独立WIC unsafe接口调用，无新增package/codec。
- ViewModels/ReferenceColorWorkspaceViewModel.ProfessionalRaw.cs：800快代理→1600HQ、active master释放、直接row export。
- ViewModels/ReferenceColorWorkspaceViewModel.RawPreview.cs：可见原像素请求、防抖、含版本质量缩放键、cache预算与取消revision。
- ViewModels/ReferenceColorWorkspaceViewModel.cs：detail取消/释放、RAW缩略预算、quickreceipt真实参数、避免full回读bitmap。
- ViewModels/TetherReferenceModeViewModel.cs / .DetailPreview.cs：真原尺寸与detail patch状态。
- Views/ColorStudioImageViewport.cs：原像素Rect覆盖与可见源region；ReferenceColorWorkspaceView.xaml/.xaml.cs：绑定/请求细节/DPI100%。
- tests/BatchSevenRawQualityTests.cs：抗alias/原像素方向/rows黄金/取消/坏TIFF。
- tests/BatchSevenRawProductTests.cs：三codec/DPI/ICC/拒假参数、native request竞态、取消、授权两机真实产品链。
- tests/ProfessionalRawWorkspaceTests.cs：新preview oracle明确切换area，其余PV4保存重开测试沿用。
- tests/Kitao.ProcessProbe/Program.cs：raw-stage/early-signal真实观测、PV4五阶段进程退出恢复。

上列Core为src/RAWSelectionAssistant.Core，其他为src/RAWSelectionAssistant；tests分别在RAWSelectionAssistant.Tests/WpfTests。源码清单可用git show源码SHA验证。测试会覆盖三份历史ui-guardian自动报告，备份本地后恢复起点字节，不把历史证据改变当本批交付；原集成树审计目录不动。
