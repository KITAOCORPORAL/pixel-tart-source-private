# 第十批冻结状态补充（2026-10-30，Europe/Berlin）

**固定PortraA流式研究原型：HOME_5060Ti_FINAL_CALLER20MP_PASSED_AND_FROZEN。** 第九批PARTIAL记录保留为历史，本次新输出/新快照并非改旧AUDITED_STATUS。

- V3caller调用前freeze＋child再核验，source/99resolved/依赖/DLL/resource/SPIRV/environment/manifest/pixel hash全登记。唯一新20MP实际完成，成功组提交预算终点命名为`SUCCESS_GROUP_COMMIT`，预先固定不再微移。
- cap仍device256MiB/staging64MiB/caller512MiB；终点采样RSS与OS peak均380588032bytes。先monitor join/cleanup、临时report serialize/fsync、budget、raw/TIFF/report发布、一次OS峰值检查；预分配receipt记录此检查并绑定reportSHA，console打印/进程退出不属于渲染验收范围。
- 成功组需report.json＋有效acceptance.json，无FAILED/AUDITED_STATUS，receipt的reportHash/outputHash/峰值合同匹配。仅raw/TIFF或无效/空receipt不成功。取消/提交错误回滚；primary与cleanup分列。
- 重复定向Ctrl-Break三次，以及真实held native回调中受控SIGINT/SIGINT/SIGBREAK，都native−7/join/live0无final；不扩大到shader任意位置抢占。
- 独立saved audit全3670行raw/TIFF exact，renderCalls0，不当重新渲染。本次full1call，不循环重试。
- 独立synthetic诊断身份：兄弟symbol只白名单公开3fixture hash，自然Canon入口未弱化且实际拒绝synthetic。3×4rows whole/stream0差；whole12stage/stream36stage与final小RGBA16可机器比较，trace不改final。BT另mode3读取，不改变PortraA输入transfer。
- 跨1650/5060Ti自然hash差异未归因，首差仍缺公司pixels；合成公司运行NOT_RUN，比较无事后PASS阈值。技术阶段冻结不等通用胶片/产品注册/商业资源许可全部通过。

权威结果HOME_ROUND10_REPORT、home-round10/execution-freeze/full-acceptance/independent-saved-audit，复现REPRODUCE_HOME_ROUND10及tools/cross-device-diagnostic/README。

---
# 家庭流式能力合同补充 V1

继承 `STREAMING_BACKEND_CONTRACT_V1.md` 唯一PortraA wire、sourceSHA、资源集合与原pixel数学。本文件不注册产品。

- 环境：ColorEngineHomeEnvironmentV1显式根及源路径，native negativeStore授权根编译在外部新源码。公司8文件中7hash相同；engine.cpp只root不同。新DLL自hash核验与完整资源SHA不变。
- caps：device256MiB/staging64MiB/caller512MiB；device仍min(cap,free−256MiB,2GiB)，无借家庭显存上调。host=整个caller imports/准入/hash/映射/native/TIFF/raw读回/清理/报告提交，不等nativevector。
- sampled RSS与OS peak分别记录；OS高水位>cap即拒绝，不因采样漏峰通过。监测失败/不可得也不能PASS。daemon=False线程必须停止join。
- 输出组：raw和TIFF先.partial，source映射在native返回后释放，consumer完成一次全行，raw完整hash、TIFF逐行完整回读、live0、negative不存在、cleanup无异常、host验收才有提交资格。报告先写临时并预算检查；data rename后report最后发布，任一失败回滚/失败marker，无成功组。安全不是多文件原子事务；进程崩溃时“无success report或有FAILED/AUDITED_STATUS”均不得当成完整组。
- primaryError保留源异常，cleanupErrors单列；取消先signal再join worker，不能跨native回调抛异常或提前free。
- saved audit不渲染（renderCalls0），只检raw/TIFF大小、layout、全pixel hash；try/finally join监测。不能以读旧图证明重新渲染。
- 家庭same-device10自然small+4synthetic+consumer/顺序/height exact；跨1650/5060Ti hash不一致且公司像素缺失，首差/计数NOT_AVAILABLE，不当same-device失败也不当跨设备通过。
- 本次唯一20MP原caller返回全像素/TIFF/cleanup完成、379396096bytes采样与OS峰值在最终report前；最后publication后峰值缺口经AUDITED_STATUS降级。最终helper对publication也验收，已真实小图/故障通过，**未再跑20MP**；最终hardened全host合同仍PARTIAL。
- whole20MP host必要source+staging+result>512MiB，NOT_RUN_BUDGET。不会因家庭设备大就放弃流式。
- D80014 MatrixDecodeResearchV2修复只snapshot lifetime，不变ABI/source/matrix/WB守卫；14-bit全链与fresh-process重开通过，实际取消检查点通过；不扩大相机。

技术状态和GPL/CC BY-SA/产品数据分发许可分离。仍研究隔离、default off，不合产品、不改UI/项目/任务桥/旧PV4/5/6，不扩B、stocks、seed、曲线、WB余量。
