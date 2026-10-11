# 跳过测试明细

状态均为 NOT_RUN，以下为本轮测试返回的真实原因。

## core-delivery.trx

- FilmCpuPerformance_WritesMeasuredEvidenceWhenRequested：Assert.Inconclusive 失败。Set PIXEL_TART_PERFORMANCE_EVIDENCE to collect local CPU evidence.
- AuthorizedFullRawSpatialEffectsRecordSameCoordinateScaleError：Assert.Inconclusive 失败。Real spatial evidence is explicitly opt-in.
- CpuGpuParityOnSyntheticScene：Assert.Inconclusive 失败。No validated GPU compute device on this host.
- CpuGpuParityAcrossEightSceneClasses：Assert.Inconclusive 失败。No validated GPU compute device on this host.
- ProductionResolutionBenchmarkWritesThreeRepeatsWhenEnabled：Assert.Inconclusive 失败。Set PIXEL_TART_RUN_V4_BENCHMARK=1 for the bounded production-size run.
- AuthorizedRealRawCorpusRecordsNewToolPixelsAndDerivedTiff16：Assert.Inconclusive 失败。Explicitly authorized real corpus is opt-in; not a synthetic RAW replacement.

## wpf-release.trx

- CompanyRealRawProductAcceptanceGate：Assert.Inconclusive 失败。NOT RUN — real camera fixture directory unavailable; set PIXEL_TART_COMPANY_RAW_ROOT.
- ThreeSamplesOfPublicBatchCommandsAgainstFresh10128Fixture：Assert.Inconclusive 失败。Opt-in diagnostic: provide a new synthetic fixture and new output directory.
- RenderCurrentStudioWithSyntheticPhotoAtDocumentedLayoutSizes：Assert.Inconclusive 失败。Opt-in automatic screenshot evidence; not native UI acceptance.
- ReviewSheets_ComposeHonestProductionEvidenceWhenRequested：Assert.Inconclusive 失败。Set review evidence paths.
- ProductionCompositionAndToolCatalog_RealAppLoadedAndNavigated：Assert.Inconclusive 失败。Opt-in production startup evidence is not enabled in this bounded regression run.
- AuthorizedRealJpegSamplesRecordActualEncodedFullStackPixels：Assert.Inconclusive 失败。Authorized real JPEG corpus requires explicit opt-in.
- ProductionPeers_KeyStatesAndReferenceFamily_ExposeRealContracts：Assert.Inconclusive 失败。Opt-in production peer evidence is not enabled in this bounded regression run.
- PremiumPrototype_ThreeRealPages_RenderReviewSet：Assert.Inconclusive 失败。Opt-in visual evidence is not enabled in this bounded regression run.
- FilmEffects_WriteTruePerEffectComparisonWhenRequested：Assert.Inconclusive 失败。Set PIXEL_TART_FILM_EVIDENCE to collect review evidence.
- BaselineGuard_RequiresExplicitApproval：Assert.Inconclusive 失败。BASELINE_MISSING: capture current UI before requesting approval.
- CurrentReferenceWorkspaceRendersAtFourScalesAndNarrowResolution：Assert.Inconclusive 失败。Set an isolated current-run output directory.
- ProductionApp_AllRoutes_RenderReviewInventory：Assert.Inconclusive 失败。Opt-in whole-app visual evidence is not enabled in this bounded regression run.
- ValidateExistingRunAcceptsLatestCapturedP2RunWithoutChangingIt：Assert.Inconclusive 失败。No captured non-reparse P2 run root is available for the read-only integration probe.
