# Selective native ABI / lifetime / valid-domain review

2026-11-04，Asia/Shanghai。只对本轮固定win-x64字节与所读字段有效；不泛化到其他LibRaw、其他binding架构或RawTherapee vendored header。

## 1. 来源与交叉核验

1. 实际raw_r.dll SHA F500C073…，native报告0.21.1-Release；实际binding SHA6DF69864…，0.21.1.7、informational commit43e9c1771c2b1387c2170e084e433d8bcef51cab。与原发布件/旧probe相同。
2. 固定LibRaw0.21.1源码cccb97647fcee56801fa68231fa8a38aa8b52ef7及绑定源码43e9c177…。只在既有本地研究目录阅读，无新vendor代码复制。
3. **实际PE代码**验证getter的context-relative偏移；由libraw_raw2image导出跳转跟到C++方法，验证raw_image、image、margins和pitch读法；raw2image_start的memmove大小/源目的地址验证saved sizes。地址、原始指令字节与完整本地disassembly的SHA在receipts/ABI_PROOF.json。它不是凭另一个header推断。
4. 无RAW初始化context后验证native iparams/lens/other返回地址，公开raw/iwidth/iheight getter与所读尺寸，parent_class与C++ this差值。每个实际context仍重复检查；有数据后再校验saved/current sizes完全一致、实际channels/aliases/pitch/rectangle，使用VirtualQuery拒绝不可读/guard区域。VirtualQuery本身不是allocation-length证明，尺寸与有效域还依赖源码契约和多个字段校验。
5. native NuGet没有完整编译器/flags/port-patch来源，未证明bit-reproducible构建。仅接受本表被二进制与runtime共同验证的字段，不去读未经验证的C++ private scratch/fuji_width。

|字段|相对于C libraw_data_t的offset|实际DLL依据|
|---|---:|---|
|image指针槽|0|C++ free_image/raw2image读取this+8；runtime parent=this=context-8|
|sizes|8|raw_height getter `[rcx+8]`；raw_width+0xA；iheight+0x14；iwidth+0x16|
|idata|192 /0xC0|libraw_get_iparams返回context+0xC0|
|other|192416 /0x2EFA0|libraw_get_imgother返回同offset|
|rawdata|193504 /0x2F3E0|固定struct；下列raw_image和saved-sizes在机器码独立锚定|
|rawdata.raw_image|193512 /0x2F3E8|C++raw2image `[this+0x2F3F0]`，即context+0x2F3E8|
|rawdata.sizes|194016 /0x2F5E0|C++restore从this+0x2F5E8复制0xB8到this+0x10|
|parent_class|381248 /0x5D140|C入口读取该slot后转入C++方法；runtime核验context-8|

**绑定陷阱：**该版本`LibRawData.Image`声明为四个ushort的内嵌数组，在x64恰占8字节，但native定义是`ushort (*image)[4]`指针。这四个ushort不是像素。本工具从已验证的slot读取IntPtr，再访问所指有效图像；绝不对结构体地址或该数组取样/做像素hash。产品此次成功路径并未使用这个字段读取像素，因此这不是本轮发现并修复的产品bug，也不擅改绑定。

## 2. 实际三阶段有效域

### Unpack active mosaic

- DecoderInfo实际为`unpacked_load_raw()` /4096；filters9、colors3。LibRaw src/decoders/generic.cpp:21起从文件读取raw_width×raw_height个short；不能写成已经命中fuji_compressed_load_raw。
- current与rawdata.saved sizes一致：raw7872×5196、raw_pitch15744；active width7752/height5184、left60/top6；单平面U16，其他color/float aliases为空。
- 读取地址：raw_image + (y+6)×15744 + 60×2，x0..7751/y0..5183。行有效15504bytes，紧排80372736bytes。mosaic颜色由记录的6×6 CFA定义，不把它误作三通道RGB。
- raw inset crop两个元数据框记录在参数中，但不新增应用；本次实际有效尺寸来自sizes与正常处理输出，不按推荐框另裁。
- 生存期：Unpack成功之后、DcrawProcess之前，context存活；捕获完成即不再借用该指针。不读取未验证的光黑/边缘/pitch padding。

### DcrawProcess image

- 实测width/iwidth7752，height/iheight5184，flip0，colors3。native像素4×U16 /8bytes，pitch62016；只取槽0、1、2，输出行46512bytes，共241118208bytes。槽3从未进入hash。
- 对应源码：dcraw_process.cpp:40起raw2image_ex；raw2image.cpp约390..402对image realloc+memset或calloc；xtrans_demosaic.cpp:428对边缘调用border_interpolate(8)；postprocessing_utils.cpp:79起对有效width/height写RGB/统计histogram。这给出输出区域与边缘的定义，不对未使用槽作算法判断。
- 域：包含原生process前处理/显影/convert_to_rgb后的U16 RGB，**尚不是MemoryImage曲线查表后的RGB16**。没有拿它与mosaic或最终RGB逐字节横比。
- 生存期：成功Process返回后、MakeDcrawMemoryImage之前，context仍存活；只有一行compaction缓冲，不保留另一份全图。

### Returned MemoryImage

- Bitmap，7752×5184×3×16bit；DataSize=241118208=required；pitch46512，little-endian。S.flip=0且原UserFlip=0，无新旋转/归一。
- mem_image.cpp:168起copy_mem_image会基于histogram/原gamma/auto-bright建立curve，再用image[soff][c]作查表；dcraw_make_mem_image分配独立返回对象。不能因为Process与最终RGB幅度不等就断言Process观测错。
- 生存期：ProcessedImage using范围内AsSpan借用；紧排有效字节写完后先ClearDcrawMemory，再context Recycle/Close。不读取released image。

## 3. 自检、缺项与限制

最终冻结binary先通过无RAW自检：getter/offset/尺寸及可读页检查；改变padding、矩形外数据和第四槽hash不变，改变有效RGB样本hash改变；5种非法尺寸/通道/pitch/裁切条件被拒绝。synthetic不代替真实数据，本次真实两context又通过布局/长度检查后才捕获各阶段。

内部raw2image后/scale后/pre-interpolate后/插值后/convert前的有效buffer：**NOT_CAPTURED / NO_VERIFIED_SAFE_C_HOOK**。现有progress callback不是像素检查点；未注册新回调、未读未知C++内部指针，也未换instrumented native DLL。native实际worker数、执行quality/passes、fuji_width与scratch读写时序未观测。当前所能证明的是相同有效Unpack结果到不同Process结果的范围，不是具体C++语句。
