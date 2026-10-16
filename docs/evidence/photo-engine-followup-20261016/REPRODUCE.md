# 可复现运行（本机实际树；所有输出目录必须全新）

工作树：N:/pixart/kitao-photo-engine-followup-20261016，SDK10.0.401，Release/x64。原件只读；下面所有RAW实验只接已核验的artifacts/followup/authorized副本。不得把PRIVATE图像/raw buffer/数据库/原件加入Git。

## 构建与专项

    dotnet build tests/RAWSelectionAssistant.Tests/RAWSelectionAssistant.Tests.csproj -c Release -p:Platform=x64
    dotnet build tests/RAWSelectionAssistant.WpfTests/RAWSelectionAssistant.WpfTests.csproj -c Release -p:Platform=x64
    dotnet build tools/VerifiedTestRunner/VerifiedTestRunner.csproj -c Release
    dotnet build tools/PhotoEngineFollowupAudit/PhotoEngineFollowupAudit.csproj -c Release
    dotnet test tests/RAWSelectionAssistant.Tests/RAWSelectionAssistant.Tests.csproj -c Release -p:Platform=x64 --no-build --filter "FullyQualifiedName~AssetLibraryP3ScopeOrderingRegressionTests|FullyQualifiedName~AssetLibraryP3PerformanceTests|FullyQualifiedName~DisplayPreviewBoundaryTests|FullyQualifiedName~PhotoBoundaryContractTests"

P3 trace：在运行之前将KITAO_P3_TRACE设为全新输出路径；必需性能方法保留cold+warm3的原始计时和GC pause，不扣除停顿，不改400ms。SQL/EXPLAIN/settings/盘符随trace保存。完整Core默认方法并行保持原样，不以专项最快值代替完整套。

## 真实RAW/显示审计

下列PhotoEngineFollowupAudit.dll实际路径tools/PhotoEngineFollowupAudit/bin/Release/net10.0-windows10.0.19041.0/win-x64。工具调用真实产品API不等同桌面实机。

    dotnet <audit-dll> sony-old <Sony_DSC08015副本> <全新结果目录>
    dotnet <audit-dll> sony-chain <Sony_DSC08015副本> <全新结果目录>
    dotnet <audit-dll> native-stages <授权Fuji或Canon副本> <固定旧raw_r.dll> <全新结果目录> 0
    dotnet <audit-dll> native-stages <授权副本> <固定旧raw_r.dll> <全新取消目录> 100
    dotnet <audit-dll> wic-matrix N:/pixart/kitao-photo-engine-followup-20261016 <全新输出目录>
    dotnet <audit-dll> display-reference <固定lcms2.dll> <全新输出目录>
    dotnet <audit-dll> display-mapping <全新json路径>

sony-workspace之前必须为该子进程显式设置PIXEL_TART_ISOLATED_RUNTIME=1与PIXEL_TART_ISOLATED_RUNTIME_ROOT=<本树全新runtime>，再运行sony-workspace <副本> <新结果目录>。未设置直接拒绝，不能写日常数据根。

run_native_repeats.py只对本机明确授权副本操作；子进程固定4/1线程，不改全局。run_candidate_repeats.py仅固定官方预编译包研究；候选PPM无ICC，合同明确未应用方向。candidate_cancel_probe.py拒绝覆写且固定原生/输入SHA。所有比较脚本的raw buffer/PPM是照片衍生资料，只留本机ignored artifacts。

显示研究compare_display_reference.py对应LCMS2.12/2.19量化8bit；independent_profile_oracle.py的近黑失败保留，不可把FAILED改PASS。未接WPF普通显示入口。

## 完整Core/WPF及发布组件

    python tools/planning-r7/verified_runner.py --assembly tests/RAWSelectionAssistant.WpfTests/bin/x64/Release/net10.0-windows10.0.19041.0/RAWSelectionAssistant.WpfTests.dll --output artifacts/new-source-wpf --dotnet "C:/Program Files/dotnet/dotnet.exe" --sdk "C:/Program Files/dotnet/sdk/10.0.401" --resource-root N:/pixart/kitao-photo-engine-followup-20261016
    python tools/planning-r7/verified_runner.py --assembly tests/RAWSelectionAssistant.Tests/bin/x64/Release/net10.0/RAWSelectionAssistant.Tests.dll --output artifacts/new-source-core --dotnet "C:/Program Files/dotnet/dotnet.exe" --sdk "C:/Program Files/dotnet/sdk/10.0.401" --single-process --resource-root N:/pixart/kitao-photo-engine-followup-20261016

同样对06-published-tests/core及wpf DLL运行；完整Core在两套WPF结束后顺序执行。本轮run_final_core.py负责此顺序及独立日志，不覆盖此前结果。发布Core需PIXEL_TART_BATCH12_RELEASE_CORE指向发布测试host中的实际Core.dll；另跑Batch12ReleasedAssemblyTests确认身份。

发布host从原测试host复制，仅替换发布Main/Core/Kernel/modules/DX12同名组件；native RID路径按SHA确认，不用自包含hostfxr/runtime覆盖测试host。实际来源见PUBLISHED_PROVENANCE/PUBLISHED_AUDIT_PROVENANCE。

## 诊断Release

    dotnet publish src/RAWSelectionAssistant/RAWSelectionAssistant.csproj -c Release -r win-x64 --self-contained true -p:Platform=x64 -p:PublishSingleFile=false -p:InformationalVersion=2.3.0-photo-followup-BLOCKED+52d2f3db... -o <全新发布目录>

本轮实际全SHA见IDENTITY.json及RELEASE_FILES.json。源码冻结52d2f3db提交完整SHA由06-release-source.txt/身份文件记录，后续提交仅工具/证据，不冒称最后文档HEAD是Release来源。

## 封存与门禁

    python tools/photo-engine-followup/seal_followup.py

仅适用于本轮固定全量输出路径且还未封存时运行；拒绝覆盖ZIP。逐个TRX/发现/参数行/计数/测试DLL/发布文件核验，再归档精确日志/TRX/summary/discovery/runsettings。不递归扫runtime、图片或数据库。严格review门禁与HOME发行状态独立：自动测试可只剩条件缺口，但Fuji与显示/发行/实机仍BLOCKED。
