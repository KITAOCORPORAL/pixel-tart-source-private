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
