# R5.3暗室交互交付 + 阶段B首项 — PARTIAL / 不发行
日期2026-10-27 America/New_York。

## 本轮实际实现
- 持续覆盖不再每次输入取消：1在途+1最新pending，同上下文单调发布中间结果，换图/变体/节点/关闭严格取消；保留旧有效覆盖，停笔最终最新。max768，栅格最多4row workers。
- 快速/精细同一编辑定义：800px精确面积基础图最多2缓存，完整身份/版本/参数/几何key；局部工具快速近似、停400ms精细。原nearest候选超误差已拒，预算不放宽。导出原全分辨率路径。
- 精细点效果复用省去每带临时stack/analysis/显示适配；普通行带最多4并行。逐位输出一致测试。RawLocalProfile记录索引/基础RAW/工具/蒙版，累计并行区段不作wall相加。
- 四变体所选ID加载槽、每张完成立即发布；同步缩放平移不改主变体。图层效果/蒙版缩略及选择态、节点绑定、直方图/波形/RGB/矢量页、窄工具栏换行、独立C裁剪上下文自由/比例拖框/旋转/翻转/重置/完成；画笔解释移Tooltip，无新教程。
- 阶段B **已启动并实现SceneExposureV1**：基础线性RAW曝光后/旧PV4显示映射前的蒙版EV，Normal线性增益，不套sRGB混合、不重复相机WB。显式可选版本，旧PV4/5/6默认null不变；未删除诊断开关。

## 最终组件实测
- 复杂覆盖24MP域20笔20k点：首帧445.64ms，连续输入36有效中间帧，最终99，并发1；不是物理鼠标测试。
- Canon20MP快速热局部81.53ms，连续输入首个快速85.03ms，精细2.98s；基础冷面积图1.56s单列/预热。快速/精细8位尺度mean0.000539/P950.002341/max0.12972；固定容限1.5/4/32，非逐像素相等。高频合成mean0.0695/P950.1753/max6.963。
- 四变体首张3.50s、全部13.04s、热缓存11.69ms，逐帧已可见；不是全部结束才显示。热/取消最终专项见PERFORMANCE.json。
- 24MP20k点双层精细42.04s→16.85s，TIFF1.15s；短/长笔约2.7s。仍非实时，不隐藏精细慢项。输入/输出各288MB、采样峰值/累计分配单列，不把进程峰值当产品硬上限。
- 场景B真实样片（-1.5EV蒙版）相对旧显示mean绝对变化0.003482/max0.087843（归一化sRGB），说明改变而非色准改善；负值/超范围线性比例及56组固定darktable/RawTherapee标量对照max3.98e-7。编码/重开相同只证明一致，不证明相机profile或物理色准。

## 交付
产品 **4b5f73b726c29b2117e0119333b44f0ef20c0beb**，基线bc80e556119c9c7b1366ddd94816ca2669ed44b1；测试追加提交与远端以PUSH_READBACK.json为准。分支company/r53-stage-a-completion-20261027。
完整包E:/Kitao-Deliveries/r53-interaction-stage-b-20261027/Guangjing-R53-Interaction-StageB-Diagnostic-win-x64.zip；同名目录CMD/BAT启动，WholeAppDiagnosticBuild=true / PhotographerTrialBuild=false，包内UserData隔离，不改策略，完整所有模块/策划15板块，不是Setup。
294文件逐项SHA读回，74359136bytes，ZIP SHA256 **b052a09cdcb4edeb6d021fac0b5193c73fe6e104c377eda3efef31e9bc2e768e**。APP_FILES.json含类型/大小/hash，无UserData/数据库/原RAW。原TRX/失败/脚本RAW_TESTS.zip，研究GPL源码/编译器不进发布包。

## 边界
A交互/界面已实际交付但仍有拉直、全栅格裁剪、设计精修及真实鼠标/DPI未验；不宣称全部R5.3视觉完成。通用ICC/profile/宽色域不再无限扩大A，归B后续，B本轮仅首项实际实现，C-E未开始。发行/科学准入仍OPEN，CalibrationEligible=false。
四项历史授权专项真实原因NONPASS.json保留，本轮不重复无关参考/整批。最终定向Core17PASS/0FAIL/0NOT_RUN、WPF56PASS/0FAIL/1NOT_RUN；原4项历史未运行不冒充补跑。完整范围以TEST_SUMMARY.json为准，不把严格runnerNOT_RUN写全绿。离屏截图/testhost/OS启动均不作真实鼠标DPI。

## 推送读回
产品4b5f73b7、独立测试12eee94e及证据9e7053b3已正常push；ls-remote读回LOCAL_HEAD=REMOTE_HEAD=9e7053b35578bbf6706839e2ce39d1bf1b92054a，工作树干净。后续仅封存读回，不变更产品src。
