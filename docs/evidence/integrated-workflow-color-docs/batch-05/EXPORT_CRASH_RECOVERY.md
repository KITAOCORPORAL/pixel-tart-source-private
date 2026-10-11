# 导出进程崩溃恢复

ManifestVersion=3 保留第四批字段，新增 ManagedTaskId/ManagedArtifactPath。编码结果在同一目标目录下的 .kitao-export-tasks/<ExportId>/encoded.bin 管理，owner.json 绑定任务、规范化目标路径、固定产物路径与 SHA256，task.json 记录 Encoded/Completed/Cancelled。pending 和成功清单继续保留原命名。

## 状态流程

编码完成→移入管理目录→持久 owner/task(Encoded)→持久 pending→File.Move 成片（不覆盖）→成功清单→删除 pending→task Completed。

恢复仅从受管理固定路径取产物，核对任务 ID、owner、路径、hash；所有路径组件拒绝重解析点。任意 pending 中的其他路径不会被移动/删除。目标存在时必须 hash 相同；清单冲突拒绝覆盖。旧第四批 pending 没有管理产物时，只允许恢复已存在且 hash 相同成片的清单。

output.recovery.lock 与 task.lock 保证一个恢复执行者；并发真实进程分别返回 Running/Completed，再次恢复 Completed。Quick Export 和实际 Publishing 都通过 FindRecoverableAsync/RecoverAsync 查找 Encoded/task 与 pending，使用同一规则，不仅覆盖发布准备阶段。

## 真正终止进程

Run-BatchFiveChecks.ps1 调用独立 dotnet 子进程在 checkpoint 直接退出 73：

| 阶段 | 重启结果 | 重复恢复 |
|---|---|---|
| 编码中（仅部分字节，无持久任务） | Missing，未生成假成功，原临时文件保留 | 相同 |
| 编码完成且 owner/task 已持久化 | Completed，从受管理临时产物移入 | Completed |
| pending 写入后 | Completed | Completed |
| 成片移入后 | Completed，仅补清单 | Completed |
| 成功清单写入后 | Completed | Completed |

另测：篡改 artifact 指向 foreign.raw 返回 Failed，foreign.raw 保留；取消只删除本任务且 hash 相符 encoded.bin，最终成片从不由取消删除。Cancelled 任务不自动续跑。

## 保留/清理策略

成功任务保留 owner/task/锁文件作为溯源，不保留已移出的 encoded.bin。失败/取消默认保留；显式 CancelAsync(removeManagedArtifact=true) 先验凭据和 hash 才删受管理产物。编码中崩溃尚无可信任务记录，孤立临时文件只报告并保留，不按名称猜测归属或自动删除。没有自动清扫用户目录。

这是进程终止测试，不是断电/文件系统损坏试验。桌面导出点击与视觉状态 NOT_RUN。
