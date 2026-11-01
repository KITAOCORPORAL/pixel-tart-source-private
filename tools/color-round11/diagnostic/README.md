# 第十一批诊断入口（不修改第十批冻结工具）

当前版本：`SyntheticCrossDeviceRunV2`，显式 `--device-role home|company`。本目录是新版本，旧 `tools/cross-device-diagnostic` 和 `home-round10` 完全保留。

## 核心门禁
`verify_run.py` 与 `compare_packages.py` **都调用同一个** `package_validation.validate_package`。比较前分别验证左右包，而不是只比较两包hash或信任run.json PASS：
- 固定fixture、shape/dtype/pixel/域，完整99resolved，73项资源及34个SPIRV符合锁定合同；包内input.npy也与固定输入逐字节匹配。
- 实际读取whole与r1/7/17/64 RGBA16，重新比较0通道差；实际读取traced whole/stream与未跟踪whole再比较0差。
- 每例12个whole stage、r7的36个band stage，唯一文件、固定执行顺序、band+halo shape、域、长度、hash、finite/min/max/sample均检查。
- BT固定mode3/13个input bits及真实input/output文件，拒绝错域、非有限。
- V2实际机器角色、GPU/driver、Python、工具source哈希、主/trace DLL和运行前构建清单匹配。角色是调用者明确声明并与命令期望role交叉检查，不是对真实地理位置的签名证明。
- 旧V1只在显式 `--allow-sealed-round10` 且run.json恰好为已封存第十批家庭hash时允许；仍对旧输出文件重新算同设备一致性，不改旧record或为任意V1猜角色。

篡改测试包括stream像素改动**同时更新记录hash**、trace final改动、左右任一包坏数据、missing/duplicate case/stage、shape/域/finite/长度/role/params/resources错误。即使记录comparison=0或trace布尔=true仍被实际数据检查拒绝。元数据与hash用于一致性，不声称防恶意同时重写验证器和全部来源的认证机制。

## 家庭已执行、公司待执行
家庭V2可携带的小合成包：`docs/research/color-engine-20261029/home-round11/diagnostic-home-v2`。角色home，GPU5060Ti/610.88；同设备whole/stream0差，trace不改结果。第十批已封home包由新验证器独立验证，与新home结果全阶段比较0差，只是家庭兼容检查，**不是跨设备通过**。

公司当前 `NOT_RUN`。当前在家庭，不把固定companyRun写到每次执行里；公司执行时必须 `--device-role company` 并实际采集本机硬件/工具。公司不需私人照片。

## 公司执行步骤
外部engine/code/resources保持原第十批版本；若已有相同诊断DLL可用，先按本地source/build清单核验；若没有，仍用第十批 `build_package.ps1`/`generate_backend.py` 在**全新**公司隔离目录生成，原自然Canon白名单不删。不要将家庭绝对路径当公司存在。

```powershell
$env:PYTHONDONTWRITEBYTECODE='1'
$env:PYTHONPATH='<公司隔离numpy等依赖目录>'
# 原有generator仅生成新外部诊断源码/被动tap，不重做自然20MP。
& tools/cross-device-diagnostic/build_package.ps1 <按第十批README指定全部显式参数>
# 以下采集源码、工具二进制、逐对象编译命令、DLL/runtime、资源/SPIRV。
python tools/color-round11/diagnostic/collect_build_identity.py `
 --checkout <新diagnostic-source> --library <diagnostic.dll> `
 --trace-library <trace.dll> --resources <已锁定resources-patch> --output <全新build-identity.json>
python tools/color-round11/diagnostic/run_diagnostic.py `
 --checkout <diagnostic-source> --library <diagnostic.dll> --trace-library <trace.dll> `
 --resources <固定resources-patch> `
 --expected-resources tools/color-round11/diagnostic/expected-resources.json `
 --fixtures tools/color-round11/diagnostic/fixtures `
 --resolved tools/color-round11/diagnostic/resolved-portraA.json `
 --device-role company --build-provenance <本机构建identity.json> --output <全新company-run目录>
python tools/color-round11/diagnostic/verify_run.py <company-run> --role company
python tools/color-round11/diagnostic/compare_packages.py `
 docs/research/color-engine-20261029/home-round11/diagnostic-home-v2 <company-run> <新comparison.json> `
 --left-role home --right-role company
```

输出根必须处于native授权研究根内，不写源照片目录，不覆盖旧输出。比较输出给首差xy/channel、变化数、max/mean/分位数和首个whole分歧stage；同设备门槛仍0差。跨设备只有量化/定位模式，**不设事后PASS阈值**。CPU/runtime/compiler/GPU/driver都保持混杂记录，首差未执行不猜。

本次明确 `buildIdentity` 是运行前对当时文件的清单，不是构建时签名或证明未遭恶意改写。run开始将其原bytes复制进包，role/机器record也在render前写出；验证器确保与run.json嵌入内容一致。公司须用本机实际记录，不能拿家庭hash抄填。

## 测试
```powershell
python -O tools/color-round11/diagnostic/test_independent_validation.py -v
python -O tools/color-round11/diagnostic/test_two_side_compare.py -v
```
第一组22项，第二组一个CLI测试同时覆盖坏左包/坏右包两个subcase。测试数据是公开合成包或临时副本，不含自然照片。
