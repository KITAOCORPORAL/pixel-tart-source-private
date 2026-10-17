# 可复现命令与文件类型

根N:/pixart/kitao-instant-film-product-v3-20261017。业务日期Asia/Shanghai，所有实验使用全新结果目录，禁止覆写原失败；测试项目和TRX名字唯一。

    python -m unittest discover -s tools/instant-film-v3 -p "test_*.py" -v
    python tools/instant-film-v3/audit_dual_intake.py --grok <精确完整Grok.zip> --muse-csv <明确Muse.csv> --muse-json <明确Muse.json> --handoff <明确交接.md> --business-date 2026-10-17 --output <全新.json>
    dotnet test tools/InstantFilmCandidate.Tests/InstantFilmCandidate.Tests.csproj -c Release --logger "trx;LogFileName=Candidate-new.trx" --results-directory artifacts/new-candidate
    python tools/instant-film-v3/references/build_upstream_subsets.py
    python tools/instant-film-v3/references/run_upstream_subsets.py

参考builder要求local ignored固定源码和Zig0.13，实际文件/命令/hash已在RUNTIME_SUBSTAGES.json；生成GPL源/exe只留artifacts，不进产品/Git。不得把本机文件已读当其他机器已具备环境。

    dotnet build tools/PhotoEngineHomeAudit/PhotoEngineHomeAudit.csproj -c Release
    dotnet <本树PhotoEngineHomeAudit.dll> instant-full <严格授权冻结清单.json> <全新输出目录>
    dotnet <本树PhotoEngineHomeAudit.dll> instant-rasters <上一步全尺寸结果目录>

严格清单是Id/FrozenTiff/Original/ExpectedOriginalSha256/ExpectedFrozenSha256；私有原件和衍生图像不提交。完整源逐行读取、输出条带、原件前后hash，不读私人目录列表。此工具不等于软件V3入口。

    dotnet build src/RAWSelectionAssistant/RAWSelectionAssistant.csproj -c Release -p:UiReviewBuild=true -p:Platform=x64
    & tools/instant-film-v3/Invoke-LogicalEvidenceDesktop.ps1
    dotnet test tests/RAWSelectionAssistant.DpiTests/RAWSelectionAssistant.DpiTests.csproj -c Release --logger "trx;LogFileName=Dpi-new.trx" --results-directory artifacts/new-dpi

逻辑生产器在非输入desktop调用本树原流程，不切换用户desktop；拒绝权限时不能退回用户前台。106截图/metadata只本机，提交仅合成metadata身份hash，物理DPI仍NOT_RUN。

完整Core/WPF使用tools/planning-r7/verified_runner.py，明确--dotnet C:/Program Files/dotnet/dotnet.exe --sdk C:/Program Files/dotnet/sdk/10.0.401 --resource-root本树，每套全新输出；Core --single-process保留原方法并行；WPF逐完整class/参数行发现。不以源码正则数测试。实际发布测试host复制相同产品组件并逐hash确认，不用自包含runtime覆盖testhost。

    dotnet publish src/RAWSelectionAssistant/RAWSelectionAssistant.csproj -c Release -r win-x64 --self-contained true -p:Platform=x64 -p:PublishSingleFile=false --artifacts-path <独立全新编译路径> -o <独立全新完整发布路径>

本轮实际冻结版本和逐文件hash见RELEASE_IDENTITY/RELEASE_FILES，诊断版无候选DLL，构建成功不批准发行。

文件类型：01_intake原始字段/审核元数据；02_references固定来源/子阶段数值；03_ownership/04_coordinates合同与TRX；05_product_gate产品未接入的准确状态；06_fullsize数值/hash无图；07_regression发现/summary/TRX日志ZIP、发布身份和DPI身份；所有私人图像、raw buffer、SQLite、编译器/上游源/native候选/Release二进制只留本地ignored。
