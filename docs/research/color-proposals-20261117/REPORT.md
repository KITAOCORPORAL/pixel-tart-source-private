# 光镜色彩研究当前结论 · 2026-11-17

**本轮研究与方案准备完成；算法未实现、产品未接入、默认未改。** 原始历史时间字段保留，当前报告日期按本聊天客户端；数值与状态的来源各自绑定提交。

## 摄影判断

优先评审HL-A独立高光压缩有理由：摄影师可把EV与亮部压缩分开决定；在暖纹理EV+1中，固定中间调匹配后仍比原新增裁切保留更多线条与暖色。**但现有V1还不值得默认推荐或立即接入。** 肩部EV0较柔，同时高亮meanY约−10.93%、相对梯度约−10.44%，皮肤体积/光泽有压平代价；合成中性亮纹理相对梯度约−39.5%。EV0中性白1→.875，不是曝光恒等；贴顶值压离白点不是恢复旧细节。

现有亮部证据只支持分路径定位：RenderedSrgbExposure在最终逐通道clip首次新增损失；BasicTone在max通道归一化/float物化会平台；RAW显示另有亮度肩部、中性轴压缩和有限精度。传感器上游是否饱和，不能由已显影TIFF推断。对三核的研究observer不在当前产品，静态公式比对未改不等于整应用新实测。

推荐的A仍需固定preclip位置、明确display-linear sRGB非负域和版本、关/Amount0真正跳过全套新计算；在逐通道clip后追加工具不能得到clip前保留收益。它与已有Highlights/Whites/Curve的公平净收益尚未测，强度中间值数学未决定；没有为了得出推荐而自动调参数。三方案/最强反例/长期影响见HIGHLIGHT_OPTIONS.md。

## 当前状态与基线

- 高光远端research/portrait-highlight-review-20261112仍1bfdfbe9，冻结实现b29bff91；数值/CLI/ROI评审通过其有限范围，未进入产品Core/UI/recipe。新HL-A/B/C均提案。
- XTrans原生双进程远端仍53e2355d；后继诊断820b4f0d必须同时看：a24普通导入一次完成，后因父应用2GiB STOP停止1/6，可执行0。相同Fuji两次P6全样本0差不证明跨相机，旧P6不是准确真值。
- 主集成文档f665/功能e818；后继产品生命周期分支功能433a1d8e、文档9dd7cabb已发布，色彩核与f665无差，最终433普通EXE全链仍PARTIAL。其LC-B批准及52自动/17受控结果不批准色彩算法。
- e818已完成的TIFF/sRGB/隐藏预览/缓存内存修复原样保留。fdf普通TIFF文件与保存重开证据复用，不重新计为色彩进展，也不直接放行XTrans父资源路径。
- 第十三批6f1ae27a是锐化封存权威，本地旧8b9分支落后。SharedIncrementEnvelopeUnsharpV1无明确人工偏好，CLOSED/NOT_ADMITTED；本轮六图/新参数/GPU/V2均0，未读匿名映射或重评答案。

已证明/反证/未知/源码/最终EXE分别列在RESEARCH_STATUS.json，祖先关系与具体标记分开核对，未按分支名推定已合入。

## 可查看材料

已核验并复用18张旧高光拼图（4实拍ROI+2合成×3EV），各3列原曝光/同曝光候选/固定锚点中间调匹配；Canon3组无锚点仍灰色不可用。没有新跑CLI、修改ROI或做输出后归一化。五组XTrans局部由15张冻结8bit PNG原尺寸并列；四张旧拼图像素组成与实际列序已核验，第五旧拼图缺失而冻结组件完整，未伪造旧图。

本地查看页与文件入口见VIEWER_READBACK.json和LOCAL_COMPARISON_INDEX.json。100%按浏览器DPR做名义设备1:1；已读回1.5DPR下1152源像素=768CSS像素，不宣称显示校准/16bit可见。受限照片和派生图只在本机，不进Git；可推送数值图FROZEN_EV0_CURVES.png来自原冻结文件，不重算。

精确缺项包括白衣真实织纹/折面、确认金属/有色反射、脸部亮部、不同真实光照/未裁切曝光参照、多ICC，以及家庭DSC08015和Canon固定中间调锚点。来源和限制见MATERIAL_GAPS.md；合成补充不填这些缺口。没有与Capture One同场/同处理比较，更不宣称同等级。

## XTrans最小评估建议

保留默认发行F500…DLL，候选157BBA…只在自己的worker隔离。当前产品无a24接线；不能简单开env，也不能整段cherry-pick旧Session覆盖最新缓存/生命周期修复。首阶段只评已测legacy RGB48域；PV4的manual multipliers、linear ProPhoto、Gamma1/NoAutoBright是另一参数域，不能借证据。

需冻结profile/native/binding/完整options/source/layout身份，分离缓存；保存文件显式版本和旧reader拒绝未知，不能加一个可被旧reader忽略的field就让它默认解码。preview/export消费同master；candidate失败明确保留旧结果，不静默default；只终止自己worker并等待。旧native额外RGB分配约230MiB，与RSS增量约141MiB不是同一量，旧父STOP不因TIFF修复自动消失。

最小范围与矩阵见XTRANS_PORT_PROPOSAL.md、XTRANS_ACCEPTANCE_MATRIX.json。新预算草案先0RAW合同/缓存路径，再另批最多2次A产品cold/rebuild；扩展B/非XTrans与独立参考另审。**当前授权0、执行0、不创建新ledger。** 三套旧消耗与STOP原样hash读回，绝不复用数字余额。

## 停点

生命周期LC-B、HL-A/B/C、XTrans接入是三项独立决定。USER_DECISIONS.md列用户下一步选择；完成推送后等待，不继续实现算法/模式、修改默认/旧配方、替换发行DLL、重启RAW批次或解封锐化V1。
