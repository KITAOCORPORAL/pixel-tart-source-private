**公司局域网：[打开光镜团队策划工作台](https://192.168.1.10:45751/workbench.html)。成员把负责人给的码填入「邀请码或个人迁移码」；负责人从本机入口点「项目成员 → 填写受邀成员署名 → 生成成员邀请 → 查看邀请码」。**

新工作台已可审、可编辑、可保存。团队审核实例为《秋冬服装型录》，预置 17 张策划页、6 镜和 3 张共享样片。另有独立的私人基础版，用于从零开始、练习升级或修改项目；它与团队审核实例的数据不混用。原 [45731 局域网审稿入口](https://192.168.1.10:45731/)和原数据保留。正式桌面界面未接入。

| 审核入口 | 进来可做什么 |
|---|---|
| [基础版列表 · 本机直达](<E:/Kitao-Deliveries/planning-workbench-pilot-20261127/private-basic/entries/owner-list.html>) · [局域网地址](https://192.168.1.10:45752/workbench.html#list) | 搜索、筛选并打开私人项目；点「页面模板」加新页。这个入口保留基础版权限；其他成员无需进入这里。 |
| [可编辑情绪板 · 本机直达](<E:/Kitao-Deliveries/planning-workbench-pilot-20261127/private-review/entries/owner-mood.html>) · [团队地址](https://192.168.1.10:45751/workbench.html#mood) | 从右侧拖素材、裁切和替换，调整对齐和图层。替换保留边框、位置和旋转，新图重新取裁切。 |
| [文字排版页 · 本机直达](<E:/Kitao-Deliveries/planning-workbench-pilot-20261127/private-review/entries/owner-brief.html>) · [团队地址](https://192.168.1.10:45751/workbench.html#brief) | 在「文字 · 项目简报」选区改字体、字号、粗斜体、下划线、颜色、字距；段落单独改对齐、行高与样式。画布另有独立标题框。 |
| [带批注分镜 · 本机直达](<E:/Kitao-Deliveries/planning-workbench-pilot-20261127/private-review/entries/owner-shots.html>) · [团队地址](https://192.168.1.10:45751/workbench.html#shots) | 看第 3 镜的参考和两条示例批注，在右栏「讨论」点「定位」可见衣领框。可改拍摄要求、换序、回复和解决。 |

本机入口仅在托管 Windows 电脑使用。[四个正式入口检查](LIVE-ENTRY-AMENDED.json)已确认能直达、地址栏清除身份码、两个正式项目数据未被测试修改。若服务在重启后没起来，双击[启动光镜团队工作台](<E:/Kitao-Deliveries/planning-workbench-pilot-20261127/启动光镜团队工作台.lnk>)或[启动光镜基础版工作台](<E:/Kitao-Deliveries/planning-workbench-pilot-20261127/启动光镜基础版工作台.lnk>)。其他成员连公司 LAN、从各自的邀请文件或网页加入。公司已为「公司审稿员 C」预留一个单次邀请，可由负责人在项目成员面板查看；使用后需要迁移时取自己的迁移码。基础版若要邀请，先在同一份策划上「升级为团队项目」；专业版可单独启用选区、分镜、图片批注。团队共有 1 名负责人和 3 个成员席位，领取、过期、撤回和移除沿用已有规则。首次开网页若出现本地证书提示，核对负责人入口或邀请文件给出的 SHA-256 指纹。正式订阅和独立公开账号登录未启用。

[桌面原入口 → 线上入口 → 行为与缺项](DESKTOP-COVERAGE.md)按 15 个分区列出名称、字段、详情和动作。[桌面现场](DESKTOP-RUNTIME.md)用普通已发布 EXE，打开 15/15、7 类资源表、12 列分镜、图片上下文菜单、文档页签和导出菜单；[浏览器导航回执](WEB-ROUTES.json)逐区打开 15/15 并记录各表。摄影演示实际包含项目简报、拍摄通告、情绪板、造型与道具清单、分镜与拍摄清单、灯位示意。模板、素材中转站和最近记录是独立标出的工作台扩展。原生副本导入读取相同项目身份、现有资源、分镜和已验证图片；[原生投影与恢复回执](BACKEND-NATIVE-PROJECTION.json)对照原文件 SHA，恢复包重导入仍用同一页 ID。

[最终浏览器验收](final-browser/README.md)在一台公司 Windows 10（构建 19045）的两个独立 Chromium 159 配置和成员身份完成：主链 13/13、草稿与排版补验 6/6、衣领批注几何 4 阶段。A 用回环地址，B 用公司有线 IPv4；B 的三张图与服务中的项目副本逐字节相同。模板、三图拖入、裁切、换图保版式、选区改字、关联分镜、圈注、回复解决、原第 3 镜拖到第 6 镜、两端重开都通过；含 2528 个 UTF-16 单元的中文草稿刷新后完整恢复，点保存才提交。批注在文字前插、图片裁切或放大后仍能定位，换到新图提示旧图。另在[最终服务并发验证](FINAL-API-CONCURRENCY.json)实测不同镜头/页面并行保存、同段冲突保留双方内容、丢响应重送不重复和撤权后拒绝状态、图片及写入。加验发现重命名对话框的关闭事件会提前取消操作，修复后[页面操作 6/6](FINAL-PAGE-ACTIONS.json)通过，包括复制未保存的表格、重命名和取消、排序与撤销，以及编辑副本后重开。

从「交付预览」可打开干净稿与含批注稿；浏览器已实测二者区分、图片与排版。浏览器实际下载的 [Word 样稿](final-browser/browser-supplement-final-2/browser-delivery.docx)包含可编辑正文、部分段落格式、表格、分镜和图片图注，能保留选区字体、字距和段落行高；不嵌入原图，也不保留自由画布几何、裁切或讨论。公司浏览器已检查宽表首屏完整，实际打印、PDF 文件逐页核对和 Word/WPS 逐页外观待验。原桌面还提供摄影 DOCX 导回、15 区汇总 DOCX、图片式 PDF 和原生项目包；工作台不是这些路径的全量移植。

仍待完成：单服务托管多个项目、跨项目素材库浏览、全局日历/收支/工具箱联动、普通附件共享、原生历史画布/富文档完整往返、将工作台资源表编辑反写桌面，以及正式桌面界面接入。工作台文件列表目前只编辑元数据；可共享 JPEG/PNG 项目副本。所有审稿入口在公司 Windows 上通过回环和有线地址验收；第二台物理电脑的入站网络与证书信任、Windows 实体键盘和中文候选窗未验。浏览器的中文 composition 和长文 paste 有独立证据。

本轮源码：[`feature/planning-workbench-pilot-20261127@d3cf9290`](https://github.com/KITAOCORPORAL/pixel-tart-source-private/commit/d3cf9290d946c5c74c5735df631a7f7097898366)。[已部署载荷与版本绑定](DELIVERED-BYTES.json)分别记录主链测试时的前端、之后只更新的重命名脚本和最终入口复验；托管程序集在提交前构建，因此其版本字符串显示基线 SHA，实际 DLL 与静态资源以上述文件摘要为准。原试点和脱敏回执保存在 [`a398d6c264b4a90b6e6ab01a0cf453f8e5945312`](https://github.com/KITAOCORPORAL/pixel-tart-source-private/commit/a398d6c264b4a90b6e6ab01a0cf453f8e5945312)。样片均为 Petr Kratochvil 的 CC0 图片：[轮廓](https://commons.wikimedia.org/wiki/File:Fashion_model_portrait.jpg)、[衣领](https://commons.wikimedia.org/wiki/File:Autumn_fashion.jpg)、[姿态](https://commons.wikimedia.org/wiki/File:Hair_In_Wind.jpg)。本仓库仅保存脱敏回执、样例操作截图和样例生成的交付文稿。

| 线 | 结果 / 分支与回执 / 待验 |
|---|---|
| 团队策划 | 工作台可编辑、保存和审阅；`feature/planning-workbench-pilot-20261127`，[最终验收](final-browser/README.md)；待实体跨机、系统中文输入法和正式 UI 审核。 |
| 公司 ICC | 捕获时旧处理预览 0 条托管强根，无可归因持有者；`research/rgb48-second-open-holders-20261123@4f738b91`，[同次 ICC、暂停、GC 与退出](COMPANY-ICC-EXISTING.md)；普通 TIFF 重开与三轮容量另验。 |
| 选片 | 两个导出目录各只有 B，A 四星保留；`test/company-tether-client-export-20261126@459fa0c5` 是分支回执基线，实际桌面 `05a9291f`，[核对版本、设备和 JPEG](COMPANY-SELECTION-EXISTING.md)；待真实手机。 |
