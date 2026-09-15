# Pixel Tart RC12 实机验收

状态：`PENDING`  
安装包：[像素蛋挞_Setup_2.3.0_RC12_x64.exe](../../artifacts/releases/2.3.0/installer/像素蛋挞_Setup_2.3.0_RC12_x64.exe)  
SHA-256：`4FDD855257A4F854CF0C347B40556E3A8BF832ACAC6FA810D14BF34D2FB51D97`

请在真实 Windows 电脑上按顺序勾选。无需理解内部实现。

## A. 启动 / 升级

- [ ] RC11 → RC12 升级安装成功
- [ ] RC12 全新安装成功
- [ ] 正常启动、无闪退
- [ ] 深色主题、左侧导航正常
- [ ] 没有白色 Popup 或错误 Close X

## B. 素材库

- [ ] 新建素材库：名称、保存位置、创建后自动进入
- [ ] 导入横图、竖图、PNG、JPEG、RAW
- [ ] 图片不变形、无人工黑框
- [ ] Masonry、Grid、缩略图加载正常

## C. Eagle-style Browser

- [ ] Folder、Tag、Smart Folder、Search、Filter、Density、Layout、Multi-select
- [ ] 操作流畅、菜单紧凑，Gallery 为主体，Inspector 不挤压 Gallery

## D. Viewer

- [ ] 双击打开 Viewer
- [ ] Fit、100%、Zoom、鼠标滚轮、Pan、Previous、Next、Esc

## E. Project / Booking

- [ ] 关联/取消 Project
- [ ] 关联/取消 Booking
- [ ] Client 正确显示，Workflow Status 可修改
- [ ] 关闭并重新打开后关系仍存在

## F. Calendar ↔ Asset

- [ ] Calendar Booking 显示缩略图、素材数量、客户选择数量、待精修/已精修/已交付数量
- [ ] “查看全部素材”跳到素材库且只显示该 Booking 图片
- [ ] Asset Inspector “查看拍摄”返回正确日期并自动展开正确 Booking

## G. Inspiration

- [ ] Gallery → Inspiration Tray / Collection
- [ ] 缩略图可见，可新建、改名、排序、删除 relation
- [ ] 删除 relation 不删除原图

## H. Offline Cache

- [ ] 建立 Reference Asset 并生成 thumbnail
- [ ] 暂时移动或断开源目录后重新打开
- [ ] 缓存缩略图仍显示、Offline badge 正常、无闪退

## I. Recent Libraries

- [ ] 准备两个素材库并执行 A → B → A
- [ ] Recent Library、Online/Offline 状态正常
- [ ] 移除最近列表不会删除 `.ptlibrary`
- [ ] 切换后没有旧库数据写入新库

## J. Context Menu

- [ ] 单选和多选分别测试：查看、整理、摄影工作流、创作、导出、生命周期
- [ ] 分组正确，多选右键保持多选
- [ ] 无永久删除

## K. EXIF

- [ ] 真实相机 JPEG 显示 Camera、Lens、ISO、Shutter、Aperture、Focal Length、Capture Time、Dimensions、Orientation
- [ ] 读不到时显示“未记录”，不编造值

## L. DPI

- [ ] 至少测试 100%、125%、150%
- [ ] 无文字重叠、裁切、白色弹层、菜单跑位；日期器正常

## 问题记录

页面：  
操作：  
预期：  
实际：  
截图：  

## 严重度

- **P0**：崩溃、数据损坏、源文件误删、库打不开、数据串库。立即停止验收并反馈。
- **P1**：功能不可用、Popup 崩坏、Calendar/Asset 跳转错误、图片严重显示异常。反馈后重新 RC12 build。
- **P2**：布局、间距、字体、密度、视觉细节。集中一轮 UI polish，不自动创建 RC13。

验收通过后，请明确回复：`RC12 实机通过`。在此之前，RC12 基线保持 `REAL_MACHINE_ACCEPTANCE = PENDING`。
