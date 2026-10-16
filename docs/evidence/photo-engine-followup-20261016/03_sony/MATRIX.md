# Sony_DSC08015 独立验证矩阵及候选发行边界

新授权原件只读存在，67,817,472字节；SHA1FE22BE78C66AB0DDCCF753D10187F142B980146FFF7F5D188533905B7F38B93。仅检查该文件，不遍历私人目录。元数据白名单记录make/model/尺寸/位深/方向/压缩，不输出序列号、GPS或私人目录列表。原件路径不进入测试命令，实际操作隔离副本。

|路径/项目|实际状态|证据/边界|
|---|---|---|
|旧0.21.1产品RGB16解码|通过|ILCE-7M4、raw7040×4688/active7028×4688、EXIF8、AutoRotate后4688×7028，801.2914ms；未拒绝|
|RAW压缩|本文件确认|RAW SubIFD Compression=Uncompressed，14位，decoder unpacked_load_raw；IFD0/嵌入预览JPEG不能当RAW压缩|
|PV4非默认调整|后端通过|CameraGains 1.05/1/.95；曝光+.5EV；与0EV输出不同。合同WB一次、ProPhotoD65渲染域、16位已裁切，非传感器值|
|缩放/1:1/区域|后端通过|768预览、64×64原点和局部区域；未证明真实UI缩略图/前台1:1|
|会话保存重开|后端通过|真实ColorStudioSessionStore、显式PV4版本和sourceSHA，重新解码master及预览逐像素一致|
|TIFF16|后端通过|7028×4688、orientation8、sRGB ICC逐字节相同、数值oracle最大误差0|
|PNG16|后端通过|4688×7028、orientation1、ICC一致、逐像素最大误差0|
|JPEG95-444|后端通过|4688×7028、orientation1、sRGB，MAE.86726/RMSE1.19254/max13个8bit码；原门禁MAE≤5/RMSE≤15|
|预取消|后端通过|旧解码和region pre-cancel；非真实切图中取消|
|前台启动/切图中取消/原生关闭内存/物理DPI|NOT_RUN|未取得桌面独占，不用后台结果替代|
|0.22.2固定预编译研究|两次一致|7028×4688未应用方向、线性gamma1/1、cameraWB baked、ProPhotoD65 convention，PPM无ICC；2.633/2.563s，峰值约343MB|
|Canon/Fuji候选对照|两次各一致|Canon5496×3670；Fuji7752×5178，新有效高度不同于旧5184，未裁图对齐|
|0.22.2源码重建/干净Windows加载|NOT_RUN_BLOCKED|指定位置及PATH无MSVC/LLVM/CMake工具链，未擅装；无可授权干净VM，未把现机预编译加载当cleanVM|

官方包EXE SHA8DF090A80C784FA03182B91AB1679FD08E5FC139D54B0BFB8B003024EF741129、DLL SHA6A459C22039ABF0EAC4D263673337C8ED5F223ACBD372FCF77610DEBF80AC8CD沿用已核对来源。研究驱动脚本逐次哈希，逐进程内存，逐像素摘要。其参数与旧产品sRGB不等价，不比较两域像素为同一色准。

候选独立合同PhotoRaw0222Research1：源SHA/几何/方向、原生和wrapper可执行身份、WB baked/线性传递/量化RGB16/颜色原色及未应用ICC单列；输出不是sensor raw或标准ProPhoto ICC D50。原照片私有衍生图像留ignored artifacts，不入Git。

发行决定仍NOT_APPROVED：LibRaw双LGPL2.1/CDDL1须选择并完成可审查义务，依赖/compiler/flags/源码对应/可重建源码提供/cleanVM仍未闭。此阶段不作法律兼容性结论，不将新DLL放普通Release，不打开候选假入口。旧项目/旧FileUnsupported合同保留，DSC09916独立身份不改；本文件通过不推广所有A7M4/A7S3/ARW。

## 追加真实工作台 ViewModel（仍非前台桌面）

在显式隔离AppData根运行真实ReferenceColorWorkspaceViewModel，未创建窗口：旧链加载和实际缩略图、创建PV4、非默认1.05/1/.95增益及+.5EV、Request/UpdateProfessionalViewport可见128×128细节实际补齐1块、真实工作台SaveSession/LoadSession后预览字节一致，全部通过。StopProcessingCommand在细节完成后执行，**不是处理中取消成功**。详情SONY_WORKSPACE.json。原件SHA再次只读核对不变。没有操作日常数据库/系统ICC/DPI。
