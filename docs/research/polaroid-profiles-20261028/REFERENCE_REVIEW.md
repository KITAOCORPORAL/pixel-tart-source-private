# 实读来源分类、commit 与权利审查

检索/阅读：2026-10-28 Europe/Berlin。本文件只记本次实际取得内容。网页引用用 URL + 获取字节 SHA，不伪造 commit；仓库用完整 commit + 文件 SHA。更多在 `evidence/references-read.json`、`rights-register.json`。

## 指定来源

### G'MIC Color presets — 类别 2：第三方预设近似
https://www.gmic.eu/color_presets/
实读首页、`instant_consumer_sample_1.html`、`instant_pro_sample_1.html`；目录链接与作者归属、下载格式与授权段落已读。Consumer 见 PX-100UV+/PX-70/PX-680/Time Zero expired；Pro 见 664/665/669/690 等。它们不是现代 i-Type/600/i‑2 的受控配对训练集。网页的参考样图不是测量证据。网站说明 preset 至少 CC BY-SA 2.0；引擎 CeCILL 与图片/LUT 授权不能互相代替。单个 LUT 生成出处/上游条款未完全核清，本批未下载 LUT、未拟合其派生物、未用其照片作示例。
Pat David 链接返回重定向短 HTML，不能说本次已阅读其完整源文。保留失败/跳转记录。

### cedeber/hald-clut — 类别 2；含历史/Impossible 名称
https://github.com/cedeber/hald-clut
Commit `3b3180f82d4dcea1e6e8c5648473539a910d7f49`。
实读 README、根 LICENSE、357 文件的 git tree 元数据；未读取单个 LUT 像素。README 聚合 RawTherapee、PictureFX、Fuji、Apple Photo、Pixelmator；根 LICENSE GPL‑3.0 不证明这些资源全有同一来源/许可。树里 Polaroid 664/665/667/672/669/690/PX/Time Zero，不能替代现代乳剂。
首次 blob clone 被停止，改用 no-checkout tree；只用于清单和许可证核查，不进产品或交付 ZIP。

### Kunkles/polaroid-lab-print-corrector — 类别 4；有测量派生参数但不是相机实测
https://github.com/Kunkles/polaroid-lab-print-corrector
Commit `25ffdfa4c27dbbccb70d7a0037613f17d7d8bea2`，MIT。
实读 README、LICENSE、HANDOFF、`js/pipeline.js`、`js/lut.js`、`js/solve.js`、`js/calibrate.js`、`charts/calibration-color.json`、`charts/calibration-bw.json`、`test-charts/README.md`。
- `solve.js::collectPairs/fitForward/fitChroma`：图卡读取后每通道核回归，强制单调后反演；不能把反校正 LUT 正向当胶片外观。
- `calibrate.js`：fiducial、homography 和 patch 采样；扫描 anchors 的规范化也是路径假设，不能偷偷按每张照片调色。
- 已提交彩色 i-Type Lab 图卡参数（98 patches）、600 B&W 图卡参数（49 patches），包括前向 grids/扫描 anchors；支持“该项目有测量派生转移参数”。但原始拍摄/批次/完整扫描 ICC/配对照片不齐，不足以验证其量化准确度，更不是 i‑2/SX‑70 相机模型。
- HANDOFF 明确个人实拍扫描 gitignored、历史发生隐私泄漏；本批不追找/复制个人素材。只执行纯函数 `pipeline.js/lut.js` 对合成输入做插值对照，不执行网页，不上传照片。
- MIT 代码与文档本地保留版权；参考输出不训练本批配置。标定 JSON 未发现独立资源许可证，但也不以 MIT 总许可代替原始照片授权或数据科学有效性。

### JanLohse/spectral_film_lut — 类别 5：光谱方法；含非 Polaroid 即时成像材料
https://github.com/JanLohse/spectral_film_lut
Commit `bcbcfc6861f49cc706dc5d00ea048e4853060aab`，代码 MIT。
实读 README、LICENSE、pyproject、`src/spectral_film_lut/film_spectral.py::FilmSpectral.__init__`、`reversal_film/fuji_instax_color.py`、`fuji_fp100c.py`。
有实际数值敏感度/密度曲线，README 说明数字化 datasheet、层间效应简化。Instax 文件明确 ISO=1000 是为结果补偿（原 800），说明数值已经包含作者调整，不能把所有参数当原始测量。即使材料名称相近，Instax/FP100C 也不是现代 Polaroid。相关入口未找到足够精确的每份数据表原始授权链；MIT 仅清楚解决代码，不自动授予制造商图表或照片再分发权。没有复制数据/生成派生 LUT。

### andreavolpato/spektrafilm — 类别 5；其中 polaroid_animation 是类别 3
https://github.com/andreavolpato/spektrafilm
Commit `3bb2c2d2801ff68b92019cf1dbcbb133d60832bc`。
实读 README、GPL LICENSE、`SPEKTRAFILM_LICENSE.txt`、pyproject、`runtime/stages/filming.py::FilmingStage.expose/develop`、`model/density_curves.py`、`profiles/io.py`、`utils/spectral_upsampling.py`、GUI `polaroid_animation.py`。
- 主链有 datasheet/profile、RGB 光谱升采样、曝光、卤化/颗粒、密度/扫描分层；这可借鉴审查问题，不是独一无二光谱真值。以最终数字 RGB 输入尤其不能声称还原真实光谱。
- 枚举 profiles 未发现本目标 i‑2/SX‑70 实测档。GUI PolaroidState/build_layer_stack 用固定 Gamma=2.2 与颜色权重动画化输入，不是宝丽来乳剂测量。
- **代码 GPL‑3.0；profiles 及其直接 LUT 编码专门 CC BY-SA 4.0**。保留署名、来源、修改说明、同许可；拟合派生不会清除义务。README 尚说 custom/no-resale，而详细 license 将不转售/不商用训练等区分为额外请求；保守登记不一致，需上游澄清后再评估，不直接挑宽松解释接入。
- Hanatos 升采样 LUT、制造商曲线、示例来自 signatureedits 的照片各有独立权利链。本批全部不作产品输入。

## 继续寻找的直接相关项目

### W-Mai/filmr — 有 600/SX70 条目，但不认定实测（类别 2/方法近似）
https://github.com/W-Mai/filmr
Commit `fee388e213dd77bfe6977b63071b1c136213373a`，MIT。
实读 README、LICENSE、`src/presets/polaroid.rs`、`src/pipeline.rs`、`src/spectral_engine.rs`。
600/SX70 preset 注释只有 Polaroid Technical Data，无具体数据表/页码/年代/批次；出现 generic Gaussian layer、standard negative spectral params，600 被配置为 ColorNegative。仅凭“physics-based”自述不能认定现代即时正像相纸拟合。无相机成对原始数据/保留组表现证据。示例快照许可未核清，不复制或拟合。对照树与本地源码保留，未加入 product。

## 官方资料（实际阅读）
- `115012394347` What film do I use with Polaroid cameras?：i-Type 无盒内电池、老相机不能原生使用；SX70 与 600 的感光度大致 1:4；ND 跨用条件。
- `13538194970258` I-2 compatibility：i-Type/600/SX70；必须开片门按 mode-OK/拨盘选择正确类型，相机不能自动识别。
- `115012361067` Temperature：适宜约 13–28°C；更冷可能绿/低对比、更热偏黄红。只作官方定性趋势，没生成温度→RGB 系数。
- `115012541948` ND：片盒 ND 与镜头 ND 是两种不同放置，必须分别记录，不将其等同原生兼容；ND 光谱/实际减光需记录。
- `115012533928` current vs past film：化学供应变化、Impossible 重建，与历史配方不同；页面较旧，其显影时间不能当所有 2026 批次承诺。
- `360001583187` scanning：手机/平板/相机扫描的反光、视角、牛顿环等差异；官方普通扫描指导不是本任务完整 ICC 标定规程。
- 官方手册归档 `115012460048`；I-2 手册页 `12703552699410` 下载英文 PDF attachment `13261628055954`（10 PDF pages，实读）。手册物理印页 8–9 设置相纸，16–17 技术参数：98mm 三片组、f8–f64、机身工作温度 4–42°C。**机身工作温度不等于胶片最佳显影范围。**
- SX‑70 原装用户手册 attachment `360002937468`（19 PDF pages，实读），旧片操作、曝光/闪光/取景与故障；不把其历史片说明直接当现代化学参数。

官方页面通过可见站内导航读取；初始旧文章 ID 失效/直取 403 均记失败，不伪造成功。PDF 与第三方源留本地研究目录，不进产品/发布包。精确附件 hash 在 `evidence/manuals.json`。没有一份手册提供可支持本任务相机色偏矩阵或精确温度响应的测量数据。

## 分类汇总
1. 真正测量数据的目标相机模型：**0 个已核准**；Lab 有图卡测量派生参数，但链路不同。
2. 第三方预设近似：G'MIC、Hald；Filmr 带物理术语但证据仍不足。
3. 边框/复古动画：spektrafilm GUI Polaroid animation 不等同其光谱核心，更不等于实测 Polaroid。
4. Lab 校正：Kunkles，单独保留，不能迁移相机训练权威。
5. 非本目标光谱方法：spectral_film_lut / spektrafilm；有数据型输入仍须逐项验证出处、假设、世代和实拍表现。




