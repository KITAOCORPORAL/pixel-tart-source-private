# Fuji 原生阶段定位：阻断保留，未安全修补旧依赖

## 固定身份与实际运行

授权Fuji SHA52D054ED9A90306DB0B7EB5D6C408A2E006532B976AC88ED792692F9F744E865；raw_r.dll F500C0732FEB21B188D5B52CEA05FD824D5B3C8016EB2CA68D8312ACC9F914B9；Sdcb.LibRaw.dll 6DF698647C79DF05E754072ED393A02315D780AF7287C455D765A767DF93D4A3。旧LibRaw0.21.1-Release；完整7752×5184，未旋转，cameraWB=true/autoWB=false/matrix=true/sRGB/RGB16/Interpolation=true，其他旧默认不改。

隔离子进程默认继承14线程：3.685/3.656s，逐像素不一致；固定4线程5.841/5.847s，不一致；固定1线程15.029/15.243s，全阶段一致。Canon同参数默认14线程1.468/1.372s，全阶段一致。耗时包含阶段buffer落盘，不能与纯产品解码耗时当同条件比较。各进程峰值及各阶段内存见原JSON；子进程退出释放，未设置全局或父进程OMP变量。

## 最早出现差异的实际阶段

progress callback位置固定到上游源码：unpack后sensor、scale_colors前（已发生raw2image/inline黑位）、scale_colors后（WB已烘焙）、pre_interpolate后、convert_to_rgb开始之前（已完成X-Trans去马赛克）、convert_to_rgb回调、dcraw_process结束、MakeDcrawMemoryImage RGB16。

默认两次：解包、scale前/后、pre_interpolate后都逐字节一致；**首次在去马赛克完成、颜色变换开始前不同**：30544个4通道样本，最大151；最终RGB16为30542样本不同、最大4871。与上批38375/11713是不同执行，原失败证据仍保留。固定4线程也首次在同处不同；单线程与Canon各阶段相同。

差异坐标/通道/幅度桶、6×6 X-Trans网格计数、496=512-16块步长行列余数在*-difference.json，不包含照片像素或渲染图。默认差异明显集中在块开头的第4–12行附近。它支持块边界读写交叠线索，但没有调试器竞争检测或源码重建受控补丁，**尚不声称完全根因已证明**。

## 源码核查

LibRaw官方tag0.21.1解析commit cccb97647fcee56801fa68231fa8a38aa8b52ef7（tag对象bf7d75a52bed53fcb1c5e0564cf3034379a4fefd）。src/demosaic/xtrans_demosaic.cpp SHA208BD3F9BE33407299C365410AD5786ABAC6C63D20C5878BD79E07F2CC8377A7；src/postprocessing/dcraw_process.cpp SHAF70F6ED43E7D42C19DF8E1F6C23140E9C9A68282AE359BA474F4DE670AAAF227。

xtrans_interpolate：omp parallel for schedule(dynamic)按top分块，LIBRAW_AHD_TILE512、步长496；读取共享image周边，结束时将结果写回共享image。实测差异所在阶段与此位置吻合。wrapper NuGet源码固定43e9c1771c2b1387c2170e084e433d8bcef51cab；只读使用其结构定义和C API，未拷贝LibRaw算法进产品。上游源码与现有Windows DLL的精确编译对应仍未证明，不能将tag源码当DLL调试符号。

Native progress100ms取消实际返回CancelledByCallback(-100010)，无最终RGB16；这只证明审计回调，不替代旧产品切图中取消。旧CAPI没有可从托管安全安装的完整process_step callbacks；目前在实际可观测边界抓取，未伪称拆分了每个黑位步骤。

## 结论

`BLOCKED_UNSTABLE_LEGACY_FUJI`。未修改旧DLL、默认线程、PV4/PV5/PV6输出或golden。单线程相同仅为定位实验；新recipe/decoder准入待源码重建、独立参考、兼容和产品全链。托管master/导出前/TIFF的旧证据保留于上一批；本轮最早差异已经在原生去马赛克后，后端输出成功不批准发行。
