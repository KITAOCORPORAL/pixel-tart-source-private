# 照片专用RAW与色彩引擎候选：PARTIAL / 研究候选，不是产品入口已发布

业务验收时区Asia/Shanghai，原始主机时间戳保留。基线远端fix/kitao-wpf-result-completeness-20261016已核对3aa65a13dba96747e52be73960c34ab89226e08b；冻结3162cb10e0bc4838331a9325a1169baff051f2a9至基线src/tests/runner无差异，旧Release290+414文件逐SHA核对零不匹配。独立工作树E:/Kitao-Worktrees/kitao-photo-engine-candidate-20261016，分支feature/kitao-photo-engine-candidate-20261016；保留原件、旧包、所有失败和其他工作树，不merge main、不强推。

## 身份和明确限制
本批产品src/tests/既有CSharp工具/runner相对基线零改动，新增可执行照片研究工具及证据。产品/现有CSharp工具本轮新构建来源d174d26af591a7143c3a47536030620357a70f40；最终研究候选源码d8ce36a（完整SHA见STAGE_COMMITS）；最终证据SHA为包含本报告的HEAD，不自引用。没有将新RAW/ICC/ACES/DRT放进普通ColorStudio，不能宣传本批已实现产品Sony或通用ICC支持。

用户限定照片专用：无视频剪辑/视频Log/FFmpeg/BRAW功能。外部OpenDRT源含未选Log函数仅留本地artifacts；CPU参考只固定linear stillRGB，不创建Resolve插件/视频IO。ACS/ACES只是命名工作/显示对照，不相机标定。

## 第一阶段：23/6条件性未执行
01_UNEXECUTED_GATES_FINAL逐名列原状态、源码触发环境变量/输入、设备或人工、隔离执行条件和阻断声明范围。23WPF/6发布Core依然NotExecuted，未改Inconclusive/断言。P1已关闭42missing不再重复大型单类诊断，最终完整回归必要防回归即可。GPUparity阻断GPU声明而非机械阻断CPU；视觉baseline需人工批准；RAW真实语料与副作用/完整图要求单列；private capture不等同物理DPI/nativeclick。旧结果hash/trx保持。

## 第二阶段：RAW研究工具证实
固定LibRaw0.22.2b93f6e...、RT5.11db575...、RawSpeed0c16fee...上游和文件许可/入口。RawSpeedRawParser/RafDecoder实际源码已读，Windowsbinary/build及三原件实际路径NOT_RUN；CR3文件404不能扩写成全部RawSpeed不支持。LibRaw候选prebuiltdll6a459c...与headers/selectedsource身份冻结，Zig只编原创CAPIwrapper。DualLGPL2.1/CDDL1及componentCOPYRIGHT登记；精确Windows重建/发行选择与源码提供/cleanVM未闭合 => 不捆绑/不开产品入口。

PhotoRaw0222Research1对授权CanonCR3/FujiRAF/SonyARW完整原件SHA先后核验，三份实际gamma1/1/WBcamera/noAutoBright/outputColor4ProPhoto16/禁止方向应用输出成功；callback100msSony返回CANCELLED且无输出；截断/未知格式稳定OpenFile拒绝，无embeddedpreview当RAWsuccess。原nativeclose后10秒WS日志保存，不是产品UIrecipe验收。

容器/rawsize/activeorigin/flip/CFA/黑白位/WB/rgbCam逐阶段见02_GEOMETRY_AND_TYPES/RAW_VARIANTS。Canon raw5568×3708 active72,38,5496,3670；Fuji7872×5196 active0,6,7752,5178，metadata7752×5184；Sony5120×3584 active0,0,4624,3080，sourceflip5、filters0/color4非Bayer。SonyRawFileType4等声明不冒充bitstream独立证明，未证明compression细节UNKNOWN。Fuji旧0.21.1/RTinternal5184/top5与候选5178/top6版本坐标保留，不事后裁图造一致。RT共享LibRaw本轮3成功不计独立；强制内部dcraw Canon/Fuji成功、Sony-2拒绝单列。

实际输出uint16PixelSHA/零值/饱和/ICC见02_RAW_OUTPUT_PIXELS，496byteICC a179...的TRC gamma1.0实际确认。输出无orientationtag，需要未来recipe保留flip/origin；当前未完成产品保存重开，不静默迁移。已WB/matrix/demosaic线性ProPhoto16仍有量化/裁切，不sensor值；扣黑后、WB后、矩阵前pixels及matrix运行时来源UNAVAILABLE。旧产品0.21.1SonyFileUnsupported稳定保留，默认拒绝不被候选成功覆盖。

## 第三阶段：有限真实照片ICC闭环（工具候选）
PhotoSrgbLinear1仅固定编码sRGB输入两个profileSHA（标准43a86.../RTv4 17aebb...），单图TIFF16RGB或JPEG8RGB/EXIF1，无alpha/CMYK/任意ICC/多页。明确missing/损坏tag/未评估拒绝。LCMS2.16MIT453baf...精确外部DLL2f864... relative1/BPCoff/adaptation1，PCSXYZD50。decode码值→linearsRGBfloat32 extended→冻结EV→输出encodedfloat供诊断→命名PhotoSrgbRelativeIntegerSink1写uint16饱和；不是隐形clamp/感知映射。linearworking域、outputdisplayencoded域和rawsensor类型分开，原V1defaultoff/研究V2explicitclip/PV456不动。

授权Canon全5496×3670 RT渲染派生TIFF与照片JPEG派生、灰阶/饱和/肤色已知码值实际执行：recipe磁盘重开、输出float/码值一致；TIFF16/PNG16码值及嵌入ICC exact回读，previewRGB8与同输出码一致；JPEG8lossyMAE~.00183/.00172，PSNR50.44/50.79dB。独立解析sRGBEOTF vsLCMS工作值max4.10e-6。标准profile生成固定日期/空ID可复现。输出只标准SHA，caller不可扩展。

OCIO2.4.2与Colour0.4.6对linearRec709D65→Bradford→ACEScgAP1D60命名链：默认四位表矩阵差6.93e-5配置不等价明确记录；使用derivedprimaries后max4.64e-8。不把ACES当cameraIDT或camera校准，不重复白点适应。引用源commit/实际distributionRECORD/许可证hash单列。

发布义务/cleanVM/产品入口/cancel预算/原件边界/RAW完整产品链未闭合，故该子集只是数值验证的可审工具候选，NOT_APPROVED_FOR_DISTRIBUTION，未达到用户要求的可发布产品增量。当前输出失败可留新目录诊断但不覆盖原件，不冒称产品atomicexport。photo/raw研究工具未打包普通Release。

## 第四阶段：照片输出成像外部对照
实际运行ACES1.0SDR组件viaOCIO2.4.2和OpenDRTv1.1.0固定af683...外部CPUscalar（GPL代码不入Git/product）。darktable4.8.1仅原log_tonemapping/filmic_spline子阶段可运行，完整filmic/WinCLI NOT_RUN，不能声称三条完整DRT A/B已满足。源码/许可证/参数/生成referenceSHA在04文件。ACESv1.3core1256...只固定source参考，实际builtin版本1.0明确，不宣称ACES2。

6种静态场景512点gray/gradient/backlit/redblue/skin/highISO(seed713synthetic)统计levels/色相/压色/plateau/clipping/performance，不同look/EOTF（OpenDRT2.2powervsACESpiecewise）不是等价配置或优劣判决。darktableidentityspline示例不是上游默认完整模块。无真实高ISO去噪/主观自然肤色真值。摄影师blindreview.csv PENDING。已有更差候选50levels/512不入产品；本批不提出生产新DRT/版本迁移。

## 第五阶段：实际发布回归与实机
新产品完整E:/KR/P16/release/win-x64 290文件，工具E:/KR/P16/tool/win-x64 414文件，真实EXE版本2.3.0-photo-candidate+d174d26...、SHA/逐文件/树摘要见RELEASE_EXE/RELEASE_FILES/RELEASE_TREE_HASHES。旧发布包未覆盖，新native候选与新exactLCMS不加入，已有Sdcb不同hashDLL不是替代。测试host C:/KH/P16A、E:/KH/P16B逐SHA绑定发布DLL。

最终源码Core1708发现/选中/返回=1701Passed0Failed7NotExecuted；实际发布Core1708/1708/1708=1702Passed0Failed6NotExecuted；实际发布完整WPF1626/1626/1626=1603Passed0Failed23NotExecuted Missing0；第二目录专项17/17Passed（Premium独立optin不混fullcounts）。strict Core/WPF仍FAILED，仅保留条件性NotExecuted，无新missing/timeout。72golden/P3400Passed文件不变，原TRX/settings/stdout/stderr/process/rawstdout归档RAW_TEST_RESULTS.zip。

未预约独占桌面/干净WindowsVM=>真实完整目录启动、工作台→Color、两图切换取消、save/reopen/16位export、10秒系统自然内存、physicalDPI NOT_RUN。策划/WPS独立NOT_RUN。只prepare新runtime并校验290文件，无Launch/鼠标/键盘，不以AX、私有桌面截图或logicalscale替代。准确nativeWindows来源/许可证/依赖和cleanVM/fullUI门禁OPEN。

## 结论分类与剩余项
已在产品：仅继承旧照片工作流，本批src未变。研究工具证实：三RAW候选全尺寸/取消/释放、有限sRGB真实照片闭环、OCIO/Colour匹配命名矩阵、两输出参考+DT子阶段数值。推测/不足：矩阵来源、compressionbitstream、审美映射优劣、RawSpeedWin支持；不能宣传。未运行：RawSpeedWindows、完整darktable、干净VM/普通实验入口/nativeWPS/DPI/摄影师盲评。最终LOCAL/REMOTE普通push核对另记。

CalibrationEligible=false，不要求购买色卡，不宣称A7M4绝对色准或宝丽来精确复刻。无视频功能扩展；保留旧失败，不合并main、不强推。
