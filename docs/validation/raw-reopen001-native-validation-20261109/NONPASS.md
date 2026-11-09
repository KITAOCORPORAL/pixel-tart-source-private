# NONPASS — RAW-REOPEN-001

2026-11-09，Asia/Shanghai。**REPRODUCED_P6_XTRANS_RETURN / ROOT_CAUSE_OPEN / NOT_FIXED**。

- 本次R2隔离DLL真实编译和native无RAW ABI/布局/hash/生命周期自检完成。Recycle/Close导出调用数仅按固定绑定释放逻辑推断，未作为直接计数。
- 最新明确授权的新批次两次串行独立进程完成；旧consumed2账本保持，新批次也consumed2/remaining0。无第三次、无失败补跑。
- P1/P3b/P5及X-Trans serial-ready完整有效输入与表相同；P6首次不同5127通道/max815，P7/P9同组差异。已定位插值返回边界，不等于已证H1/竞态/未初始化根因。
- 原DLL、旧失败及所有旧树/收据保留。A产品源码未改，不打包RAW修复版；修复后App验证NOT_RUN。B独立收藏功能包不宣称修复RAW。
