# 在线选片整合合同 v1

基线8e447075；行为来源c36e1f38（产品feef4bc2）。保留8e桌面基础设施、Core边界、Bearer鉴权和受保护媒体读取。未合并另一分支的Core HTTP/provider、媒体cookie会话、数据库和预览壳。

| 边界 | 必须匹配的字段 |
|---|---|
| 客户相册 GET | project.id / publicId / selectionVersion / revision，rule，progress，isLocked；新增nullable confirmation |
| 客户确认 POST 回执 | projectId / selectionVersion / revision / isLocked=true / confirmedAtUtc / items |
| confirmation.items | selectionProjectId 为本项目；imageId 为已加载assets.selectionAssetId；数量一致、无重复或未知ID；selected为bool |
| PC final-selection | 同一FinalSelectionSnapshotResponse；imageId映射本地Asset.Id，不用远端文件名做匹配；校验项目/版本/修订/锁定/时间后保存 |
| 归片关联 | SelectionProject.WorkflowProjectId 与PhotoProjectRecord.SourceOnlineSelectionProjectId双向核对；重开和再次同步复用同一归片项目 |

`selectionAssetId`只用于相册素材、上传和选择突变；最终确认条目沿用此分支`imageId`。不能直接拷贝另一网页对字段/头/媒体会话的假设。

确认POST返回有效完整回执即显示成功，不依赖额外刷新。响应丢失则客户权限GET相册，从服务器持久化confirmation恢复，只有通过相同校验才显示成功。错误项目HTTP200不能作为成功。重读时依据服务器已保存的固定confirmedRevision，不能用另一项目或仅HTTP状态替代。

HTTP摄影师界面的已选/收藏只读；None provider的本地编辑保持。HTTP和DPAPI凭据均留桌面Services/OnlineSelection，Core原ProductionCoreContainsNoHttpLocalhostOrCredentials断言原样保留。

本地fault_proxy.py只用于测试：实际转发至本次冻结服务；能在服务器提交后丢弃确认响应、注入错误项目HTTP200和阻断后续刷新。它不进入应用包，不是产品重试逻辑。

手机验收必须使用整合版服务及JS和同一整合桌面包。自动HTTP、Node DOM adapter与桌面浏览器证据均不算Safari真机；待用户反馈后才收回手机相册、独立进程重开并提交最终人机闭环回执。另一家庭相册若可访问，独立记录版本，不混用。
