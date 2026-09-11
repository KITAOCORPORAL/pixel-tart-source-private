# Pixel Tart 开发环境迁移指南

## 项目环境

操作系统：Windows 10 x64（Build 19041 或以上）或 Windows 11 x64。迁移源电脑为 Windows 10 Pro 22H2，Build 19045。

开发框架：C#、WPF、MVVM；在线选片 LocalDev 预览包含 ASP.NET Core 服务和微信小程序客户端。

.NET版本：.NET SDK 10.0；项目目标框架为 `net10.0` 和 `net10.0-windows10.0.19041.0`，默认运行目标为 `win-x64`。迁移源电脑验证版本为 10.0.302。

Node版本：主解决方案无需 Node.js。微信小程序由微信开发者工具运行；仓库没有 `package.json` 或 Node 依赖安装步骤。迁移源电脑的可选 Node.js 版本为 v24.15.0。

Python版本：主程序构建无需 Python。仅生成 UI 验收拼版时需要 Python 3；迁移源电脑的可选版本为 3.13.13。

数据库：SQLite，通过 NuGet 包 `Microsoft.Data.Sqlite` 和 `SQLitePCLRaw.bundle_e_sqlite3` 使用。数据库、用户数据和 LocalDev 运行数据均在本机生成，不进入 Git。

依赖工具：

- Git for Windows，并具备私有 GitHub 仓库访问权限。
- .NET 10 SDK。
- Visual Studio（可选，安装“.NET 桌面开发”工作负载）或其他支持 C# 的编辑器。
- Inno Setup（仅生成 Windows 安装包时需要）。
- 微信开发者工具（仅开发在线选片微信小程序时需要；登录信息和 LocalDev token 不进入 Git）。
- PowerShell 5.1 或更新版本。

## 安装步骤

新电脑：

1. 安装 Git for Windows、.NET 10 SDK；如需图形调试，再安装带“.NET 桌面开发”工作负载的 Visual Studio。确认命令可用：

   ```powershell
   git --version
   dotnet --version
   ```

2. 从 GitHub 克隆唯一源码仓库，并切换到迁移报告指定的最新分支：

   ```powershell
   git clone https://github.com/KITAOCORPORAL/pixel-tart-source-private.git
   cd pixel-tart-source-private
   git fetch --all --prune
   git switch migration/pixel-tart-handoff-20260911
   ```

3. 还原依赖并构建：

   ```powershell
   dotnet restore RAWSelectionAssistant.sln
   .\build_debug.ps1
   ```

   可选的完整验证：

   ```powershell
   dotnet test RAWSelectionAssistant.sln --configuration Debug
   ```

4. 启动桌面应用：

   ```powershell
   .\run_app.ps1
   ```

   在线选片 LocalDev 预览使用独立临时数据库，不读取正式产品数据：

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\tools\PixelTart_OnlineSelection_LocalDev_Preview.ps1
   ```

## Codex继续开发流程

拉取最新代码：

```powershell
git switch migration/pixel-tart-handoff-20260911
git pull --ff-only
```

创建分支：

```powershell
git switch -c feature/<功能名称>
```

提交前先确认状态，并确保没有图片、视频、RAW、数据库、缓存、日志、用户数据、构建产物或凭据：

```powershell
git status
git diff --check
git diff --cached
git commit -m "type(scope): 简述修改"
```

push：

```powershell
git push -u origin feature/<功能名称>
```

如果克隆后的远程名称不是 `origin`，先执行 `git remote -v`，并在上述命令中使用实际远程名称。不得用 zip、聊天附件或本地目录副本作为代码源。

## 当前开发状态

已完成：

- Pixel Tart UI Design System v1 的 Token、Theme、公共组件工程化。
- Asset Library 与 Tether Capture 的视觉迁移基础。
- 全局 ResourceDictionary / `StaticResource` 启动稳定性修复及 Theme Startup smoke test。
- 在线选片 V1 的隔离 LocalDev Preview、ASP.NET Core 本地服务、SQLite 存储和微信小程序客户端基础。
- Windows x64 Developer Preview 构建链验证；安装包和发布目录不进入 Git。

进行中：

- Asset Library 页面 Phase 2 的视觉与测试收尾保存在独立功能分支。
- Windows 10/11 实体电脑上的 100%、125%、150% DPI 与安装体验复核。
- 在线选片 LocalDev 的多客户端冲突合并、完整规则强制和真实微信开发者工具验收仍未完成。

下一步：

1. 在个人电脑克隆迁移分支，执行 restore、Debug build 和测试。
2. 以真实 Windows 显示缩放验证启动、主题、字体、图标、素材库导入、缩略图、Inspector 和布局切换。
3. 再从迁移分支创建新的功能分支继续开发；不要直接在 `main` 上工作。
4. 用户数据、测试素材、LocalDev token、数据库、日志和安装产物只保留在个人电脑本地。
