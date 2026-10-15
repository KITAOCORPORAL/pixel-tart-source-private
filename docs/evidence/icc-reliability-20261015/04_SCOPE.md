# RAW / Structure审查进度

RT5.11固定commit实际boxblur.cc已获取并阅读；DT4.8.1实际box_filters.c已获取并阅读。外部GPL文件只在artifacts，Git只有原创提取/编译/运行脚本和哈希。可执行比较RT完整标量boxblur与DT原始水平helper转置复用两次，对双精度截断窗口oracle4组尺寸半径最大差~5.6e-7；不是完整DT优化vertical/dt_box_mean，也不是完整Structure独立等价批准。编译restrict兼容宏失败轮次保留。不复制GPL到产品。

Structure固定Canon三轮重测中，逐tile时间、GC、processload、halo和319tile保留；未改算法或接入生产。前批9.05s波动不能仅凭此轮较快消除，需看同负载时段/最差/median。无强制GC/trim/预算扩大。

RAW候选0.22.2新增本地CAPIcallback取消/RAIIclose/10s自然WS观测工具，不进入Release。旧0.21.1SonyFileUnsupported证据保留；sharedLibRawRT不算独立。Sonycolor4语义未证实，黑位后/WB后/矩阵前像素UNAVAILABLE。旧recipe未迁移，新候选recipe产品保存重开合同仍OPEN；原产品和候选分开，不能据研究TIFF16成功宣称普通Sony支持。LGPL2.1/CDDL选择、精确源码对应/提供义务和依赖链审查未闭合，绝不捆绑候选。
