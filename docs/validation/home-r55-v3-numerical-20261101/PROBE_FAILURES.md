# 探针自身的诊断错误（保留，不算产品修复）
- 首个MSCMS脚本把BGRA当BM_xBGRQUADS(0x10)，跨通道比较误差。按官方BMFORMAT改为BM_xRGBQUADS(0x08)后原照片和保留ICC夹具与WIC逐位相同；产品一直使用WIC Bgr24/Bgra32，不存在该手工PInvoke调用错误。
- ICC矩阵探针推广到非256宽保留图时先有Path拼接括号错误，继而遗留固定输出stride=256导致Python子进程访问异常。仅本地测试子进程/临时输出；原始日志保留，改为实际宽度后重跑。合同阈值、产品、输入和旧FAIL没有改动。未经完成的结果不作为证据。
- 源码读取一次HTTP连接被远端关闭，cmsopt固定commit同路径重试成功；初次icm.h路径404未用于事实结论。官方BMFORMAT与API页面另核实。

- LCMS flags枚举核对：16384为SOFTPROOFING，不是HIGHRESPRECALC；本轮终检显式补1024(HIGH)/2048(LOW)及BPC组合，并保留原结果，不以误命名作结论。
