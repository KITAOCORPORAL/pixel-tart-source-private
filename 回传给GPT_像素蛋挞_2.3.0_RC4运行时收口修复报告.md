# 像素蛋挞 2.3.0 RC4 运行时收口修复报告

## 1. 结论

RC4 的代码修复、专项回归、Debug/Release 全量回归、非快进合并、候选安装包生成和隔离安装/卸载已经完成。完整真实安装版动态录屏、安装后生产进程运行、RC3 到 RC4 的真实安装升级和物理双屏验收尚未完成，因此本报告不宣称 RC4 已通过完整正式封版验收。

## 2. Git 与基线

- 实际开始 HEAD：d4110a9e20f71cb0ef78fe11514b88ae6c1c4c2e
- RC3 产品代码合并 HEAD：fe5de3dfdd9f5f865049287352eb5d89249a29fe
- RC3 正式二进制源提交：b2a5f017930f4c54f26416ac79658ba6d8d7f925
- 修复分支：fix/2.3.0-rc4-runtime-workflow
- 最终功能提交：f50e2c2b4de3522da6cc89f063ca576f0679f39a
- 合并提交：e5311e615881cac11aae353856fe3c140cc9ee00
- 报告生成时分支：release/2.3.0
- 报告生成时 HEAD：e5311e615881cac11aae353856fe3c140cc9ee00
- 合并方式：正常非快进合并
- planning/2.4.0-camera-sdk：仍存在，未修改、未合并

## 3. 版本和运行时约束

- ProductVersion：2.3.0
- FileVersion：2.3.0.0
- SchemaVersion：4
- RC4 是否修改 Schema：否
- OutputType：WinExe
- Provider：None
- Release Fake/Mock Camera：未启用
- localhost：未引入
- 厂商 SDK：未下载、未引入

## 4. 排期保存事务、结果和重试

根因是旧流程按主排期、人员和后续动作分阶段保存，UI 又缺少稳定编辑会话与明确结果，容易形成主记录成功、联系人或工作人员失败的半成功状态；重试还可能再次创建排期。

RC4 引入稳定 EditorSessionId/BookingId 和显式保存结果 DraftSaved、Created、NeedsDocumentAttention、ValidationFailed、DatabaseFailed、FileOperationFailed。SQLite 聚合保存使用同一连接和同一事务提交 ShootBooking、ShootRequirementItems、BookingContacts、BookingStaffMembers，并在事务内统一更新相关提醒时间或禁用状态。数据库写入异常会回滚，命令不会返回虚假成功。

编辑器首次打开即绑定稳定 BookingId。连续点击或失败后重试更新同一 BookingId，联系人和工作人员采用同一事务内的受控替换，不会生成第二条排期或重复人员记录。

草稿状态复用 ShootBookingStatus.Draft。草稿不触发正式提醒，正式创建仍执行必填校验。失败时当前步骤、标题、客户/联系人、工作人员、时间、地点、准备清单、资料选择、金额和备注继续保留；错误只作用于当前保存流程，修正后可以直接重试。

编辑器中的总额、定金和已付金额属于 ShootBooking 聚合字段，随主事务保存。本轮创建器没有额外暂存独立 FinanceTransactions 行，因此不存在需要一并写入的独立交易草稿。

## 5. 文档安全与恢复

- 默认添加方式：仅关联原位置。
- 已选择资料立即进入编辑器暂存列表，保存失败不会清空选择。
- 资料动作在 BookingId 稳定后提交；复制仍复用既有 TaskEngine 和文件安全链路。
- 文件复制失败时返回 NeedsDocumentAttention，排期不会显示为资料完全成功，也不会因重试创建第二条排期。
- 仅关联模式不复制、不移动、不覆盖、不重命名、不删除原文件。
- 移除关联继续只移除数据库关系，不删除源文件或 ManagedCopy。
- 文件失败原因、重试、改为仅关联和重新定位入口由现有文档工作流继续承担。

## 6. 日历、跨日和详情同步

- SelectedDate 改变时先清除旧 SelectedBooking，再从新日期任务中选择有效项。
- SelectedBooking 必须属于 SelectedDate；无任务日期显示紧凑空状态。
- 切换月份、日期和任务时重置详情滚动/选项卡状态，避免旧详情残留。
- 跨日排期使用同一 BookingId，在覆盖日期上显示连续/延续语义，并与重复任务区分。
- 右侧详情减少后台技术操作，恢复概览、资料和天气等用户任务视角。

## 7. 创建排期交互

- 只保留一套四步导航：基础信息；时间、天气与提醒；策划资料；人员与收支。
- 深色窗口不再出现第二套白色步骤条。
- 底部取消、保存草稿、上一步、下一步/创建排期保持稳定位置。
- 联系人和工作人员使用常驻标签和分区布局；不以 Placeholder 替代标签。
- 最后一步按钮为创建排期，编辑已有排期时为保存排期。
- 取消存在未保存内容时显示“是否放弃本次编辑？”确认。
- 冲突结果保留具体冲突集合和返回修改/允许重叠语义，不以普通字段静默覆盖。
- DatePicker 和非活动标题栏继续使用运行时主题资源，避免系统浅色泄漏。

## 8. 天气与提醒

- 天气默认模式为当前所在城市。
- WindowsCurrentLocationService 使用 Windows Geolocator 单次定位，不持续后台定位。
- 定位成功后只传递城市/行政区语义；日志不记录精确经纬度。
- 权限拒绝、系统定位关闭、超时或天气服务失败时安全降级，不阻止排期保存。
- 支持切换手动城市和跟随拍摄地点；手动候选保留行政区信息用于同名城市消歧。
- 无可靠预报时显示预报不可用/地点待确认，不显示“无天气风险”。
- 提醒预设覆盖 10 分钟、30 分钟、1 小时、2 小时、1 天及自定义；排期时间变化时重算，同一规则不重复创建。

## 9. 收支、联机、本地分片和状态栏

- 收支页默认不永久占用右侧编辑表单；仅在新建收入、新建支出或编辑时打开抽屉。
- 空列表显示操作引导，空数据时禁用 CSV 导出；月份和筛选控件使用明确标签。
- 联机拍摄未启动时使用全宽启动页，不提前加载浏览器和检查器三栏；首张照片 Ready 后才进入完整监看布局。
- “开始本地分片”Normal、Hover、KeyboardFocus 和 Pressed 保持同一主填充色，只改变边框、焦点和轻微位移。
- 工具箱固定/未固定状态继续使用明确边框反馈。
- 页面状态栏改为页面状态与全局通知分离，旧扫描/后台错误不会泄漏到收支等无关页面。
- 滚动条、卡片和主题资源继续统一使用运行时资源。

## 10. 修改文件

功能提交共修改 34 个文件，新增 WindowsCurrentLocationService、Version230Rc4RuntimeWorkflowTests 和 Version230Rc4RuntimeWorkflowUiTests；其余修改集中在排期服务/仓储、天气、文档、日历、收支、联机、主窗口和对应视图。

## 11. 测试与构建

- 原基线：1416
- 新增：45
- 最终：1461
- Core：1016/1016
- WPF：358/358
- DPI：87/87
- Debug 全量：连续 3 轮，每轮 1461/1461，0 失败、0 跳过、0 警告、0 错误
- Release 全量：连续 3 轮，每轮 1461/1461，0 失败、0 跳过、0 警告、0 错误
- 最终 Release 复核：1461/1461
- 原 1416 项：全部保留

重复回归：

- 排期原子事务：30/30
- 幂等重试：30/30
- 保存失败输入保留：20/20
- 日期与详情同步：30/30
- 跨日任务：20/20，旧跨日回归另 30/30
- 天气定位：20/20
- 天气模式与手动城市：20/20
- 文档失败恢复：20/20
- 文档默认模式：20/20
- 收支空状态：20/20
- 联机空状态：20/20
- 本地分片状态：30/30
- 状态栏隔离：20/20
- 页面互斥：20/20
- Core 并行：3/3
- Core 非并行：3/3

## 12. UI 与真实交互证据

隔离验收构建路径：

D:\AI AGENT\RAWSelectionAssistant\artifacts\ui-review\2.3.0-rc4\acceptance-bin\KitaoPhotoSelector.Acceptance.exe

隔离数据路径：

D:\AI AGENT\RAWSelectionAssistant\artifacts\ui-review\2.3.0-rc4\isolated-data

已真实点击工作台、收支空状态、联机未启动状态、工作日历无任务日期、创建排期四步、全天切换、资料默认模式、人员与收支步骤、取消及放弃未保存内容。应用正常关闭，没有操作用户真实资料。

截图目录：

D:\AI AGENT\RAWSelectionAssistant\artifacts\ui-review\2.3.0-rc4\evidence

UI 总览：

D:\AI AGENT\RAWSelectionAssistant\artifacts\ui-review\2.3.0-rc4\像素蛋挞_2.3.0_RC4运行时收口UI总览.png

交互日志：

D:\AI AGENT\RAWSelectionAssistant\artifacts\ui-review\2.3.0-rc4\interaction-log-rc4.json

真实安装版录屏目标路径：

D:\AI AGENT\RAWSelectionAssistant\artifacts\ui-review\2.3.0-rc4\像素蛋挞_2.3.0_RC4真实交互验收.mp4

本轮没有安全可用的 MP4 录制器，该文件未生成。没有用静态截图拼接视频，也没有用 RenderTargetBitmap 冒充真实点击。完整真实动态验收待用户完成。

## 13. 安装包、安装和升级

RC4 安装包：

D:\AI AGENT\RAWSelectionAssistant\artifacts\releases\2.3.0\installer\像素蛋挞_Setup_2.3.0_RC4_x64.exe

- 文件大小：49,994,050 字节
- SHA-256：A0DB75F7A85C2AE45C861492E14F6954D9C72E47C436BAE59999CFF531F1CA6C
- 签名：未签名
- 自包含 Runtime：win-x64
- Publish：266 个文件，169,396,266 字节

安装包已安装到仓库 artifacts 下的隔离目录，退出码 0；安装文件存在。随后使用卸载器正常卸载，退出码 0，安装目录已移除。

没有启动安装后的生产进程。原因是生产进程按设计使用真实 LocalAppData，只有名称以 .Acceptance 结尾的验收进程支持隔离数据根；启动生产进程会违反“不得操作用户真实 LocalAppData 数据库”的边界。

RC3 到 RC4 的真实安装升级因此也未执行。自动化迁移、受控 Schema 4 数据库、完整性和幂等回归通过，但正式升级仍标记为待安全人工验收。

RC1、RC2、RC3 安装包和哈希均已保留：

- RC1：7C9AD2689BBCC5960D7B20396D8951D63F012A615447FEAE453BC6CABD588A2C
- RC2：E26050081A9D1AC45D5A4B6B7B43FFE0F835B1898350CCA85DF53B1F33EBFA91
- RC3：B3AD224E75394C0219FFC583A8965DE39BF38176ED1AE61BC185D60C8A225876

## 14. 停止项

- 物理双屏测试：未执行，继续挂起
- 合并 main：否
- 创建 v2.3.0 Tag：否
- 进入 2.4.0：否
- 下载厂商 SDK：否
- 是否建议正式封版：暂不建议；等待真实安装版动态录屏、安装后运行/重启、RC3 到 RC4 安全升级及物理双屏验收

## 15. 已知限制

详见：

D:\AI AGENT\RAWSelectionAssistant\artifacts\releases\2.3.0\known-limitations-rc4.md
