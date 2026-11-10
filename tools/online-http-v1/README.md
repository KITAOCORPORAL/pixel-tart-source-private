# 在线选片 HTTP v1 集成

基线37ea6c9a，旧分支349bf4006faf9b80a3691b7d9fd368271ca58ebd只读参考。复用其API DTO/route命名、LocalSelectionObjectStorage、LocalDevJpegVariantService，以及HTTP/DPAPI访问存储模式。未合并旧UI/mini-program/预览shell，也不修改旧分支或279条脏状态。新LocalDev单进程JSON仓储/角色鉴权替代旧服务的单token流程，以支持稳定ID幂等创建、独立owner/client权限、撤销/过期和确认快照。

Core默认Factory保留None且无HTTP/凭据实现；真实HTTP适配器与配置选择位于桌面Services/OnlineSelection基础设施层，原ProductionCoreContainsNoHttpLocalhostOrCredentials断言原样保留。正式App默认Provider=None；只在显式进程配置PIXELTART_SELECTION_ENDPOINT（loopback HTTP或明确HTTPS根）和PIXELTART_SELECTION_OWNER_KEY（至少32字符）时选择HTTP测试适配器。App不自动启动监听器。当前首片规则支持目标/最少/最多、文件名、收藏和确认锁定；不支持收费、加选、PIN、水印、客户备注和原图下载，对这些发布请求明确拒绝，不默默忽略。

完整测试包解压后，运行Start-OnlineSelection-LocalTest.cmd：生成独立本机测试根/owner创建凭据，启动包内自包含LocalDev服务与完整诊断桌面。正常Start-Guangjing-Diagnostic.cmd仍不配置provider。停止服务用Stop-OnlineSelection-LocalTest.cmd，仅停止与本Root记录相符的PID/EXE/启动时间，不删除数据。所有凭据/链接/数据库/图片只留用户测试根，勿提交或打入包。默认不更改防火墙、DNS、系统服务或全局环境变量。

桌面：在线选片→创建并导入照片→目标1并选择一张自有合成JPEG/PNG→上传/重试代理→等服务确认→发布项目→复制选片链接。手机网页/本机浏览器打开该链接→查看/勾选/确认。PC→刷新客户结果→交付结果→同步归片工作区→选明确归档目录。重开App/项目后仍显示确认版本、选择和归片项目关联。同步只导入选择/归档与导航，不自动扫描/复制；RAW匹配不齐时保留需确认状态。

幂等：本地项目Guid在首次请求前落盘，Create Idempotency-Key=project:Guid；上传身份为projectGuid/assetGuid/代理SHA256，服务验证JPEG并返回内容确认，重试不增资源。本地代理Ready且CloudAssetId为空显示未上传，不能当作云确认发布。服务签发的client令牌仅该相册且不能调用owner API，独立owner凭据DPAPI保护；受保护链接含fragment令牌，不把令牌写进HTTP路径日志/项目JSON。确认后锁定快照，刷新校验项目/已知素材/selectionVersion/修订并用本地文件名映射，拒绝旧版/未知ID，不悄悄改原图。

可重放：build.py用既有SDK记录新构建批次；start_service.py显式隔离Root/端口启动真实Kestrel；run_case.py分别在独立完整App TestHost中跑HttpPhase1..4。原生UIA操作WPF入口，文件/文件夹选择通过实际系统对话框与已观察ID的Win32确认消息；不是直接注入导入模型，也不宣称物理鼠标/DPI。浏览器真实勾选/确认用受保护链接，在阶段2与阶段3之间进行。http_negative.py是实际HTTP权限/锁定/幂等/过期验证，不能替代桌面或浏览器入口；guard tests中的fake只用于恶意结果防护，绝不算HTTP闭环。

首版服务只监听loopback，production=false，单摄影师测试环境，不是公网多租户服务。物理iPhone Safari/微信实测须有明确授权HTTPS入口与设备；当前桌面浏览器390px仅响应式证据。PC/手机档期同步不在本轮范围。

2026-11-10 本机闭环已封存，当前报告见 `docs/validation/online-selection-http-v1-20261110/FINAL_REPORT.md`。最终产品c44f800a；94条所选TRX PASS（90相关回归+4独立完整App阶段），最终批次44条服务请求；物理手机仍待授权HTTPS/设备。

补充核验工具（仓库根运行，只用于隔离合成数据）：

```text
python tools/online-http-v1/verify_revoke.py <PRIVATE_CONNECTION.json> <app-evidence> <workspace.json> <new-receipt.json>
python tools/online-http-v1/seal_delivery.py <delivery-root> <new-docs-dir>
```

`verify_revoke.py`在桌面HttpPhase4后只读检查撤销请求及重开版本；不输出凭证。`seal_delivery.py`核对冻结产品、发布绑定/所选TRX/实际请求和包排除项，生成逐文件清单、ZIP及公开证据。两者拒绝覆盖既有结果；封存工具用于本轮固定布局的独立交付根，已存在ZIP时须保留原件并选新根，不自动重跑业务或RAW。
