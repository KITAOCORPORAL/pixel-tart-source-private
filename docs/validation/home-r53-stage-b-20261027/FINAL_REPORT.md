# 家庭 R5.3 / 阶段B / PhotoCraft 首批 — 完整诊断版，PARTIAL / 不发行
报告日期：2026-10-27 北京时间（Asia/Shanghai，UTC+08:00）；原始时戳若来自Windows/TRX/JSON保留原时区。

## 基线与保护
家庭真实仓库 N:/pixart/pixel-tart-source-private，原integration旧树三组未跟踪资料保持；未reset/clean/stash/rebase旧树、未强推、不合main。fetch后公司仍 `190d1304bd24480c5203467276b0acd48f719c8c`，公司产品 `4b5f73b7`为历史；独立树 N:/pixart/guangjing-r53-stage-b-20261027、家庭分支 home/r53-stage-b-20261027。旧家庭stage-a未作为开发基线。设计包已在家庭旧目录和Downloads实存/hash核对，不要求重下载。

本批第一产品提交 `9afc58996aa280d602142802a45f8344b143b60b`（场景曝光/缓存/缩略图/裁剪等），追加CMS最终产品 `b6eaa32e08bb37d79dcdfde7db17613a1558f5c6`。最终包只对应后者，前一中间包保留但不冒充CMS最终包。原公司E盘ZIP未假设同步。

## 用户现在能做什么
1. 在显示映射前调整SceneExposureV1，曝光前网格重用、连续有效中间帧、停400ms精细；GeometryV2/高频风险回退精细，不把所有图片承诺为90ms。独立mask快照实际缩略、状态、手动更新、清除全图、自己的锁定；EV不再隐式捕获选中层mask。调整/更新/清除/锁定可撤销、保存重开、切变体同步。基础RAW CameraGains在新页可操作/重解码/撤销，与显示后局部相对冷暖分开，不是Kelvin。
2. 快速cache own+lease+epoch，Clear不Dispose在途buffer。精细cache原先返回共享本体会被调用方Dispose破坏：已改cache owning、返回有账本reservation的独立结果，变体切回逐像素回归恢复。关闭/换图/换几何/撤销/迟到回填有保护。
3. 图层缩略改统一低分辨率真实stack prefix输入/输出，启用/顺序/强度/混合/mask均进pipeline；RAW从master基础不是最终图再次套效果。192px输入只解一次，最多64层、当前context/variant/revision栅栏；mask按源aspect/geometry，控件复用不显示旧node结果。复杂空间/reference完整高精度thumbnail和>64层目前不声称全部支持。
4. 普通PNG/JPEG/TIFF在新显式变体非破坏裁剪/quarter turn/flip/±15°拉直；GeometryV1默认结果保留，V2才double bilinear/黑色越界。源坐标mask、撤销、重置、重开、TIFF16/PNG16输出读回通过，真实RAW+SceneEV+拉直TIFF16检查0码差。未适配reference/空间detail栈拒绝，不暗烘焙丢层；alpha延用rendered入口白底策略，不能称透明文件无损alpha编辑。
5. 颜色管理窗口可创建显式新RGB颜色变体，embedded ICC/内容SHA、指定(重解释原码)与转换分开、无profile需确认sRGB、损坏/LUT/CMYK拒绝；实际working仍encoded sRGB供旧tools，output可选sRGB/P3/AdobeRGB兼容/ProPhoto兼容。实际LCMS float转换并嵌入完整ICC，不只是标签。保存重开/切变体/undo/state及VM导出都已测。
6. 显示ICC为workspace独立设置，复用Windows关联/手动配置/fallback，窗口位置/激活/3s关联变化更新；CPU精确sRGB→设备RGB，默认兼容off。物理屏/系统ACM/HDR是否二次转换仍未验，开关明确SDR需用户确认；不烘焙导出。没有统一低分辨率3DLUT/GPU替代。
7. 图层V2先菜单放行Darken/Lighten，旧三模式不变。Overlay/SoftLight/Color/Luminosity保留定义/数值原型但未准入菜单（同名算法不等价）；不套scene RAW，不加Fill。
8. 即时胶片相纸预览可用已解码RAW基础显示，原图/当前recipe/变体数不变；自由画布新增透明度和显隐/undo/持久化，现有策划自有副本→画布衔接与15板块保留回归；不声称完整R3资源/所有画笔/策划拼贴预览卡已完成。旧新手教程保持退役，没开发替代。

## 家庭性能和数值
同家庭机器、同Canon TNAN8886.CR3（b7d35851…，5496×3670）、同800px/EV序列：旧每次FastBase+局部 1684.22/1601.15/1590.97ms；新cold prepare26.54ms、热87.61/87.92/81.74ms。持续30次/30ms输入，首有效114.36ms、13有效中间帧，含拖动最终精细3357.81ms。不是公司85ms套用。误差mean0.6409/P951.7302/max12.6564（RGB8），固定预算1.5/4/32未放宽。最终导出不使用快速图。2x高频反例mean9.643/P9523.478失败保留，高频精细回退误差0；启发式不保证所有照片/极细mask，回退会慢。
8轮工作区/几何/undo/变体/关闭账本每轮0，managed约16.9MB末期稳定；WorkingSet含native/GC波动，约峰值2.2GB（测试含native与多master/对比副本），不把峰值包装成产品硬预算。PERFORMANCE.json是纠正measurement-only scope后重新跑的同机结果，原污染frame列表保留MEASUREMENT_CORRECTION.json。
GeometryV2对固定OpenCV4.10/Pillow12.3实际运行，RGB8 max0.236 / 0.000156；量化/边界另记。sRGB/P3/Adobe-compatible/ProPhoto-compatible TIFF16/PNG16共8输出嵌入SHA与实际码值检查0差，授权Canon512px派生也走新CMS闭环0差。Windows WIC独立RGB8 in-gamut max2.5487/255，outgamut max6不同不称等价；LCMS2.19/Pillow不同版本另记，不当独立算法。保存一致仅是pipeline一致，不是肤色/相机/相纸色准改善。

## 本机验证
权威整合：Core55PASS/0FAIL/4NOT_RUN，WPF52PASS/0FAIL/1NOT_RUN。另有策划15板块6PASS、单一Canon RAW TIFF读回1PASS、独立create/reopen两个testhost各PASS；去重合并Core62PASS/0FAIL/3NOT_RUN、WPF53PASS/0FAIL/1NOT_RUN（同名跨进程只计一次，原run均保留）。最终时序专项另1PASS是同名重测，不重复加总。66份开发TRX全部保留摘要，开发失败不藏起来；详见TEST_SUMMARY/NONPASS/DEVELOPMENT_RUNS。
NOT_RUN：长笔/24MP专题需独立性能输出未在此整合环境启用；两native旧参考kernel完整矩阵未带入家庭（已核对固定源、旧56 scalar fixture重放不是家庭编译两个完整引擎）；旧native样例生成器未启用，实际本机构建/启动独立完成。四项公司历史授权任务仅继承原因，不能说家庭通过。鼠标/DPI禁止工具条件仍NOT_RUN；离屏图/testhost/启动不冒充。

## PhotoCraft实际借鉴和边界
已实际阅读固定7ac7b8…10文件与licenses/NOTICE/ATTRIBUTION，采用profile原字节身份、显式document→display/output separation、CPU精确基线与近黑测试，未复制Rust/商标/字体/图标、未引CMYK/替代LibRaw。详见原NODE_COMPARISON追加段、PHOTOCRAFT_REFERENCE.json，未制造平行研究目录。
复用随包LCMS2.12已有hash/MIT通知。旧research ControlledIccLinearV1与PV7/syntheticICC门禁保持；新RGB matrix/shaper子集为诊断版，不吹通用CMS。
ColorV1只放行TIFF16/PNG16；JPEG实际读回MAE5.4156/RMSE7.8625/max49超预算，阻断输出，不放宽。出版水印/resize对新ColorState拒绝防误贴sRGB；即时胶片CurrentEdits拒绝新状态防丢ICC，原图/兼容路径继续可用。ProPhoto编辑工作域、ICC LUT/BPC/完整意图、物理ACM/HDR/多屏色准、软打样/超色域警告、全相机/肤色色差仍OPEN。

## 家庭完整包和启动
`N:/pixart/Guangjing-R53-Home-20261027/Guangjing-R53-Home-CMS-StageB-Diagnostic-win-x64.zip`（ZIP_IDENTITY.json给最终SHA/大小）；同名目录下 Start-Guangjing-Diagnostic.cmd / .bat，或.ps1 -DataRoot 指向带诊断marker的隔离根。所有模块保留、不是Setup，UserData/原RAW/开发reference runtime/第三方研究源不入ZIP。264 runtime/native/resource资产逐项齐全，组件pack/test DLL SHA实际相同。
真实启动PID45720、Responding=True、完整shell标题“光镜·完整范围诊断预览·未完成验证/不能发行”，只是OS级启动证明；不做鼠标/DPI验收。本机完整publish0error，已有分析器warning保留日志，不宣称全库lint0warning。

## 下一步
沿原B/C/D/E继续：颜色管理wide editing和显示物理闭环、JPEG/出版ColorState适配、柔光/颜色明度双成熟实现数值；即时胶片R3实际资源与授权边界、自由画布画笔/显隐管理/输出、策划拼贴预览/关联卡。没有把无依赖开发全部等在外部样本/鼠标签字，但本批仍PARTIAL，不能发行，CalibrationEligible=false。

## 最终包身份
完整ZIP 75,001,388 bytes / 328 files，SHA256 `c40fda0ecd4d594ec77e11b25a6ff0560e04cb870d32ddbbea5061ac6a6cc7be`，全entry逐项hash读回。ZIP内validation为打包时快照，本段/推送读回是包外最终封存，不把报告提交说成产品DLL变化。
启动：N:/pixart/Guangjing-R53-Home-20261027/Guangjing-R53-Home-CMS-StageB-Diagnostic-win-x64/Start-Guangjing-Diagnostic.cmd（或同名BAT）。普通推送及远端最终SHA见PUSH_READBACK.json。
