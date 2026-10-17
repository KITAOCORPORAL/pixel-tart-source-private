# 正式模块与最小产品链

候选实现迁至src/RAWSelectionAssistant.Core/Services/Projects/InstantV3；原namespace保留用于兼容候选配方，tools/InstantFilmCandidate仅引用Core，不重复编译数学实现。显式V3 recipe嵌入既有InstantFilmDefinition.AlgorithmVersion3；V1/V2拒绝带V3 payload，旧PV4/5/6输入合同不改。未知算法/组件稳定拒绝。

现有即时成像面板新增V2/V3选择，V3曝光/趾肩/密度/柔化/扩散/乳剂/过期/seed等参数进入冻结recipe。真实Generate命令、历史版本撤销重做、会话保存/重开、原图对比/局部/预览已连接Core。新增真实工作台测试通过：显式选择、非默认参数、撤销重做、会话重开、TIFF16/PNG16/JPEG直接导出。同一正式ProfessionalRawExport入口直接消费V3条带，不先写研究TIFF。

资源输出通过TransferToBuffer移交给可Dispose的高位buffer，预览不进无预算长期缓存；行条带、源retain与导出实际租约持续持有；研究API也调用同一实现。并发session退休保留活跃任务源。普通历史输出路线仍走旧函数。

扩展CandidateCreative/Paper/Defects均组件Version1，缺省null保持候选旧配方解释；编号中性Womb01351.Stock0–5，公式是编码域计算复现，不等于旧App输出；42灰阶点误差≤0.000051，Stock2不在公式步骤裁切。基础16项候选通过，不宣称所有扩展真实照片/物理DPI已验收。
