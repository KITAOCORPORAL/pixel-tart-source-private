# Polaroid i‑2 / SX‑70 × 相纸配置库：研究与离线首版

日期：2026-10-28（Europe/Berlin）。**总体 PARTIAL；离线首版可运行；产品接入 NOT_STARTED；实测模型 0。**

## 1. 顺序与工作区安全
- 继续让 R5.3 UI 纠偏优先。本批没有修改任何产品源码、XAML、现有配方或教程开关，没有新建页面、合并、reset、stash、clean、commit、push、打包完整应用或安装包。
- 开始读取 `company/r53-color-tools-film-20261028@4e9dfc4a9b65ab78379c1fa2bf1ff514742a0bec`，origin fetch 后 0/0。13 tracked + 4 untracked UI 更改只读留证。
- 研究过程中另一进行中的 UI 工作将其提交为 `ecf5efc16a8f4d0b7dbb9d5e7d9f97e38c2702e5`、`ae6e83b2b7368d25f64c064b120b2e8a3d9aa965`。后一次核对 HEAD 比 origin 多 2 个提交。**不是本批提交，不宣称已推送**。见结束快照。不把并发变动解释为本工具破坏/清理了用户改动。
- 实读 HANDOFF、开源先行 POLICY、R5.3 最终设计、开发标注和布局文档。R5.3 最终文档优先于旧 R5 开发标注。最终定稿要求：左唯一相纸、中央实际结果、右相机/相框/参数/贴底显影。
- 本批核对已解压定稿文件及 hash；原 ZIP 的期望 hash 来源于已有证据，未在本批再次取得原 ZIP 重哈希，不写成亲自复验。浏览器仅用于外部公开研究，没有用于本地应用 UI、原型网页执行或桌面验收。
- 枚举共享 worktree/ref/remote/脏状态。其他 clone 没有擅自 fetch/checkout。所有当前证据在 `evidence/`，保留起始与结束状态。

## 2. 实际生成，不凑组合数
|稳定 ID|内容|证据|接入|
|---|---|---|---|
|polaroid-modern-color-style-smooth-1|单调中性影调 + 连续彩度；无相机色偏|风格近似，非实测|仅离线|
|polaroid-modern-bw-style-smooth-1|同一影调，灰阶黑白|风格近似，非实测|仅离线|

JSON、33³ `.cube` 与模型/数据/输入域/许可 hash 清单均已生成。共享是**无证据时的保守近似**，不是宣称所有乳剂等同。i-Type/600 不因包装制造差异。SX‑70 已列矩阵但**没有 SX‑70 专用新模型**，不把同一 LUT 换名凑四套。现代、Impossible PX、历史 Time-Zero、过期历史片不合并训练。Reclaimed/Duochrome 保留第二批。

彩色/黑白的差异有类别依据；具体 toe/shoulder/default saturation 是创意选值，不声称由官方数据推导。公开 NASA 肖像只用于查看肤色、高光、红橙和阴影，没有伪造相纸扫描目标。

### 实际对照图
- `results/comparison-portrait.png`：输入 / 彩色平滑候选 / 黑白候选，均嵌入 sRGB ICC。
- `results/comparison-gradients.png`：中性与红绿蓝、红橙、紫、蓝绿渐变。
- 第一次旧 V3 转写的彩色近黑异常保存于 `results/legacy-v3baseline-interpolation-FAILED.json`，失败基线配置在 `evidence/`；不能作为正式候选分发。

## 3. 验证实绩及含义
- `tool-tests.json`：19 PASS / 0 FAIL。输入有限值/范围、无 ICC 显式拒绝、强度端点、分组泄漏/权利/扫描自动增强防错、模型 hash、确定性等。
- `preparation-tests.json`：ICC 转换、单应性/独立检查点、patch 采样、无效区域拒绝和整条拟合工具通过**合成自检**；不是实拍测试。
- 链接实际 `CandidateTone.cs` C# 执行，4,361 输入点的 Python 基线转写最大绝对差 **2.22e-16**。只链接 tone 文件与最小类型适配，不是完整应用回归。
- 执行固定 commit 的 Lab `pipeline.js/lut.js`；Python/JS 同 LUT 插值最大差 **4.44e-16**。该误差是第三方变换复现，不是相机/相纸误差。
- 33³ 平滑候选，各 14,105 点：彩色 ΔE00 P95 **0.04010** / 最大 **0.15262**，黑白 P95 **0.02534** / 最大 **0.04803**；相对于候选解析变换，不是真实配对照片。原定 maxRGB<0.02、P95<1、灰阶不下降门禁均 PASS，未更改门槛。
- 旧 V3 彩色 baseline 则 maxRGB **0.03166**、ΔE00 max **9.68317**、灰阶局部下降，门禁 FAIL。保留历史实现；新候选显式新 ID/版本，未偷偷修旧项目。
- **真实配对照片拟合：NOT_RUN；人工盲评：NOT_RUN；全应用缩略/预览/显影/保存重开/全格式导出一致性：NOT_RUN。** 合成 `heldout_gate=PASS` 只证明工具运转，不证明胶片准确。

## 4. 数据与权利结论
详见 `REFERENCE_REVIEW.md`、`rights-register.json`、`data_manifest.json`。
已实际阅读四个指定仓库（固定 commit）和 G'MIC 目录/Consumer/Pro 页面，补查 W-Mai/filmr 的 Polaroid preset。发现“有实测 Lab 图卡转移模型”≠“有相机实测”；Instax 数据≠Polaroid 数据；光谱项目自述物理准确≠已验证。
本批承认的 i‑2 / 原装 SX‑70 配对训练照片 **0 组**。未下载/拟合第三方 LUT 来生成候选，不通过拟合绕过许可证。未复制 GPL 代码进入产品；第三方源码只留隔离研究区。

## 5. 可复跑校准与下一阶段
- `offline.py generate/validate/apply/calibrate`，`prepare_pairs.py` 手动对应点配准与有效 ROI 采样，`test_offline.py/test_prepare.py`。
- 先测一个明确现代相纸版本；拟合 tone-only / tone+neutral-preserving matrix / tone+cross terms，以 validation 选型，heldout 只报告。3D residual 暂停，无数据不增加自由度。
- 按 scene/batch/parent_capture/文件 hash 锁住分组；固定扫描 ICC/流程，只预先声明曝光/WB，不逐图任意调色。工具目前为已处理 8-bit RGB 图像/patch 流，**不是 RAW 解码器，也尚未接受 16-bit/float TIFF 图像入口**。精确流程及限制见 `CALIBRATION_AND_INTEGRATION.md`。
- R5.3 完成后再将获批配置作为现有 V3 色彩组件的新显式版本接入，同链渲染与完整配方验收必须补齐。现有完整模块保留，旧教程继续停用，不在本批改动它们。

## 最后读取快照
- HEAD `92c6da2e7311c55a788898db07160047cc3d7051`；ahead/behind origin `0 / 0`；dirty 条目 0。此为读取时点，不是本批推送。
- 受控实测工具目前只承诺已配准 RGB patch 级报告；16-bit/RAW 密集图像评估仍未实现。
