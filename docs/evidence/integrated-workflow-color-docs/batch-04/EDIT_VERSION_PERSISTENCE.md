# 跨会话编辑版本

生产注入：MainViewModel 创建 StudioVersionRepository(AppDataPaths.DataDirectory/StudioVersions)，传入 ReferenceColorWorkspaceViewModel。测试可以显式注入临时目录，不污染用户目录。

键：素材库照片使用 LibraryId + AssetId + SourceHash，外部照片使用 external-sha256。路径不参与键；改名/搬迁不创建新目录。跨库 Resolver 保留原身份时仍找到原目录；不同库 GUID 不自动合并。源内容改变由既有 hash 保护拒绝，不能静默套用旧版本。

每个版本保存独立参数、ProcessingVersion、名称、创建信息与归档标记。UI 支持新建（复制当前）、命名、下拉选择/循环切换、归档和保存目录。默认 ID 由稳定身份派生；旧会话含已有版本时保留原 ID。

仓储保存使用独占文件锁、Revision 比较、临时文件 flush 与原子替换。会话先写恢复快照，再写版本目录，再更新会话 CatalogRevision。目录成功而会话第二次写入失败时，重开可接受同内容重试；不同内容/过期修订拒绝并保留会话，不自动合并参数。当前没有人工合并版本冲突的专用 UI，需另存会话后处理，状态 PARTIAL。

异步载入沿用 activation revision/取消；编辑历史以 EditVersionId 隔离。自动测试覆盖改名后另一工作区找到同一版本、命名归档保存、仓储过期写入拒绝和旧会话回归。未切换正式 ProcessingVersion，历史像素结果不因目录而迁移。
