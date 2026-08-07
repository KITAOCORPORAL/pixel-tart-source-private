# 像素蛋挞 2.3.0 RC5 真实运行缺陷清零报告

## 结论

RC5 代码、测试、Release 构建和候选安装包已完成，并已通过正常非快进合并回 `release/2.3.0`。完整真实动态验收未完成：当前 Windows 桌面存在多个真实窗口，桌面控制读取/切换会抢焦点，无法安全完成“创建排期→四次状态修改→重启→天气权限降级→归档”的完整操作链。因此本报告不宣称 RC5 完整动态验收通过，也没有伪造 MP4 或把静态代码测试当作动态证据。

## Git 与版本

- 开始 HEAD：`3a3f25f324cd5f0d239cb00bc07d9369da9554fa`（RC4 合并点之后的干净工作树）
- 修复分支：`fix/2.3.0-rc5-runtime-polish`
- 功能提交：`4b08bc25805eb271e44d49188d7bcaa816f3a55b`
- 合并提交：`ce47e81a0d303f2212f9b4b5adba544da4208967`
- 当前分支：`release/2.3.0`
- ProductVersion：`2.3.0`
- FileVersion：`2.3.0.0`
- OutputType：`WinExe`
- 数据 SchemaVersion：`4`，未新增迁移、未改变现有四个迁移版本
- 正式应用授权 Provider：`None`；Release 构建未启用 Mock/Fake Camera
- 当前工作树：报告提交后应保持干净

## 修复内容

### 本地分片启动与交互

`RunOperationAsync` 现在只把已经持久化到 `ProjectHistory` 的项目 ID 传入任务执行，避免首次操作因 SQLite 外键引用未落库项目而失败。主页“开始本地分片”进入快速向导已在最终安装二进制中启动，未出现异常提示；Hover/Focus/Pressed 使用同一主填充，仅保留边框/焦点反馈。

### 时间与时区

新增 `IBookingTimeDisplayService`/`BookingTimeDisplayService`，统一使用 Booking 的 `TimeZoneId` 将 UTC 转为用户可见时间。工作台、完整日历、详情、归档、提醒、通知、人员到达时间、收支关联和天气摘要均不再自行调用 `DateTime.ToLocalTime()`。中国时区显示为“中国标准时间 UTC+8”，不向普通 UI 暴露 `China Standard Time`。

### 日历主状态

`CalendarWorkflowStatus` 固定为：空闲灰色、有拍摄/未拍摄红色、已拍摄绿色、待返图黄色、已返图蓝色；日历同时显示文字。空闲日使用低对比灰色细线，多排期最多显示三个状态段并显示场次数，跨日继续复用同一 BookingId。详情概览增加即时保存的流程状态选择器；回退到未拍摄状态要求确认，保存成功后发出 BookingChanged 刷新工作台、今日、未来 7 天、完整日历、筛选和重启查询。

### 状态栏与中文显示

`CurrentPageStatus`、`BackgroundTaskStatus`、`NotificationStatus` 分离，归片扫描提示不会覆盖摄影收支或工作日历页面。任务中心生命周期状态全部映射为中文（包括“已完成”），不再直接显示 `Completed` 等内部枚举。

### 天气、侧栏、收支和资料

- 新建排期天气默认开启并优先自动请求当前位置；权限失败时提供重试定位、选择城市和打开 Windows 定位设置的降级路径；城市显示包含城市、省/州、国家。
- 一级侧栏入口改用独立矢量语义图标，收起模式保留图标并配置中文 Tooltip 与 AutomationProperties.Name。
- 摄影收支主筛选收口，默认“全部分类”，搜索框为“搜索交易、客户、项目或备注”，项目/排期/币种移入更多筛选。
- 资料卡只保留预览、打开和更多菜单，统一使用“检查资料状态”。
- 创建排期 Stepper 改为深色圆点/连接线，表单最大宽度约 980 DIP 并居中。

## 测试与构建

最终总数：`1493`（Core `1024` + WPF `382` + DPI `87`），原 `1461` 项全部保留并新增 RC5 专项。

- Debug 单轮：1493/1493，通过 1493，失败 0，跳过 0，警告 0，错误 0
- Release 单轮：1493/1493，通过 1493，失败 0，跳过 0，警告 0，错误 0
- 已完成此前回归门禁：Core 并行 3/3、Core 非并行 3/3、Debug 全量 3/3、Release 全量 3/3；每轮 1492 项时全绿。任务中心中文状态修复后再次完成 Debug/Release 全量 1493 项。
- 构建：Debug 0 警告/0 错误；Release 0 警告/0 错误
- Release 无 localhost、无厂商 SDK、无 Fake Camera；Provider 仍为 None

## 安装包与证据

- RC5 安装包：`artifacts\releases\2.3.0\installer\像素蛋挞_Setup_2.3.0_RC5_x64.exe`
- 文件大小：`50,012,219` bytes
- SHA-256：`BEDA28D09EC439764A037A624EB872C83EEB452DF9BF8F1D968FF0E37C301FAD`
- RC4 SHA-256 保持：`A0DB75F7A85C2AE45C861492E14F6954D9C72E47C436BAE59999CFF531F1CA6C`
- RC1、RC2、RC3、RC4 均保留，未覆盖
- 隔离安装目录：`artifacts\ui-review\2.3.0-rc5\runtime-install`
- 隔离数据库目录：`artifacts\ui-review\2.3.0-rc5\runtime-data`；未操作真实 LocalAppData 数据库
- 有效静态实机截图：`01_Sidebar_Expanded.png`、`06_LocalSplit_Opened.png`；重复或无法独立证明交互状态的截图已移除
- UI 总览：`artifacts\ui-review\2.3.0-rc5\像素蛋挞_2.3.0_RC5真实运行修复UI总览.png`
- 真实运行录屏：未生成。按要求未以静态截图伪造 `像素蛋挞_2.3.0_RC5真实运行验收.mp4`。

## 限制与停止项

本轮没有创建 `v2.3.0` Tag，没有合并 `main`，没有进入 2.4.0，没有生成正式无 RC 安装包，也没有宣称物理双屏通过。由于完整动态录屏缺失，RC5 只能作为“代码/测试/候选安装包完成、动态验收待人工继续”的候选版本；在用户完成真实桌面录屏前，不建议正式封版。
