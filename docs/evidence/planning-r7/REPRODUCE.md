# R7 重现

工作目录 D:/AI AGENT/kitao-planning-r7-20261014。SDK D:/AI AGENT/.dotnet/dotnet.exe（10.0.302）。仅进程环境变量，未修改持久配置。

```powershell
$env:TEMP="$PWD/artifacts/r7/temp"; $env:TMP=$env:TEMP
$env:PIXEL_TART_ISOLATED_RUNTIME='1'
$env:PIXEL_TART_ISOLATED_RUNTIME_ROOT="$PWD/artifacts/r7/reproduce/runtime"
$env:KITAO_R7_EVIDENCE="$PWD/artifacts/r7/reproduce/screenshots"
$env:KITAO_RICH_FLOW_EVIDENCE="$PWD/artifacts/r7/reproduce/r6-fixture"
$env:KITAO_PLANNING_COMPANY_SCREENSHOTS="$PWD/artifacts/r7/reproduce/company"
& 'D:/AI AGENT/.dotnet/dotnet.exe' test tests/RAWSelectionAssistant.Tests/RAWSelectionAssistant.Tests.csproj -c Release -p:Platform=x64 --logger 'trx;LogFileName=core.trx'
& 'D:/AI AGENT/.dotnet/dotnet.exe' build tests/RAWSelectionAssistant.WpfTests/RAWSelectionAssistant.WpfTests.csproj -c Release -p:Platform=x64
./tools/planning-r7/Invoke-IsolatedWpf.ps1 -BinaryDirectory "$PWD/tests/RAWSelectionAssistant.WpfTests/bin/x64/Release/net10.0-windows10.0.19041.0" -SourceDirectory "$PWD/tests/RAWSelectionAssistant.WpfTests" -OutputRoot "$PWD/artifacts/r7/reproduce/wpf"
```
完整WPF包含既有合成窗口测试；不是Computer Use和人工验收。完整套件有已记录失败，脚本progress.json和TRX必须检查，不能只看PowerShell退出码。

发布：
```powershell
& 'D:/AI AGENT/.dotnet/dotnet.exe' publish src/RAWSelectionAssistant/RAWSelectionAssistant.csproj -c Release -r win-x64 --self-contained true -p:Platform=x64 -p:PublishSingleFile=false -p:InformationalVersion=2.3.0-planning-r7+9446c1997ea88575072f0f9a8b72e4bc95eb8687 -o artifacts/r7/release-final/win-x64
```
按顺序完成构建再发布，避免同目录DLL锁。将测试目录复制到独立目录，只替换RELEASE_IDENTITY中的产品DLL，hash比对后vstest执行；不覆盖testhost的runtime。WPF发布专项filter：PlanningR7、PlanningRichFlowTests、PlanningFoundationWpfTests、PlanningCompanyFollowupTests、PlanningR5UiTests。Core发布组件执行完整Core测试DLL。

人工验收仍NOT_RUN：真实键盘Ctrl+F/H与列表焦点、表格鼠标选区、IME、WPS合并格/页码域更新、物理DPI、真实打印机/PDF驱动、安装启动。勿将OpenXML改包及XPS序列化标成WPS/打印机通过。
