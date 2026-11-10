# Chrome采集包安装与打包门禁

准备入口的唯一模板为本目录 `Prepare-Guangjing-Capture.cmd`。Native Host已经支持无包路径参数的 `--prepare`，从 `AppContext.BaseDirectory/..` 解析包根，并保留存在的隔离数据根。

禁止再生成 `--prepare "%~dp0"`：尾部反斜杠会让结尾引号进入.NET收到的路径。模板只给exe本身加引号，不向--prepare传包路径；捕获原生退出码后再pause，最终exit /b保留失败。

每次组装完桌面与Native Host二进制后，归档前必须执行：

```text
python tools/chrome-capture/package_launchers.py <package-root>
python tools/chrome-capture/test_prepare_launcher.py <package-root> <new-evidence-folder>
```

第二项使用真实Native Host运行含空格解压目录，并通过独立副本移走DEVELOPMENT_ID来验证失败退出码；不写注册表，不动Chrome或真实扩展待处理项。图片/页面采集窗口与Chrome IndexedDB不得为修复启动脚本而重载、清理或覆盖。

本机安装修复报告见 docs/validation/chrome-capture-install-fix-20261110/INSTALL_FIX_REPORT.md。原开发包身份和预验收记录保留，实际公司解压目录及注册读回以安装修复回执为准。
