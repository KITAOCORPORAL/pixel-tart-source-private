# 像素蛋挞 2.2.0 阶段A排期数据层报告

## 1. 阶段结论

阶段A已完成。当前工作位于 `release/2.2.0`，仅实现Schema 2与拍摄排期领域基础，没有进入阶段B，没有加入工作日历页面、侧栏入口或工作台拍摄卡片。

## 2. 正式基线与分支

- 开发基线：`main` 提交 `13e48eb26ce710bbd0845a062953ccc21e03957d`。
- 正式Tag：`v2.1.0`。
- 开发分支：`release/2.2.0`。
- 产品版本：`2.2.0`。
- 文件版本：`2.2.0.0`。
- `main` 未合并。
- 未创建 `v2.2.0` Tag。

## 3. SchemaVersion

- 原SchemaVersion：1。
- 当前SchemaVersion：2。
- Schema 1迁移保持不可变。
- Schema 2使用独立 `CalendarSchemaMigration`。
- 默认迁移目录按顺序执行Schema 1和Schema 2，迁移版本连续且不可跳级。

## 4. Schema 2新增表

Schema 2严格只新增四张表：

1. `ShootBookings`
2. `ShootRequirementItems`
3. `BookingDocuments`
4. `BookingReminders`

未创建 `ProjectRelationships`，未创建模板、健康检查、选片、联系表、交付包或监听相关表。

## 5. ProjectRelationships边界

- `IProjectRelationshipService`保持未来接口。
- 本阶段没有修改其方法。
- 本阶段没有提供任何具体实现。
- `ShootBookings.ProjectId`只允许关联一个现有 `Projects.Id`。
- 删除项目时使用 `ON DELETE SET NULL`，排期本身保留。

## 6. 迁移、备份与回滚

- Schema 1→2前生成SQLite一致性备份。
- 正式Composition Root将Schema迁移备份写入Migration备份目录。
- 每个迁移步骤在独立SQLite事务中执行。
- 提交前执行外键检查和数据库快速完整性检查。
- 强制失败测试确认Schema 2创建的对象全部回滚，SchemaVersion仍为1。
- 高版本数据库继续进入只读恢复模式并拒绝写入。
- 损坏数据库保护测试继续通过，原文件不会被空库覆盖。
- 所有迁移测试使用临时隔离数据库，未运行正式应用，未操作用户正式LocalAppData数据库。

## 7. 排期领域模型与服务

新增：

- `ShootBooking`、`ShootBookingDraft`、`ShootBookingSummary`；
- `ShootRequirementItem`；
- `BookingDocumentRecord`；
- 排期状态、准备项优先级、文档类型和链接模式；
- 当前视图查询、全局分页请求和游标；
- 金额摘要、金额警告和多收状态；
- 排期冲突、冲突处理和保存结果；
- `IShootBookingRepository`、`IShootBookingService`、`IBookingConflictDetector`；
- SQLite排期仓储、排期服务和冲突检测器。

## 8. UTC、时区、跨天和全天

- 所有起止时间以UTC `DateTimeOffset`写入SQLite。
- 保存前验证Windows时区ID。
- 普通跨天排期允许保存。
- 结束时间必须晚于开始时间。
- 全天排期采用当地零点到结束日期零点的右开区间。
- 全天排期至少覆盖一个完整当地日期。
- 歧义当地零点使用较早出现的UTC时刻；无效当地零点返回明确错误。

## 9. 冲突检测与允许重叠

- 重叠规则：已有开始时间早于新结束时间，且已有结束时间晚于新开始时间。
- 首尾相接不算冲突。
- 已取消和已归档排期不阻塞新排期。
- 双方均未允许重叠时返回NeedsAttention式保存结果，不写入数据库。
- 用户可明确“仍然保存”，记录 `ConflictOverride`。
- 用户可标记当前排期允许重叠。
- 任一排期允许重叠时，冲突保留为非阻断信息。

## 10. 归档与恢复

- 已实现 `ArchiveAsync` 和 `RestoreAsync`。
- 未实现 `DeleteAsync`、`PurgeAsync`或其他永久删除接口。
- 子表外键使用 `ON DELETE RESTRICT`，不允许误级联删除排期子数据。
- 归档只隐藏排期、记录归档时间，并在同一事务中禁用关联提醒。
- 归档保留准备清单、文档关联和提醒记录。
- 恢复后准备清单和文档关联保持原状。
- 恢复不会自动重新启用提醒。

## 11. 金额规则

- 总金额、定金和已收金额均使用整数最小货币单位。
- SQLite只检查单个金额字段不得为负。
- 没有 `PaidAmountMinor <= TotalAmountMinor` 跨字段CHECK。
- 已收金额高于总金额允许保存。
- 领域服务返回 `PaidAmountExceedsTotal` 非阻断警告，中文提示为“已收金额高于当前拍摄总金额”。
- 余额保留有符号结果。
- 余额为负时返回 `Overpaid`，展示金额为负余额绝对值，即“多收金额”。
- 服务不会自动修改用户输入的总金额、定金或已收金额。

## 12. 当前视图与全部未归档分页查询

- 当前视图查询使用时间重叠条件，只读取指定范围。
- 支持状态、拍摄类型和关键词过滤。
- 全部未归档搜索强制 `IsArchived=0`。
- 全局搜索使用 `(StartAtUtc, Id)`游标分页。
- 默认每页50条。
- 最大每页100条，超出请求自动限制为100。
- 返回下一页游标，不一次加载全部历史记录。
- 关键词使用参数化SQL，不写入AuditLog或NotificationCenter。

## 13. 文档路径与元数据

- 新增文档引用仓储和服务，不包含文档复制界面或复制任务。
- 只保存路径、规范路径、扩展名、大小、修改时间、可选哈希和缺失状态。
- SQLite表无BLOB字段。
- 支持PDF、Office文档、TXT、JPG/JPEG和PNG引用。
- 文件丢失时只更新 `IsMissing`，不抛出界面级崩溃。
- 支持重新定位到新文件路径。
- 移除关联只删除 `BookingDocuments`关系记录，不执行任何文件删除。
- 归档排期不会移除文档关联或删除电脑文件。

## 14. 提醒仓储

- 已实现 `SqliteReminderRepository`。
- 提醒默认关闭并保存为Disabled。
- 支持绝对时间和相对拍摄开始时间的仓储计算。
- 支持按排期查询、按时间范围查询到期提醒和禁用排期提醒。
- 到期查询排除已归档、已取消和已结束拍摄。
- 本阶段没有启动提醒调度器。
- `DisabledLocalReminderScheduler`继续是唯一实现，`IsEnabled=false`。
- 软件不会因阶段A在退出后常驻。

## 15. 未实现能力

本阶段未实现：

- 工作日历页面；
- 月视图、周视图和日视图；
- 左侧工作日历入口；
- 拍摄详情页面；
- 文档复制界面；
- 提醒调度和错过提醒补报执行器；
- 工作台今日拍摄和未来7天；
- ProjectRelationships；
- 永久删除排期；
- 项目模板和项目状态机；
- 本地选片和精修回匹配；
- 联系表、交付包和文件夹监听。

## 16. 测试结果

Release最终测试：

- Core/业务测试：624/624；
- WPF测试：5/5；
- 逻辑DPI测试：27/27；
- 合计：656/656；
- 失败：0；
- 跳过：0。

原614项测试全部保留并通过；本阶段新增42项专项测试。

专项覆盖：

- Schema 2四表和无ProjectRelationships；
- Schema 1→2备份；
- 迁移失败完整回滚；
- 外键RESTRICT；
- 无永久删除契约；
- 金额超额保存、警告和多收计算；
- UTC、时区、跨天和全天；
- 时间冲突、首尾相接和允许重叠；
- 当前范围查询；
- 50/100条游标分页；
- 归档恢复和子记录保留；
- 文档引用、丢失、重新定位和移除关联不删文件；
- 提醒默认关闭、相对时间和已结束排期排除。

## 17. 构建结果

- Debug完整构建通过。
- Release完整构建通过。
- Release构建：0警告、0错误。
- 未执行Publish。
- 未生成安装包。

## 18. Git与发布状态

- 当前分支：`release/2.2.0`。
- 未合并main。
- 未创建Tag。
- 未生成2.2.0安装包。
- 未自动进入阶段B。

## 19. 是否建议进入阶段B

建议在用户审查并明确确认本阶段报告后进入阶段B。阶段A的Schema、迁移、领域服务、归档恢复、金额规则、文档引用、提醒仓储和分页查询已完成并通过测试，但本报告不自动授权或启动阶段B。
