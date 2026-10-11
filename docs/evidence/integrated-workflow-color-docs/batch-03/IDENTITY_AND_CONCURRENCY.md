# 素材身份与标注并发

## 真实调用链

- AssetLibraryViewModel.RelocateAssetAsync → AssetRelocationService → AssetItems 条件更新 + AssetRelocations 审计。
- 跨库明确选择 .ptlibrary 和已有素材 → CrossLibraryRelocation.LinkAsync → CrossLibraryRelocations。Resolver 查询映射并核对目标库 ID/hash，最多解析一层，避免循环。
- 策划参考菜单、画布菜单、联机页和 Studio 使用同一服务；源项目关系和编辑参数不改写。原片只读。
- PhotographerAnnotationStore.ReadAsync 返回 Revision；CompareExchangeAsync 在 SQLite 立即事务中比较全部摄影师字段和 Revision，然后写入。
- 触发器对旧 Rating/Comment/Color 写入增加 Revision，避免 ABA。
- LinkedTetherAnnotationService 先保存产品侧草稿与 ExpectedLibraryRevision，素材侧 CAS 失败时不覆盖权威数据。客户备注仍留在产品库。

## 自动证据

BatchThreeIdentityTests：两个独立连接竞争、旧快照拒绝、ABA、同名错误内容拒绝、同 hash 移动保留身份、跨库映射和目标变更检测。

IdentityBridgeFoundationTests.AnnotationProductCommitSurvivesLibraryFailureAndRestartRejectsStaleRevision：注入素材侧 IOException，产品侧客户备注和草稿保留；重启服务后重试成功；旧版本再次提交拒绝。

## 限制

独立连接测试并非两个独立操作系统进程实测。旧批量颜色 UI 的所有入口尚未统一为用户读取时版本提交，仍为 PARTIAL。新增表/触发器采用加法 CREATE IF NOT EXISTS，没有替换旧主键；未对生产库迁移或断电恢复做实测。跨库源库需可访问以读取映射；不是全局素材定位索引。文件离线与路径缺失的 UX 细分仍需真人核对。
