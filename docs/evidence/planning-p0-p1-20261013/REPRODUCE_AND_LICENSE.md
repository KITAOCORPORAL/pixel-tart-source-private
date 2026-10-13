# 复现与许可

在独立工作树运行 .NET 10 Windows x64，不需要浏览器。

    dotnet test tests/RAWSelectionAssistant.Tests/RAWSelectionAssistant.Tests.csproj -c Release -p:Platform=x64
    dotnet test tests/RAWSelectionAssistant.WpfTests/RAWSelectionAssistant.WpfTests.csproj -c Release -p:Platform=x64
    dotnet publish src/RAWSelectionAssistant/RAWSelectionAssistant.csproj -c Release -r win-x64 --self-contained true -p:Platform=x64 -p:PublishSingleFile=false -p:InformationalVersion=2.3.0-planning-foundation+28642bc71bb09dcc7b0def84f9cda02bc8483b1a -o artifacts/planning-release/win-x64

发布测试：测试输出复制到artifacts/planning-release-tests的core/wpf独立目录，仅以发布Main/Core/Kernel/模块DLL覆盖同名文件，不覆盖测试host运行时。逐项哈希在RELEASE_IDENTITY。Core设置PIXEL_TART_BATCH12_RELEASE_CORE为独立测试目录内实际发布Core路径，启用位置校验，然后dotnet vstest执行完整程序集。EXE未启动。

使用用户授权的合成数据：临时目录内文本JSON、临时SQLite项目和故障文件锁；无客户照片、生产库、凭据。没有新增第三方依赖、下载资产或外部算法。复用现有WPF/.NET/OpenXML依赖，但没有新增或宣称完整Word/WPS能力。

输入依据：基线docs/evidence/canvas-toolbar-20261013/PLANNING_IMPLEMENTATION_BATCHES.md及真实代码。原研究/审计/其他工作树未修改。测试改写的ui-guardian历史报告副本保留ignored artifacts，跟踪文件恢复本轮基线。
