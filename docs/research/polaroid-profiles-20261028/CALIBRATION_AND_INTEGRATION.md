# 校准流程、最小补拍、后续接入合同

## 当前工具边界
`offline.py` 是隔离研究脚本，不是第二个产品渲染器。它只负责参数/LUT 候选、低复杂度拟合和审计，未来移交已存在的 V3 颜色组件，不负责另造光学/颗粒/边框引擎。

输入：明确 embedded ICC 的 8-bit RGB/RGBA PNG/JPEG（无 ICC 必须显式 assume_srgb）；Pillow/LCMS 相对比色转换一次至 encoded sRGB D65 [0,1]。RGBA 只取 RGB，本批不做透明照片产品效果。相纸 mask 独立。输出嵌 sRGB ICC，不读取显示器 ICC。16-bit/浮点 TIFF、RAW 需要现有已审计输入管线预处理；本工具未实现，不能 silently 降位深冒充高精度校准。拟合计算 float64，但源量化仍有 8-bit 局限。

候选颜色顺序：encoded sRGB→EOTF→中性 toe/shoulder 单调曲线→中性保持彩度（黑白省略彩度）→clamp→OETF→照片强度混合。黑位 .012、toe .025、shoulder .3 是既有创意默认的沿用与明确新连续模型，不是官方测量。无自动 WB/自动曝光/显示映射，无第三方 LUT 叠加。

## 实拍处理入口（已运行合成自检）
1. 填 `paired-intake-template.json`，每组相纸类型与世代固定；至少 development/validation/heldout 三组，scene、batch、parent capture 和原图 hash 均不得跨组。复制/裁切/调色版必须继承原 capture ID。
2. 扫描时关闭 AE/自动增强/自动白平衡/自动锐化，固定机型、灯源、分辨率、曝光、ICC；记录暗/白参考与重复扫描。保留相纸批次、生产日、保存史、曝光、照明/闪光、显影温度/时间、扫描时间和设备。
3. `prepare_pairs.py`：至少 4 个非共线对应点 + 2 个独立检查点；给定阈值先验验收 reprojection；在 scan 坐标里 binary valid mask 排除边框、反光、划痕、错位。不把这些区域混进误差平均。
4. 仅使用提前写入的曝光 EV、WB gains；不利用目标图逐张“最优化调色”。归一化对 digital reference 一次；扫描先统一 ICC。ROI 栅格在 homography 下采样（scan bilinear）并求 patch 平均。本版报告是 **patch-level ΔE00**，不是密集全像素配准评估；视差/非平面/复杂遮挡无法由单 homography 解决，应剔除且记录。
5. `offline.py calibrate` 比较 tone-only / tone+两个中性保持交叉通道基项 / tone+五个交叉项。tone 为正增量有界 PCHIP、二阶平滑惩罚；交叉项有界及 L2、灰轴上为零；无 3D residual fitting，避免小数据过拟合。输出模型 JSON 和评估（含分区 NO_DATA），按 validation P95 选型、heldout 不回流训练。
6. ΔE00 在相同 sRGB D65→Lab D65 计算；固定光源/观察假设，不宣称测得相纸反射率。报告 median/P90/P95/P99/max/mean 与 gray/skin/skin_highlight/highlight/red-orange/purple/blue-green/shadow/backlight，缺区判 fail/missing，不用总体均值掩盖。
7. 模板 P95 门槛为初始预算：overall 5、gray3、skin4、skin_highlight4、highlight4、其他6。它们不是已验证可达性能。**真实数据拟合前冻结**；重复性或噪声不达就失败/分析原因，不事后放宽。合成自检的 threshold=5 仅测试程序，不作为科学验收。
8. 人工盲评另走 blind-review-template.csv，场景/批次外分组，匿名 A/B 顺序、固定观看 ICC/亮度、不提前揭示模型，分别记录肤色/高光/阴影/真实性/偏好，未收集评价前写 NOT_RUN。

## 最小补拍清单（阶段性试点，不是准确性保证，不要求现在购买色卡）
先只选 **一个明确现代 i-Type Color 版本**，i‑2 固定机身/曝光/光源；使用已有灰阶/可用有权素材，不先申请采购。
- 3 个独立生产批次分别对应开发、验证、保留；每批 6 个独立场景（灰阶+红橙紫蓝绿、浅肤/深肤与皮肤高光、暗部、逆光等组合覆盖），不同批次不复用同场景以满足独立性。
- 每场景至少 1 张同场景 digital RAW/reference + 2 张即时重复曝光估计方差，共 18 场景 / 36 张即时片的最小方法试点。单张里必须有足够中性 patch；现脚本要求开发组 >=8 灰阶且 linear luminance span>=.45。
- 先在可记录、稳定室温显影，按该批官方说明等待；测量显影温度/时间和扫描时点，不只记天气。每张重复扫描一次估计扫描方差，不要用相纸边框白代替测量白。
- 无配对已有网络图只作视觉参考。若无法获取上述数据，保持风格近似可运行，不启动实测声称。
- 完成一个相纸端到端后，再补 **同批 SX‑70 Color** 在 i‑2 与原装 SX‑70 的同场景对照。镜头柔化/眩光/曝光差异独立标定，暂不强行加 camera RGB bias。600/i-Type 先检验共享误差，再决定是否需要分档。
- 黑白独立模型/光谱灵敏度测试；不能把 RGB→709 灰阶说成实测黑白感色。温度曲线必须额外分温度组与重复量测；官方冷绿/暖红仅用于提示，先不拟合精准响应。
- SX70+600 ND 与改装单列；记录 ND 型号/透射率/安装位置、相机改装版本、实际测光及闪光条件。i-Type 外供电/换盒不是原生。

## 后续产品接入：R5.3 完成后，复用现体系
1. 唯一左栏选相纸；右相机/相框/参数；中央实际结果，禁止新页面或布局改变。相框授权问题与色彩模型分开，不能拿此任务移走右框。
2. 在 `CandidateRecipe` 的新显式色彩组件版本保存 ProfileStableId/ModelVersion/ChemicalGeneration/CameraRevision/EvidenceGrade/InputDomain/DataHash/ModelHash/许可及源；旧 Algorithm3/LegacyCreative 不变。UI 用短句“风格近似·相机未标定”或获实测门禁后“实测拟合·固定扫描”。
3. 新配方冻结完整模型参数或内容寻址模型副本、版本/hash、source ICC、曝光/WB、amount/seed/随机快照。旧项目继续旧结果；新版本只通过明确新建/选择启用。缺档/hash 错误显式报错，不按同名加载最新模型。
4. 在既有源管线统一输入：Original RAW 的 scene→Display 已存在；CurrentEdits wide→encoded sRGB 已存在；只在 V3 tone 节点消费一次，不套旧 `InstantFilmPipeline.Apply`、额外显示映射、白平衡或旧色偏。第二曝光同域合同不变。
5. 色彩与空间分开：新颜色后复用 `CandidateSpatial` 有限 halo、optics、emulsion、defects 与 surface。随机沿现有 substreams/冻结 seed；稳定校色期间关闭。LUT 仅逐像素色彩，不放柔化、滚轴、纸面或随机缺陷。强度只对定义的照片效果，frame 不参与 blend。
6. 用同 frozen profile 驱动相纸缩略、实时预览、显影、保存重开、PNG16/TIFF16/JPEG/export 全部入口。全源坐标相同，monitor ICC 只显示不进文件；不能只做离线 cube 单测宣布接入完成。
7. 准入还缺：两套成熟相关实现的适用节点数值对照、真实 ICC/照片、灰阶和极端色、完整旧 golden/PV4/5/6、新旧 recipe 保存和跨进程 reopen/export、全模块回归/实际桌面验收。本批没有通过这些门禁，也没有运行/替换完整应用。




