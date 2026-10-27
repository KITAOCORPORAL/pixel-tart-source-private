# 光镜阶段B颜色管理闭环 + 即时胶片/出版 · 家用交付

**限定闭环已实现并自动验证；R5.3整体与阶段C全移植仍PARTIAL。完整诊断应用，不能发行，无安装包。**

## 基线与保全
2026-10-27 America/Phoenix。主机China Standard Time原始日志已跨10月28日，UTC/epoch原值保留，不冒充客户端日期。

fetch + 独立ls-remote核对 `home/r53-stage-b-20261027` 起点仍 `047e51bf7ad156aaf94427cad93dd45380b6fbd1`，无后续提交；继承产品 `b6eaa32e08bb37d79dcdfde7db17613a1558f5c6`。在已有独立树 `N:/pixart/guangjing-r53-stage-b-20261027` 继续，不从stage-a回灌旧代码。不重做已完成场景曝光性能/cache租约/缩略前序/裁剪/首批ICC。

当前最终发布源码 **`b843670653dcb1899f77b873e31a11455d754f36`**。主要产品53062dc1，出版可达c0ebd894，后续film JPEG/出版数值及完整导出命令验证1e18c7f2/002a54dc，最后b8436706修实际ICC导出文案和同目录保护测法。证据与脚本另提交；最后远端HEAD见PUSH_READBACK / 外部FINAL_REMOTE_READBACK，不把文档HEAD当二进制源码。

22其他工作树只读核对；原主树 `N:/pixart/pixel-tart-source-private` 211项未提交清单保持。观察到 `C:/Users/Admin/Documents/ChatGPT/破甲/GUANGJING_RAW_C1_FEASIBILITY` 在本批期间外部前进到dbd19d66，本agent未写入/回退该树，其余旧树HEAD不变。不reset/clean/stash/rebase、不合main、不强推、不清理旧ZIP/数据根。PRESERVATION如实记录该并行变化，未声称全部旧HEAD没变。

## 1. JPEG根因与实际开放
原失败保留jpeg-before和JPEG_SAMPLES.zip：WPF BitmapFrame的profile解释在FormatConvertedBitmap读取时生效，旧验证拿已解释的RGB与输出编码RGB直接相减。P3平滑图错误读回MAE5.398/RMSE7.810/max50；IgnoreColorProfile后raw编码MAE0.473/RMSE0.724/max3。不是RGB/BGR颠倒或提高门槛修复。

已核对RGB16量化 `/257`到BGR8、JPEG质量95/色度4:4:4、无额外input profile转换、ICC字节身份、编码域与统一sRGB域分别比较。PNG16/TIFF16四目标空间每个码值读回0差，排除LCMS输出转换错误。独立Pillow12.3 + 实际libjpeg-turbo3.1.4.1，same RGB/same quality95/subsampling0，16组合及自然派生图WIC/Pillow产物经独立解码相同；不是两个不同标签文件假比较。运行feature版本3.1.4.1与初始查询3.1.3已在REFERENCE_MANIFEST区分，未假称运行3.1.3。

|输出目标|JPEG实际状态|位深/默认|
|---|---|---|
|sRGB|已放行，真实转换/嵌ICC|8bit，默认交付|
|Display P3|已放行，真实转换/嵌ICC|8bit，显式选择|
|Adobe RGB公开原色兼容|已放行，非Adobe专有profile拷贝|8bit，显式选择|
|ProPhoto/ROMM公开原色兼容|验证通过，但仅显式选择并警告|8bit，不推荐普通交付，不设默认|

灰阶、平滑渐变、饱和块、高频分别保留统计。自然图仅此前授权Canon 512×342派生TIFF，SHA EEBCE771…DED4B，不冒充相机色准或全分辨率。JPEG编码MAE sRGB .745 / P3 .691 / AdobeCompat .736 / ProPhotoCompat .611，统一sRGB色彩MAE .745/.769/.775/1.059。仍使用MAE5/RMSE15，未放宽；JPEG不是无损，也不承诺任意极端高频均达预算。

交互导出结构检查：尺寸、实际位深、JPEG444、ICC byte identity、逐行解码完整性；`PixelComparisonPerformed=false`，0诊断字段不是伪造像素误差0。完整逐像素重新渲染保留显式 `Inspect` 验证入口，不永久封死格式。导出对话框/选择目录/状态/receipt实际显示目标ICC，不再固定sRGB；只换ICC标签仍禁止。

## 2. 文档结果 → film → 出版
新增owned `DocumentRenderResultV1`：float32RGB、32bit/channel、ICC字节/SHA、独立straight alpha、几何/方向、编辑版本ID。不含monitor profile；alpha不明确时不能丢弃或隐式白底。新创建照片ICC变体逐行检查透明并拒绝；已存V1旧白底策略兼容保留，未当贴纸合同。

即时胶片Original/CurrentEdits冻结来源版本ID + 完整DocumentColorState/LocalEdits/Geometry定义，full document结果明确转换到encoded-sRGB film域，相纸/边框也解释sRGB；不会从已显示器转换预览截图显影。输入用新InstantInputRecipe v3，旧recipe v1/v2 null字段不序列化，旧DecodeKey不暗迁。配方/随机seed冻结，取消、失败或换目标不提交半成品，重复点击不添半成品；重拍从冻结原输入开始，旧变体保留。

真实自动产品流程：合成P3 ICC TIFF→暗室曝光→当前编辑V3显影→新变体→session保存重开→JPEG8/TIFF16成功→出版缩放+文字水印→P3 JPEG/TIFF→服务层Adobe兼容PNG16→二次sRGB JPEG。每个出口检查实际ICC/位深/尺寸，原图/原变体未改。另新testhost进程重开同session，像素SHA `39C62ED9…6E651` 与保存前一致（WORKFLOW/NEW_PROCESS_REOPEN），不是同VM伪重启。

出版进入的是已完整渲染、hash验证的成片，实际embedded ICC定义码值→encoded-sRGB float32 bilinear缩放→encoded-sRGB水印合成→目标ICC转换/编码。WPF只生成8bit水印颜色/覆盖，照片缓冲保持float/16；不是线性光水印，也不声称8bit字形边缘有16bit精度。独立中心bilinear2像素数值≤2 U16；透明水印公式和WPF覆盖量化另测，不压白。Exif方向RGB与alpha共同规范化。未适配旧PV4出版路径保持原限制；出版列表仅全部为managed receipt成片时提供P3/Adobe/ProPhoto，普通输入不展示未支持能力。

所有Managed出版出口，含task service、预览、第二次出版、快速导出intermediate receipt的颜色身份均接通，取消/异常只删本次唯一临时文件，不删现有目标；source hash变化拒绝。未删除旧安全门禁。新路径精简验证不会做第二次整图渲染。

## 3. 宽输出与宽编辑明确分开
V1仍encoded sRGB：LCMS float输入转换本身保留负/>1（P3绿→sRGB出现-2.90686/1.01827），未编辑回转P3误差约3e-7；旧BasicTone/WhiteBalance EncodeGamut、Levels clamp和Curve表域会压缩/裁切，USER_EXPORT_AND_GAMUT保存逐工具结果。输出ProPhoto不能恢复已经被工具裁切的颜色。

新显式ColorState Version2实际建立公开ROMM原色、D50白点xy(.3457,.3585)、gamma1的线性ICC profile，input→该工作域LCMS转换，float32曝光/Normal opacity源坐标mask/几何在线性域实际计算；output直接working→target，preview独立转sRGB且逐行有界面积采样。不是修改字符串。working ICC SHA按版本核对，不允许给旧工具乱贴新工作名。

**本批最小集合只有线性曝光、Normal强度/蒙版和几何**。白平衡、曲线、HSL、色轮、色阶/预设、其他混合模式未适配到V2，在mutation/definition校验拒绝，不静默近似。V1/PV4/5/6默认数学/旧DecodeKey保持；PV7/research ICC默认关闭不动。P3→线性ProPhoto→P3 maxfloat8.82e-8；WIC独立8bit同profile原色max.944/255，量化/负值裁切不同不报精确全等。PhotoCraft/LCMS/RawTherapee固定参考、许可证与数值边界见NODE_COMPARISON/LINEAR_WORKING_WIC。

## 4. 混合模式
保留Darken/Lighten及旧Normal/Multiply/Screen结果，本批诊断菜单/Setter实际放行Overlay、SoftLight、Color、Luminosity，中文显示。使用W3C/PDF SDR domain定义，调节层语义：`before + opacity*mask*(blend(before,fullEffect)-before)`；输入0..1限定，原HDR的未作用部分由caller保留；不是Photoshop像素层sourceAlpha/Fill，也不声称scene/HDR混合。

蒙版/强度/负/>1 opacity0、预览/保存重开/完整无损输出、source alpha独立均增量验证。PhotoCraft固定float32公式独立numpy标量port四模式maxfloat1.62e-7（非完整Rust运行）。PDFium固定a8432342源码，在隔离TCC运行原整数Blend/Lum/ClipColor/SetLum：Overlay/SoftLight max1.400/1.488码，Color/Luminosity max1.931/3.434码，量化差保留，不改门槛或冒称bit exact。研究源码/二进制不进产品。原Pillow SoftLight另公式差异记录保留，不拿不同公式硬配。

## 5. 导出总耗时与失败恢复
同一家庭机、同一512×342授权派生图。封存当前实测：用户完整ExportSelectedCommand（含冻结、源身份、渲染、编码、读回、hash/清单提交）sRGB150.65 / P3168.73 / AdobeCompat136.65 ms。直接WriteAsync约50–54ms，分项渲染约33–36ms、编码13–14ms、读回1.7–2.3ms；其与完整命令开销不是同一指标。显式Inspect额外约36–42ms，产品默认secondRender=0。不是公司耗时，不是全分辨率、鼠标交互或通用FPS承诺。

测试覆盖行编码中取消、编码后取消、输出存在、空间不足IOException0x80070070/encoder故障注入、冻结导出期间definition切换、关闭对应取消信号、出版service取消/临时清理；无假成功文件、原像素未改。透明边缘新增四模式Alpha0/64/128/255与PNG重开独立补测PASS，颜色处理不再乘第二次源Alpha。**没有真的填满磁盘；native窗口在导出中关闭未验**，明确区分。完整命令首次测三个ICC同目录被原“同名不覆盖/不能恢复不同调整清单”安全保护拒绝，原失败TRX保留，最终各空间新目录测量，未放宽覆盖规则。

## 6. 本机验证与原生边界
最终source b8436706，Release win-x64 self-contained、WholeAppDiagnosticBuild=true、PhotographerTrialBuild=false。29发布DLL绑定testhost读回hash：

|范围|通过|失败|未运行|
|---|---:|---:|---:|
|Core受影响回归|68|0|0|
|WPF主受影响回归|55|0|0|
|透明边缘独立补充|1|0|0|
|新进程独立重开fixture|1|0|0|

曾缺清单的相关PhotoGrade授权测试已给固定旧授权Canon派生TIFF manifest并执行PASS；不扫私人目录。无关历史24MP/长笔画/全native reference/新规划包专项不为刷数重跑，见NONPASS。早期构建错误、2个blend夹具失败（TIFF旧兼容预览/未await新模式渲染），完整命令冲突和修复均保留DEVELOPMENT_RUNS/RAW_EVIDENCE，不删除挑绿。scene exposure性能/cachelease/缩略prefix/crop原专项不重做。

真实EXE已启动，响应窗口、主程序/WPF(Core在主程序集)/Core及三个模块hash/ProductVersion对应最终源码；OS空窗口CloseMainWindow退出与CMD launcher实启已验。**用户AGENTS禁止Computer Use/Browser/WebView/CDP，未启用**。真实鼠标ICC→edit→film→save→正常UI退出→重启→出版未执行；物理多屏/ACM/HDR/DPI仍NOT_RUN，不用VM/离屏图替代。images目录3张是WPF RenderTargetBitmap，明确OFFSCREEN不是桌面截图。

显示默认安全路径off保持，手动/关联profile不可用回退已有状态保留。应用显示变换只在presentation，系统ACM/HDR责任仍未实机证明，不称自动校准；没有把monitor转换烘焙进成片。

## 7. 阶段C继续范围与交付
R3体验已继续，不只修接口：唯一分组相纸选择器移动到左侧即时胶片入口（不再左右重复），右侧相机/框/输入/seed/随机/强度/进阶、取消/显影、已显影只读冻结来源摘要、无动画显影和重拍闭环。完整R3资源、真实扫描/联名授权/175项原型核对仍未全移植，未知分发权不装包，STAGE_C_STATUS不写完成。既有双重曝光/效果/本次透明水印不冒充所有界面/画笔能力。

完整包 `N:/pixart/Guangjing-R53-Home-20261027-CmsClosure/Guangjing-R53-CMS-Closure-Final-Diagnostic-win-x64`，启动 `Start-Guangjing-Diagnostic.cmd`；同名.zip身份见ZIP_IDENTITY。全部现有模块/策划15板块保留，旧教程不恢复、不做替代。290app文件、264声明运行资产缺失0；不含UserData/私人照片/数据库/研究二进制/设计联名资源，无Setup。

合成P3显影项目/线性V2项目由 `Prepare-CMS-Synthetic-Samples.ps1 -Destination <全新绝对目录>`重绑包内图路径，需PS7.5+保留日期字符串；原始snapshot/receipt保持不改，不能把绝对路径旧证据说成跨机授权。`.ptplan v1`仍不含自由画布。手动步骤MANUAL_RETEST。

新增格式为显式ColorState2/InstantInput3，新版读取/拒绝未知版本；旧项目无字段不暗迁。成熟参考固定/许可证清单不作为相机色准证明，CalibrationEligible=false。未引入新产品native依赖，原LCMS2.12 MIT及existing notices保留。

产品与证据分别提交，普通push home/r53-stage-b-20261027、独立ls-remote读回，详PUSH_READBACK。HANDOFF.MD已更新下一入口：V2工具逐项适配/完整R3授权资源和交互，再继续D/E；不把本轮写成R5.3全部完成。
