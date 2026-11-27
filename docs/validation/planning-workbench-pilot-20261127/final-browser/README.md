# 最终公司工作台浏览器验收

独立 QA 项目 `5db8a1f2-72fa-4977-a98e-9688f4a5d67d` 从基础版 revision 26 开始。首次回合不修改原验收脚本，A 用回环 HTTPS、B 用有线 IPv4 HTTPS；两个独立持久浏览器配置，同一台实际 Windows。QA 端口 45753 不是审稿入口。[运行服务及前端的 SHA-256](RUNNING-BYTES.json)取自监听该端口的发布进程目录。未测试第二台实体机器、操作系统中文候选窗、物理打印或 Word/WPS 的逐页外观。

| 最终包回执 | 实测结果 |
|---|---|
| [原主链路 13/13](browser-20261127-final/BROWSER-E2E.json) | B 取得三张与源副本相同的原图。[A 定位衣领](browser-20261127-final/06-owner-collar-location.png)，回复并解决；B 将原第 3 镜移至第 6 镜；[两端重开](browser-20261127-final/08-member-reopened.png)后图、位置、评论、镜头顺序和署名保留。无 JS 异常。 |
| [浏览器补验 6/6](browser-supplement-final-2/BROWSER-SUPPLEMENT.json) | 新页粘贴含 2528 个 UTF-16 单元的中文长段落；刷新后 SHA 完全相同、仍显示本机有改动、服务器版本未变。点击保存后才提交。仅“衣领”取等宽字体、3px 字距；行高 1.8；编辑器重开与干净预览均保持。最终[预览首屏](browser-supplement-final-2/wide-preview-first-screen.png)窗口 1700px、对话框 1360px、表格 1238px 且可见 1238px，概览表无需横向滚动。实际从浏览器下载[交付 DOCX](browser-supplement-final-2/browser-delivery.docx)，OOXML 中该段完整、局部字体 Consolas、字距 45 twip、整段行距 432/240。 |
| [同一衣领实物几何 4 阶段](browser-collar-final/BROWSER-COLLAR-GEOMETRY.json) | 对主链路中已解决的 B→A 原讨论，分别在原放置、裁切、拖柄放大及重载后定位。按 DOM 中实际原图的屏幕边界、裁切窗与初始归一化框独立计算，与可见叠框四边尺寸最大分量误差 0.0182 CSS px。源 SHA、源归一化 rect、放置对象和已解决状态保持。见[裁切前](browser-collar-final/01-before.png)、[裁切后](browser-collar-final/02-after-crop.png)、[放大后重开](browser-collar-final/04-after-reopen.png)。 |

补验初次在错误的外层 DOM 节点查找 dirty 标记，结果 PARTIAL；当时 2528 字与“本机有改动”实际已恢复。仅更正测试选择器后，新建隔离文档再次完整执行 6/6；没有靠该首次结果宣称恢复通过。几何测量以 CSS 像素计，不代表专用显示器像素校准。所有截图只含合成身份及受权样例，没有邀请、令牌或个人浏览器配置；长文粘贴通过浏览器 paste 事件完成，不接触系统剪贴板。
