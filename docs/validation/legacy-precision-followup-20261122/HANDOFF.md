# 产品线交接

先前产品cf5a63d3；增量产品11a424c90199b32cbf73cc6a14eefd651612b213。工作树N:\pixart\guangjing-legacy-precision-lifetime-20261122，分支fix/legacy-precision-lifetime-20261122。当前相关产品代码相对d07未变；本轮不回写产品树。

先读REPORT/RECEIPT/PIXEL_DISPLAY_COMPARISON。Sony方向8完整周期、真实处理后40MP首次导出及中性完整周期通过。实际处理后40MP同进程重开仍STOP，完整合入条件未满足。不要拼接先前“原片100%且关闭处理预览”的回执来覆盖它。

合入代码需显式cf5a63d3→11a424c9，或审查COMBINED_D07_MEMORY.patch。正式产品接入/普通窗口验收由产品线负责。所有颜色公式/顺序和发行解码器不变，新增RAW0，旧停止余额不动；空间/图/复杂混合未升级容量结论。

Source绑定、计量、原始采样与每次失败均在本目录；实际照片/载荷/TIFF只在LOCAL_ARTIFACTS本机路径。高光页仍复用4项，私有公司图缺失，锐化V1封存。下一步只读追踪剩余payload-read-2峰值的真实持有者/提交，不先销毁共享master或改变缓存/GC策略。
