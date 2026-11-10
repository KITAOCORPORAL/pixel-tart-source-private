# 公司电脑 Chrome 采集安装修复

2026-11-10，Europe/Berlin；记录 2026-11-10T07:32:30.944202+01:00。状态 **INSTALLATION_FIXED_BRIDGE_READY_CHROME_SEND_PENDING**。

从feature/chrome-capture-v1-20261110@189178e3接续。已完成实际解压包定位、准备脚本修复、无包路径参数准备、当前Chrome用户HKCU注册及读回、配套桌面启动和真实Native目录回执。原Chrome采集窗口/页面及待处理项不重载、不清理；本次条纹图实际发送仍需用户在原扩展窗口点击，不能用协议模拟结果替代它的AssetId。

## 实际路径与复现

实际包：`E:\Kitao-Deliveries\11.10\Guangjing-ChromeCaptureV1-Diagnostic-win-x64`。

原启动行是 `"%~dp0native-host/Guangjing.Capture.NativeHost.exe" --prepare "%~dp0"`。批处理根目录以反斜杠结尾，额外包路径参数经Windows原生参数解析后把末尾引号带入路径，实际异常路径出现 `win-x64"/extension/DEVELOPMENT_ID.json`。ORIGINAL_PREPARE_REPRO.log保留原始stderr。Native进程失败后pause还掩盖了失败，原CMD外部退出码为0。

修复为只传 `--prepare`，不传包路径。Native源码已确认 `args.Length==1` 时从 `AppContext.BaseDirectory/..` 定位包根。实际包内直接执行成功，清单、配置和.reg已重建；原隔离资料根 **`E:\Kitao-Deliveries\chrome-capture-v1-20261110\company01`** 及管道名保持。

仓库唯一模板 `tools/chrome-capture/Prepare-Guangjing-Capture.cmd`，后续包通过 `package_launchers.py` 复制模板。先捕获Native errorlevel再pause，exit /b返回原结果，失败不再伪装成功。

## 含空格路径与失败传播

实际复制自包含Native Host到两个独立含空格目录运行：

- 首轮回归 `package with spaces/Guangjing Chrome Capture`，准备成功exit0，清单exe指向该真实目录，原数据根不变。
- 可重放仓库门禁 `test_prepare_launcher.py` 在 `unpacked folder with spaces/Guangjing Capture Package` 同样成功。它只在专有副本中临时移走身份文件，确认Native失败能传播为非零退出码3762504530；随后恢复该文件。回归不写注册表、不启动新用户资料根、不动Chrome存储。

## 当前Chrome用户与注册

当前命令用户SID与当前Chrome进程所属用户SID已核对一致。完整SID只用于本机核验，没有写入公共报告。仅修改 **HKCU/Software/Google/Chrome/NativeMessagingHosts/com.guangjing.capture** 的默认REG_SZ；32/64位读取视图均指向实际11.10目录下的 `native-host/com.guangjing.capture.json`。原值已在本地HKCU_NATIVE_HOST_BEFORE.json保存；未改HKLM或其他Chrome设置。

Chrome主进程14472与配套桌面4280的令牌级别也一致，均为同一当前用户。只读检查了相关Chrome Native Messaging策略，未发现禁止当前用户主机的配置；没有修改策略。

清单name为com.guangjing.capture、type为stdio，exe为本实际包内的Guangjing.Capture.NativeHost.exe；allowed_origins只允许 `obgahfdabcpkohkdcnidpmeooceomogk`。读回见HKCU_NATIVE_HOST_READBACK.json。含空格回归生成的测试注册文件没有导入，实际注册仍指向11.10包。

## 桌面与桥接

实际运行Start-Guangjing-Capture.cmd，日志记录配套桌面PID4280。通过同一实际Native Host的标准I/O协议请求destinations，得到ok=true/Ready，包含 **2个素材库、3个项目、1个灵感板**，不是静态选项或伪造回执。

实际普通JPEG页在51291和51292均返回200；当前已打开的条纹图可继续使用，无需重新右键新建一项。

## 唯一剩余人工动作

现有采集ID：`39cdab10-fcab-4a76-8663-4403125baf48`。桌面查询状态 **NOT_SUBMITTED**，因此目前没有这次采集的实际AssetId。

浏览器工具对chrome-extension协议返回明确URL策略拒绝，仅允许http/https。没有改走原生Chrome窗口、CDP、扩展私有存储或间接发送来绕过此限制。注册、目录回执与桌面安装步骤已独立完成。

请用户在原“发送到光镜”窗口点“刷新目的地”，如尚未选定则选择“浏览器采集测试库 / 采集项目 A / 采集灵感板 A”，再点“发送原图”。收到操作反馈后，从桌面journal核验这同一CaptureId的AssetId/hash/项目和板关联，再从光镜可见入口打开原网页并提交真实Chrome采集回执。当前不以机器协议探针AssetId替代它。

## 包与源码

二进制、扩展源码和已加载的开发扩展文件均未修改；冻结产品仍是ed5d15d451c5f92dc8ff99d9a4ff91af3e76289c。修改仅为启动/打包工具与安装说明，不需要重建桌面或重载扩展。Core无HTTP/凭据原断言保持，RAW调用0，全部模块/策划15项/教程退役保持。

实际11.10 ZIP已修补，原ZIP备份保留：`E:\Kitao-Deliveries\chrome-capture-v1-20261110\install-fix-20261110T060755Z\ORIGINAL_DEVELOPER_PACKAGE.zip`。新包 **112,968,483 bytes / 512 files**，SHA256 `1B7AB147766ECC67A7D550DB44BCCB7BF310A900A348834F8B63B295F63B89D3`；逐项读回通过。新包包含固定模板及INSTALL_LAUNCHER_FIX.json；下一个包也必须先执行package_launchers.py和含空格门禁。

本地原始证据：`E:\Kitao-Deliveries\chrome-capture-v1-20261110\install-fix-20261110T060755Z`。最终提交与REMOTE_HEAD分开保存INSTALL_FIX_PUSH_READBACK.json。原开发包、机器证据及最初失败均保留。

公开console日志仅规范化换行和行尾空格以满足Git检查；本地原始GBK/原生输出字节保留，逐文件原始SHA与公开SHA见PUBLIC_LOG_NORMALIZATION.json。失败信息和退出码未改。
