# 复现

工作目录 N:/pixart/kitao-planning-workflows-20261013。Windows x64 / .NET10。

    dotnet test tests/RAWSelectionAssistant.Tests/RAWSelectionAssistant.Tests.csproj -c Release -p:Platform=x64
    dotnet test tests/RAWSelectionAssistant.WpfTests/RAWSelectionAssistant.WpfTests.csproj -c Release -p:Platform=x64
    dotnet publish src/RAWSelectionAssistant/RAWSelectionAssistant.csproj -c Release -r win-x64 --self-contained true -p:Platform=x64 -p:PublishSingleFile=false -p:InformationalVersion=2.3.0-planning-workflows+6b3cfc9b91e28675a1b57f59ba846e4de7ea217c -o artifacts/workflows-release/win-x64

发布回归：独立core/wpf测试目录使用正常testhost/deps/runtimeconfig，仅把实际发布Main/Core/Kernel/模块DLL覆盖同名文件；不把自包含hostfxr/coreclr复制进测试host目录。逐DLL SHA对应RELEASE_IDENTITY。Core环境变量PIXEL_TART_BATCH12_RELEASE_CORE指向独立目录中的实际发布Core，再dotnet vstest执行完整程序集。

原始TRX：artifacts/planning-tests。日志：artifacts/stage*.log、artifacts/workflows*.log。TEST_SUMMARY保留文件哈希、总数、真实NotExecuted条目及失败/跳过名称；跳过不能计入通过。EXE不自动启动。

失败记录：一次为了避开正在运行的测试输出，直接使用-o自定义目录混入应用自包含runtime，testhost未找到共享框架而中止，实际0测试；保留stage3-output-runtime-aborted。随后使用独立正常testhost仅覆盖托管DLL，19专项通过。没有删掉该失败来伪造一次成功。
