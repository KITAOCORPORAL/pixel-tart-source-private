# 第二组源码命令行审查交付

**状态：R3 未完成；待桌面验收的开发版本。视觉差异未宣称关闭。**

日期：2026-10-20（Europe/Berlin）。产品源码：d2e52278841b797744af1ce749e2a5e9b170fed1。

## 修改与修复

保留第二组全部原始改动并建立本地字节快照。在此基础上修复失效的变体时间绑定、代理图破坏原像素缩放语义、克隆命令绑定、变体选择重入和导出状态/停止显示路径。详情见 REVIEW_FINDINGS.md；八个源码/测试文件见 CHANGED_FILES.md。

## 交付物

- 最终 EXE：E:/Kitao-Deliveries/darkroom-r3-company-20261020/app-cli-verified-stamped/KitaoPhotoSelector.exe
- DLL：E:/Kitao-Deliveries/darkroom-r3-company-20261020/app-cli-verified-stamped/KitaoPhotoSelector.dll
- 本地证据：E:/Kitao-Deliveries/darkroom-r3-company-20261020/cli-review-20261020
- EXE 未启动；不会未经用户再次明确开始桌面验收而恢复 Computer Use。
- 最终程序集 InformationalVersion：2.3.0+d2e52278841b797744af1ce749e2a5e9b170fed1；构建与哈希见 FINAL_BINDINGS.json、RELEASE_MANIFEST.json、BUILD_LEDGER.json。

## 测试身份与范围

最终发布 DLL 原样替换到测试宿主，逐文件哈希一致后，独立进程按类运行。不是借用旧 WPF16/Core27；也不把更早的调试通过次数叠加到最终数字。

最终发布 DLL 实测：WPF 54/54（新增11项+既有43项），Core 30/30；零失败、零跳过。最终原始TRX与身份见 TEST_RESULTS.json。

新增11项：全尺寸/代理比例及属性重新配置2项；模型绑定字段1项；五页命令绑定1项；导出窗口真实命令/格式/进度1项；变体克隆目录仓储及便携重开1项；三格式停止清理与重试3项；两张授权照片实际处理链1项；绑定变体选择与Solo分组1项。

已授权照片只从显式manifest的两文件读取，使用最长边768派生图。PNG16/TIFF16回读最大误差0；JPEG8为有损，误差见 TWO_PHOTO_SUMMARY.json。不能扩展成完整分辨率RAW、相机标定或桌面点击成绩。

## 失败记录

- 第二组早期编译用不存在的 CancelQuickExportCommand；已改 StopExportCommand，原失败日志保留。
- 新增回归初次4通过5失败：两项视区、克隆入口、导出进度真实失败；一项绑定审计测试错误地把 RelativeSource 当VM，修正测试解析。
- 第二次8通过1失败：绑定审计未识别胶片条项的 EditVersions.Count，修正类型归属，不伪改产品字段。
- 第三次9通过1失败：两照片测试相纸初值相同未触发预览，补明确 RequestLightPrintPreview 前置操作；不是把未就绪状态忽略为通过。
- 后续新增回归10/10、扩充11/11，最终以发布包再次重跑。所有失败TRX和日志均保留，不删除。

## 构建记录完整性

最终两次发布均有提交SHA、完整源码文件哈希manifest、命令/退出码/时间、EXE/WPF/Core哈希和完整290文件发布清单。第一次最终发布未启用项目的SHA版本串开关，随后显式开启并重建、重跑全部最终测试；不将其与后一次混称同一二进制。

历史组一/组二现有二进制哈希已补登记，但缺当时完整源码manifest；若中间测试编译产物被覆盖，准确中间DLL哈希不可追溯，台账明确缺口，未以最终哈希冒充。后续最终交付严格绑定冻结源码和实际发布产物。

## 仍未完成及未验收

DESKTOP_ACCEPTANCE.md中的14类项目全部为未验收。完整历史时间线、拖动排序、持久化面板顺序、完整导航/裁切/取色、导出配方管理、完整队列UI及动效规格仍不宣称完成。实际格式选择为下拉框，非配方管理。

未打开HTML、未启动临时服务器/CDP、未生成或复用任何截图证明第二组。前轮14张HTML PNG只算前轮生成；本轮没有视觉关闭证据。

## 推送

只正常推送 feature/darkroom-lightprint-r3-20261019；源代码提交与证据提交分别记录。推送完成及远端精确SHA见 PUSH_STATUS.md。
