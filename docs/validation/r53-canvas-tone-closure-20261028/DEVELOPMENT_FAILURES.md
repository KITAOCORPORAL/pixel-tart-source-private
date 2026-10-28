# 保留的开发失败
- 首次画布新增UI编译：var多声明/resize变量作用域冲突，最小修复，原日志保留。
- 原WPF8预乘→解预乘透明旋转边缘红220变198；未放宽delta12，改float预乘插值/最终单次unpremultiply后原断言通过。
- 新风险组合测试蒙版没写CompositeVersion，产品未知版本拒绝仍保持，修夹具显式版本。
- 曾改变锁定显隐行为，原CanvasObjectTransformTests失败；恢复旧产品合同，不删旧断言。
- 第一阶段独立reopen因缺项目NOT_RUN，第二精确adapter方法新进程实际执行PASS；不改runner冒充全绿。
- 离屏首次未等完整异步预览出现“预览暂不可用”，增加LoadedPreviews2张实际发布断言和稳定等待，未以导出通过代替可见显示。
- 新版影调V2为明确版本修正，旧V1继续编辑不偷偷迁移；actual native tone-stage有RT NaN样本保留，科学准入不升级。
