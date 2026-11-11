# 手机档期首版源码核对

基线：批量采集验收 `057fe82085183df723b4542fbff2e09fc8bc896b`，其中包含产品源码 `1698664d12913aadea64bad6f2a68cb52d9ee38d`。先 fetch，再建立 `feature/mobile-schedule-v1-20261111` 独立工作树。未合并或重置其他家中工作树。

现有日历权威数据为 `ShootBookings`，由 `PixelTartDatabase` 的 SQLite 保存。模型有 `Revision`、UTC 时间、预约时区、状态、金额、联系人、人员及准备清单。`ShootBookingService.SaveAsync` 复用时间与字段校验、`BookingConflictDetector` 冲突提示、`SqliteShootBookingRepository` 聚合事务。PC 入口是 `WorkCalendarViewModel` → `ShootBookingEditorViewModel` → 同一 service/repository。

基线虽已有 Revision 字段，但保存前未校验编辑器读入版本，保存事务也未比较预期 Revision；状态、归档快捷写入未递增 Revision。首版新增 schema 9 的 `BookingMutationReceipts` 与 `ScheduleDevices`，没有复制预约表或建立第二套预约清单。

在线选片中可复用的模式：原生 HTTP 服务、受保护链接 fragment、同源静态手机页、显式 owner 授权、token hash、到期/撤销、no-store/no-referrer/CSP、脱敏 HTTP 请求审计。选片客户令牌只适用于选片项目，不能授权读取全日历，所以新增独立档期设备令牌及只开放时间/状态的修改协议，未扩大原选片令牌权限。授权摘要仍存现有日历数据库。

`Guangjing.Schedule.Server` 通过显式运行根读取与 PC 完全相同的 `Data/pixel-tart.db`，设备认证后调用原 service。手机不创建预约，不编辑客户、金额、联系人等字段。修改请求包括预约 Id、ExpectedRevision、OperationId；版本冲突 409、字段错误 400、时间冲突 409，并可复用仍然保存/允许重叠策略。修改与幂等回执同一数据库事务保存。

PC 编辑器记录打开版本并用稳定表单指纹重试；状态工作流也比较其读入版本。手机刷新和 PC“刷新档期”直接重读唯一数据库；预约详情刷新会重载当前所选预约。

首版部署是家中 PC 上的受保护测试服务，手机需同一 Tailscale 网络，PC 和服务需运行；不代表离线公网云同步。测试仅使用三条合成预约，用户日常数据库未接入。
