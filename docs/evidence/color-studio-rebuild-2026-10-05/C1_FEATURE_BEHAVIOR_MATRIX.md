# Capture One 可观察行为调查

调查日期：2026-10-05。C1 16.6.1.2962，Windows 11 Pro 10.0.26200，中文界面。授权样本 DSC04831.ARW；使用新变体 [2]，不覆写输入文件。工作色彩空间、屏幕实际 DPI 未得到可核对读数，仍为未知。下表为本机观察；文末另列通过官方公开帮助API取得的文档依据，两类证据不互相替代。

下表仅记录公开可操作行为，不推断 C1 未公开算法。所有“待验证”都不是通过。本地截图含用户照片，不纳入 Git 源码提交。

|类别|来源与观察步骤|实测结果|Pixel Tart 行为与差距|仍未确定|
|---|---|---|---|---|
|工作区|本机 C1 调整页，展开和滚动工具|RGB 直方图固定在右上；工具区独立滚动；底部为照片浏览区|本轮按用户新图设置左辅助、中央照片、右双直方图与工具；不照搬当前 C1 自定义布局|C1 工具排序、宽度持久化未逐项测试|
|白平衡|同一 RAW 的白平衡组|同像白平衡显示 6124 K、色调 −2.4；有吸管与复位|Pixel Tart 使用明确标注的相对色温/色调，不能声称传感器 Kelvin 重建|范围、输入边界、选取中性点的导出行为待验证|
|曝光|中性→点击曝光滑杆 +1.8→双击回零|照片整体变亮，直方图向右，高光接近端点；回零恢复|BasicTone exposure 使用真实线性增益；数学与 C1 不宣称一致|C1 极限范围、步进、导出像素未测|
|HDR 阴影|中性→阴影 69→双击回零|暗部人物及地面抬升；天空变化小于曝光；复位恢复|Pixel Tart 暗部权重曲线与端点保护，需新版实机比较|高光、白场、黑场及叠加顺序未逐项测|
|RGB 曲线|展开曲线→添加约124/124点→上移→工具复位|照片中间调变亮且RGB直方图变化；复位回直线。可见 RGB、亮度、红、绿、蓝通道及输入输出读数|Pixel Tart RGB与单通道控制点/形状保留插值，真实节点；没有冒称 C1 插值|C1 拖后读数无法从截图可靠判定，未填写虚假精确值；亮度通道效果待测|
|色阶|展开色阶→中间控制柄向左拖至显示0.2→复位|中间调抬升，照片与直方图变化；复位显示0|Pixel Tart 明确定义 gamma 中性1，允许不同公开参数约定|C1 数值含义与范围未推断，黑白端点与单通道待测|
|颜色编辑/肤色|打开工具并查看基本/高级/皮肤色调页|存在三页，未取样时部分控件禁用；脸部取样后暖色扇区激活，平滑度25；均匀度色相调至77，主要影响暖色区域，天空未出现明显整体偏移，随后回零|Pixel Tart 范围、羽化、H/S/L和肤色均匀化有真实处理数学|仅同一暗光人像作可见观察；饱和度/亮度均匀化、羽化边界、输出像素仍未量测|
|色彩平衡|尚未实测|NOT_RUN|Pixel Tart 全局/暗部/中间调/高光 OKLab 向量工具|C1 联动和重叠权重未知|
|细节|清晰度入口已见；未做像素对照|仅入口观察|Pixel Tart 细节节点使用不同半径结构/清晰度/锐化及边缘保护降噪|C1 各算法、噪声副作用未验证|
|局部调整|图层/蒙版入口可见|仅入口观察|本轮有颜色范围的非破坏节点及独立临时选区；空间画笔图层尚未实现|C1 蒙版羽化、复制删除、叠加次序待研究|
|查看诊断|调整前后观察固定RGB直方图|曝光、阴影、曲线和色阶均随结果更新|Pixel Tart 两张图读同一处理版本、影调映射共用线性Y边界|C1 统计阶段/色彩空间/抽样精度未确认|
|批处理|本轮未实际同步|NOT_RUN|Pixel Tart 选定类别同步+独立批量撤销，有数据测试|C1 选择性同步边界待测|
|输入输出|用户原位导入同组 ARW/JPEG|可选择RAW/JPEG，原文件保持|Pixel Tart真实RAW float→节点→编码；TIFF为派生测试|导出窗口实见所选单图、JPEG 8bit质量100、sRGB IEC61966-2.1、300px/in、固定100%比例，配方另含TIFF/PSD。未执行导出，因此实际编码/ICC/像素差异仍未验证|

## 本地观察证据

相对仓库路径：`artifacts/color-studio-rebuild-2026-10-05/c1/`。

- `DSC04831-neutral.jpg`
- `DSC04831-exposure-1_8.jpg`
- `DSC04831-shadows-69.jpg`
- `DSC04831-curve-raised.jpg`
- `DSC04831-levels-midpoint-0_2.jpg`

这些是 C1 研究证据，不是 Pixel Tart 新 Release 验收证据。

补充本地截图：DSC04831-skin-hue-uniformity-77.jpg、DSC04831-export-recipe-settings.jpg。前者为变体中皮肤色调取样和均匀化，后者仅导出设置观察。

## 官方公开帮助文档补充（非本机实测）

获取时间：2026-10-05 05:34–05:38 UTC。只使用 PowerShell `Invoke-RestMethod` 读取 `support.captureone.com` 的公开JSON，没有打开浏览器、WebView或桌面工具。先用 `/api/v2/help_center/articles/search.json?locale=en-us&query=…` 找到标题及链接，再读取 `/api/v2/help_center/en-us/articles/{id}.json` 的 `article.body`；以下不是仅依据搜索摘要。最初尝试带locale路径的搜索端点返回404，随后使用公开的正确搜索端点，未绕过访问防护。

所有条目均属于**公开功能和交互约定**，不是对私有处理数学的推断。官方文章持续更新，其中明确提及16.7.x的新增行为不归入本机16.6.1.2962实测，也不自动成为本轮Pixel Tart已实现能力。文档所述范围、默认值、键盘动作和限制仍需本机逐项操作，才能转为本地观察结果。

|编号/类别|来源证据（已读取正文）|文档可复现步骤与公开约定|Pixel Tart现状及对齐方向|未确定/未实测|
|---|---|---|---|---|
|D01 白平衡|[The White Balance tool overview](https://support.captureone.com/hc/en-us/articles/360002595838-The-White-Balance-tool-overview)；article更新2026-09-19|Color页→White Balance：Mode包含光源预设、Shot及Custom；吸管可选中性灰或未裁切白色。Kelvin文档范围800–14000，向右更暖、向左更冷；Tint去除绿/洋红偏色。初值来自相机，自动按钮与预设均为可观察入口；源文件不被直接修改|本轮仅提供明确标注的相对冷暖/绿洋红偏移，不能把相对数值标为C1 Kelvin；若将来提供相机白平衡，需实际输入配置与传感器白平衡数据|本机全部合法范围、步进、数值异常、灰卡吸管及导出尚未逐一测试；文档没有给出私有相机矩阵或白平衡算法|
|D02 JPEG白平衡边界|[Adjusting JPEGs with the White Balance tool](https://support.captureone.com/hc/en-us/articles/360002596478-Adjusting-JPEGs-with-the-White-Balance-tool)；更新2026-09-29|JPEG除Mode预设选择外可用其他白平衡功能；JPEG已处理过白平衡和颜色，进一步调整余量低于RAW|Pixel Tart应分别报告RAW与JPEG输入约定；现有显示域相对调色不能称为恢复JPEG丢失的信息|未在同一受控光源与色卡上量测C1 RAW/JPEG容差，不推断两者内部处理顺序相同|
|D03 色彩平衡结构|[The Color Balance tool overview](https://support.captureone.com/hc/en-us/articles/360002594857-The-Color-Balance-tool-overview)；更新2025-03-16|Master以及Shadow/Mid-tone/Highlight三分区；3-way和独立大色轮为同一功能的显示方式。分区有饱和度和明度；可与Layers配合局部调整，设置可存预设。文档描述高光调整可能轻微影响中间调，但不改变阴影|本轮OKLab全局/暗部/中调/高光节点属于独立数学实现；可对齐分区职责、连续过渡和单组复位，但不能称权重与C1相同|C1分区权重、色彩空间、明度保持公式未知；本机色轮尚未操作|
|D04 色轮动作与复位|[Adjusting color balance](https://support.captureone.com/hc/en-us/articles/360002594937-Adjusting-color-balance)；更新2025-03-16|Master中心为无偏移，向外增加饱和度；双击Master色轮回中心。左右方向键调色相，上下调饱和度；分区滑杆可滚轮调整。3-way修改同步到相应独立色轮；Master明度滑杆特意禁用以兼容旧设置|对齐方向为所有视图共用一个参数状态、复位可撤销、禁用有明确含义；本轮Pixel Tart没有声称已实现C1色轮全部快捷键|默认/范围/步进、连续拖动及其导出效果待本机测试，不把文档动作视为已通过|
|D05 高级颜色范围|[Advanced Color Editor](https://support.captureone.com/hc/en-us/articles/360002601978-Advanced-Color-Editor)；更新2026-09-26|Color Editor→Advanced→吸管取色；View selected color range将范围外去饱和用于观察；拖外框调整范围，内柄细调中心，Smoothness/Hue/Saturation/Lightness改变所选颜色。单图最多30个范围；可新增、删除、启停；可在图层蒙版内使用|本轮ColorRange节点支持真实范围、羽化、H/S/L及节点启停；临时观察overlay与编辑节点分开。颜色范围节点不等同空间图层；不要求复制30个上限|C1选区距离、Smoothness核、叠加公式和极限色域行为未知；文档没有证明内部使用OKLab|
|D06 肤色均匀化|[Adjusting skin tones](https://support.captureone.com/hc/en-us/articles/360002596077-Adjusting-skin-tones)；更新2026-09-05|Skin Tone吸管选希望保留的颜色，扩范围包含不希望的色偏；Uniformity向右将范围内的色相/饱和度/明度拉向参考色。Smoothness保证过渡；可用粗略局部蒙版保护其他同色区域。该工具也能用于非肤色|本轮SkinTone基于取样参考、颜色范围和均匀化；同色背景也可能命中，无人脸识别。现有临时遮罩不能冒充C1空间限制蒙版|已有本机暖色取样/Hue77观察，但S/L均匀化、羽化及输出像素未实测；官方描述不提供私有公式|
|D07 色阶输入/输出约定|[The Levels tool overview](https://support.captureone.com/hc/en-us/articles/360002602797-The-Levels-tool-overview)；更新2025-03-16|RGB组合模式调整影调，R/G/B独立通道可校正色偏。输入端点映射到输出值，默认输出0/255；文档强调输入/输出不一定是绝对黑白端点，给出输入高光220→输出244且其上值继续线性映射的示例。中间滑杆调中间调；Auto含可配置裁切阈值|Pixel Tart使用明确的0–1输入/输出与gamma中性1合同；其端点钳制不能称为C1完全等价，保持本轮文档和测试的语义。自动色阶未假称实现|本机只观察中间调0.2及复位；未量测端点外推、RGB重分配、Auto阈值和编码结果|
|D08 曲线通道与控制点|[The Curve tool overview](https://support.captureone.com/hc/en-us/articles/360002612118-The-Curve-tool-overview)；更新2026-10-03|RGB、Luma、R、G、B五曲线；横轴输入、纵轴输出，控制点调整影调与颜色。Luma文档描述为调亮度/对比时不增加饱和度；曲线可用于图层。16.7.4的Pick Neutralize Point是更新文章中的新功能，不属于本机16.6.1|本轮RGB/R/G/B曲线及可保存控制点采用自己的形状保留插值；不能把RGB曲线当Luma，不能因右侧有明度直方图就声称已实现Luma曲线|C1插值、Luma定义及极值行为未公开；新16.7.4吸管不计本轮实测或现有能力|
|D09 空间图层和蒙版|[Overview of Layers and Masks](https://support.captureone.com/hc/en-us/articles/360002601658-Overview-of-Layers-and-Masks)；更新2026-09-18|画笔、线性/径向渐变等蒙版限定局部；同层可组合曝光/清晰度/色彩平衡。可建空调整层、启停、重命名和删除；文档标最多16层。16.7.0组合蒙版、16.7.2预览缩略图、16.7.3同名层复制策略是后续版本说明|Pixel Tart当前只有颜色范围编辑与观察overlay，缺少完整空间画笔/图层保存及复制删除；该差距继续列为未实现，不能用颜色范围自动测试关闭|本机空间蒙版仅入口观察；后续版本功能不套用至16.6.1，不引入新的AI开发|
|D10 羽化与复制语义|[Add a feather to the mask edge](https://support.captureone.com/hc/en-us/articles/360002632858-Add-a-feather-to-the-mask-edge)（更新2026-09-01）；[Copy a mask to another Layer](https://support.captureone.com/hc/en-us/articles/360002615437-Copy-a-mask-to-another-Layer)（更新2026-07-26）|选中层→Feather Mask；Radius以像素计，默认10、上限100，Display Mask可开关，Apply/Cancel明确。渐变蒙版Apply会要求栅格化，之后不能再调原渐变。Copy Mask From复制到目标调整层，文档明确覆盖其已有蒙版；复制渐变可保留可编辑性|Pixel Tart颜色羽化是颜色距离边界，不能标为这类像素半径空间羽化；未来空间层须明确预览/提交、目标与覆盖行为，不增加空按钮|本机未测；这两篇公开交互不揭示羽化卷积核或实际栅格分辨率|
|D11 批量输出与配方|[Export Recipes](https://support.captureone.com/hc/en-us/articles/360021057158-Export-Recipes)；更新2026-09-29|浏览区选变体→Export窗口→勾一或多配方→Export；每个已选配方生成相应格式/尺寸/色彩空间的新文件，使用保存的调整、不改原件。可配置JPEG/TIFF/DNG/PNG/PSD、profile、输出锐化、元数据和命名；选中配方覆盖其他导出设置。引用素材需原件在线。16.5.2 Copy Original Files为不带调整原件复制；16.7.7配方文件夹属新版本|Pixel Tart快速导出使用冻结所选栈；发布配方有独立职责，配方未启用不覆盖手动设置。RAW调整后输出TIFF/PNG/JPEG，不伪称仍是可编辑RAW；当前格式集合并非C1全量|本机只查看设置，未执行C1导出、验证ICC/位深或多配方数量；DNG导出不等同保留相机RAW编辑语义。16.7.7配方文件夹不计本轮|

官方文档补充后的结论：白平衡、色轮、范围/肤色、色阶/曲线、空间蒙版与配方输出的公开约定已有可核对来源；**C1完整行为调查仍为PARTIAL**。没有新增本机拖动、数值边界、导出像素或完整批处理实测，也没有据此提高Pixel Tart的CODE、RELEASE_RUNTIME或用户批准状态。
