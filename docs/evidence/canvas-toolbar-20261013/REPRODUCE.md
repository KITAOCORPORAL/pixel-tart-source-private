# 检查与复现命令

在 N:/pixart/kitao-canvas-toolbar-20261013 执行，.NET 10，Windows x64。无需浏览器、localhost、Computer Use 或桌面输入。

    dotnet test tests/RAWSelectionAssistant.Tests/RAWSelectionAssistant.Tests.csproj -c Release -p:Platform=x64
    dotnet test tests/RAWSelectionAssistant.WpfTests/RAWSelectionAssistant.WpfTests.csproj -c Release -p:Platform=x64
    dotnet test tests/RAWSelectionAssistant.WpfTests/RAWSelectionAssistant.WpfTests.csproj -c Release -p:Platform=x64 --filter "FullyQualifiedName~CanvasToolbarTests|FullyQualifiedName~ColorStudioZoomPanTests|FullyQualifiedName~ColorRangePreviewOverlayTests|FullyQualifiedName~StudioLocalizationTests"
    dotnet publish src/RAWSelectionAssistant/RAWSelectionAssistant.csproj -c Release -r win-x64 --self-contained true -p:Platform=x64 -p:PublishSingleFile=false -p:InformationalVersion=2.3.0-canvas-toolbar+ea8c437bf44fbaef20556fd5a073e93c4053452a -o artifacts/canvas-release/win-x64

发布组件回归：将Core/WPF测试输出复制到本工作树独立ignored测试目录，用发布的KitaoPhotoSelector.dll、Core、Kernel及各模块DLL覆盖同名文件。保留测试host的运行时和deps配置，不将self-contained运行时整套覆盖测试host。逐项SHA比较见RELEASE_IDENTITY。然后对这两个独立目录执行dotnet vstest。

Core设置 PIXEL_TART_BATCH12_RELEASE_CORE 为实际测试目录内发布Core的绝对路径，以启用程序集位置门禁。所有实际命令输出保存在 artifacts/canvas-*.log，TRX在 artifacts/canvas-tests。TEST_SUMMARY内有计数、跳过名、失败名和TRX哈希。

发布EXE不在本轮自动启动，不能将托管组件测试通过称为实际桌面启动通过。
