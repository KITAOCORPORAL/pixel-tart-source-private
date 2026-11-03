# 阶段收口与第十三批人工摄影评价准备

日期：2026-11-03，Asia/Shanghai。接续用户已审查公司 CPU 收口分支 9b8c52715b45540ffaf1d1fc99e0a7bd832678fc，fetch 无增量。复用独立公司研究 checkout，新建 research/color-engine-round13-human-review-20261103；开工干净，不动旧树和未提交资料。

## 1. 阶段结论不再扩张

保留已审查的两层证据：CPU double 记录身份不同；直接相同输入/矩阵下 tc_b 已有输出差异，tc/brightness 相同子集的 upsample 也有差异，不能全部归为上游传播。当前没有建立实现错误证据，暂不改像素数学。这里引用已有 CPU 收口结果，没有重跑 validator、旧 capture hash 或 localcopy，也没有进一步精度定位。家庭完整四数组继续为材料缺项，但不阻断本次评价准备。

本次新增研究 GPU compute/readback、RAW、20MP、自然图/六图渲染、BT、构建、像素候选、独立样本实验和产品接入均为 0。只有新只读入口的文件校验测试与合成 UI smoke；浏览器绘制不冒充引擎或自然摄影处理。

## 2. 公司文件可用性与精确缺项

只看 E:/Kitao-Research 顶层名称与明确的 round13 路径、原 N:/pixart/ColorResearchHome-20261030/round13-unsharp-v1。未发现已授权迁移的冻结 A/B 包。不扫描其他盘、无递归照片搜索、无公司照片替代；没有迁移引擎资源或重计算。

入口需要 61 文件：6 个原 print_linear 输入、36 个 whole 输出、12 个原完整 PNG、6 个已有匿名裁片、1 个旧匿名 key。公司明确 E: 布局和公司可访问的原 N: 布局均缺 61/61。逐文件绝对路径、期望 SHA 和字节数详见：

- human-review-prep-20261103/MISSING_FILES_HOME_PATHS.json
- human-review-prep-20261103/MISSING_FILES_COMPANY_EXPLICIT_PATHS.json

这是公司访问检查，不代表家庭机器丢失文件。新的真实评价目录未创建，缺项时返回 2；未生成占位照片或替代结果。真实六图校验、真实入口生成、观看与人工偏好均 NOT_RUN_MISSING_PRIVATE_FILES / NOT_RUN_AWAITING_HUMAN。

## 3. 家庭可直接运行的最小只读入口

见 tools/color-round13-human-review/README.md。prepare_review.py 固定旧 metadata 和 review_ab.py 的 SHA，通过 AST 只读旧映射；不执行旧渲染或分析脚本。启动完整验证原输入/输出 SHA、尺寸、float finite、原 PNG 显示像素、匿名裁片 hash 与映射。缺项/漂移拒绝，无覆盖、无静默换图。

历史 full PNG 文件级 SHA 未记录，不能补造：入口将原完整 PNG 解码像素对照冻结 RGBA16 的同一 round-to-even(/257) RGB8 转换，现场 SHA 另记。key 与固定旧映射及冻结匿名裁片交叉绑定。

验证成功后只在 Git checkout 和冻结输入目录之外生成私有 review.html 与本地校验 JSON，HTML 直接嵌入原 PNG 字节；不重新编码、锐化、调色或生成新照片。X/Y 保留原匿名顺序，不显示候选名称、指标或胜负；共用缩放与滚动坐标，支持切换、适合窗口和 100% 既有局部。100% 为源像素/CSS 像素，不伪称物理屏幕校准或产品 ICC 验收。

沿用每图既有固定裁片，Nikon 额外提供原有 [310,390,35,60] 肤色 ROI，不增加新 ROI。页面无网络、外部依赖、服务器或持久化存储；人工记录只在内存，用户点击下载导出。关闭前导出并核对。生成 HTML、照片、裁片、匿名校验材料和评价答案只本地，不进 Git。

## 4. 只问五项，等待真人

- 花瓣/叶片细节是否过软；
- 暗侧黑点和光晕有何差异；
- 肤色与饱和边缘是否有不自然彩边；
- 噪声变化能否接受；
- 各场景偏好 X、Y、无明显偏好或无法评估，以及具体原因/位置。

没有更高级等诱导题，不预填答案、不由代理代评。六图一直是开发集；独立样本验证仍 NOT_RUN_DEVELOPMENT_ONLY。人工偏好仍 NOT_RUN_AWAITING_HUMAN。空白导出的 30 项全部 null，状态仍 NOT_RUN，不会因生成页面/导出文件变成摄影通过。

## 5. 实际执行、测试与边界

8 项针对性离线文件测试通过：固定六图清单、精确缺项、输入/输出漂移、非有限/尺寸、PNG 像素与元数据差异、key/crop 错配、原 PNG 字节匿名嵌入、输出保护/无预填答案。只使用临时生成的极小非摄影夹具，不重跑旧候选或旧 CPU 验证。

已有本地 Edge 92.0.902.67 headless（--disable-gpu）合成 UI smoke 通过：六场景切换、初始空答案、同尺度和滚动坐标、空格切换、100%/ROI、空白本地导出、适合窗口、桌面/窄屏布局、无页面错误及无 HTTP 请求。桌面/窄屏截图已检查，仅为本地合成 UI 测试；不是已观看私人六图。浏览器测试没有填写任何偏好。

Python 3.13.13、NumPy 2.4.6、Pillow 12.2.0；其余实际版本见执行收据。缺文件拒绝为预期行为，不记成真实六图测试通过。自然图、float/RGBA16、参考资源、引擎二进制与 GPL 研究补丁没有新增至 Git。产品 src/UI、默认链、旧 PV4/5/6、旧工具、配方、证据与时间原样保持。独立 DLL/进程/LUT 不自动解决许可，技术研究不是分发批准。

## 6. 已有成果索引与停止点

|材料|用途/边界|
|---|---|
|COMPANY_FILM_SCALE_CPU_CLOSURE_20261103.md + 对应能力表|已审查数值收口；不重跑、不再追逐跨 GPU bit 一致|
|HOME_ROUND13_UNSHARP_REPORT.md + CAPABILITIES_HOME_ROUND13_V1.md|第十三批冻结唯一候选、技术结果与软化/噪声取舍；不是摄影批准|
|home-round13/candidate-natural.json、ab-review.json、evaluation-plan.json|冻结六图 hash、已有 ROI 与开发集边界；评价人不提前看指标|
|HOME_ROUND12_CAUSE_REPORT.md、HOME_ROUND11_REPORT.md|既有因果诊断和自然样本阶段证据，历史不重写|
|BACKEND_CONTRACT_V2.md、STREAMING_BACKEND_CONTRACT_V1.md|产品接口/资源/取消/线程限制，仍不构成接入或许可批准|
|tools/color-round13-human-review/README.md|家庭只读入口、校验合同、人工操作与导出说明|
|human-review-prep-20261103/|本轮精确缺项、执行收据、专项测试与合成 UI 证据|

SharedIncrementEnvelopeUnsharpV1 继续 DEFAULT_OFF / NOT_ADMITTED。本轮交付后等待真实摄影评价，再决定是否值得改候选。不自动开始第二候选、独立样本实验、精度定位或产品接入。LOCAL_HEAD/REMOTE_HEAD 与工作区状态在实际 push 后读回，不能用收据文件自指其最终 commit。
