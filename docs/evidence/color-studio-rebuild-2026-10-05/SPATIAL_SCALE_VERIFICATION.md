# Details / Film 空间尺度量测与修复

日期2026-10-05。CODE与数值TEST有本次证据；RELEASE_RUNTIME由主任务同一发布实机另记。USER_VISUAL_REVIEW=NOT_APPROVED。

## 输入与方法

用户授权 `DSC04831.ARW`，LibRaw真实16位sRGB解码7028×4688；原件hash见三份JSON。每个效果分别在原尺寸、1600×1067、800×534运行**相同参数**，以生产RAW nearest-center代理契约定位相同原图区域。全图逐像素实际渲染，误差采样约6.5–7.1万像素；记录OKLab L平均、P95、最大绝对差，及encoded RGB平均差。没有将空间非线性过滤说成与降采样可交换，也没有仅用文件存在判断正确。

证据：`artifacts/color-studio-rebuild-2026-10-05/spatial-before/SPATIAL_SCALE_MATRIX.json`、`spatial-after/SPATIAL_SCALE_MATRIX.json`（中间诊断）、`spatial-final/SPATIAL_SCALE_MATRIX.json`。最终TRX `spatial-final-gate.trx`，**34通过/0失败/0跳过**，含真实空间量测、20工具测试、11旧Film测试、2新增尺度/兼容测试。

## 真实根因与修改

1. Film原grain和所有texture直接用渲染图片x/y，同seed在预览与full重新生成不同相位；Paper低亮处线性±.06使暗部在全黑和明亮纸纹间跳变，差值大。新增 `PixelTartFilmSettings.SpatialVersion=2`：长边1600参考的连续坐标，high grain改连续SmoothNoise，texture共用相同坐标；surface按原linear Y+.02淡入，避免深黑区域大块亮斑。
2. Bloom/halation旧半径夹2…8输出像素，全图相对扩散范围被截短。V2按图像短边规定sigma，连续Gaussian权重；无对应效果时不计算空滤镜，避免颗粒单独开启也做大图两次blur。
3. Details旧整数半径在800强制1像素、full不同近似；锐化阈值硬开关产生阈值附近跳变。改分数像素面积积分box，参考半径连续缩放；锐化阈值用smoothstep渐入。
4. 降噪旧半径full夹4，代理又按整数取邻域。改固定5×5采样位置，间距按1600参考尺度，亚像素以双线性Lab取样；bilateral亮度权重保留，CPU邻域成本固定。降噪full耗时23.2s→17.8s（单次同机记录，不当通用benchmark）。

修改文件：`Core/Services/Projects/PixelTartFilm.cs`、`ColorStudioToolProcessor.cs`、`ViewModels/TetherReferenceModeViewModel.cs` 的SetFilm；新增 `ColorStudioSpatialScaleTests.cs` 和WPF显式编辑迁移测试。

## 兼容及误差预算

旧Film JSON缺省SpatialVersion=1，所有原数学路径保留。仅实际Film编辑（包括选Profile/启用）通过SetFilm升级该Film为2；加载、重新打开及应用既存snapshot不偷偷升级。Undo可恢复V1。新增测试逐像素验证缺省V1和显式V1相等、JSON V2保存、所有texture在对齐图像中心坐标处跨3倍分辨率相位精确一致。

新Details尚未发布为旧产品数学；本轮修改没有替换原Develop结构/细节。Film V2与旧V1观感不同是显式版本，而非旧方案无提示重写。

容差是该照片的工程回归预算，不是用户视觉批准或所有照片保证。L范围0…1：

| 计算 | Mean ΔL上限 | P95 ΔL上限 | Max ΔL上限 | 理由 |
|---|---:|---:|---:|---|
| Details 800交互代理 |.005|.015|.07|大于8倍缩小丢失高频，允许局部锐化差异；停止后收敛1600 |
| Details 1600稳定代理 |.003|.009|.045|平均/95%低于约1% L；同时限制少量边缘峰值 |
| Film两个代理 |.003|.008|.04|grain/texture必须保持相位，光晕差异只来自邻域采样；不能接受原Paper的大范围错位 |

各项同时断言均值、95%和最大值。更严格的“全部输出像素差0”不适用于空间滤波代理。用户检查锐化纹理时仍需实际倍率或导出原尺寸判断。

## 修复前后：1600稳定代理对full

| 工具及参数 | 旧Mean/P95/Max ΔL | 新Mean/P95/Max ΔL |
|---|---|---|
| clarity30 + structure30 |.000487 /.001370 /.006257|.000309 /.000799 /.004996|
| sharpen50 radius2 threshold.01 |.001960 /.008646 /.029136|.001424 /.004774 /.022333|
| luma/chroma noise35 |.001670 /.004606 /.015168|.000461 /.001194 /.008155|
| grain70 size35 seed42 |.002499 /.006200 /.018521|.000183 /.000670 /.003265|
| Paper surface80 texture80 seed42 |.071957 /.256592 /.366578|.000054 /.000163 /.000793|
| bloom70 halo70 |.000037 /.000178 /.016573|.000020 /.000110 /.002763|

800最终：clarity mean.000749/P95.002044/max.009979；sharpen .003157/.010488/.044988；denoise .001424/.003887/.013644；grain .000601/.001625/.005610；Paper .000120/.000386/.002034；bloom/halo .000045/.000254/.005456。全部预算通过。

## 输出与实机边界

同分辨率预览和编码前输出仍共享同Core栈；JPEG本身是有损编码。因Details数学改善使平滑合成图的DCT最坏尾差6→7，不直接放宽单一常数来掩盖差异：WPF测试新增独立WIC质量95编码oracle，要求实际快速导出逐解码像素等于对完整处理结果独立编码（精确差0），同时保留均差<1.5和最大尾差日志。透明合成由独立格式测试覆盖。

本项已完成真实数值闭环；仍需主任务新Release实机开启Film/Details并检查参数、重启及输出，不能把本项TRX当该runtime步骤。

第八集中WPF编译后的 `studio-final-changes-focused.trx`：本模块的JPEG独立编码oracle、float scRGB同色、Film版本显式编辑/Undo恢复测试均通过；整份聚焦29通过/1失败/1跳过，唯一失败为另项工具组布局位置，不能把整份TRX称全绿。本文件只记录已实际通过的处理验证。

最终全Core（含空间版本与RAW ICC接口）`core-full-spatial-final.trx`：**1571通过、0失败、6跳过**，50s。6跳过含本项真实空间opt-in（已另以`spatial-final-gate.trx`实际执行通过）、5RAW语料opt-in及GPU/performance可选项；未将跳过记录为通过。
