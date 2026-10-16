# 可复现命令（家里实际路径，不引用公司发布包）

工作树：N:/pixart/kitao-photo-engine-home-20261016；Windows11 26200 x64，SDK C:/Program Files/dotnet/sdk/10.0.401。

## 构建与发现完整测试

    dotnet build tools/VerifiedTestRunner/VerifiedTestRunner.csproj -c Release
    dotnet build tests/RAWSelectionAssistant.Tests/RAWSelectionAssistant.Tests.csproj -c Release -p:Platform=x64
    dotnet build tests/RAWSelectionAssistant.WpfTests/RAWSelectionAssistant.WpfTests.csproj -c Release -p:Platform=x64
    python tools/planning-r7/verified_runner.py --assembly tests/RAWSelectionAssistant.WpfTests/bin/x64/Release/net10.0-windows10.0.19041.0/RAWSelectionAssistant.WpfTests.dll --output artifacts/new-full-wpf --dotnet "C:/Program Files/dotnet/dotnet.exe" --sdk "C:/Program Files/dotnet/sdk/10.0.401" --resource-root N:/pixart/kitao-photo-engine-home-20261016
    python tools/planning-r7/verified_runner.py --assembly tests/RAWSelectionAssistant.Tests/bin/x64/Release/net10.0/RAWSelectionAssistant.Tests.dll --output artifacts/new-full-core --dotnet "C:/Program Files/dotnet/dotnet.exe" --sdk "C:/Program Files/dotnet/sdk/10.0.401" --single-process --resource-root N:/pixart/kitao-photo-engine-home-20261016

Core性能门禁应在WPF套和审计负载结束后顺序执行；本轮最后源码Core仍出现原400ms范围门槛失败，不反复挑绿、不改预算。runner是adapter发现逐精确FQN匹配TRX，不以源代码数测试。23WPF/6发布Core条件未执行按原名保留。

## 诊断目录，不是已批准审阅发布

    dotnet publish src/RAWSelectionAssistant/RAWSelectionAssistant.csproj -c Release -r win-x64 --self-contained true -p:Platform=x64 -p:PublishSingleFile=false -p:InformationalVersion=2.3.0-home-photo-BLOCKED+ff1df9b27db4b14530d1d9a85107e9351a6a623b -o artifacts/home-photo/07-blocked-diagnostic/win-x64

该目录290文件，阻断状态独立记录，成功build不能覆盖Fuji重复性或性能失败。产品EXE未启动。发布测试host为正常测试输出复制到08-published-tests，只用该发布目录Main/Core/Kernel/模块替换同名DLL，记录hash；不把自包含hostfxr/runtime整体覆盖测试host。发布Core的PIXEL_TART_BATCH12_RELEASE_CORE指向08-published-tests/core中的Core.dll，再执行全量及身份单项。

## 本机后端审计与参考

    dotnet build tools/PhotoEngineHomeAudit/PhotoEngineHomeAudit.csproj -c Release
    dotnet PhotoEngineHomeAudit.dll inventory <new-json>
    dotnet PhotoEngineHomeAudit.dll product-raw <authorized-copy> <pinned-original-SHA> <new-output-directory>
    dotnet PhotoEngineHomeAudit.dll legacy-codes <authorized-copy> <pinned-original-SHA> <new-json>
    dotnet PhotoEngineHomeAudit.dll compare-tiff <first-product-tiff> <second-product-tiff> <new-json>
    dotnet PhotoEngineHomeAudit.dll wic-reference <new-output-directory>

实际DLL位于tools/PhotoEngineHomeAudit/bin/Release/net10.0-windows10.0.19041.0/win-x64。wic-reference要求KITAO_HOME_SOURCE_ROOT绑定本树。产品源码/原生DLL与独立审计发布目录逐SHA匹配；此工具调用真实产品API，但不是启动产品/导航验收。不要给非授权私人文件使用审计。

LCMS比较使用compare_lcms_references.py，显式传source-root/wic-result/native/output；固定现有2.12binarySHA，Pillow2.19版本与moduleSHA另记。第一次理想OETF失败结果保留；exactProfile参考max6码、两个LCMS构建8bitmax0，不扩展任意ICC。

官方候选包只在ignored candidate-local，archive/EXE/DLLSHA全固定。probe_official_candidate.py只用授权清单中hash一致文件副本，完整PPM读回geometry/payload/sha；进程termination不是LibRawcallback。cleanVM/source rebuild/distribution gate不闭合，不放普通Release。

原始完整results.trx、runsettings、stdout/stderr、发现清单与第一次失败已在RAW_TEST_RESULTS.zip封存；不包含图片、RAW、SQLite、凭据、native候选或发布二进制。


## 本次续接的证据封存与核验（不重新挑绿运行性能测试）

以下命令在工作树根执行。每个输出目录必须全新；已有结果不覆盖。

    python -m unittest discover -s tools/photo-engine-home -p "test_*.py" -v
    python -m unittest discover -s tools/planning-r7 -p "test_*.py" -v
    python tools/photo-engine-home/seal_delivery.py --output artifacts/home-photo/new-delivery-integrity

已运行6+7项自检，全通过。封存器逐结果重读原TRX、精确FQN/参数行/计数，核验最终测试DLL、290个发布文件和测试/后端实际组件身份，再ZIP逐成员SHA回读。原结果FAILED/NotExecuted不改变。首次WPF测试DLL被后续构建覆盖的限制单独记录，不把现有DLL冒充首次版本。

### 对既有结果重算门禁

    python tools/planning-r7/review_gate.py --summary docs/evidence/photo-engine-home-20261016/02_tests/02-final-core-summary.json docs/evidence/photo-engine-home-20261016/02_tests/02-final-wpf-summary.json docs/evidence/photo-engine-home-20261016/02_tests/08-published-core-summary.json docs/evidence/photo-engine-home-20261016/02_tests/08-published-wpf-summary.json docs/evidence/photo-engine-home-20261016/02_tests/08-published-identity-summary.json --policy tools/planning-r7/review-classification.json --output artifacts/new-strict-postpublish.json --postpublish
    python tools/photo-engine-home/home_acceptance_gate.py --review artifacts/new-strict-postpublish.json --product docs/evidence/photo-engine-home-20261016/03_backend/CANON_PUBLISHED_BACKEND.json docs/evidence/photo-engine-home-20261016/03_backend/FUJI_PUBLISHED_BACKEND.json --repeatability docs/evidence/photo-engine-home-20261016/04_color_audit/FUJI_FIRST_REPEAT_DIFFERENCE.json --reference docs/evidence/photo-engine-home-20261016/06_reference/LCMS_EXACT_PROFILE_HOME_REFERENCE.json --output artifacts/new-home-acceptance.json

两个命令当前都应返回1并输出BLOCKED；不是命令失灵。09-gate-recheck已逐JSON比较，保持原判定不变。门禁58条openGates来自源码和发布重复记录，唯一未运行条件是29项，不得声称新增58个缺口。

### 精确profile数值参考复现

    $env:KITAO_HOME_SOURCE_ROOT = 'N:/pixart/kitao-photo-engine-home-20261016'
    dotnet artifacts/home-photo/07-audit-runtime/win-x64/PhotoEngineHomeAudit.dll wic-reference artifacts/new-wic-reference
    python tools/photo-engine-home/compare_lcms_references.py --source-root N:/pixart/kitao-photo-engine-home-20261016 --wic-result artifacts/new-wic-reference/wic-reference.json --native artifacts/home-photo/07-blocked-diagnostic/win-x64/lcms2.dll --output artifacts/new-exact-profile-reference.json

该脚本还要求wic-result同目录有本机对应output-srgb.icc；ICC文件不入Git。优先先把wic-reference运行在全新输出目录，再将该目录wic-reference.json传入。原本机profile在artifacts/home-photo/06-wic-reference-fixed/output-srgb.icc；不要用其他profile替代。Pillow和本机LCMS二进制身份见参考JSON；未安装依赖时不得称已复现。
