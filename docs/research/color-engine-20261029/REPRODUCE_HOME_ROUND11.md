# 第十一批复现：独立包门禁与小规模摄影覆盖

基线research/color-engine-home-20261030@07df3c56c551c38893abc1e4b7eaefb8e4a83be4。旧20MPchain/freeze/receipt/output和AUDITED_STATUS不改。本轮**禁止再跑已通过20MP**；代码仅tools/color-round11和新证据。环境是家庭5060Ti/610.88，不冒充公司设备。

## 1. 只读第十批保全（不是20MP渲染）
```powershell
$env:PYTHONDONTWRITEBYTECODE='1'
$env:PYTHONPATH='N:/pixart/ColorResearchHome-20261030/deps/python-frozen'
python tools/color-round11/verify_frozen_stage.py --output <全新read-only-preservation.json>
```
检查冻结源码、report/receipt、已有raw像素hash和旧AUDITED_STATUS存在。不自动构建/解码/渲染旧full，输出名必须新。

## 2. 共享验证器与独立测试
```powershell
python -O tools/color-round11/diagnostic/test_independent_validation.py -v
python -O tools/color-round11/diagnostic/test_two_side_compare.py -v
python tools/color-round11/diagnostic/verify_run.py docs/research/color-engine-20261029/home-round11/diagnostic-home-v2 --role home
python tools/color-round11/diagnostic/verify_run.py docs/research/color-engine-20261029/home-round10/diagnostic-home --role home --allow-sealed-round10
python tools/color-round11/diagnostic/compare_packages.py <left-run> <right-run> <新comparison.json> --left-role home --right-role company
```
比较前每侧独立重验输出data；stream像素改后同步改hash仍拒绝。两套CLI只复用一个函数，不依赖assert。旧V1只接纳exact-sealed家庭record并重新验数据，不改旧身份。`--role`/两侧role必须明确，期望与包角色不符拒绝；不以role字符串当真实地理认证。

### 公司入口（NOT_RUN，需要公司设备实际执行）
第十批公开fixture/生成引擎兄弟symbol不改，若已有同版本diagnostic/trace DLL和资源可复用，先用本轮collector实际采构建身份；如缺则用旧build_package在**新隔离目录**生成，不放宽natural白名单，不重跑自然20MP。
```powershell
& tools/color-round11/diagnostic/run_company.ps1 -Checkout <公司diagnostic-source> -Library <公司diagnostic.dll> -TraceLibrary <公司trace.dll> -Resources <原固定resources-patch> -Output <全新公司诊断包根>
```
公司入口先collect_build_identity再role-company render再verify。需Python/NumPy和外部绑定、CMake cache/ninja可读；所有代码生成/资源hash按diagnostic/README，不用家庭绝对路径。机器可读新包含执行前record、原bytes的build provenance、input.npy、stage和RGBA16。公司合法返回公开合成小包后再与home比较，cross-device只量化定位，无事后PASS阈值；无公司自然像素时count/first不可推。

### 家庭新role入口的实际运行
本机新目录 `N:/pixart/ColorResearchHome-20261030/round11/diagnostic-home-v2-complete`，已生成且不可覆盖。caller参数化，可在新输出复现小包；不为此触碰旧20MP或BT全部研究。完整实参/identity在home-round11/new-home-machine-execution、diagnostic-build-before-run。

## 3. 最多6自然图，生产准备合同独立
文件 `tools/color-round11/photo-preparation/sources-v1.json`只列6个授权目录/原清单SHA。当前6图已处理，输出均在N盘；不要照抄已有名字重跑覆盖。若新机器缺文件，保留缺源项；不可换另一照片/删白名单。
```powershell
dotnet build tools/color-round11/photo-preparation/PhotoPrepare.csproj
python -O tools/color-round11/test_photo_admission.py -v
dotnet tools/color-round11/photo-preparation/bin/Debug/net10.0/PhotoPrepare.dll <清单photo-id> <全新production目录>
```
Tool调用原生产decoder和Display EV0，没有改RAW math。完整RAW准备仍用已有512MiB master/1GiB估算准入，不是20MPfilm render；raw准备进程峰值单列，不能冒称全部<512MiB。

```powershell
python tools/color-round11/prepare_photos.py
python tools/color-round11/generate_natural_backend.py <冻结stream源码> <新clean外部源码> <round11/natural-inputs-v1>
# 使用home-round11/natural-configure.txt的工具和flags构建新shared DLL，nativeRAW/Desktop OFF。
python tools/color-round11/run_photos.py --root N:/pixart/ColorResearchHome-20261030 --output <全新round11自然输出根>
```
`prepare_photos.py`本机root固定、已存在会拒绝；新根需显式另版配置/source admission和新生成库身份，不能覆盖旧结果。不做通用目录扫描。原source Canon symbol保留，只追加NaturalPhotographyStreamV1，真实source/pixel/dimension配对且longest<=1200；guard错误4控制无read/live0。

当前input标准ProPhotoD50linear负/>1允许、原WB已应用，output encoded sRGBD65 RGBA16 opaque。Bridge固定、第六批数学不变，6D orientation8按旧生产行映射转1。before是production Display而after是场景film/print，是相对response比较，不是paired-color-error。

## 4. 只对已有数据做分析与局部
```powershell
python tools/color-round11/review_photos.py
python tools/color-round11/audit_prior_endpoints.py
python tools/color-round11/photo_numeric_review.py
python tools/color-round11/concrete_findings.py
# 同6代理额外只读stage有限性，需独立--trace外部构建（不改数学）
python tools/color-round11/audit_stage_finite.py <研究root> <全新stage本地目录>
```
review输出已存在要用新实验名/配置，不覆盖。12个同坐标局部及before/after仅本地；源footprint端点审计沿相同BOX传播input indicator，strict-input-interior且无旧端点足迹但output0视为新处理端点，不反推sensor/某node。

本地结果：
- `N:/pixart/ColorResearchHome-20261030/round11/production-{id}` 原生产准备；
- `.../natural-inputs-v1` fullA本地中间及最长1200proxy；
- `.../natural-coverage-run1` 私人前后图/rgba16、report；
- `.../local-review-v1` 12区局部与relative ROI；
- `.../diagnostic-home-v2-complete` 新home角色公开包。

自然照片/私人RAW/局部/大缓冲和外部资源源码/DLL不进Git。本轮Git只清单/hash/指标/文字+公开合成小包。见HOME_ROUND11_REPORT及能力表，许可证边界与技术验收独立。产品src/UI/default/PV4/5/6不改。


## 5. 2026-11-01 已保存数据续接核验（Europe/Helsinki）
只运行以下只读入口；不重复上述生成/渲染步骤，也不修改第十批冻结链。

```powershell
python -O tools/color-round11/test_saved_outputs.py -v
python -O tools/color-round11/diagnostic/test_two_side_compare.py -v
python -O tools/color-round11/verify_saved_outputs.py --evidence docs/research/color-engine-20261029/home-round11 --inputs N:/pixart/ColorResearchHome-20261030/round11/natural-inputs-v1 --outputs N:/pixart/ColorResearchHome-20261030/round11/natural-coverage-run1 --stages N:/pixart/ColorResearchHome-20261030/round11/natural-stage-finite-v1 --output <全新saved-audit.json>
```

实际续接证据：home-round11/resume-20261101。阶段 tap 的序号跨照片按进程累加，不是每图从 0000 重新开始；首次新增审计器因此报缺文件，失败日志保留在 checks-final.json/saved-natural-audit.txt。修复文件定位并补回归后，checks-completion.json/saved-natural-audit.json 记录实际 6 图、18 次全通道比较及 72 阶段读回全部通过。旧像素、报告和门槛均未修改；没有重渲染。

run_resume_checks.py 保留首次检查调用参数，续接封存时日志改为排他创建，避免覆盖旧失败记录；run_completion_checks.py 为修复后调用记录，输出采用排他创建。重做核验应选择全新输出路径，不把失败记录改写成成功。自然照片、所有自然阶段像素仍只留本地。
