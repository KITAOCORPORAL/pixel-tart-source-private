# 构建、测试与截图复现

工作目录 N:/pixart/kitao-planning-r5-ui-20261013，Windows x64，.NET10。

    dotnet test tests/RAWSelectionAssistant.Tests/RAWSelectionAssistant.Tests.csproj -c Release -p:Platform=x64
    dotnet test tests/RAWSelectionAssistant.WpfTests/RAWSelectionAssistant.WpfTests.csproj -c Release -p:Platform=x64
    dotnet publish src/RAWSelectionAssistant/RAWSelectionAssistant.csproj -c Release -r win-x64 --self-contained true -p:Platform=x64 -p:PublishSingleFile=false -p:InformationalVersion=2.3.0-planning-r5-ui+34c7af4a0b652704d9bdbfc3cab3f970e6c62445 -o artifacts/r5-release-final/win-x64

设置KITAO_R5_SCREENSHOTS为绝对输出目录后运行PlanningR5UiTests，生成160矩阵及4状态PNG。无此变量仍执行逻辑布局矩阵，不写图。图片只含授权合成数据。模块宽度按屏幕像素/scale扣固定主壳宽估计；不打开Windows主窗口、不切系统DPI。

发布测试独立core/wpf目录，使用测试host自身运行时，覆盖发布Main/Core/Kernel/模块DLL并核对哈希，不将整个自包含runtime拷进testhost。Core的PIXEL_TART_BATCH12_RELEASE_CORE指向隔离测试目录内真实发布Core，启用实际程序集位置门禁。EXE不启动，仍NOT_RUN。

原始TRX在artifacts/r5-tests，日志artifacts/r5-*.log。失败TRX保留；最终TEST_SUMMARY记录每次计数/跳过名/失败名与哈希。完整测试会生成旧ui-guardian报告：保留本轮生成副本后仅恢复本工作树基线文件。

不提交原型、参考PNG、客户照片、数据库、凭据、Release二进制。离屏截图/并排差异图在本地artifacts交付，证据仓库只提交索引/哈希与状态。原图著作权/媒体授权不由本批代码变更扩展。
