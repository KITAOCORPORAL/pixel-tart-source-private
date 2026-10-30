# 家庭第九批复现（版本化路径，原像素/资源本地）

接续 `research/color-engine-company-20261030@0f3204c09b15e041c8e70cf95ddbee789fcf7cec`。研究树和新根不得复用旧家庭输出；本机 `N:/pixart/ColorResearchHome-20261030` 已存在，**不要照例对已有路径重跑覆盖**。`home-environment-v1.json`是显式配置：换根需另存配置并重新生成native授权根/构建hash；不改变caps、PortraA99字段或源SHA。非产品注册。

## 一次环境初始化
```powershell
$env:COLOR_RESEARCH_ENV=(Resolve-Path tools/spektralab-research/home-environment-v1.json).Path
$root='N:/pixart/ColorResearchHome-20261030'
$env:PYTHONDONTWRITEBYTECODE='1'
$env:PYTHONPATH="$root/deps/python-frozen"
# 仅隔离安装，原系统/产品PATH不改：
python -m pip install --no-deps --target "$root/deps/python-frozen" numpy==2.4.6 psutil==7.2.2 tifffile==2026.10.15
```
本次固定Clang20.1.2/LLVM-MinGW20250402、CMake3.31.10、Ninja1.11.1.4、glslang16.6.0、Vulkan-Headers b5c8f996...从旧研究包复制到N:/.../deps（旧目录不动）。工具/资源/DLL哈希见home-round9/environment-and-provenance.json。source-patch/source-stream-v1均从clean c1447...新worktree开始；apply_company_patch仅兼容BT，apply_stream_v1只原流式算法+显式授权根，不将已有dirty source重复应用。

```powershell
python tools/spektralab-research/apply_company_patch.py <全新clean source-patch>
python tools/spektralab-research/apply_stream_v1.py <全新clean source-stream> <固定resources-patch> --environment $env:COLOR_RESEARCH_ENV
```
先独立patch build得BT SPIRV；只将该SPIRV放新资源overlay，其集合必须精确公司 `60e459...a10ad`，不能从新stream build其他默认资源冒充已冻集合。配置/构建完整实参已在home-round9/patch-configure.txt及stream-configure.txt和原日志；核心与第八批相同：Release、nativeRAW/Desktop OFF、Vulkan ON，显式C/C++ compiler/ninja/header/glslang路径。新DLLhash真实进入request，不使用公司hash硬贴。

## 同源生产解码与A复建
已授权精确文件 N:/pixart/RAW/Real/canon/TNAN8886.CR3，hash B7D358...33401；没有则停止自然full，不扫描私目录。
```powershell
dotnet build tools/spektra-natural-input/NaturalInput.csproj
dotnet tools/spektra-natural-input/bin/Debug/net10.0/NaturalInput.dll TNAN8886.CR3 B7D358517DB0CD5B5C54215AA089A5A30C79234AEF50E9381931128493E33401 <新production目录>
python tools/spektralab-research/home_prepare_a.py <production目录> <新preparation目录>
```
full A与公司pixelSHA/尺寸不完全一致就报错；不能更新旧hash制造通过。新完整场景small固定321×215、641×429、1200×801，不按比例round成214/428。`home_prepare_small.py`是此次错误路径之后从已验证full A在新small根补建的复现入口，不再跑RAW/完整桥。

## GPU串行小门禁与一次full
```powershell
python tools/spektralab-research/home_small_gates.py --whole
python tools/spektralab-research/home_small_gates.py
python tools/spektralab-research/stream_concurrency_test.py
```
所有out新目录，natural whole/stream0通道差才继续。`home_small_gates`写control concurrencyPassed=false，必须核验concurrency报告−30/−7/live0后显式封存true；不能人工默认true。额外最终故障控制用 `stream_controls.py --environment <cfg> --output-prefix <唯一新prefix>`，不会重做whole或full。顺序重复 `home_sequential_controls.py --prefix <新prefix>`；height `stream_height_proof.py --prefix <新prefix>`。

本批已发起一次完整full，后续**不可自动再跑**；下列是最终helper未来新批明确批准时的入口，不代表本批已用最终helper重复20MP：
```powershell
python -O tools/spektralab-research/stream_full_once.py --environment $env:COLOR_RESEARCH_ENV --gates <已封small-exact.json> --controls <已封controls.json> --input <fullA.npy> --manifest <admission.json> --output <全新full目录>
```
任何旧输出/外来root/错manifest/错尺寸/caps不符拒绝。最终caller确保清理/monitor join后，临时成功report序列化/fsync纳入cap，再raw、TIFF、report最后发布并二次检查；失败回滚/FAILED marker。Python-O不绕过准入。

```powershell
python -O tools/spektralab-research/test_home_safety.py -v
python -O tools/spektralab-research/test_saved_audit.py -v
python tools/spektralab-research/stream_saved_reopen.py <已成功且无FAILED/AUDITED_STATUS的full目录> <新audit.json>
```
saved audit只读全成片，renderCalls0。此批full现已带AUDITED_STATUS（最终report发布后OS峰值缺取样），新的严格audit必须拒绝其作为全合同PASS；之前已完成字节audit保留，不能删除标记后“再通过”。像素可以用于研究，但不被当最终hardened host门禁。

## BT与D80014最小后续
BT仅原/patch资源运行同一spk_bt_probe、pointwise/CABI两回归，原断点失败和补丁兼容都留证据。probe modes6/7非instrument输出不用于断点解释。不重查曲线/桥/WB。

```powershell
dotnet build tools/color-research-decoder/ResearchDecoder.csproj
dotnet tools/color-research-decoder/bin/Debug/net10.0/ResearchDecoder.dll <明确D80014路径> 593E1B0EE73EF7E699D73465E3BD8222AD0ED79BEB69E9265E440756D4B50A1A <新matrixV2目录> matrix
dotnet tools/color-research-decoder/bin/Debug/net10.0/ResearchDecoder.dll reopen <matrixV2目录> <新reopen目录>
python -O tools/color-research-decoder/test_hardened_contract.py -v
```
修复的惰性WB值快照会更新source/implementation hash，新V2 recipe自含；不得把旧V2 identity改写成新版。取消home脚本已在固定本机路径执行一次；复跑须新输出/报告名，不覆盖。cancel实测在unpack后检查点，未宣称全部阶段可抢占。

跨设备：公司private RGBA16没在家，不可从SHA反推出首差。拿到合法same-input whole/stream输出后在行级对照先记count/first位置，不调阈值。home whole20MP因publicABI host下界>512MiB拒绝，无一次强制upload。

### V2源码字节与Git换行
此批recipe锁定实际Windows工作树字节。提交后逐项核对：Program.cs按-text保留实测字节；其他既有文件Git blob为LF、Windows工作树为CRLF，在本机core.autocrlf=true下可精确重建，全部记录hash匹配。详见home-round9/v2-checkout-bytes.json。不能把Git blob LF哈希直接与工作树CRLF哈希比较后宣称算法变动。不同系统/路径重建DLL会改变依赖身份，必须新建变体recipe，不能覆写旧recipe来假装兼容。
