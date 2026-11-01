# 第十一批续接封存（2026-11-01，Europe/Helsinki）

原会话因上下文耗尽停在“最终核验、暂存审查、推送”，第十一批改动未提交。此次接续基线仍为 research/color-engine-home-20261030@07df3c56c551c38893abc1e4b7eaefb8e4a83be4。只在该研究树收尾，不合 main、不改其他工作树或产品源码。

## 三个有限核验目标
1. 冻结成果未漂移：只读重算第十批 caller、receipt/report、已有 20MP raw 输出 hash，并查冻结路径 diff。
2. 诊断交接可独立拒绝坏包：重跑固定合同与真实 CLI 的左/右包像素篡改、错 role 拒绝，以及 home 自比较；不伪造公司数据。
3. 自然覆盖证据可读回：重读已保存 6 图代理、whole、两种 rows 输出、72 阶段文件，不只相信记录里的 PASS。

## 必要修改及失败保留
- 新增 verify_saved_outputs.py 和 test_saved_outputs.py；总报告验证器增加阶段报告身份/顺序/尺寸/有限性检查。
- CLI 测试新增左右角色分别错误拒绝及真实 home 自比较；没有改像素数学或阈值。
- 原 final-tool-identity.json 漏记 audit_stage_finite.py，且无法覆盖本次验证器更新。保留原快照，另以本目录 tool-identity.json 记录最终源字节和相对原快照的变化，区分实际渲染时源码、先前核验源码与本次最终源码。
- 新审计首次误以为每图阶段文件从 0000 编号，第二张图文件查找失败。实际 native tap 是进程全局序号（第二张从 0012、最后一张从 0060）。修复定位并增加回归，未改原像素或补造文件。首次失败原样见 checks-final.json/saved-natural-audit.txt，成功见 checks-completion.json。

## 实际结果
- 独立包校验 22/22；CLI 3/3（篡改/错 role 各含左右 subcase）；saved-data 16/16；原照片准入拒绝 2/2，共 43 项唯一测试通过。
- 新 home 包和显式旧 sealed-round10 包：分别重新读数据通过；各 3 个 case、每 case 48 个阶段文件。
- 6 张既有开发代理与冻结输入 hash/shape/finite 匹配；whole 与 r17/r37/trace 的 18 次全图全通道比较均 0 差；72 阶段的 hash、长度、有限值及统计重新读取通过。
- 第十批冻结源码/既有 20MP 输出/receipt 一致，受保护路径没有变更。只读 hash 检查不是新性能测量。
- 公司 PowerShell 入口仅语法解析通过，未执行。Python 源文件 AST 检查通过；本次没有重编 native/managed 引擎。
- 本次续接 GPU render 0 次、RAW decode 0 次、20MP engine 调用 0 次。原照片准入测试在解码前拒绝。

## 接纳边界与后续
可以交接的是独立诊断与只读验证工具，不是一般摄影效果候选。CanonR6 暗部标准差比约 0.132、SonyNEX5R 11,562 个无旧端点足迹的新零值像素的问题仍在；没有修曲线、换样本或用合成数据掩盖。公司实际跨 GPU 对照、绝对色准、C1 同源导出、独立 holdout、人工偏好和分发许可仍未完成，不升级 PASS，不注册产品后端。

下一步先在真实公司设备执行公开小诊断包，定位首个差异阶段；暗部/端点修复须另立像素候选并完成 POLICY 的两成熟实现对照。20MP 冻结成果无须重复跑。私人 RAW、照片、局部、自然阶段缓冲和外部引擎资源/二进制均不进入本次提交。

本文件是续接技术检查结论；提交与远端读回另以 git-receipt.json 为准，不在实际推送前声称成功。
