# LibRaw 参数审计

审计日期：2026-10-11（Europe/Berlin）；原始机器日志时间为Asia/Shanghai。实际依赖：Sdcb.LibRaw **0.21.1.7**，其 NuGet repository commit **43e9c1771c2b1387c2170e084e433d8bcef51cab**；win64 runtime **0.21.1**；实机 native Version **0.21.1-Release**。官方 LibRaw tag 0.21.1 commit **cccb97647fcee56801fa68231fa8a38aa8b52ef7**。未升级依赖。

## 核验方法与权威

读取实际 csproj / NuGet nuspec / 程序集版本；从现用 DLL 的 `OutputParams.FromLibRaw` 读取新 context 的参数，见 LIBRAW_RUNTIME_DEFAULTS.json。只读核对官方源代码与确切包装源码，哈希见 UPSTREAM_IDENTITIES.json。网页工具未返回来源内容；使用允许的 curl.exe 与官方 Git 仓库，不使用 Browser/WebView/CDP。首次若干 GitHub raw 请求重置、全历史 clone 失败，改为浅克隆及指定 commit 后成功。

官方来源：
- https://www.libraw.org/docs/API-datastruct.html
- https://github.com/LibRaw/LibRaw/tree/0.21.1
- https://github.com/sdcb/Sdcb.LibRaw/tree/43e9c1771c2b1387c2170e084e433d8bcef51cab

## 旧路径和独立新路径

| 项目 | LibRaw 0.21.1 原始默认 / 旧 LibRawDecoder | PV4 显式配置与记录 |
|---|---|---|
| 黑电平 | user_black=-1；user_cblack 四通道=-1000001；旧路径不覆盖 | 相同 sentinel，采用文件/相机配置；记录 unpack 后 Black、CBlack 四通道、模式行列及完整数组 digest |
| 白电平 | user_sat=-1；adjust_maximum_thr=.75 | user_sat=-1；adjust_maximum_thr=0 禁止按数据自动调整；记录 ColorMaximum；LibRaw 黑扣除/自动标度仍启用 |
| 白平衡 | user_mul=0；旧路径 use_camera_wb 或 use_auto_wb 互斥 | 读取并验证 cam_mul，先选择相机值或相机值×相对通道增益，再用 user_mul 应用；两个自动/相机标志均 false；记录 requested / normalized pre_mul 并核验 |
| 已渲染/已 WB 输入 | 旧路径按现有语义 | AsShotWbApplied!=0 明确拒绝；JPG/PNG 拒绝进入专业 RAW；不反套传感器增益 |
| 去马赛克 | user_qual=-1，由 LibRaw 选择；旧路径 interpolation=true | AHD user_qual=3；仅三色 Bayer，X-Trans/Foveon/非马赛克首期明确失败；half_size=false，four_color_rgb=false |
| 自动增亮 | no_auto_bright=false、bright=1、auto_bright_thr=.01；旧路径未覆盖 | no_auto_bright=true、bright=1、thr=.01（禁用下不参与），曝光校正关闭 |
| 相机矩阵 | use_camera_matrix=1；旧路径 true | true；实际解析的 rgb_cam（不是硬编码相机矩阵）；校验 RawData.IOParams.RawColor==0，即 LibRaw 会进入颜色转换分支；缺失配置拒绝 |
| 输出空间 | output_color=1；旧路径明确 sRGB | output_color=4，即官方 **ProPhoto D65**；不是标准 ICC ProPhoto D50，更不是传感器 RGB |
| gamma | gamm=[.45,4.5,0,0,0,0]；旧路径未覆盖 | [1,1,0,0,0,0]；读取处理后实际参数；颜色转换内生成的其他 gamma 系数由 LibRaw 推导；不执行 sRGB 逆编码 |
| 位深 | 8；旧专业模式选择16 | RGB16，转 float32=ushort/65535；解码已量化，不能宣称原生 float 解码 |
| 高光 | highlight=0 | highlight=0；不重建高光。scale_colors 和 convert_to_rgb 的 CLIP 均限制 uint16；记录输出饱和/零值样本计数 |
| 旋转/裁切 | flip=-1；crop/greybox=全图 sentinel | flip=0；外层按 EXIF 朝向显示，TIFF16保留朝向tag；crop/greybox全图 sentinel显式配置 |
| 降噪/补偿/外部profile | 多数 0/null | threshold/median/green_matching/FBDD/DCB/exp 全关闭，Aber=1，CameraProfile/OutputProfile/BadPixels/DarkFrame=null |
| unpack | RawSpeed=1，DNG SDK 编译默认；float转整数flag | RawSpeed=0、DNG SDK=0、options=2（官方 CONVERTFLOAT_TO_INT）、shot=0、specials=0、最大raw内存2048MiB、posterization=0、Coolscan gamma=1；记录 JSON |

完整 OutputParams / unpack 配置保存于每个 ProfessionalRawContract，随编辑版本、会话、导出配方落盘。

## 关键发现与最小适配器

`postprocessing_utils_dcrdefs.cpp::scale_colors` 先复制 user_mul，随后 camera-WB 分支可能再次覆盖 pre_mul。因此不能同时设置 user_mul 和 use_camera_wb=true。入场草稿存在此问题，修复后真实红增益1.25使两个相机输出变化，实际 normalized pre_mul 符合一次应用。gamma=[1,1] + no_auto_bright使 mem_image 的曲线按65536范围恒等，不添加显示传递函数。

`RawContext` 未公开黑电平、AsShotWbApplied、RawColor和 unpack 参数。ProfessionalLibRawAdapter 只对 Sdcb 0.21.1.7 / LibRaw 0.21.1 / x64 开放，使用该包公开结构与 UnsafeGetHandle；管理结构 OutputParams偏移与包内显式native结构相符后再访问；ColorMaximum/cam_mul/rgb_cam和官方 getters 一致才继续。真实 Sony/Canon验证通过。版本/ABI不合则失败，没有推测偏移或换库回退。

相机特征化来自 LibRaw 内嵌/内置/文件矩阵解析；保存数值及来源说明，**没有区分所有型号具体来自何表、没有DCP双光源或厂商私有profile**。UseCameraMatrix flag在open识别阶段即影响选择；本批未声称能选择任意厂商矩阵。独立空间逆矩阵仅是官方输出空间定义，不替代相机profile。

## 裁切结论

REAL_RAW_RESULTS.json：Canon饱和通道样本236231；Sony五张7711–14268。DecoderClipped=true表达管线含裁切阶段，不表示计数能追溯传感器每个剪切像素；0/65535也可能来自真实黑白或转换边界。线性数据仍可能失去黑以下和高光；曝光减小、软肩、浮点保留不能恢复这些信号。
