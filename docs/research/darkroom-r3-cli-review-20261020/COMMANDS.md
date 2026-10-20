# 可复现命令与边界

工作区：E:/Kitao-Worktrees/darkroom-r3-company-20261020。SDK：D:/AI AGENT/.dotnet/dotnet.exe。仅当前子进程设置 DOTNET_ROOT、APPDATA、LOCALAPPDATA、NUGET_PACKAGES、ProgramFiles 和独立测试数据根，未更改系统持久配置。

## 最终发布


default cwd = E:/Kitao-Worktrees/darkroom-r3-company-20261020


dotnet publish src/RAWSelectionAssistant/RAWSelectionAssistant.csproj -c Release -r win-x64 --self-contained true --no-restore -p:PublishSingleFile=false -p:PhotographerTrialBuild=true -p:SourceRevisionId=d2e52278841b797744af1ce749e2a5e9b170fed1 -p:IncludeSourceRevisionInInformationalVersion=true -o E:/Kitao-Deliveries/darkroom-r3-company-20261020/app-cli-verified-stamped

## 最终 WPF（每类独立进程）

先将发布包中测试目录已有的20个同名DLL复制入测试bin，并记录双方hash；不能在复制后再build而覆盖它们。

dotnet test tests/RAWSelectionAssistant.WpfTests/RAWSelectionAssistant.WpfTests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~CLASS --logger "trx;LogFileName=CLASS.trx" --results-directory OUTPUT

CLASS：R3CommandLineReviewTests、LightPrintR3Tests、PhotoGradePageV1Tests、InstantV3CorrectionBindingTests、InstantV3PhaseCancellationTests、ColorStudioZoomPanTests、ColorStudioFilmstripStateTests、StudioQuickExportFormatTests、BatchFourVersionTests。

显式照片manifest和证据目录由 KITAO_R3_CLI_AUTHORIZED_MANIFEST / KITAO_R3_CLI_OUTPUT / KITAO_PHOTO_PAGE_AUTHORIZED_MANIFEST 提供；无扫描。测试不Show窗口，不调用Computer Use。

## Core

dotnet test tests/RAWSelectionAssistant.Tests/RAWSelectionAssistant.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~ProfessionalRawPipelineTests|FullyQualifiedName~ProfessionalRawTiffTests|FullyQualifiedName~ColorStudioSessionStateTests|FullyQualifiedName~ColorStudioStackTests|FullyQualifiedName~BatchNineInstantFilmTests" --logger "trx;LogFileName=core.trx" --results-directory OUTPUT

完整每次命令与退出码在BUILD_LEDGER和本地各任务JSON；这是CLI回归不是桌面验收。
