# Pixel Tart Visible Feature Audit — RC2

审计基线：`feature/pixel-tart-product-redesign` / ProductRedesign RC1 代码基线（2026-08-12）。

本审计只记录产品当前公开界面中的操作入口，不把“能打开页面”误判为“生产可用”。每个入口必须属于 `ProductionReady`、`PreviewDisabled` 或 `Hidden`。`Installed Verified` 仅在隔离安装版实际完成点击并观察到结果时标记为“是”；自动化或代码证据不会替代前台实机验收。

| Surface | Control | State | Real Action | Error Handling | Installed Verified |
| ------- | ------- | ----- | ----------- | -------------- | ------------------ |
| 侧栏 | 工作台 | ProductionReady | 导航到工作台并刷新项目/任务/排期摘要 | 加载失败保留空状态并显示提示 | 是（入口） |
| 侧栏 | 归片工作区 | ProductionReady | 打开四步归片流程：索引、导入、匹配、输出 | 阶段错误显示可读提示，复制/导出遵循安全校验 | 是（入口） |
| 侧栏 | 工作日历 | ProductionReady | 打开月/周/日视图与排期详情 | 空数据、冲突和数据库错误显示空状态/NeedsAttention | 是（入口） |
| 侧栏 | 在线选片 | ProductionReady（本地桌面工作区） | 打开项目、代理预览和结果同步工作区 | Provider 为 `None` 时显示本地能力边界，不伪装云端成功 | 是（入口） |
| 侧栏 | 联机拍摄 | ProductionReady（Watch Folder MVP） | 选择看守目录、显式启动/停止、导入稳定文件 | 非递归边界、文件稳定性和代理失败显示安全状态 | 是（入口） |
| 侧栏 | 摄影收支 | ProductionReady | 新增/编辑收支并按项目汇总 | 金额校验和持久化失败显示可读错误 | 是（入口） |
| 侧栏 | 项目历史 | ProductionReady | 浏览并继续本地项目 | 缺失项目显示安全恢复提示 | 是（入口） |
| 侧栏 | 工具箱 | ProductionReady（容器） | 打开工具目录和固定管理 | 工具可用性由统一 `FeatureAvailability` 决定 | 是 |
| 工作台 | 四个快捷工具 | ProductionReady | 整理图片、RAW 转 JPG、批量压缩、拼图均导航到真实工作流 | 各工作流使用统一任务、验证、撤销和通知链 | 是 |
| 工作台 | 工具箱快捷入口 | ProductionReady | 打开工具箱 Popup/完整页 | 不占用核心快捷位 | 是 |
| 工作日历 | 月/周/日切换 | ProductionReady | 切换真实日历视图并保留选中日期 | 视图切换不修改 Booking | 自动化通过 |
| 工作日历 | 新建排期 | ProductionReady | 打开快速创建并通过 BookingWorkflowService 持久化 | 冲突、关闭档期和未保存草稿有明确提示 | 是（入口） |
| 工作日历 | 日期右键菜单 | ProductionReady（待前台复核） | 新建排期、查看当天详情、关闭/开放档期 | 命令失败显示提示并保持原状态 | 否（输入限制） |
| 工作日历 | 关闭档期 | ProductionReady（待前台复核） | 持久化 DayAvailability，不改变业务颜色 | 关闭日阻止新建并显示原因 | 否（输入限制） |
| 工作日历 | 日期状态 Badge | ProductionReady | 由统一 CalendarDayVisualStateResolver 推导颜色/锁/今天/选中 | 缺失或冲突数据回退安全状态 | 自动化通过 |
| 工作台 | 迷你日历 | ProductionReady | 与完整日历共享状态解析器 | 刷新失败保留上次安全快照 | 是 |
| Task Center | 活动任务卡 | ProductionReady | 从唯一 TaskRecord 显示实时状态和进度 | 失败显示原因、NeedsAttention 或可重试动作 | 自动化通过 |
| Task Center | 失败任务“查看详情/重试” | ProductionReady | 查看可读失败阶段并按能力重试 | 技术诊断仅复制，不直接展示堆栈 | 自动化通过 |
| RAW 转 JPG | 转换 Modal | ProductionReady | 真实 RAW 解码、CreateNew/AutoNumber/Flush、校验输出 | 失败/取消/恢复状态与 TaskCenter/History/Notification 一致 | 是（入口） |
| 批量压缩 | 压缩 Modal | ProductionReady | 选择文件、参数、输出、校验和 Undo | 冲突、部分完成、取消和失败进入明确状态 | 是（入口） |
| 整理图片 | 预览操作清单/执行 | ProductionReady | 生成并执行安全 FileOperationPlan | 冲突和源文件变化阻止执行并可重试 | 是（入口） |
| 拼图 | 画布预览/导出 | ProductionReady | 编辑模板并导出新文件，不覆盖源图 | 渲染或写入失败显示错误，不删除源图 | 是（入口） |
| 批量水印 | 工具箱卡片/页面 | PreviewDisabled | 仅展示水印布局、透明度和位置预览 | 页面明确显示“预览功能”；不可执行动作 Disabled | 是（页面可见） |
| 批量水印 | 添加照片 | PreviewDisabled | 当前版本未开放真实导入链 | Disabled；Tooltip：`此功能仍在开发中` | 否 |
| 批量水印 | 批量导出 | PreviewDisabled | 当前版本未开放批量导出 | Disabled；Tooltip：`当前版本仅支持水印布局预览，批量导出尚未开放。` | 否 |
| 批量重命名/转档 | 普通用户入口 | Hidden | 保留代码合同，不在 ReleaseCatalog 或普通快捷区显示 | 不产生空白页或假成功 | 不适用 |
| 删废片/FTP | 普通用户入口 | Hidden | 保留代码合同，不在 ReleaseCatalog 或普通快捷区显示 | 不产生删除/网络副作用 | 不适用 |
| 顶部菜单 | 文件/项目/编辑/视图/工具/帮助 | ProductionReady 或明确 Disabled | 已绑定导航、任务、设置、反馈和退出命令 | 不可用的撤销/重做/更新项 Disabled 并带原因 Tooltip | 自动化通过 |
| ContextMenu/Drawer/Modal | 统一交互容器 | ProductionReady | 通过现有命令或关闭/取消合同完成闭环 | Esc、取消、错误和持久化结果明确 | 部分（右键受输入限制） |

## Zero Dead Control 结论

- 本轮发现并修复水印页两个无实际动作的可见按钮：均改为 Disabled，并提供用户可读 Tooltip/AutomationProperties。
- 批量水印保留在工具箱的预览区域，不进入四个默认快捷工具，也不能 Pin；`FeatureAvailability.Preview` 是唯一可用性来源。
- 已审计的生产入口均绑定真实 Command 或现有点击处理器；失败状态必须回到统一任务/业务状态服务，不以临时 MessageBox 或静态文本冒充成功。
- 本文的“安装版”列只代表已完成的隔离安装点击证据；尚未完成的前台输入操作明确标记为“否”，不作推断。

## 后续前台复核

需要用户在可见桌面完成：日历右键菜单、关闭档期及重启保持；在线选片项目打开、四页签和结果同步。复核时仅使用脱敏测试素材和独立数据目录。
