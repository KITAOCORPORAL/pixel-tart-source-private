# 家用照片引擎连续开发交付

日期2026-10-16 Europe/Berlin。基准远端feature/kitao-photo-engine-home-20261016核验为fe73d02cf85df518c4eaebf4a09f9a5157451eab，无新增提交；独立树N:/pixart/kitao-photo-engine-followup-20261016，分支feature/kitao-photo-engine-followup-20261016。保留其他工作树、旧Release和授权原件；未开发视频/Log/BRAW/FFmpeg。

## 总结：有实际修复和产品验证，但不是正式可发行版

|优先级/事项|产品实际实现|研究实际运行|推测/未证|未运行|发行阻断|
|---|---|---|---|---|---|
|P3查询|精确active/空值桶/AddedAt/Id索引，COUNT覆盖、page免临时排序；范围/分页/排序/大小/重启恢复测试通过|冷/预热3次、全SQL/参数/EXPLAIN/settings/连接/COUNT/执行/行读取/cursor/GC计时|历史长尾全部根因尚未证；本轮首个完整失败转到batch100的2818.8ms保留|OS磁盘等待/完整UI逐阶段计时未完全分离|最终自动性能通过，不单凭猜测环境结案|
|Fuji旧链|未改旧算法/DLL/默认线程/recipe/golden|逐阶段：解包/scale前后/pre-interpolate一致；首次去马赛克后不同，14/4线程复现、1线程及Canon对照一致|共享块边界竞态线索，未受控native补丁证明|精确旧Windows DLL源码重建及安全兼容修复|**BLOCKED_UNSTABLE_LEGACY_FUJI**|
|Sony_DSC08015|已有旧产品RGB16成功，PV4真实非默认WB/曝光、真实VM缩略/细节/会话、全尺寸TIFF16/PNG16/JPEG回读均通过|0.22.2三授权文件重复输出一致，native callback取消成功|不外推所有Sony/A7M4/A7S3/压缩变体|桌面切图中取消、所有真实前台验收；DSC09916仍独立未跑|不为此文件偷换候选依赖|
|显示基础|类型合同/独立缓存键/防御复制/取消/失效/状态提示，默认不连接像素|两monitor实际关联+LCMS16bit、Pillow8bit、WIC格式矩阵与独立profile oracle|LCMS/WIC差820码；独立近黑133>16码；屏幕DWM行为未知|真实屏幕、跨屏、生产viewport全模式/导出隔离集成|**不接WPF普通入口**|
|候选发行|未进入普通Release|官方固定EXE/DLL及研究RGB16已运行|本机预编译不是cleanVM或源码对应|精确源码Windows重建、依赖/许可选择/源码提供签核、cleanVM|**NOT_APPROVED**|
|照片科学|类型化阶段/域/白点/WB/适应/显示与导出隔离合同；不改历史像素|3照片×2现有曝光参数私有匿名包/数值计数|场景类别尚未人工标注；不以灰阶数评优|本轮等价ACES/OpenDRT/full darktable、摄影师盲评|不换生产曲线，CalibrationEligible=false|

## P3原始失败与最终实际复测

原历史源码2807.1ms、发布835.8ms>400ms保留；本轮首次独立范围19.661/17.769/17.438ms通过，首次完整Core范围52.317/46.878/60.527ms通过但batch100 **2818.8ms>750ms失败**。不隐藏失败，不把单次通过改写历史。

确认当前路径仍有SQL SUM(FileSize)和scope导航复用分类计数；原Core必需测试只执行repository，不含这两个UI路径，不冒称公司修复是本轮根因。实测原ORDER BY的CASE-null桶使page使用TEMP B-TREE，新增精确表达式复合索引后不再排序全部4999条后LIMIT101。SQL、参数、计划、DB盘符、SQLite settings及各段计时见01_p3/*SQL_TRACE.json。

|完整最终测试|发现|选择/结果|执行|通过|失败|条件未执行|缺失/超时|
|---|---:|---:|---:|---:|---:|---:|---:|
|源码Core|1714|1714|1707|1707|0|7|0/0|
|源码WPF|1628|1628|1605|1605|0|23|0/0|
|实际发布Core|1714|1714|1708|1708|0|6|0/0|
|实际发布WPF|1628|1628|1605|1605|0|23|0/0|
|实际发布身份专项|1714|1|1|1|0|0|0/0|

最终P3 cold/warm：源码3.556ms；**3.138/2.700/2.853ms**；实际发布11.713ms；**12.705/11.129/11.712ms**。取最慢值，不扣GC、不提400ms门槛。完整Core保留原方法并行设置，两套Core在WPF负载结束后顺序运行。专项索引3/3、颜色边界/精度15/15、既有Python门禁13/13通过。

23WPF/6发布Core条件测试原名与状态逐项核对完全不变，见06_regression/CONDITIONAL_TESTS.md/json；源码额外1项为发布身份依赖，实际发布单项通过不篡改源码skip。raw runner仍FAILED因为NotExecuted；严格自动review为REVIEW_BUILD_WITH_OPEN_GATES（0错误/29唯一外部条件，58重复源码/发布记录），不是全任务发行批准。

## Sony独立身份与实测

DSC08015.ARW：67,817,472字节，SHA1FE22BE78C66AB0DDCCF753D10187F142B980146FFF7F5D188533905B7F38B93，ILCE-7M4，TIFF-based ARW，RAW SubIFD未压缩14位7040×4688；旧有效7028×4688、EXIF8、旋转4688×7028。IFD0/嵌入JPEG不当RAW压缩结论。旧产品成功不是预设拒绝；DSC09916原身份和拒绝证据不改。

旧PV4真实1.05/1/.95通道增益和+.5EV；真实会话保存重开再解码master/preview一致，真实后台VM缩略/128px原像素细节、保存重开一致。TIFF16 orientation8/7028×4688和PNG16 upright4688×7028逐码oracle最大0、ICC精确；JPEG95-444 MAE.86726、RMSE1.19254/max13个8bit码，低于既有MAE5/RMSE15限制。实际发布组件重跑同链也通过。原件最后SHA不变。

候选两次Sony未旋转7028×4688一致，Fuji7752×5178与旧7752×5184不同，未裁切对齐，Canon5496×3670。候选cameraWB已烘焙/线性ProPhoto原色D65 convention、RGB16已渲染及量化，不是sensor raw、不是ICC ProPhotoD50，PPM无ICC单列。原生取消81.159ms返回-100010并close，无输出，不冒称前台切图取消。

## 构建身份及封存

仅`BLOCKED_DIAGNOSTIC_BUILD`：

- sourceCommit：52d2f3dbedbddcdd1314773fa331099d5c4a2e10。
- 完整目录：N:/pixart/kitao-photo-engine-followup-20261016/artifacts/followup/06-blocked-diagnostic/win-x64。
- Release win-x64、自包含、非单文件，290个文件逐一哈希回读。
- EXE SHA256：C80E76CC17A1CAEF4B1E020250F34F83FB5A27823F358AF3EB3FE55C28D0D317。
- Core SHA256：C804B2431BF24D814456989597549D73F6AF4B6667C73865577AFBDFF8B57BEE。
- 发布/测试host/RID原生/后端audit来源见IDENTITY/RELEASE_FILES/PUBLISHED_PROVENANCE；原raw_r保留，libraw0.22.2不入目录；EXE未启动。
- 原TRX/发现/参数行/计数及测试DLL身份逐一校验，原日志ZIP1894成员/6,248,812字节，SHA5EDDD12D0EC67970EA5C39F658B0951AE287C851EEC825C8D5C780EBDB4F840F。
- ZIP仅精确runner日志/summary/discovery/runsettings/TRX，非runtime递归；无照片/原件/导出图/raw pixel/ICC/SQLite/凭据/构建二进制。文件清单及类型见DELIVERY_FILES.json。私有图像留本机ignored目录，不上传。

阶段提交见STAGE_COMMITS.json：P3观测e745ae63/索引5729e98a；Fuji f3a90517；Sony d8ce0b26+73c61eed；显示4226088c与后续失败oracle1fc8a735/9354f95a；色彩合同/私有盲评52d2f3db；候选取消63573966。源码与Release身份不拿最后文档HEAD混淆。

复现命令见REPRODUCE.md，真实桌面NOT_RUN清单见06_regression/NATIVE_NOT_RUN.md，下一轮具体问题见NEXT_BLOCKERS.md。未要求色卡，不宣称相机绝对色准/宝丽来精确复刻。正常推送/远端SHA在PUSH_STATUS.md与最终回复独立核验；无强推、无合并main。
