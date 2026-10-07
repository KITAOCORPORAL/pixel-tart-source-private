# 最新布局图与 r1 实机视觉对照

基准是用户提供的 `image(20261005-022726).png`，原图尺寸 1672×941、SHA256 `82A1EAB8C4DEBDC278AAF2D6086A7FD6527C7362B9613AEBE9B6F4D24E34AC0E`。它是设计要求，不是 Pixel Tart 运行证据。本轮 After 均来自同一 r1 Release，SourceHead `31d3ac3ae56d6ccd1d9b4c608bf522dbb735d591`。全图身份、会话和 SHA256 见 [SCREENSHOT_MANIFEST.json](SCREENSHOT_MANIFEST.json)。

| 区域 | 原图目标 | r1 实际操作与公开证据 | 差异 / 限制 | RELEASE_RUNTIME |
|---|---|---|---|---|
| V01 顶栏 | 标识、菜单、RAW、导入、导出紧凑可达 | 顶部快速导出实际操作并产生文件；空选择时禁用。[092](evidence/092-r1-before.png) [093](evidence/093-r1-after.png) [104](evidence/104-r1-after.png) | RAW/导入全部入口、取消输出及各种宽度未完整重放 | PARTIAL |
| V02 左侧四模式 | Reference / 3D Color / Film Preset / Nodes 切换真实内容 | 四模式逐一打开；参考空态、预设列表、节点空态及 3D 球面分别显示。[008](evidence/008-r1-after.png) [009](evidence/009-r1-after.png) [010](evidence/010-r1-after.png) [049](evidence/049-r1-before.png) | 窄窗 English 模式文字截断；全部重启状态未验，关联 CU-01/CU-04 | PARTIAL |
| V03 左 3D 辅助区 | 球面在左栏；旋转、缩放、设置与点云有真实响应，展开不侵占主图 | mini 球面、真实点云及模式设置可见；Orbit、滚轮、Reset、展开 Pan/Fit 已操作。[049](evidence/049-r1-before.png) [050](evidence/050-r1-after.png) [053](evidence/053-r1-after.png) [105](evidence/105-r1-before.png) [107](evidence/107-r1-after.png) | 展开浮窗覆盖中央照片和右栏；相机状态与 mini 独立，CU-02。所有显示参数全范围和性能未验 | FAIL |
| V04 中央照片 | 最大工作区，Fit/100%/缩放/平移且提示/浮层不遮主体 | Fit 和 100% 真正操作；主图随编辑更新，临时高亮可 Esc 清除。[061](evidence/061-r1-after.png) [081](evidence/081-r1-after.png) [067](evidence/067-r1-after.png) | 3D 浮窗覆盖，CU-02；主图持续 Pan 与“100% 物理像素”尚未完整测量 | PARTIAL |
| V05 右顶双直方图 | RGB 在上、明度在下，均属于右栏并跟随当前处理预览 | 双图位于右栏顶部；白平衡调节前后图形变化；RGB/R/G/B/亮度 UI 通道切换实际点击。[017](evidence/017-r1-before.png) [018](evidence/018-r1-after.png) [332](evidence/332-r1-after.png) | alpha0 定量排除及异步末帧竞态未全验；影调区间入口需要内滚，CU-06 | PARTIAL |
| V06 右七工具页 | 3D Color、Color、Levels、Curve、Details、Film、Creative 各有真实内容与独立滚动 | 七页逐一打开，曲线/色阶/细节/胶片参数有实际拖动记录。[011](evidence/011-r1-after.png) [012](evidence/012-r1-after.png) [013](evidence/013-r1-after.png) [014](evidence/014-r1-after.png) [015](evidence/015-r1-after.png) [016](evidence/016-r1-after.png) | 1180×720 的右栏参数/复位/Creative 不可达，CU-01；非法数值 Escape 路由异常，CU-05 | FAIL |
| V07 底部操作行 | 数量、评分、过滤、色标、同步所选和更多入口紧凑明确 | 多选、评分、色标过滤、隐藏已选提示、同步和导出入口均实际操作。[089](evidence/089-r1-after.png) [100](evidence/100-r1-before.png) [101](evidence/101-r1-after.png) [104](evidence/104-r1-after.png) | 同步弹层显示 9 张，实际目标 8 张，CU-03；所有格式/评分组合未全验 | FAIL |
| V08 横向胶片栏 | 图片主导、当前与多选明显不同；滚动/切图后选择连续 | 逐张、Ctrl、Shift、Ctrl+A、多选评分及过滤操作；当前图与选择集合分开。[061](evidence/061-r1-after.png) [089](evidence/089-r1-after.png) [250](evidence/250-r1-after.png) | 横向滚动边界、所有过滤后范围组合仍未全验 | PARTIAL |

## 窗口与 Windows 实际缩放

以下 DPI 来自新进程或系统读数；截图尺寸只用于证据定位，不被当作 DPI 证明。会话及更正见 [SESSION_IDENTITIES.json](SESSION_IDENTITIES.json)。

| 窗口逻辑尺寸 | Windows 缩放 / 实际 DPI | 本轮例证 | 结果 |
|---|---|---|---|
| 1600×920 DIP | 100% / DPI96 | [061](evidence/061-r1-after.png) | 八区可见，但全工具组合未验 |
| 1600×920 DIP | 125% / DPI120 | [111](evidence/111-r1-after.png) | 重开批次与布局已看；未覆盖全工具 |
| 1600×920 DIP | 150% / DPI144 | [049](evidence/049-r1-before.png) | 3D 和主图可见；展开遮挡 CU-02 |
| 1180×720 DIP | 125% / DPI120 | [112](evidence/112-r1-after.png) [114](evidence/114-r1-after.png) | 右栏裁切 CU-01 |
| 1180×720 DIP | 200% / DPI192 | [123](evidence/123-r1-after.png) | 右栏裁切 CU-01 |
| 1920×1080 DIP | 150% / DPI144 | [332](evidence/332-r1-after.png) | 全图布局与双直方图可见；数值 Esc 问题另见 CU-05 |

已实际检查 Windows 100%、125%、150%、200% 四档，以及 1180×720、1600×920、1920×1080 三种逻辑窗口，但 **3×4 的所有组合未逐一执行**。`s01` 早期 DPI 推断已撤回；本表只引用有明确 DPI 的后续会话。多输入和长时间性能不能从单张全图推断通过。

`USER_VISUAL_REVIEW=NOT_APPROVED`。以上是 Codex 的局部观察，不代表用户批准视觉结果。
