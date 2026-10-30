# 最小流式接口提案（第七批，未实现）

状态 **PROPOSED_NOT_IMPLEMENTED**。不是上游能力、不是产品能力、不是“全尺寸已通过”。本轮必要下界/实测证明现有striped+public result不足以在公司当前负载安全完成20MP，所以停止full请求，单独评估最小外部研究改造。

## 为什么只加一个逐行writer不够

Python现在已按行写/回读，省掉第二份CPU输出拷贝；但`spk_render`在返回前仍保留全source/negative/assembled RGB/GPU RGBA16/CPU result及staging。给现ABI套一个callback不消掉这些内存。把独立640窗口拼起来会改变帧长边、边缘反射和空间halo，也不等价。

## 最小版本边界建议

单独外部checkout，暂名`SpektraStreamingResearchV1`，新入口（概念，不是已存在symbol）：

```c
spk_status spk_render_stream_v1(
  spk_engine*, const spk_frame_source_v1*, const char* frozen_params,
  const spk_stream_limits_v1*, spk_row_consumer_v1, void*);
```

- source以有界read_rows回调提供完整frame坐标/原尺寸/方向/域和hash；不经现`spk_open`上传整source。
- 第一版只接受本确定性PortraA配方和完整身份；grain/glare关闭，但**不能关默认unsharp/换曝光**。其全局节点必须明确检查关闭/常量，未知效果拒绝，不隐式丢弃。
- 暂不缓存完整GPU negative：negative可写外部CPU/file-backed row store并有hash；GPU仅保留strip+halo。仍保留完整画面尺寸以计算pixel pitch/tier_ratio。
- 在完整print链需halo处，读取原始negative相邻行并沿**上游完整链**生成足够范围。默认sigma.7的FIR radius来自原`gaussian_kernel_1d`，不能另抄近似。首/尾按原画面reflect规则，内部band边缘永不当frame边缘。
- 禁止每strip自算全局max/autoExposure。若允许它们，先对完整frame有界reduce固定常数，再strip使用相同常数；新算法/再现性门禁独立。
- 编码/near-identity矩阵/quantise全部调用原路径，GPU只materialise strip RGBA16，读回有界行块并按source row序写consumer；取消和consumer异常完整停止，失败无成功hash。并发/线程只允许串行。
- budget参数真正限制rows和allocator，不能沿上游“接收但未使用”的strip_budget_bytes。显式host/device/staging三类预算；已分配或不可释放对象计入，禁止仅用全局显存used当进程峰值。

## 必须先过的对照

1. 新源/新库完整hash和变更图；GPL研究补丁留外部，不复制产品src。
2. 321×215、641×429、1200×801与同whole既有输出逐像素exact，含首尾、不整除、wire/people、halo边界和near-zero/负/>1。
3. rows改变、consumer分块改变不改像素hash，错误/取消/回调清理测试；默认unsharp、frame尺度和profile bytes不变。
4. allocator实测随strip而不是height增长；source/negative/result不重新常驻完整GPU；逐行TIFF读回/尺寸/全pixel hash。
5. 然后只尝试5496×3670 PortraA一次；成功才讨论扩stock/B，不重复6full任务。

本轮只给可复现阻点和方案，未修改引擎为流式、未build新streaming DLL、无新streaming output。许可证不阻断隔离研究，但代码/资源产品分发仍单列，独立进程/DLL/LUT不自动解决。
