# 性能实测基线

平台：Windows 10.0.26200，Intel i7-14700KF（20核/28线程），64GiB RAM，.NET SDK10.0.401，LibRaw0.21.1 x64；Release ProcessProbe独立进程。日期2026-10-11（Europe/Berlin）。程序级测量，不等于桌面端到端交互延迟。

| 样本 | 全尺寸 | 解码 | 1600边预览4次 | 全尺寸render | TIFF16原子写+校验 | 解码峰值 | 写出累计峰值 |
|---|---|---:|---|---:|---:|---:|---:|
| Sony A7M4 | 7028×4688 | 1025ms | 159/168/155/135ms | 2074ms | 695ms | 930.6MiB | 2057.9MiB |
| Canon 5D III | 5796×3870 | 1192ms | 158/174/140/133ms | 1384ms | 500ms | 651.5MiB | 1472.5MiB |

每次EV变化均复用相同master，不解码；冷WB重新解码成本单列REAL_RAW_RESULTS.json。首次JIT/缓存、并行测试、磁盘差异会影响结果。只有本机记录，不宣称普遍速度达标。

PERFORMANCE_SONY.json / PERFORMANCE_CANON.json记录各阶段WorkingSet、PeakWorkingSet、managed bytes。解码后Sony驻留约428MiB；同时master+full+输出验证可超过2GiB，当前AtomicTiffWriter/TiffReadBack整文件验证和多份RGB缓冲是显著开销；不是流式低内存实现。六素材质量probe同时保留多个master及readback，进程累计峰值约4.9GiB，该值不能当单帧产品峰值；也不能隐藏。

Studio保存重开/metadata/hashing/bitmap/orientation/analysis的整体首开与UI延迟未独立仪器测量，DESKTOP_LATENCY=NOT_RUN。preview最大1600边，在高DPI和100%放大不等于全尺寸；优化流式输出、LRU RAW master预算、抗混叠和全尺寸细节/GPU属于明确后续。
