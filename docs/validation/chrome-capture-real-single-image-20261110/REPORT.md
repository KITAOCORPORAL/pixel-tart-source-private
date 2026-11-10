# 公司Chrome普通JPEG真实采集回执

2026-11-10，Europe/Berlin；记录 2026-11-10T08:12:59.444313+01:00。本次状态 **PASS_USER_CHROME_SEND_DESKTOP_IMPORT_AND_SOURCE_OPEN**。扩展首版整体仍为 **PARTIAL_OTHER_REAL_CHROME_SCENARIOS_PENDING**。

## 本次实际结果

用户在本地会话反馈原文“已发送”。随后从当前公司配套桌面的company01真实journal找到Completed记录，核验素材库实体、托管原图、项目关系、灵感板关系，再从实际完整桌面原生入口点“打开原网页”，真实公司Chrome打开匹配HTTP页面。

**实际AssetId：`88490206-e6c5-4597-973d-4a1042abbfb7`**。

| 字段 | 本次真实值 |
|---|---|
| Chrome采集ID | `287b50b2-74be-4aeb-856a-b5e5ea6a36b4` |
| 素材库 | 浏览器采集测试库，`6ebb56f2-831e-48e7-a55c-243daffad92c` |
| 项目 | 采集项目 A，`6829e4fa-f338-46ea-88c4-3ff7db86455c` |
| 灵感板 | 采集灵感板 A，`c61da831-a70c-4d2f-aea1-c19c66974d8c` |
| 页面 | `http://127.0.0.1:51292/ordinary.html` |
| 图片 | `http://127.0.0.1:51292/ordinary-640.jpg` |
| 页面标题 | `光镜 JPEG 右键采集验收` |
| 采集时间UTC | `2026-11-10T06:56:34.428+00:00` |
| 实际入库时间UTC | `2026-11-10T06:56:56.8943558+00:00` |
| 入库类型 | Imported |
| 原图 | 640×480 JPEG，原始字节和托管副本SHA256相同 |
| 原图SHA256 | `831578E607FF9681313E18CF86DF17CB391D911BF7A12460B658BAFB3BB2C460` |

当前库总素材 **1条**，该内容hash对应 **1条**实体；项目A关联和板A引用已直接从实际SQLite读回，均指向此AssetId，没有拿机器协议探针的AssetId替代。

## 采集ID差异与保留

安装修复时保留的旧窗口ID39cdab10-fcab-4a76-8663-4403125baf48在桌面journal中无提交记录；本次完成的是上表新的287b50b2记录。以实际Completed记录为准，没有改写旧ID或模拟其发送。代理没有清理Chrome队列、重载扩展或控制被URL策略禁止的扩展页面。用户的本次发送、实际新记录、fixture原图hash和目的地关联共同构成当前Chrome→本机入库证据。

## 打开原网页

实际发布桌面进程 **4280** 的可见OpenCapturedSourcePage按钮由原生UIA触发，NATIVE_OPEN_SOURCE.json返回sourceActionExists=true。随后公司Chrome新标签 **1541117533** 的URL为 `http://127.0.0.1:51292/ordinary.html`，标题 `光镜 JPEG 右键采集验收`；页面可见图片complete=true、naturalWidth=640、naturalHeight=480。截图保留实际HTTP原网页。

浏览器工具只允许http/https，禁止观察chrome-extension页面；因此扩展窗口绿色成功文字没有由代理独立读取，未将“已发送”改写为“代理已观察扩展显示成功”。桌面Completed状态和真实AssetId/原图/关联及原网页打开已独立核验。

## 版本与交付

冻结桌面、Native Host和扩展仍为 **ed5d15d451c5f92dc8ff99d9a4ff91af3e76289c**；安装修复交付a3c4a35c。12个冻结关键文件的hash全部匹配，src及extensions无变更，本轮未重建软件、未改注册或队列。本轮RAW0，全部模块/策划15项/教程退役保持。

实际包 `E:/Kitao-Deliveries/11.10/Guangjing-ChromeCaptureV1-Diagnostic-win-x64`。原始本地证据 `E:\Kitao-Deliveries\chrome-capture-v1-20261110\chrome-real-capture-20261110T070215Z`；最终源码/远端读回单列CURRENT_CAPTURE_PUSH_READBACK.json。

余下srcset、原图不可取得、重复/跨项目复用、桌面暂不可用重开重试、目标失效的**真实Chrome界面**验收仍单列待做；既有机器9项/恢复检查不能替代这些UI项目。当前回执仅收口这张普通JPEG的用户发送→桌面实际入库→打开原网页。
