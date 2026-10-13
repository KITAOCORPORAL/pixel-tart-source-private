# 第十一批：Kitao Studio 光镜底层交付

工作树D:/AI AGENT/pixel-tart-batch11-20261013；分支feature/kitao-studio-batch11-raw-memory-color-20261013。
起点7184966a4921215a1a306019578f23646bcf872a；发布源码67d67a4818f3f76fb298ee2212bcf08bc4eee256。

|需求|实际结果|状态/阻塞|
|---|---|---|
|安全隔离|独立工作树，其他dirty/ahead保全，远端起点一致|完成|
|A7M4文件|确认SHA/容器/原生依赖；OpenFile和OpenBuffer均-2；三入口一致；新增可诊断拒绝|BLOCKED_NATIVE_FORMAT：此文件未解码，不宣称型号全支持|
|依赖|官方现有NuGet通道无更新runtime；原LGPL/CDDL依赖/发布来源保持|未升级，不导入未经审查二进制|
|Fuji7752x5184|64行零halo重建、完整精度/坐标；旧曝光/WB/颗粒配方及三格式真实回读|通过；不是所有节点/相机支持|
|空间依赖节点|Reference/Details/Develop结构/Bloom/Halation保留全图成本，计划列节点及安全拒绝|未流式化，Fuji复杂空间配方仍可能预算拒绝|
|Canon与双VM|真实全链、重开/发布/块排队、关闭取消/淘汰/释放；GC与OS分列|路径通过；真实桌面窗口验收未执行|
|账本所有权|pin租约/退役代际/身份回调，分类预算计入活动旧对象|专项通过；不是OS硬限额或全部历史缓存覆盖|
|色彩域|实际白点/传递/裁剪/位深/方向/ICC合同，旧黄金/灰阶/EXIF/块/码流参照|合成及支持RAW通过；未做新标定/数学迁移|
|性能|真冷路径阶段，纯TIFF编码/验证/hash单列|逐节点非默认独立耗时和全部native分配仍未完全拆分|
|结构化接口|错误码/阶段/native/预算/用户提示；LastProcessingFailure，无布局修改|完成；后续UI文档另接|
|完整回归|Core1657/0/6，WPF1542/0/21；发布Core同值，发布WPF4/0/0|通过，跳过单列|
|真实JPEG|授权测试目录未提供|NOT_RUN|
|桌面/DPI/视觉|禁止自动EXE/Browser/WebView/ComputerUse|NOT_RUN/NOT_APPROVED|

真实进程峰值4580769792bytes；释放后账本0，managed1614116608→测量GC后29463896，workingSet3994861568→3387686912。峰值含同进程前序Fuji/Canon；不能叫独立Canon峰值。GC非产品强制策略，OS页保留不能直接推断泄漏；账本0也不等于内存收敛。

EXE：D:/AI AGENT/pixel-tart-batch11-20261013/artifacts/batch11/release/win-x64/KitaoPhotoSelector.exe。
SHA256：160FEF80708C60F1E7449DC31E8626E7B86A241E168CF87197A97A224CCAA464。
版本2.3.0-batch11+67d67a4818f3f76fb298ee2212bcf08bc4eee256。发布组件hash与隔离测试一致。

只提交本批源码、报告与结构化证据；不含照片/数据库/凭据/构建二进制。未改UI布局，Grok下午文档另行对接。相纸型号/策划中心/浏览器扩展/APP/三端同步不进入本批。HDR高光恢复/全ICC/实测相机与胶片标定继续开放；创作预设不叫准确复刻。
