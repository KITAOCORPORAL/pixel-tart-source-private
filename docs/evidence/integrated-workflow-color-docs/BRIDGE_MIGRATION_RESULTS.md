# 身份桥迁移结果

## 迁移内容

`IdentityBridgeSchemaMigration` 将产品数据库从 v6 加法升级到 v7。迁移只创建桥接表和索引，不改变旧表主键，不搬移源片，也不重写已有项目关系。

## 自动验证

`IdentityBridgeFoundationTests` 使用合成文件和临时数据库验证：

1. Resolver 的 Available、Offline、Changed、Ambiguous、Missing 五种状态；
2. 新导入和已存在素材均返回确定 AssetId；
3. 相同散列多个候选不会误连；
4. v6 数据库升级前生成备份；
5. 迁移失败可回滚，旧数据仍可读取；
6. 联机素材映射写入幂等；
7. 同一 TetherAsset 指向不同 Asset 时显式冲突；
8. 跨模块操作键可安全重试和完成。

聚焦测试：102 通过，0 失败。

## 尚未验证

- 未对生产数据库执行迁移；
- 未进行断电、磁盘满和多进程并发故障注入；
- 未接入真实相机 Watch Folder；
- 未运行 Release 桌面迁移流程。

因此迁移结果为：CODE=PASS、TEST=PASS、RELEASE_RUNTIME=NOT_RUN、USER_ACCEPTANCE=NOT_APPROVED。
