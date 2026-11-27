# 光镜「共享到团队」｜家庭复现交接

这是独立的 Windows 候选版。家庭机从自己有权限的源码工作树构建，用三张随仓库附带的 CC0 样片在新目录生成独立项目。没有公司地址、公司数据库、成员链接、证书私钥或真实客户资料需要带回家。

- 分支：`feature/planning-share-to-team-20261127`。旧工作台试点源码：`d3cf9290d946c5c74c5735df631a7f7097898366`；旧试点回执：`58e0d6166afab544469dc856c5e455b6dc78a218`。
- 本轮功能源码：`4900732c8ea643401bfaea41f5477142cd7bd874`，公司普通 EXE 由该干净工作树构建。家庭构建使用 `1bec347fc07937ca0309d1f4766ad7fbdebd8675`：此后续提交只修正启动器记录 EXE 路径的时机，产品源码和候选二进制没有变动。已验步骤及剩余事项见[验收回执](docs/validation/planning-share-to-team-20261127/REPORT.md)。

**回家后第一件事：**按下文构建并生成新的隔离样例；A 在桌面首次共享后，用第二台实体 Windows 电脑上的 B 通过新邀请加入。先确认家庭局域网和公有证书可以正常访问，再验证 B 改通告、圈衣领，及 A 重开原项目一次点回同一工作台。公司侧主流程使用一台实体 Windows 和独立成员浏览器；无需重新调查桌面入口。

## 1. 从干净源码构建

需要 Windows x64、**.NET SDK 10.0.302**、**PowerShell 7.5 或更新版本**与 **Microsoft Edge WebView2 Runtime**。如果当前窗口是 Windows PowerShell 5.1（`powershell.exe`），先运行 `pwsh.exe -NoLogo -NoProfile` 再执行下列 PowerShell 代码。不要用 5.1 直接运行启动器：它缺少候选所用的 .NET API 和进程环境选项。若 `pwsh.exe` 不在 PATH，先打开已安装的 PowerShell 7，或显式使用其 `pwsh.exe` 路径。

先在已授权的仓库内取回分支；已有该分支的工作树时进入它，避免覆盖本地改动。下面是独立、可重复创建的 detached 复现工作树：

```powershell
git fetch origin feature/planning-share-to-team-20261127
$sourceSha = '1bec347fc07937ca0309d1f4766ad7fbdebd8675'
git cat-file -e "$sourceSha^{commit}"
if ($LASTEXITCODE -ne 0) { throw '家庭构建提交还未从远端取得。' }
git worktree add --detach ../planning-share-home-src $sourceSha
if ($LASTEXITCODE -ne 0) { throw '无法创建工作树；如已有工作树，先进入并核对 HEAD。' }
Set-Location ../planning-share-home-src
git status --short --branch
git rev-parse HEAD
```

上面固定构建 SHA 的工作树用于复现。要继续提交开发，进入已有对应分支的工作树；若本机从未检出该分支，可在仓库里执行 `git worktree add --track -b feature/planning-share-to-team-20261127 ../planning-share-home-dev origin/feature/planning-share-to-team-20261127`，从最新交接提交接续。不要在有本地改动的工作树里强制切换或覆盖。

在仓库根选择自己机器上的 `dotnet.exe` 和一个尚未存在的交付目录。若有多个 SDK 导致仓库 `global.json` 自动选到较新版本，传入独立的 10.0.302 SDK 目录中的完整 `dotnet.exe` 路径：

```powershell
$dotnet = 'dotnet.exe'                  # 或 'C:\SDKs\dotnet-10.0.302\dotnet.exe'
& $dotnet --list-sdks
& $dotnet --version                     # 必须等于 10.0.302
$delivery = Join-Path (Split-Path -Parent (Get-Location).Path) ('PlanningShareHome-' + (Get-Date -Format yyyyMMdd-HHmmss))
& ./tools/PlanningShareFixture/Build-ShareHome.ps1 -Delivery $delivery -Dotnet $dotnet
```

[构建脚本](tools/PlanningShareFixture/Build-ShareHome.ps1)默认拒绝有未提交改动的工作树和已存在的目标。它执行以下三次构建，复制启动器及[样片与许可](tools/PlanningShareFixture/ApprovedSamples/LICENSE.md)，并在 `build-receipt.json` 写入 SDK、源码 SHA、源码状态和产物哈希。`-AllowDirty` 只用于开发诊断，回执会标记 `UNFROZEN_WORKTREE`。候选通过 `PlanningReviewPilot=true` 开启共享操作，通过 `WholeAppDiagnosticBuild=true` 要求隔离数据根；启动普通 `KitaoPhotoSelector.exe` 的完整桌面，主窗口标为诊断预览，不使用验收宿主或 `--planning-review`。

```powershell
# 手工构建时，仍使用未被旧候选占用的新目标目录。
& $dotnet publish tools/PlanningReview/PlanningReview.csproj -c Release -r win-x64 --self-contained true -o (Join-Path $delivery 'Service')
& $dotnet publish src/RAWSelectionAssistant/RAWSelectionAssistant.csproj -c Release -r win-x64 --self-contained true -p:PlanningReviewPilot=true -p:WholeAppDiagnosticBuild=true -o (Join-Path $delivery 'Desktop')
& $dotnet publish tools/PlanningShareFixture/ShareFixture.csproj -c Release -r win-x64 --self-contained true -o (Join-Path $delivery 'Fixture')
Copy-Item -LiteralPath ./tools/PlanningReview/Start-ShareCandidate.ps1 -Destination $delivery
Copy-Item -LiteralPath ./tools/PlanningReview/Start-PlanningShare.cmd -Destination $delivery
Copy-Item -LiteralPath ./tools/PlanningShareFixture/ApprovedSamples -Destination $delivery -Recurse
```

## 2. 先仅启动服务，再生成样例，最后打开桌面

让电脑和 B 成员设备连接同一个可信任的家庭局域网。用 `Get-NetIPConfiguration` 找有家庭网关的网卡，用 `Get-NetIPAddress -AddressFamily IPv4` 核对其当前 IPv4（不是回环、VPN 或虚拟网卡），再用 `Get-NetTCPConnection -State Listen -LocalPort <端口>` 确认专用端口空闲。下面地址与端口都是例子，请换成实际值；启动器也会核查实际监听进程和地址。

```powershell
$lan = '192.168.1.23' # 替换为家庭电脑当前实际 IPv4
$port = 45861        # 示例：使用独立空闲端口
$launcher = Join-Path $delivery 'Start-ShareCandidate.ps1'
& $launcher -Delivery $delivery -Lan $lan -Port $port -HostOnly
& (Join-Path $delivery 'Fixture/ShareFixture.exe') $delivery (Join-Path $delivery 'ApprovedSamples')
if ($LASTEXITCODE -ne 0) { throw '隔离样例生成失败。保留目录检查，不启动桌面。' }
& $launcher -Delivery $delivery
```

第一次使用 `-HostOnly` 会生成本机随机目的地 ID、专用 HTTPS 证书和控制密钥，并校验回环与家庭局域网两个监听口。`Fixture` 在首次启动桌面之前创建 `_private/desktop-data`，生成原生 SQLite 项目、已保存的 WPF 富文本、三张项目自有 JPEG、六个分镜与固定 PNG。两个项目都叫「秋冬服装型录」，副标题分别为「首轮样例｜北窗衣领」「同名对照｜独立项目」，项目和文档 ID 各自不同；`_private/fixture.json` 可对照这些 ID 和第三镜的衣领图。若已生成数据，后续只重跑启动器，切勿再跑生成器覆盖；需要全新测试时另建一个交付目录。不要直接双击候选 EXE，否则不会得到这次启动器的隔离目录和团队配置。

`_private/` 包含 HTTPS 私钥、控制密钥、候选专用桌面和服务数据，只留在主机。脚本不会导入证书信任、改防火墙或停止占用端口的其他应用。若家庭地址变化，或证书过期，保留旧记录并换新交付目录配置；不要用另一个地址启动已经绑定的目录。复开同一候选可以双击 `Start-PlanningShare.cmd`，也可以执行 `& $launcher -Delivery $delivery`。确需重启该服务时才传 `-HostOnly -RestartHost`，它只重启已核对路径、PID 和启动时间的候选监听器。

## 3. 在真实家庭网络做 A / B 验收

A 是这次启动的普通桌面：进入 **策划中心**，选择副标题「首轮样例｜北窗衣领」，在 **文字** 页增加一行唯一的家庭验收文字并保存。点击项目标题栏的 **共享到团队**，检查预检提示，发布成功后应打开负责人工作台。核对新增文字、加粗/斜体/深绿色「衣领织纹」、可编辑的「拍摄通告」表、6 镜的原顺序与第 3 镜衣领图、3 张样片及情绪板里的固定画布 PNG。修改一个表格单元格或当天时间表，保存后再在另一端核对。

B 是另一台电脑或手机的全新浏览器会话。先核对交付根目录公开证书 `团队协作候选证书.cer` 的 SHA-256 是否等于启动器回执中的 `certificateSha256`，然后让 B 按设备规则仅信任这次本地证书。浏览器能否信任这种证书由家庭设备独立验证；若遇到 TLS 错误，先记录，不把桌面所做的指纹钉扎视为 B 已信任。B 只需要公有 `.cer` 与自己的邀请链接；测试结束按设备规则移除这次测试的证书信任。不要把 `_private`、`.pfx` 或控制密钥发给 B，也不要用公网端口转发代替家庭局域网验收。

A 在负责人工作台的 **项目成员** 填写 B 的署名 → **生成成员邀请** → **复制成员链接**。也可以点 **查看邀请码**，让 B 在项目网址的「邀请码或个人迁移码」框填写。B 加入后打开第 3 镜的「打开现场参考」，选择「框选图片批注」，圈衣领并留言；再改一格正文通告表或「当天时间表」，点击「保存修改」。A 应看到 B 的署名与内容。同一浏览器资料关闭再打开会保留 B 的会话；换浏览器或电脑用 B 自己的个人迁移码。已消费的邀请码不能再次领取。邀请和迁移码视作凭据，回执只记录结果。

回到 A 的本地策划再改一行并保存，然后点 **继续团队协作**：应打开同一线上房间，保留 B 的编辑和批注，本地新改动不会覆盖线上。重开普通 EXE 后再核对一次。最后打开同名对照项目，确认它仍显示 **共享到团队**（未开过的本地身份不会自动绑定到首个房间）。需要三端证据时再加第三个独立成员设备与邀请，把「同机浏览器隔离」和「实际不同设备」分别记录。

记录：功能 SHA、SDK、`build-receipt.json` 的 EXE 与 DLL 哈希、A/B 设备及日期、成功与失败的具体步骤。截图与日志只使用验收样例，可以掩盖当前地址与凭据。故障优先看隔离目录的 `_private/service-*.err`、`desktop-receipt.json` 与应用日志；本机回环正常但 B 不通时，先核对设备是否同网段、访客网络隔离、可信任网络下 Windows 是否允许该候选服务入站以及证书/系统时间，再重试。
