# 产品操作与资源专项

正式Core模块、非工具启动的ProfessionalRawExport直接条带出口已连接。真实工作台专项覆盖：五类选择、非默认显色、seed、旧创作风格/材质/反射持久化、撤销重做、会话重开、TIFF16/PNG16/JPEG直接出口；运行中显影时切到下一照片，旧任务取消不提交。此处是实际ViewModel/命令测试，非前台鼠标验收。

高位输出引入明确所有权可Dispose预览buffer；原非拥有历史buffer不在缓存清理时强制Dispose。注册的Master按同一Entry pin不重复计费；退休含活动任务继续保留直到最后引用释放。外部API未注册源才独立Reserve，测试明确覆盖重复任务与关闭并发。

候选/正式共用Core20项通过：旧13项保留、6stock42灰阶、扩展开关/极值/BW中性、未知组件、线性多曝基础、registered source/no double charge、buffer transfer、真运行中取消、并发session关闭等。多曝仅正式Core算子及合同，没有第二照片UI/项目路径，状态仍未完成。

P3先核对旧followup仅精确排序索引和其回归，git apply --check通过后选择性复用，不合并整条分支。专项2/2通过、400ms不改；原665.788ms失败保留；最终完整套另报，不拿专项一次通过说根因全闭。

注意：最初早期全量WPF正在持有旧host时build复制失败，原MSB3026/3027日志保留；改全新--artifacts-path而非杀测试/覆盖，最终完整套使用冻结的新isolated host。
