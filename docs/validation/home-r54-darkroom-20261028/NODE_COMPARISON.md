# 本批变更与像素边界
既有数学准入沿 ../r54-node-system-20261028/NODE_COMPARISON.md，参考固定Pillow/OpenCV行为对照；本批未换公式、色域、输入域、位深、tone、ICC或框重采样。
图缓存生命周期按消费者引用计数提前释放，源借用、最终输出转移、异常/取消finally清理。halo0只允许明确逐点白名单；最大4条64行并行，蒙版坐标仍全源。
回归包含小图完整cache参考逐值比较、共享祖先effect计数、2.08MP条带蒙版/组强度边界点、真实20MP三点float检查+原尺寸TIFF16结构验证、取消和异常。没有把这些称为外部全像素校准。
框V1 nearest不变：12框premultiplied alpha诊断仅评估，不悄悄启用bilinear；旧PV4/5/6路径与Graph absent未改。当前14bbb60d只改变新并行分支位置，位置不参与路由，既有锁定位置保留。
