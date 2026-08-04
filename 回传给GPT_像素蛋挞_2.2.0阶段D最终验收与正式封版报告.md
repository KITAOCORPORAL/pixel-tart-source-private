# 像素蛋挞 2.2.0 阶段 D：提醒、工作台、天气、最终验收与正式封版报告

## 1. 最终结论

像素蛋挞 2.2.0 阶段 D 已完成并通过正式封版门禁。SQLite 测试隔离问题已先修复并完成规定的重复回归；提醒、工作台整合、阶段 C 跨重启恢复和可选天气功能均已完成；Debug 与 Release 最终为 `867/867`，0 失败、0 跳过、0 警告、0 错误；正式安装包、独立桌面安装版验收和 2.1.0→2.2.0 隔离升级验收均通过。

正式安装包：`D:\AI AGENT\RAWSelectionAssistant\artifacts\releases\2.2.0\installer\像素蛋挞_Setup_2.2.0_x64.exe`

- 大小：`49,822,660` 字节
- SHA-256：`B4638CBCB8467B30EA21F474ED2E15603A1A6621081F5E05DB75C3177B313788`
- ProductVersion：`2.2.0`
- FileVersion：`2.2.0.0`
- 签名状态：未签名
- 正式目录安装包数量：1

阶段 D 完成后立即停止；未开始、未规划、未创建 2.2.1 内容。

## 2. 恢复核验与 Git 基线

- 仓库：`D:\AI AGENT\RAWSelectionAssistant`
- 开发分支：`release/2.2.0`
- 正式二进制源提交：`f72a0caf264022a52705ebc8a03470d2e52417dd`
- 阶段 A：`49246dbdb5717e43f4dda3cc6f2acdb084c26679`
- 阶段 B：`b54bd0d7d850f4811ce196094a78b0b18878ceaa`
- 阶段 C 最终功能检查点：`5973e3f1e8b1f9075bb1755a02a7587244cf8f47`
- SQLite 隔离修复：`9f4c1311c913f5b8090799043ff505abcabbc41f`
- 阶段 D 提醒与工作台：`3719657ae6fd72b9d1f063d7820e6b27c732710d`
- 阶段 D 天气与恢复：`9a24acd63a493735c2143731a517fd06e56c6506`
- 排期详情命令状态修复：`42ed8bf0f429936fcd9913ebb3b85b84be5be8ec`
- 正式安装版验收加固：`83eb41f18253ea0ada68acd05aaeba1293e3ecba`
- 隔离升级验收加固：`f72a0caf264022a52705ebc8a03470d2e52417dd`

上述提交均位于正式二进制源提交的祖先链上。没有使用 `git reset --hard`，没有强制清理工作树，没有改写阶段 A、B、C 历史。

封版流程完成后，`release/2.2.0` 已合并至 `main`，并创建带注释 Tag `v2.2.0`；最终复核工作树干净。

## 3. SQLite 测试竞争根因与修复

失败测试为 `Version220CalendarSchemaTests.ChildForeignKeys_RestrictParentDeletion`，并行全量运行时偶发 `ObjectDisposedException: Cannot access a disposed object 'SQLitePCL.sqlite3'`。

根因是多个独立 SQLite 测试各自使用临时数据库，但部分 Fixture 的 `Dispose` 调用了进程级 `SqliteConnection.ClearAllPools()`。一个测试结束时会清空其他并行测试仍在使用的底层 SQLitePCL 连接池和句柄，从而形成跨测试竞争。该问题只在测试基础设施并行生命周期中复现，产品路径、单线程正式数据库路径和外键约束本身没有缺陷。

修复提交：`9f4c1311c913f5b8090799043ff505abcabbc41f`（`test(sqlite): isolate calendar schema test connections`）。

修改文件：

- `tests/RAWSelectionAssistant.Tests/TestSupport.cs`
- `Version210RecoveryCoordinatorTests.cs`
- `Version210StressTests.cs`
- `Version220BookingDomainTests.cs`
- `Version220CalendarSchemaTests.cs`
- `Version220DocumentReminderTests.cs`
- `Version220DocumentWorkflowTests.cs`

修复方式是根据每个测试自己的 `PixelTartDatabase` 连接字符串调用 `SqliteConnection.ClearPool(connection)`，只清理本测试连接池；每个测试继续使用唯一临时目录和唯一数据库文件。未修改产品代码、外键策略、`ON DELETE RESTRICT`、Schema 迁移或 SchemaVersion，也没有通过关闭全部并行测试掩盖问题。

规定回归矩阵全部通过：

- 失败测试单独连续 20 次：全部通过
- `Version220CalendarSchemaTests` 连续 10 次：全部通过
- Core 当前并行配置连续 3 轮：全部通过
- Core 非并行配置连续 3 轮：全部通过
- Debug 全量连续 3 轮：每轮 759/759
- Release 全量连续 3 轮：每轮 759/759
- 每轮 0 失败、0 跳过；Debug/Release 构建 0 错误

## 4. 提醒与工作台整合

- 新提醒默认关闭，只有用户明确启用后才参与调度。
- 支持相对拍摄开始时间和自定义绝对时间。
- 最近 24 小时内的错过提醒可补报；拍摄已结束、提醒已触发、已关闭或已取消时不补报。
- 同一提醒通过原子数据库领取最多触发一次。
- `Scheduled→Triggered` 与 `Notifications` 写入在同一 SQLite 事务中完成。
- 排期时间变更会重算相对提醒；归档或取消会关闭提醒；恢复归档不会擅自重新启用。
- 调度器仅在软件运行期间工作，退出时停止，不注册后台服务，不在软件退出后继续联网或常驻。
- 工作台提供今日拍摄和未来 7 天，覆盖全天、跨天和正在进行的排期，并显示时间、状态、地点摘要、准备进度、文档数量、提醒状态和天气摘要。

## 5. 阶段 C 跨重启恢复与文件安全

阶段 C 的文档复制恢复继续复用 `TaskEngine`、`FileOperationPlan`、`FileOperationValidator`、`FileOperationExecutor`、`FileVerificationService`、`NeedsAttention`、`PartiallyCompleted`、`UndoJournal`、`NotificationCenter`、`ErrorCodeCatalog` 和 `AuditLog`，没有建立第二套文件复制系统。

独立安装版深度探针验证：文档引用、丢失检测、重新定位、移除关联不删除文件、安全复制不移动/不删除源文件、跨重启恢复可见、恢复重试、归档恢复安全和数据库完整性全部通过。所有文件测试均使用独立临时目录，没有操作用户桌面、真实照片、客户资料或真实 LocalAppData 数据库。

## 6. 天气 Provider 架构

天气功能通过以下抽象隔离，不直接写入 ViewModel：

- `IWeatherProvider`
- `IGeocodingProvider`
- `IWeatherForecastService`
- `IWeatherCacheStore`
- `WeatherLocationCandidate`
- `WeatherLocation`
- `HourlyWeatherForecast`
- `DailyWeatherForecast`
- `BookingWeatherSummary`
- `WeatherRiskNotice`

默认实现为 `OpenMeteoWeatherProvider`。API 基础地址可配置，代码中没有硬编码商业 API 密钥；默认无 API 密钥。自动化测试使用可控 Fake Provider，但正式 Release 明确不启用 Fake Provider。

天气默认关闭。用户只有主动启用、搜索或刷新后才联网；天气失败不会阻止打开日历、创建、编辑、归档或保存排期，不创建后台常驻服务。

## 7. 地点解析、缓存与显示

地点解析由用户主动触发：使用排期 Location 搜索候选，用户确认城市、地区和国家后保存本地地点映射，再以经纬度请求天气。无法确定地点时显示明确提示，不阻止保存排期。

缓存目录：`%LocalAppData%\KitaoPhotoSelector\Cache\Weather\`。

- 本地 JSON 缓存，不新增数据库表
- 新鲜缓存有效期：60 分钟
- 服务暂时失败时允许显示最近缓存
- 显示更新时间；过期时显示“天气数据可能已过期”
- 支持手动刷新和清除缓存
- 缓存损坏时重新请求，不影响排期
- 缓存文件名使用不可逆地点键，不使用完整拍摄地址
- 缓存不包含客户姓名、电话、金额、备注、文档、策划或协议内容

月视图提供紧凑天气图标、代表温度和降雨概率；周/日视图增加风速；排期详情显示温度、体感、降雨、风、湿度、云量、能见度、日出日落、更新时间和来源；工作台今日拍摄和未来 7 天提供摄影天气摘要。界面不显示完整客户地址。

## 8. 摄影天气风险与降级

集中配置并覆盖较高降雨概率、强风、高温、低温、低能见度、雷暴/恶劣天气、数据过期和预报不可用风险。风险只通过现有 `NotificationCenter` 和页面提示提供参考，不自动取消、移动或修改排期。

无网络、DNS 失败、超时、限流、服务不可用、返回格式变化、地点无法解析、无可用预报、缓存损坏和 API 配置缺失均会安全降级。请求具备防抖和相同请求并发去重，不无限重试；软件退出后不继续请求。

天气请求不上传客户姓名、电话、金额、内部备注、策划、协议、文档路径、文件名、准备清单或完整排期对象。AuditLog 只记录 Provider、BookingId、操作类型、成功/失败、通用错误码和 CorrelationId，不记录地点搜索词、经纬度或完整地址明文。

## 9. 天气服务许可与已知限制

Open-Meteo 是可替换的默认 Provider。天气预报的覆盖范围、服务可用性、频率限制、数据许可和准确性受 Open-Meteo 当时的服务条款及数据源限制；本产品不把远期气候平均值伪装成具体预报。超过可靠预报范围时会提示临近拍摄时再查看。

天气是可选联网参考，不是发布阻断项；但天气导致日历崩溃、排期无法保存、隐私数据上传、Release 启用 Fake Provider、无限重试或软件退出后继续联网均属于发布阻断。最终验收未发现这些阻断问题。

## 10. 最终测试与构建

- Core：`768/768`
- WPF：`61/61`
- DPI：`38/38`
- 总计：`867/867`
- Debug：867/867，0 失败，0 跳过，0 警告，0 错误
- Release：867/867，0 失败，0 跳过，0 警告，0 错误
- 原 698 项阶段 B 基线、759 项阶段 C/SQLite 门禁基线全部保留
- 125%、150%、200% 逻辑 DPI 自动化通过
- 浅色、深色、高对比主题与无障碍名称检查通过
- `physicalDpiManuallyTested=false`：本轮没有要求用户截图或切换 Windows 缩放

## 11. 正式安装版验收

正式安装包独立桌面验收 RunId：`daf8ee4f035f44019ad120b2eccd1e66`。

- 隔离方式：Win32 `CreateDesktopW`
- 当前桌面被操作：否
- 安装：通过
- 默认工作台、月/周/日工作日历：通过
- 排期新增、编辑、重启后持久化：通过
- 提醒默认关闭、用户启用、重启后持久化、最多触发一次：通过
- 天气默认关闭、天气探针：通过
- 阶段 C 文档安全与恢复：通过
- 工具箱、设置、整理复制、拼图 JPG/PNG 导出：通过
- 源文件完整性：通过
- Release Fake Provider：未启用
- 授权 Provider：`None`
- 卸载退出码：0
- 安装目录移除：是

证据：`artifacts\diagnostics\2.2.0\formal-isolated-desktop-acceptance\latest-result.json`。

## 12. 2.1.0→2.2.0 隔离升级验收

正式包升级验收 RunId：`f0eda0b34c504cb3bac18e6694d5f6d8`。

- 2.1.0 安装与文件版本核验：通过
- 旧格式项目和设置在独立目录中构造：通过
- 2.2.0 覆盖安装：通过
- 2.2.0 启动和重启：通过
- SchemaVersion：2
- `PRAGMA integrity_check`：`ok`
- 旧项目导入：1
- 快捷工具导入：`Workflow`、`PhotoOrganize`、`BatchCompress`
- 迁移备份：2 份
- 旧 JSON 保留：是
- 源文件不变：是
- 卸载后用户数据保留：是

2.1.0 不支持本轮新增的验收根覆盖，因此为严格避免访问用户真实 LocalAppData，旧版进程未启动；只安装并核验其正式文件版本，再在隔离目录构造符合 2.1.0 的旧格式数据。实际启动、重启、迁移和卸载均由支持隔离根的 2.2.0 正式候选完成。该限制已如实记录，没有伪造旧版启动结果。

证据：`artifacts\diagnostics\2.2.0\formal-isolated-upgrade-acceptance\latest-result.json`。

## 13. 数据库与发布边界

- SchemaVersion：`2`
- Schema 2 日历表：仅 `ShootBookings`、`ShootRequirementItems`、`BookingDocuments`、`BookingReminders`
- `ProjectRelationships`：不存在
- 天气数据库表：未新增
- 数据库迁移：阶段 D 未修改
- 正式 Publish：已执行
- 正式安装包：已生成
- `release-manifest.json`：已生成
- main：已合并
- Tag：已创建 `v2.2.0`
- 2.2.1：未进入

## 14. 是否建议进入下一阶段

阶段 D 已经是本轮批准的最终验收与正式封版阶段，不建议也不允许自动进入 2.2.1。后续工作必须由用户另行明确授权。当前任务在 2.2.0 正式封版、main 合并和 `v2.2.0` Tag 验证后立即停止。
