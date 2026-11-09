# 在线选片真实联网 provider：接口核对与首个交付切片

2026-11-09，Asia/Shanghai。**SOURCE_AUDIT_COMPLETE / REAL_NETWORK_PROVIDER_NOT_IMPLEMENTED / NO_NETWORK_ACCEPTANCE**。本轮只读核对，不替换默认provider、不伪造服务、不上传照片，也不把mock测试当联网验收。

## 当前实际入口与可复用资产

基线为收藏交付42476a1c，已用角标产品源码02702071；在线选片功能源码本轮零变。完整App在 `App.xaml.cs` 304附近显式调用OnlineSelectionProviderFactory.CreateDefault，并注入JsonSelectionWorkspaceStore、SelectionResultSyncService、真实SelectionProxyJpegService和独立Proxies目录。ApplicationCompositionRoot把WpfSelectionProxyRenderer接到该代理服务。不能只看MainViewModel的测试/备用构造默认值，就误说正式App没有代理生成接线。

Factory实际仍返回NoneOnlineSelectionProvider，Kind枚举只有None；IsConfigured=false，所有联网方法返回ProviderNotConfigured。`feature/online-selection-v1@393c1a545bb3d504ac1d3721576dc96f0c220f2f`已只读查看相应路径，默认同样为None；其脏工作树相应provider目录也未发现HTTP实现。未合并、未修改该树279条脏状态；没有因分支名称断言其能力已进入当前产品。

|现有接口/服务|生产调用与状态|首片处理|
|---|---|---|
|CreateProjectAsync / UpdateProjectAsync|接口存在；当前UI创建的是本地草稿，未调用remote create/update|本地草稿保留；首次同步显式创建远端项目并持久化确认，失败不标远端完成|
|UploadAssetAsync(projectId,asset,Stream,progress,token)|Core SelectionUploadQueue实际调用此接口；队列本身没有进入当前生产ViewModel的构造/字段/命令接线|复用队列，补上传命令/状态回写和重开恢复；只上传代理JPEG|
|PublishProjectAsync(projectId,SelectionPublish)|已有“发布项目”按钮和真实ViewModel调用；当前None返回未配置|真实服务成功后才标Published；服务返回可验证发布凭证|
|GetProjectAsync|接口存在，当前打开项目从本地workspace读取|用于远端确认/重连；明确本地Guid与服务器对象的对应关系|
|GetSelectionProgressAsync / GetFinalSelectionAsync|接口存在，当前生产ViewModel没有远端轮询调用；ApplyFinalResult有内部入口但不能当用户联网入口|增加可见“刷新客户结果”入口，校验projectId、assetId、确认状态和修订再持久化|
|UnpublishAsync|接口存在，当前无明确撤销发布命令接线|首片必须有可操作的撤销/过期保护；不能把接口存在说成按钮已实现|
|DeleteCloudAssetAsync|当前删除云副本命令已调用provider；默认仍未配置|校验项目/云素材归属，只删除云副本，不碰本地原件|
|SelectionProxyJpegService + WpfSelectionProxyRenderer|正式App已注入，生成受限尺寸/质量代理并保留源文件；RAW仍用既有解码器|首片先用单张自有JPEG/PNG验收，不把RAW问题研究混入网络交付|
|JsonSelectionWorkspaceStore|已接入正式App保存本地项目/素材/规则/最终结果|沿用本地持久化，增加必要的服务器确认/重试身份；不要另建平行库|
|SelectionResultSyncService|现有结果匹配/冲突与归档能力可复用|通过真实确认结果驱动现有同步入口；冲突不能自动猜原片|

## 现有合同的具体缺口

1. 目前没有已核验的服务BaseUrl、API版本/OpenAPI、鉴权协议或部署可达性。ProviderKind没有真实实现值，Factory没有受配置控制的真实provider选择。接口不自动提供这些能力。
2. `SelectionPublish`只有PublicId/TokenVersion/时间，不能当作完整受保护URL/PIN授权。Publish方法没有传SelectionRule；规则虽本地保存，却没有已证明的服务器传输/验证通路。首片要定义版本化PublishRequest（包含规则）和服务器签发结果/受保护入口，不在客户端拼接假URL或把本地PublicId当令牌。
3. 本地代理成功会把SelectionAsset.Status设为Ready；这不证明云端已上传。首片必须区分“代理就绪”和“服务确认上传”，发布要求CloudAssetId/服务器确认与内容身份有效。
4. 远端项目、上传幂等/内容摘要、错误码/取消/超时/权限、结果修订/确认锁定尚无可执行协议。GetProject只返回项目元数据，不自动解决远端规则/素材恢复；首片至少要保证当前桌面项目重连一致。
5. 生产没有真实上传队列、远端进度/最终结果的可见调用链；不能只写Http provider后就宣称整个用户流程上线。

## 第一个可交付切片：单项目、单张代理、单客户确认闭环

**目标不是配置灯亮，而是一次真实客户选择能回到当前桌面项目。** 保留现有本地草稿和原片，先限定一张明确授权的JPEG/PNG代理，不做收费、批量并发、多客户协作、RAW升级或断点分片平台。

1. 明确服务URL/TLS/鉴权与API v1；在正式App注入配置化真实provider，保留None的未配置分支；凭据不写进项目/日志/分享链接正文。
2. 本地草稿→以本地项目Guid作稳定请求身份创建远端项目；确认结果落入现有workspace。服务失败、重复响应或重开不能产生第二个项目。
3. 既有代理生成→复用SelectionUploadQueue上传这一张JPEG，保留progress/token；服务器确认内容身份和CloudAssetId后才进入可发布状态。原片从不作为上传Stream或服务DTO路径。
4. 显式提交SelectionRule并取得服务器签发的受保护客户入口；复制/打开入口实际可用。未上传/未配置/无权限不能显示发布成功。
5. 客户端打开该真实入口，只选择这一张并确认；服务器锁定该修订/确认状态。
6. 桌面真实“刷新客户结果”入口调用GetFinalSelection/进度，严格校验project/asset/revision，沿用ApplyFinalResult与Json workspace保存；重开后选择仍在。
7. 通过现有归片同步入口把确认结果交回当前项目；项目错配/同名冲突明确失败，不移动或删除原图。撤销/过期后的入口应失效。

## 接下来可直接开工与外部依赖

可直接开工：**API v1 DTO/状态映射及桌面adapter骨架、现有队列和结果入口接线**，以受控测试服务器做契约/失败/重开测试；这些测试只证明客户端合同，不标真实联网已验收。不得先把Factory默认切到一个不存在的服务。

真正可交付联网切片的依赖：后端负责人提供可用的HTTPS测试部署及接口版本/鉴权/项目与上传幂等/规则和结果确认合同，客户入口承接方式，以及一张自有测试素材的公开范围。当前仓库证据不足以猜这些值；下一轮确定服务合同后才运行端到端网络验收。现有本地代理、上传队列、项目持久化和结果同步可复用，无需重新搭建素材身份或导入体系。

本轮在线选片服务请求0、部署0、provider替换0（不把Git/NuGet工具流量计成provider验收）。该核对不影响已用角标与完整诊断包交付。
