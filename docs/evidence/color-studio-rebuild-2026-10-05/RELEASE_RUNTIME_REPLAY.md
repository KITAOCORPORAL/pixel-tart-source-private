# 最终 Release 运行重放记录

本文件记录同一 r1 Release 的实际运行。单项 PASS 仅覆盖表中写明的动作、窗口和输入；未覆盖的复合需求保留 PARTIAL / NOT_RUN。候选构建截图与自动测试不计入本表。

`VisualApproved=false`，`UserVerified=false`，`USER_VISUAL_REVIEW=NOT_APPROVED`。

## 唯一构建身份

- SourceHead：`31d3ac3ae56d6ccd1d9b4c608bf522dbb735d591`
- EXE：`N:/pixart/pixel-tart-source-private/artifacts/releases/color-studio-rebuild-2026-10-05-r1/publish/win-x64/KitaoPhotoSelector.exe`
- 构建：Release / x64 / win-x64 / self-contained；发布于 `2026-10-05T13:08:11.6769967+08:00`。
- EXE SHA256：`C9841FB530ABF0E209B8E9BB9B6870C917D54DED985702B2EF41F36EE6DB28CA`
- DLL ProductVersion：`2.3.0+31d3ac3ae56d6ccd1d9b4c608bf522dbb735d591`
- DLL SHA256：`DD5BFB88219EC853DD328AA00743BE4519C0A259E19C2016467239948420BCD3`
- 发布 manifest：`artifacts/releases/color-studio-rebuild-2026-10-05-r1/release-manifest.json`，287个文件；manifest SHA256：`DDFE6381BD897F2BF7A997144E71E143A10CD26ED2F3CA38A10D4BAA7A9BAE21`。脱敏身份副本见 `RELEASE_IDENTITY.json`。
- 会话：PID `36892`，启动于 `2026-10-05T13:08:50.5696117+08:00`；本地记录 `artifacts/color-studio-rebuild-2026-10-05/final-runtime-session.json`。隔离数据根 `artifacts/color-studio-rebuild-2026-10-05/final-runtime`。
- 本次已记录窗口：1600×920；5张After图均为此完整窗口。
- 真实系统 DPI：尚未取得可核对读数；不以截图尺寸或自动 WPF 缩放推断。

证据根：`artifacts/color-studio-rebuild-2026-10-05/final-runtime-evidence/`。图片含授权测试素材，仅在本地保留；下表使用该目录内的文件名。

## 重放表

| 编号 | 实际操作 | 输入/参数 | 观察及证据 | RELEASE_RUNTIME |
|---|---|---|---|---|
| RT01 | 启动、进入色彩工作室、重开3张批次 | 同组授权2 JPEG+1 RAW | r1启动成功；重开后胶片栏3/3、当前选中1张，目标预览载入并恢复曝光EV 1.127。`01-layout-restored-exposure.jpg`；本项仅指启动及此既有工作文件重开，不包含本轮重新保存/进程重启 | PASS（限定范围） |
| RT02 | 完整布局、收起全局侧栏、双直方图展开/折叠 | 1600×920 | 中央目标照片、左3D辅助区、右侧RGB/明度上下双图和编辑工具、紧凑底栏均可见；分析组折叠后仍在右栏，不覆盖主图。展开见01/02，折叠见03/04；未验证其他窗口及DPI | PASS（限定1600×920） |
| RT03 | 左侧参考/3D/胶片预设/节点，右侧7工具页 | 同一目标 | 实际观察左3D、右色彩/曲线/色阶/细节；01–04提供部分页证据。左侧其余3模式及右侧3D/Film/Creative尚未逐一重放，不将标签可见算通过 | PARTIAL |
| RT04 | 白平衡/曝光/HDR、复位、undo/redo | 同一JPEG；恢复EV 1.127，冷暖偏移25.199 | 01为恢复曝光；02操作冷暖后照片色彩及右侧直方图更新，随后Ctrl+Z恢复冷暖0。没有完成本轮HDR全参数、各组复位及redo，曝光恢复也不等同本轮曝光拖动 | PARTIAL |
| RT05 | RGB及单通道色阶/曲线增删拖动 | 同一JPEG；曲线输入0.496→输出0.654，RGB色阶gamma 1.448 | 03曲线点调整使中间调明显改变，随后通道复位；04 RGB色阶中间调使照片提亮，随后复位。本次未验证R/G/B单通道、控制点删除与全部端点 | PARTIAL |
| RT06 | 色域取样、范围/羽化、肤色、色彩平衡 | 同一JPEG | PENDING | NOT_RUN |
| RT07 | 细节与Film预设/参数 | 同一JPEG；清晰度37.275 | `05-clarity-37275.jpg`：计算结束、状态恢复正常，照片局部细节增强；结构仍为0，Film、锐化、降噪等其余子参数未重放 | PARTIAL |
| RT08 | 球体旋转/平移/缩放/复位/Fit/显示设置/展开 | 真实点云 | PENDING | NOT_RUN |
| RT09 | 图像取色→云；球面→图像；影调区间→图像；Esc/离开清除 | 同一当前处理版本 | PENDING | NOT_RUN |
| RT10 | Fit/100%/缩放/平移、RAW实际细节、取消计算 | 授权RAW | PENDING | NOT_RUN |
| RT11 | 切图、Ctrl/Shift、多选/过滤/评分/色标、选择性同步 | 3张输入 | PENDING | NOT_RUN |
| RT12 | 导出PNG/JPEG/TIFF、核对实际文件格式/像素/ICC | 同一冻结调整栈 | PENDING | NOT_RUN |
| RT13 | 保存、关闭、同EXE重启并重开 | 新工作文件 | PENDING | NOT_RUN |
| RT14 | 窄窗、宽窗、中文/英文/繁体、菜单边界 | 同EXE | PENDING | NOT_RUN |

当前状态：**NOT_READY_FOR_USER_RETEST**。后续桌面重放需遵循用户最新禁止Computer Use的规则，当前等待用户对本轮例外的明确答复；此等待不是产品FAIL。未执行项保留 NOT_RUN / PARTIAL，不用模块候选截图代填，用户批准保持false。
