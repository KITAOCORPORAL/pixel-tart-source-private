# 光镜 Chrome 单图采集 v1

基线：在线选片验收2661f2d3 / 产品7769b8ee；独立分支feature/chrome-capture-v1-20261110。Eagle右键单图入口、Raindrop简洁保存窗口与最近目的地、Image Harvest桌面交接意图作为交互参考；没有复制这些产品源码。Chrome协议按官方Native Messaging与activeTab规范实现。批量、侧栏、自动整页抓取不在首版。

## 单图闭环

图片右键“发送到光镜”→仅读取与右键URL匹配的img/picture元数据→显式srcset最大w或x候选（混合/歧义不猜测）→读取原始JPEG/PNG字节→IndexedDB保存待处理→本机列出可用素材库/项目/灵感板→用户明确选择→Native Messaging→当前Windows用户Named Pipe→完整桌面既有ImportWithIdentitiesAsync/ManagedCopy/内容hash身份/ProjectAssetLink/InspirationTray关联→持久化实际AssetId回执→扩展严格核对身份和hash后显示成功。

入口没有替换采集动作：CLI/NativeHost协议测试仅是机器侧证据；只有真正Chrome右键触发后的回执才能算Chrome入口验收。开发版需要用户在Chrome扩展页面手动加载unpacked目录，并导入单项当前用户注册文件。首次脚本准备命令遭自动审批拒绝后改为只生成可检查.reg文件，代理不执行注册表修改。

## 身份、恢复和所有权

CaptureId在首次操作时生成并落盘。全局事务与来源journal以CaptureId锁定内容和目的地指纹；同ID重试不能更换目标/内容。提交前可能出现阶段性完成，重试通过既有内容hash去重复用素材，项目与灵感板关联幂等，只有读回关联和实际素材后完成回执。改选目的地是用户明确操作并生成新CaptureId，同一库内相同图片优先复用现有AssetId；不同库继续使用LibraryId+AssetId身份。

目的地每次发送前及导入后重新核验；失效不改送最近/第一个项目。最近目的地只在成功回执后更新。元数据与操作journal为本机应用数据，以LibraryId+AssetId引用；图片由既有托管导入持有副本，来源URL、图片URL、页面标题和采集时间持久化。素材检查器提供“打开原网页”。

桌面不可用时扩展保留图片字节和元数据，不显示成功；连接恢复后用户重试同项。已完成素材/关联后来被人工删除或改动时拒绝伪造旧回执。原图403/404、读取失败、格式不支持、原图过大均明确报错，不降级到页面缩略图。

## 权限与边界

必需：contextMenus / activeTab / scripting / storage / nativeMessaging。没有tabs、history、cookies、downloads、webRequest、通知或持久全站内容脚本。optional_host_permissions仅声明HTTP/HTTPS候选范围，只有用户在“读取/重试原图”点击时申请本次图片源域名。网络读取credentials=omit；本地原图缓存和IndexedDB仅用于用户主动采集。DOM仅读取右键所选图片的匹配节点。

首版支持HTTP/HTTPS页面中的JPEG/PNG；单图16MiB、32MP；srcset只选择明确可解析的最高声明候选，不能宣称自动找到所有网站私有原图接口。Native请求24MiB上限，回执1MiB上限，stdio四字节小端长度前缀。Native manifest限定稳定开发扩展ID；管道CurrentUserOnly，无HTTP监听器。正常桌面不自动开启桥接，配套入口仅传当前进程环境配置。

原始字节SHA256、托管副本及回执hash必须一致，没有RAW解码或像素重算。Core只增加本机数据journal，原无HTTP/凭据断言保留。全模块/策划15项/教程退役和完整诊断包保持，无安装器。

## 参考资料（2026-11-10核对）

- Chrome Native Messaging: https://developer.chrome.com/docs/extensions/develop/concepts/native-messaging
- Chrome activeTab: https://developer.chrome.com/docs/extensions/develop/concepts/activeTab
- Chrome匹配规则: https://developer.chrome.com/docs/extensions/develop/concepts/match-patterns
- Eagle官方扩展: https://eagle.cool/extensions
- Raindrop官方扩展: https://help.raindrop.io/install-extension
- Image Harvest公开功能介绍只作用户指定的桌面交接参考，未引入其实现或依赖。
