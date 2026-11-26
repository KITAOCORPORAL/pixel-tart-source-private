# 光镜策划协作试点

本目录托管一份隔离的策划副本。负责人可先在专业模式给选中文字、分镜卡和图片区域添加批注；从「项目成员」升级后，项目、讨论与素材的 ID 不变。成员席位统一由 `Rules.MemberSeats` 配置，当前为 3，另有 1 名负责人。待领邀请占一个席位，取消、过期或移除成员释放席位。换电脑使用个人迁移码；签名由服务从身份确定。邀请、迁移码及本地身份密钥不得放入版本库。

浏览器可用公司有线地址加入。Windows 审稿程序使用现有 `PlanningCenterView`，在「原编辑器选区」中使用原 `PlanningRichEditor`、`PlanningShotRow`、`PlanningReferenceTile` 发起讨论；日常审稿和轻量协作编辑在两端共用同一讨论界面。原生审稿视图只读，文字编辑通过协作服务提交。此试点入口仅在 `PlanningReviewPilot=true` 的构建中存在。

**存储。** `PlanningProjectStore` 与 `ProjectShotStore` 保存原生项目副本；新图使用 `ImportProjectCopiesAsync` 并由 `OpenVerifiedRead` 核对副本。客户端不接收外链、绝对路径或存储根。每个协作写入包含项目、成员凭据、操作 ID 和目标版本。单托管进程串行校验并原子替换项目日志；未写入之前不回成功。独立内容块分别提交，同块旧版本的原文和新输入保存在冲突记录中；追加回复无需覆盖整串讨论。队列和文字草稿留本机，断线重试使用原 ID。用于试点的小项目采用整本快照和去重回执，未做大团队或长期大规模压力验收。

**复现。** Windows x64、.NET 10 SDK/ASP.NET Core 10、WebView2 Evergreen、PowerShell 7.5+。以公司已有 SDK 在仓库根依次运行：

```powershell
dotnet publish tools/PlanningReview/PlanningReview.csproj -c Release -o "$delivery/Service"
dotnet publish src/RAWSelectionAssistant/RAWSelectionAssistant.csproj -c Release -p:PlanningReviewPilot=true -o "$delivery/Desktop"
(Split-Path (Get-Command dotnet).Source) | Set-Content "$delivery/runtime.local"
./tools/PlanningReview/Start-Pilot.ps1 -Delivery $delivery -Lan $wiredIPv4 -Desktop
```

若不提供输入，仅在全新试点目录创建《秋冬服装型录》。导入已有策划时，用新实例、空数据根和新端口，指定 `-Project <原项目GUID> -SourceProjectCopy <含 ProjectPlanning 和 ProjectShots 的隔离副本>`。导入只读清单中的原生 JSON 及已拥有的相对路径文件；有待恢复草稿会停止。旧外链不会在原电脑或局域网自动解析。退出试点不会修改日常项目。「项目成员 → 生成单机恢复副本」在托管目录 `exports` 输出新版副本，保留原生文件、扩展批注和历史署名，不复制账户密钥。重新导入恢复副本从私人项目开始，旧凭据不会被复活。旧项目仅有富文本打包正文时保留格式并允许选区批注；试点不并发重写富文本格式、表格或附件。

新建证书仅用于指定主机的有线 IP 和回环地址，不修改系统信任或防火墙。原生入口验证 SHA-256；另一台电脑通过单次邀请加入，首次连接核对邀请页的指纹。负责人启动配置、私人资料、测试浏览器资料与数据库都在交付目录，不进 Git。正式订阅/付费账号体系、跨网连接和正式包发布不在此试点交付内。

验收脚本：`accept_browser.py` 运行两份持久化 Chromium 配置完成摄影过程；`accept_protocol.py` 和 `accept_reconnect.py` 在另一实例验证并发、复用身份、撤权、掉线和丢回执。`Check-NativeReview.ps1` 操作指定试点 WPF 窗口。回执和已知边界见 `docs/validation/planning-collaboration-pilot-20261126/REPORT.md`。
