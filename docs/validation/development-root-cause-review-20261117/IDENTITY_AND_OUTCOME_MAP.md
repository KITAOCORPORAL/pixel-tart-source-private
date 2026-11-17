# 身份、保存与完成结果地图

|当前术语|实际所属域/存储|必须保留的含义|
|---|---|---|
|PhotoProjectRecord.Id / MainViewModel._currentProject|归片项目；SQLite Projects|SourceDirectories是原始输入目录；SelectionInputs可来自客户返回，但交付中间文件目录不自动变成原片来源。|
|SelectionProject.Id / OnlineSelection Project|选片相册；本地workspace+服务项目|与归片ProjectId不是同一个ID，名称不是身份。WorkflowProjectId和SourceOnlineSelectionProjectId表达关联。|
|WorkflowProjectId / SourceOnlineSelectionProjectId|编辑工作文件/相册/SQLite互反关系|保存、独立重开、结果回写贯通；唯一约束和冲突拒绝是保护，不因当前小样本看似多余就删除。|
|LibraryId / AssetId|素材库|普通独立照片可为空；不能用新GUID或路径临时替代一个已有库身份。|
|ActiveEditVersionId / SourceHash|编辑版本及像素来源|冻结版本和内容身份一致才导出；源图、配方和发布版本分别核对。|
|path / LocalSourcePath / ProxyJpegPath|文件角色|路径可为原片、代理、中间、最终输出；相同string类型不表示相同业务角色。rawPaths变量可能实际装交付PNG，不应据变量名推断原片。|
|.ptstudio.json|可移植编辑快照|与SQLite“保存项目”是两个操作；普通保存用.session.lock+内容指纹，公开低层SaveAsync不是用户入口。|
|FinalResult project/version/revision/lock|服务器确认与客户端回写|服务回执和本地素材ID要校验；不能只凭页面导航、Task返回或本地勾选声称确认成功。|
|RuntimeRoot / instance mutex / PID+creation|运行与资源所有者|进程名、品牌、无窗口不能证明同一root；同DLL的两个合法实例域也应隔离。|
|fdf/e818/621/tree hash/EXE hash|交付文档/功能源/另一绑定二进制/代码树/文件字节|树相同不等于带版本号二进制相同；每个结果保留原source，最终包需自己的实际入口回执。|

## 当前操作与拟议协议

当前CreateForWorkflow正常Task返回可能是校验拒绝；当前Unloaded取消可能仍有provider在途；当前Yield后的Close可能仍在nested Closing。不能用这些代理信号作终结证明。

推荐方案中的CloseAttempt、document/revision、Active/Suspended/Released、终止epoch与Task登记是待审契约，不是当前已经实现的枚举。文件锁、SQLite事务、HTTP幂等和任务终结属于不同范围，不合并成一个全域Busy。

## 最短接手路径

1. 根AGENTS：十条原文、像素政策和本轮边界。
2. HANDOFF + CURRENT_STATUS：知道当前源码、已过闭环、待本人步骤和未实施修复。
3. FINDINGS + OPTIONS：按证据ID读对应范围，不加载全量历史/采样。
4. 名称失败看普通UI回执；误杀看RuntimeOwnership夹具；关闭看5d4cb04e独立矩阵。确定条件前不重跑已充分的40MP/TIFF/39项测试。
