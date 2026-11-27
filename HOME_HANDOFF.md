# 光镜「共享到团队」｜家庭接续（WIP 检查点）

- 分支：`feature/planning-share-to-team-20261127`。基线试点源码 `d3cf9290d946c5c74c5735df631a7f7097898366`，公司试点回执 `58e0d6166afab544469dc856c5e455b6dc78a218`。两者不是本轮功能 SHA；本检查点尚无本轮功能提交。
- 特意从已同步的试点回执分出独立工作树，旧分支、稳定桌面、旧局域网入口和实验数据保持原样。已确认当前旧工作台含多页、受管图片、邀请和批注；**普通桌面「共享到团队」入口尚未接线，未构建本轮功能、未跑发布验收。** 正在并行接线，不用重新调查原试点。

## 在家庭 Windows 上接回

从已有本仓库终端执行，保留原工作树；下列分支名与远端一致：

```powershell
git fetch origin
git worktree add --track -b feature/planning-share-to-team-20261127 ../planning-share-to-team origin/feature/planning-share-to-team-20261127
Set-Location ../planning-share-to-team
git status --short --branch
git rev-parse HEAD
dotnet --list-sdks
```

已有同名分支则直接在对应工作树继续，别重复创建。从仓库根用 .NET SDK 10（公司为 10.0.302，可用本机 SDK 或完整 `dotnet.exe` 路径）运行；下面只是**基线构建命令**，不是完整的本轮功能启动：

```powershell
dotnet publish tools/PlanningReview/PlanningReview.csproj -c Release -r win-x64 --self-contained true -o ../PlanningShareCandidate/Service
dotnet publish src/RAWSelectionAssistant/RAWSelectionAssistant.csproj -c Release -r win-x64 --self-contained true -p:PlanningReviewPilot=true -o ../PlanningShareCandidate/Desktop
```

查看 `tools/PlanningReview/Program.cs`（现有 HTTPS 宿主、Room、邀请与批注）、`tools/PlanningReview/NativePlan.cs` 和 `NativeWorkbenchProjection.cs`（已保存原生副本映射）、`src/RAWSelectionAssistant/Views/PlanningCenterView.xaml` 和 `src/RAWSelectionAssistant/ViewModels/PlanningCenterViewModel*.cs`（普通策划中心、保存）。`tools/PlanningReview/fixtures/autumn-collar.jpg` 是原仓库授权 CC0 样片，`PHOTO.md` 有作者与校验。准备三张授权样片和通告表的复现脚本尚未提交；不把公司项目目录当夹具复制。

家庭机另选自己可写的新候选目录、隔离桌面数据根与隔离服务根。候选启动器应根据家庭机选择局域网 IPv4、未占用端口并生成带回环及该 IPv4 的独立证书，固定本机 TLS 指纹，不复用公司路径、地址、私钥或成员凭据。旧试点可参考 `tools/PlanningReview/README.md`，为新测试项目从负责人「项目成员」生成新邀请。测试代码尚未接入之前，旧试点不能代替普通 EXE 主链验收。

**回家后第一件事：**检查是否已有后续提交；若远端仍停在本检查点，接 `PlanningCenterView` 的项目标题栏动作，按「已确认保存 → 稳定项目/文档 ID → 受管副本预检 → 可重试发布并原子就绪 → 从绑定继续」接线。网络路径、后续修改提示、实际桌面到网页验收均待做。
