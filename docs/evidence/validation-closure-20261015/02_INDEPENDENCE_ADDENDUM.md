# RAW后续核实：第二程序不必然等于独立解码

本轮追加RT verbose后发现默认EnableLibRaw=true，最初成功的三原件容器/解包层共享LibRaw；这些原结果全部保留，不能计为第二独立RAW解码实现。RT后续处理虽独立，其final比较文件原本就标配置不等价，不作色准批准。

随后只改本批独立RT_SETTINGS中的Raw Decoder.EnableLibRaw=false，强制RT内部dcraw，Verbose=true，完整输出/exit见02_RT_INTERNAL_*.json。Canon和Fuji实际成功，Sony实际拒绝（exit 4294967294/-2，Error loading file），不以默认LibRaw成功掩盖。

|原件|LibRaw0.22.2|RT5.11内部dcraw|阶段比较|
|---|---|---|---|
|Canon|unpack black2048/white16383，5496×3670|black2048/white16382，来源camconst，5496×3670|容器几何同；白位配置差1，算法/WB/矩阵链未等价|
|Fuji|6×6 black1020/white16383；crop0,6,7752,5178|black1020/white16383（dcraw）；crop0,5,7752,5184（camconst）|黑白位可观察一致，active area不等价，不裁图做假一致|
|Sony|sony_ycbcr_load_raw成功|REFERENCE_UNSUPPORTED_INTERNAL_DECODER|原产品0.21.1 FileUnsupported也保留；不能称双实现同阶段验证通过|

RT stdout确认三文件矩阵来源camconst.json；其black/white/crop/WB pre_mul和scale_mul直接可观察，不再列全部metadata为UNAVAILABLE。但其传感器像素dump、扣黑后像素、WB后矩阵前像素仍UNAVAILABLE；日志系数不是像素中间结果。LibRaw实际选中matrix的精确来源仍OPEN。原02_RAW_CONCLUSION早期UNAVAILABLE描述由本追加证据限定更新，不删原运行。

RT共享LibRaw一组、强制内部一组配置和结果分开，后者才用作独立参考。Sony候选可读只证明兼容性，不证明A7M4绝对色准；本批没有产品升级或新Release。Fuji内部dcraw/camconst5184与新LibRaw5178的差别也保留。
