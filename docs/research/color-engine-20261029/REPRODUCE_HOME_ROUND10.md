# 第十批复跑说明：最终caller与合成诊断

接续575b0b1614010c20bcbd9bf38ac2356d6ac7467f。原第九批输出及AUDITED_STATUS禁止覆盖/解除。固定自然环境使用 `tools/spektralab-research/home-environment-v1.json`；本机所有依赖在N盘研究根，不改系统PATH/产品依赖。

## 已执行的唯一full
```powershell
$env:COLOR_RESEARCH_ENV=(Resolve-Path tools/spektralab-research/home-environment-v1.json).Path
$env:PYTHONPATH='N:/pixart/ColorResearchHome-20261030/deps/python-frozen'
$env:PYTHONDONTWRITEBYTECODE='1'
python -O tools/spektralab-research/stream_full_once.py `
 --environment $env:COLOR_RESEARCH_ENV `
 --gates N:/pixart/ColorResearchHome-20261030/small-exact-results.json `
 --controls N:/pixart/ColorResearchHome-20261030/control-results.json `
 --input N:/pixart/ColorResearchHome-20261030/prepared-A/A-linear-d50-full.npy `
 --manifest N:/pixart/ColorResearchHome-20261030/prepared-A/admission-manifest-v2.json `
 --output N:/pixart/ColorResearchHome-20261030/round10-full-final-caller-once
```
这是历史实参，目录已经存在，不能再次运行覆盖。它先生成 `.execution-freeze.json`（真实文件名为输出目录名+`-execution-freeze.json`）与invocation，再启动child。child验证源码/依赖/输入/资源/参数，守卫失败即报告，不自动重试。快照原bytes在home-round10/execution-freeze.json，hash968f46...a09e6。caller链成功后没有再改；换root/源码/依赖必须新批新freeze而非改旧receipt。

接受边界固定SUCCESS_GROUP_COMMIT（详见报告），receipt4096bytes预分配，绑定report hash与最终检查峰值。输出组读者要求report.json＋acceptance.json、匹配hash、无FAILED/AUDITED_STATUS；只有raw/TIFF不是成功组。序列化、数据rename和report最后rename均在预算检查内，最后一次OS高水位检查后记录预分配receipt；不把后续print/exit新增成另一个渲染范围。

实际独立读回：
```powershell
python tools/spektralab-research/stream_saved_reopen.py `
 N:/pixart/ColorResearchHome-20261030/round10-full-final-caller-once `
 <全新audit.json>
```
这是完整saved-byte audit、renderCalls=0，不是重渲染。第九批带AUDITED_STATUS的组继续拒绝当完整合同成功。

## 只需小图复核取消/失败
`test_repeated_interrupt_home.py`针对独立进程连续3次Ctrl-Break；`test_deferred_int_break_home.py`以受控signal.raise_signal在真实native held read触发INT/INT/BREAK，不向其他app广播Ctrl-C。当前测试输出名已存在，复跑需新测试配置/输出名且冻结；本轮未为取消反复跑20MP。final-caller-safety-tests8、saved-audit-tests4结果附档。

## 公共跨设备包
入口 `tools/cross-device-diagnostic/README.md`，实际家庭结果 `home-round10/diagnostic-home`。固定synthetic fixture可再生；公司仅需相同合法资源和工具，不需私人照片。generator守卫8源文件和root-normalized hash，新兄弟symbol不弱化自然source白名单。执行器要求资源/SPIRV逐项等于expected，完整99resolved一致；same device whole/stream要求0差，trace必须不改变final。

```powershell
python tools/cross-device-diagnostic/verify_run.py tools/cross-device-diagnostic/fixtures docs/research/color-engine-20261029/home-round10/diagnostic-home
python -O tools/cross-device-diagnostic/test_compare_rows.py -v
python -O tools/cross-device-diagnostic/test_package_controls.py -v
python tools/cross-device-diagnostic/compare_packages.py <家庭run> <公司新run> <新comparison.json>
```
公司执行流程详见包README的build_package.ps1和run_diagnostic.py参数。公司NOT_RUN不预填成功。比较器先校验identity和artifact hash，逐行给变化数/首坐标/max/mean/分位数，stage按固定序列同shape对齐；不从hash推差异计数、不事后放宽阈值。公司compiler/runtime新hash应记录，不冒称与家庭DLL相同。inventory_run.py接受诊断root/run/新provenance，支持公司build脚本布局并记录逐对象命令、cache和工具/runtime哈希；是post-build/post-run清单，不当pre-run freeze。公司必须实际运行，不能拷贝家庭环境。

## 保全与许可
不修改产品src/UI/模型/任务桥/default链，不跑D80014/BT完整回归/WB/曲线/桥；RAW、自然大像素、外部engine/资源不进Git。只提交工具、公开合成小输入/输出、冻结identity和指标。GPL/CC BY-SA/厂家资源许可单列，技术stage冻结不等于产品分发批准。
