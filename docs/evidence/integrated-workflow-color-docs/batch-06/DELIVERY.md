# Kitao Studio 光镜 · 第六批交付

日期：2026-10-11（Europe/Berlin）。实际工作树：N:/pixart/pixel-tart-integrated-workflow-color-docs。分支：feature/pixel-tart-integrated-workflow-color-docs。

## 现场与保全

N盘存在，约958GiB可用；真实Git工作树与用户入口映射核对。起点/首次fetch远端均 **cbbab8f4b870d62d345b0e05d66beb343e8ddb6d**，远端未前进。第五批源码 **84d7baaf696ddd82932071f175665ffa2f98d053** 与batch01–05颜色证据已阅读，旧内容和人工状态未重做/升级。

入场已有14个tracked修改和5个untracked RAW草稿，备份到ignored artifacts/batch06/entry-backup后审查续接，不弃置。未发现适用额外AGENTS.md，遵守用户全局Browser/WebView/Computer Use/CDP/Explorer禁止；无reset、clean、强推、自动合并。原集成工作树 **a9f61bab4d6fa048f6ed9412aa4341db7c3e61fc** 及两份未跟踪审计目录保留。

## 源码身份

源码SHA：**d2c20ab7254a61694a96b775643936351ba3ea6c**。

`feat(studio): connect versioned linear RAW decode preview and export pipeline`：21文件，独立PV4 decode/contract、真实Studio入口、会话/目录/导出接线、失败/任务隔离、测试probe。证据提交在此源码之后，具体证据SHA与LOCAL_HEAD/REMOTE_HEAD见交付回复；文档不伪造自己的递归commit hash。

## 已实现

- 固定LibRaw0.21.1 / Sdcb0.21.1.7 / x64最小适配器；黑白电平、已WB、实际camera matrix以及输入/输出参数可检查。
- 显式unpack与dcraw参数，线性ProPhoto D65 RGB16→float，白平衡一次应用；相机×RGB倍率，曝光2^EV。
- 新显示变换：线性sRGB转换、亮度软肩、中性轴色域压缩、sRGB传递函数。不是C1复刻，不是逆sRGB恢复sensor。解码已量化/裁切，后续float保负和超范围直至输出。
- Studio显式“新 RAW 处理版本”新ID，保存转换来源/参数/实际输入；旧项目不迁移。缺配置失败不建成功版本；切换/晚到/旧渲染/缩略/同步隔离。
- 同定义会话重开、永久版本目录、preview1600、全尺寸TIFF16与受管理receipt；旧HDR/仿色/胶片/空间工具禁用。JPG保留已渲染语义，不使用假Kelvin。

PV3已有ACEScg实验契约，不能重新赋义；本批用PV4，旧PV1/2黄金和PV3数学不变。

## 验证与素材

Core最终源码全量：**1633通过/0失败/6跳过**。WPF最终源码全量：**1523通过/0失败/14跳过**（10m58s）；最终源码专项**4/4**，含两相机真实Studio保存重开和TIFF导出；实际发布Core/decoder DLL隔离命令probe两相机也通过。重叠集合不相加。

6实际RAW：Canon EOS5D III一张既有公开本机fixture，Sony A7M4五组授权ARW/JPEG；逐文件副本/源hash、redecode、WB生效、preview与full同坐标、TIFF16回读，误差0。真实人像/火焰混合光存在，但灰卡/实拍阶梯和相机色准视觉质量仍NOT_RUN。合成与真实与人工判断分开。原片/成片/原始TRX/生产库/凭证/二进制未提交。

## Release

EXE：**N:/pixart/pixel-tart-integrated-workflow-color-docs/artifacts/batch06/release/win-x64/KitaoPhotoSelector.exe**。

SHA256：**19D47269C133AF55554216A59E67CCAD5DB8594E859FABD10C65271DA16F90BB**。

ProductVersion：**2.3.0-batch06+d2c20ab7254a61694a96b775643936351ba3ea6c**；FileVersion2.3.0.0；主程序集/Core均同发布源码身份。win-x64 self-contained publish，290个文件hash见RELEASE_IDENTITY.json。

BUILD=PASS。RELEASE_STARTUP、DESKTOP_RUNTIME、USER_VISUAL=NOT_RUN；USER_ACCEPTANCE=NOT_APPROVED。未启动Release、未开浏览器/Explorer、无Installer。不把WPF自动命令当真人验收。

## 具体剩余项与限定评价

本批**生产基础接线完成，质量/人工验收PARTIAL**，不是全专业系统完成：

1. 实拍中性灰/曝光阶梯、相机色准/肤色/极饱和视觉批准；真实未知相机/已WB小RAW专项。
2. JPEG/PNG输出质量、Release启动和桌面/DPI验收。
3. 当前LibRaw0.21.1 RGB16先裁切，没有sensor高光余量；HDR/高光重建另阶段。
4. preview1600 nearest不等于100%细节，抗混叠/空间/GPU另阶段。
5. TIFF原子验证整文件缓冲，Sony单文件独立写出实测峰值约2.06GiB；流式与缓存预算需要后续。
6. 显示器/输出ICC、DCP/双光源、选择性颜色与肤色分别规划，不宣称本批完成。

第五批文档历史100步/128MiB、整份版本冲突选择、无可信记录临时文件保留策略不扩大收尾范围。

## 必需证据

DELIVERY.md、REQUIREMENTS_TRACE.md、LIBRAW_PARAMETER_AUDIT.md、RAW_PIPELINE_IMPLEMENTATION.md、WHITE_BALANCE_AND_EXPOSURE.md、PREVIEW_EXPORT_COMPARISON.md、PERFORMANCE_BASELINE.md、TEST_AND_RELEASE_MATRIX.md、MANUAL_ACCEPTANCE.md、FILES_AND_TYPES.md；追加来源/素材/测试/发布机器可读digest。
