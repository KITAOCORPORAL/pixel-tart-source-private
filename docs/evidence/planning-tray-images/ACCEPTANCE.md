# 验收矩阵与状态边界

- TrayCopySurvivesClearOfflineReopenAndDocxWithoutJsonImagePayload：托盘读入、同图双放置、独立ID、基本撤销重做、JSON无pict、清空托盘/删除源文件、重开、进阶文档、实际ImageParts、DOCX导回来源保持。
- HashFailuresCreateNoCopyAndLockedSaveRetainsRecovery：错误SHA、四种解析拒绝、主文件锁、旧字节不变、重试与重开。
- CellPlacementImageUndoRedoKeepsSidecarAndPosition：表格当前单元格、撤销重做、位置重开。
- RealResolverProjectCollectionPickerCancelAndImageEvidence：真实AssetLibraryContainer/Repository/IdentityResolver、当前托盘和本项目收藏、排除其他项目、选择器取消不写文件、尺寸/图注编辑、源文件改变拒绝；静态UI和DOCX证据。
- SourceSwitchDuringResolveAndCopyWriteFailureDoNotMutateDocument：异步解析中换项目不插入、目录写失败不修改文档。

这些是合成数据自动测试，不是Windows/WPS人工操作。已有颜色数学、素材库解析合同仍由完整回归覆盖；本轮不改Color Studio算法。

## 不夸大的边界
1. 仅明确打开的素材库可实际解析；其他库Offline。没有跨库自动搜索与自动重关联。
2. 本地工程移动路径重写、图片GC、崩溃后未引用完整副本清理未实现。需保留JSON与副本目录；DOCX可单独离线交付。
3. 不承诺WPS重编码后保持库身份：图片仍作为文档内图片，不能误关联原素材。
4. 真实Windows输入、物理DPI、WPS编辑导回：NOT_RUN；OpenXML验证绝不代替WPS。
5. 既有复杂表格/分节/Word工具边界和r5视觉差异继续OPEN；本轮不加假入口。
