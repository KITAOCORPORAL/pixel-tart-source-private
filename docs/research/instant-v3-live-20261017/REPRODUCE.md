# 重现与隔离运行

工作树N:/pixart/kitao-instant-film-v3-live-20261017，基准a51bcf96efc0e3d03438138dfa525ab61c7449b7。业务日期Asia/Shanghai，主机日志原时区。

## 准入/公式/所有权/产品专项

    dotnet run --project tools/InstantV3AdmissionProbe/InstantV3AdmissionProbe.csproj -c Release -- <已固定native-references目录> <全新结果目录>
    dotnet test tools/InstantFilmCandidate.Tests/InstantFilmCandidate.Tests.csproj -c Release --logger "trx;LogFileName=Candidate-new.trx" --results-directory artifacts/new-candidate
    dotnet test tests/RAWSelectionAssistant.WpfTests/RAWSelectionAssistant.WpfTests.csproj -c Release -p:Platform=x64 --artifacts-path artifacts/new-isolated-test-build --filter "FullyQualifiedName~InstantV3ProductTests|FullyQualifiedName~StudioLocalizationTests" --logger "trx;LogFileName=Product-new.trx" --results-directory artifacts/new-product

原成熟入口来自上一树固定DT/RT/stb构建（hash/config已保留）；不得假定机器上同名EXE正确。首次pipe阻塞和peak读已退出进程异常留原日志，修复后9输入行为门禁通过。所有参考外部GPL源/生成exe仅ignored，不提交/链接产品。

## 真产品原尺寸直出/预览oracle

    dotnet build tools/PhotoEngineHomeAudit/PhotoEngineHomeAudit.csproj -c Release --artifacts-path artifacts/new-product-tool-build
    dotnet <当前本树PhotoEngineHomeAudit.dll> instant-product <授权冻结清单> <全新输出目录>
    dotnet <当前本树PhotoEngineHomeAudit.dll> instant-product-preview <授权冻结清单> <上一步实际产品直出目录> <全新oracle目录>
    dotnet <当前本树PhotoEngineHomeAudit.dll> instant-product-visual <全新合成对照目录>

授权冻结清单必须含Id/FrozenTiff/Original/ExpectedOriginalSha256/ExpectedFrozenSha256。Fuji只冻结输入，绝不能用新native结果覆盖。instant-product直接调用正式ProfessionalRawExport三格式，每个独立重新条带渲染，PNG/JPEG不读取已写TIFF；oracle才只读TIFF用于独立缩图比较。

原件/照片/导出/预览/数据库/raw buffer禁止Git。合成视觉对照在artifacts/v3-live/06-product-visual/SYNTHETIC_PRODUCT_COMPARISON.jpg，仅本机；不含私人照片。产品操作USER_GUIDE.md。

## 完整回归/发布

使用tools/planning-r7/verified_runner.py，明确--dotnet "C:/Program Files/dotnet/dotnet.exe" --sdk "C:/Program Files/dotnet/sdk/10.0.401" --resource-root本树，各套独立目录，Core --single-process保留方法并行。不要并行改写运行中的testhost输出，锁冲突保留失败后改--artifacts-path新目录。

本轮实际冻结来源和全部290文件见06_regression/IDENTITY.json与DELIVERY_FILES.json；最后文档HEAD不冒作发布源码。发布Core/WPF testhost只替换相同产品Main/Core/Kernel/模块/DX12 DLL并hash确认，不用自包含runtime覆盖。

    dotnet publish src/RAWSelectionAssistant/RAWSelectionAssistant.csproj -c Release -r win-x64 --self-contained true -p:Platform=x64 -p:PublishSingleFile=false --artifacts-path <独立编译目录> -o <独立完整发布目录>
    & tools/instant-film-v3/Invoke-LogicalEvidenceDesktop.ps1
    dotnet test tests/RAWSelectionAssistant.DpiTests/RAWSelectionAssistant.DpiTests.csproj -c Release --logger "trx;LogFileName=Dpi-new.trx" --results-directory artifacts/new-dpi

逻辑生产器要求本树先build -p:UiReviewBuild=true；非输入desktop进程隔离，不SwitchDesktop、不用户输入，不改物理DPI。不能以逻辑91Pass冒充前台验收。

## 封存

    python tools/instant-film-v3/seal_live.py

仅本轮固定全量路径全部完成时运行，拒绝覆盖既有ZIP。逐TRX精确FQN/data row/count/测试程序集身份回读，逐release hash，归档精确summary/discovery/runsettings/stdout/stderr/TRX。无runtime目录递归，无图像/数据库/二进制/凭据。文件清单区分数值/日志/代码/合同/图像的本机位置。
