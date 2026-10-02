# Batch A — 素材库复测

本批代码已修复并通过相关自动测试；真实 UX 仍未通过。

Release EXE：
N:\pixart\pixel-tart-source-private\artifacts\releases\runtime-user-findings-batch-a\publish\win-x64\KitaoPhotoSelector.exe

SourceHead：fea716c17d5018e51fb6ede9c0d3317953c2de42

保持你的 Windows 缩放，优先 150%；未自动改动环境。

## 约 5 分钟

1. 素材库：检查搜索框宽度；搜文件名/标签/备注，点击建议不会打开高级筛选。
2. 左侧：确认智能文件夹→标签分组→文件夹；展开父子文件夹，搜索一个子文件夹，再清空搜索。
3. 选一张图片：添加两个标签，删掉其中一个；添加文件夹，再删除该关系。
4. 用标签筛选，检查结果、导航计数与 Inspector 一致；试一次撤销，再重启确认关系仍然存在。
5. 多选两张：标签选择器显示部分选中，勾选可统一添加，取消可统一移除。
6. 检查标签选择器 Esc/点外部关闭；打开其他 popup 后没有两个同时存在，图片区域不移动。

发现问题只需截图 + 一句话。无需技术日志或 JSON。

尚缺本轮所述 16 页截图，不能声称已按其完全相同路径复现。当前未进行自动点击、导航、截图、DPI 修改；没有 Observer/Recorder/Native Harness。

Batch A 真实操作和 After 截图完成后，再进入 Batch B。全部 USER_APPROVED / VisualApproved / UserVerified 均为 false。
