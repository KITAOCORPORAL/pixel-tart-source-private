# 开发版安装与真实Chrome验收

光镜 Chrome 单图采集开发版 0.1.0

一、当前状态
本机 Native Host / 完整桌面实际入库、相同CaptureId重放、同图关联项目B复用AssetId、桌面重开重试、目标失效、源文件恢复与来源网页入口已验。真实 Chrome 右键入口等待开发版手动加载后验收，不把协议探针当 Chrome 入口。

二、在当前公司电脑安装（当前用户注册，不需要Python或.NET SDK）
1. 将整个文件夹放在固定位置。双击 Prepare-Guangjing-Capture.cmd，仅生成/更新本文件夹的主机清单和当前用户注册文件，不自动写注册表。
2. 检查 Register-Guangjing-NativeHost.reg：唯一目标为 HKCU/Software/Google/Chrome/NativeMessagingHosts/com.guangjing.capture，路径指向本文件夹。确认后，在当前Windows用户的普通命令提示符执行：reg import "E:\Kitao-Deliveries\chrome-capture-v1-20261110\Guangjing-ChromeCaptureV1-Diagnostic-win-x64\Register-Guangjing-NativeHost.reg"。也可使用当前用户注册表编辑器导入；不要切换到其他管理员账户。不要覆盖其它同名既有注册项；如已安装另一版本先核对其路径。
3. 在公司 Chrome 打开 chrome://extensions，开启开发者模式，选择“加载已解压的扩展程序”，选择本包 extension 文件夹。稳定扩展ID：obgahfdabcpkohkdcnidpmeooceomogk。
4. 双击 Start-Guangjing-Capture.cmd，打开配套完整桌面。当前机器已准备“浏览器采集测试库”“采集项目 A/B”“采集灵感板 A”。验收资料根独立，不读取日常项目数据。复制到其它电脑后若无目的地，请先在桌面创建/打开素材库并准备项目/灵感板。

三、单图闭环（先做普通JPEG）
当前测试页服务已在本机51292就绪；失效时双击Start-TestPages.cmd并保留测试窗口，Ctrl+C结束。
Chrome打开 http://127.0.0.1:51292/ordinary.html → 在条纹图片上右键“发送到光镜” → 选择测试库、采集项目A、采集灵感板A → 发送原图 → 等“已确认入库”及实际AssetId。
切到光镜，查看被选中的图片，在图片标题下点“打开原网页”，应回到该测试页。扩展未收到匹配回执时不会显示成功。

四、余项验收
srcset页：http://127.0.0.1:51292/srcset.html，应采1600×1200声明原图，不是320×240缩略图；按实际原图哈希核对。
原图失败页：http://127.0.0.1:51292/unavailable.html，最大候选403，应明确报错，不能把可见缩略图送入库。
再次右键普通图送项目A，素材不重复；改送项目B且灵感板选择“不加入”，应复用同一AssetId并新增关联。
关闭桌面后采集或重试，待处理项保留；重开后同一项重试不重复。目的地失效时重新选择，不能自动投递到第一个项目。

五、权限/范围
必需权限 contextMenus、activeTab、scripting、storage、nativeMessaging。没有全站持久内容脚本、tabs/history/cookies/downloads/webRequest。跨源原图读取仅在你点击重试时申请该图片域名，网络请求不携带浏览器cookies。源元数据只取右键图片对应的匹配DOM节点。
首版JPEG/PNG，单图16MiB/32MP；只选择明确可解析的srcset最高候选，失败不降级缩略图。批量和侧栏下一轮。
桥接为当前Windows用户Named Pipe，无采集HTTP接口。普通Start-Guangjing-Diagnostic.cmd不启用桥接。测试页HTTP仅承载合成静态资料，不能导入图片。
来源URL/标题/时间和操作回执持久化在本机应用数据，以LibraryId+AssetId引用；原始字节经既有托管导入，无RAW解码或像素重算。全模块、策划15项、教程退役保持，WholeAppDiagnosticBuild=true / PhotographerTrialBuild=false，无安装器。

产品源码：ed5d15d451c5f92dc8ff99d9a4ff91af3e76289c
