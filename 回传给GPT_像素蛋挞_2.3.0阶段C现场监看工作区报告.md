# 像素蛋挞 2.3.0 阶段C：现场监看工作区报告

生成时间：2026-08-05（Asia/Shanghai）
开发分支：`release/2.3.0`

## 恢复与开始门禁

阶段C开始前以本地仓库为唯一依据完成恢复核验。当前分支为 `release/2.3.0`，阶段B功能提交 `beab5ecdf4eace01a5bdf910e4b51accfcccd3ee` 是开始HEAD的祖先，阶段B报告已由独立提交 `ad4ae7e30e91a8db32f132fb04083b9a5ab82455` 保存，开始时工作树干净。未checkout回阶段B功能提交，未使用 `git reset --hard`，未重写阶段A、恢复门禁或阶段B历史。

修改前执行了 clean、restore、Debug/Release build 与全量 test。恢复基线两种配置均为 Core 874 + WPF 77 + DPI 45 = 996/996，0失败、0跳过、0警告、0错误。Watch Folder启动/停止、稳定检测、事件去重、JPG/RAW配对、重启恢复、`WaitForCompletionAsync`、`AwaitableProgress`/`DrainAsync`、Provider None、Release无Fake Camera、WinExe和无localhost门禁均通过。

## 42项交付记录

### 1. 阶段C开始HEAD

`ad4ae7e30e91a8db32f132fb04083b9a5ab82455`（`docs(tether): report watch-folder tethering MVP`）。

### 2. 阶段B功能提交

`beab5ecdf4eace01a5bdf910e4b51accfcccd3ee`。

### 3. 阶段C功能提交

`b36233eeb45c8dad540d89ae1aa42bc49655bf33`（`feat(tether): add live monitoring workspace`）。

### 4. 当前HEAD

本报告生成前的已验收功能HEAD为 `b36233eeb45c8dad540d89ae1aa42bc49655bf33`。报告和UI证据按要求作为独立后续提交保存；最终交付HEAD记录在本次回传消息中。

### 5. 工作树

阶段C功能提交后工作树干净。报告与UI证据提交完成后再次复核为干净，无未知修改、无强制清理。

### 6. 产品版本

产品版本 `2.3.0`；文件/程序集版本 `2.3.0.0`，未修改版本基线。

### 7. SchemaVersion

`3`。

### 8. 是否修改数据库

未新增迁移、表、字段或索引，`TetherSchemaMigration` 无差异，仍只复用 `TetherSessions`、`TetherAssets`、`TetherAnnotations`。新增的是现有 `TetherAnnotations` 的会话查询及参数化、事务化 Upsert 调用路径；未保存图片、缩略图、RAW、LUT或ICC二进制，未新增 `ProjectRelationships`。

### 9. 三栏布局

完成。左栏270 DIP（约束220–300）、中央自适应且最小640 DIP、右栏320 DIP（约束280–340）。小于1350 DIP时右栏折叠为抽屉、左栏缩窄、中央优先；中央工具栏使用受约束的星形列/自动列，避免左右控制组叠放。主侧栏、顶部菜单和全局任务中心仍保留；全屏时按规范临时隐藏。

### 10. 虚拟化列表

完成回收虚拟化、像素滚动、缩略图按需加载/离开视口释放、筛选、排序、选中项滚动可见、键盘导航及增量更新。新增资产不清空并重建整个集合；1000项和100张连续到达由可重复专项测试覆盖。

### 11. 代理预览

完成。默认复用阶段B `TetherProxyCacheService`，最长边2048；加载使用冻结的 `BitmapSource`，快速切图通过请求版本和取消令牌阻止旧图回写。RAW优先使用配对JPG，无配对JPG显示安全占位。

### 12. 100%查看

完成按需100%读取、Fit/Fill/100%/自由缩放、滚轮缩放、拖动、双击切换和重置。源文件采用 OnLoad 等价方式及时释放流，不批量预取，不长期锁文件；有限LRU缓存、取消和内存压力回退代理图已实现。

### 13. 自动最新

默认开启。Ready新照片会进入列表并在安全状态下选中；正在100%、比较、编辑备注、查看旧图或进行画布交互时不会抢焦点，并累计新照片数量。

### 14. 锁定逻辑

完成“锁定当前”“解锁并跳到最新”和新照片计数入口。锁定不阻止新照片入列，不改变当前缩放/位置；解除锁定由用户决定跳转。

### 15. EXIF

完成文件类型、拍摄时间、品牌、型号、镜头、焦距、光圈、快门、ISO、曝光补偿、白平衡、色彩空间、像素尺寸、文件大小和配对状态。缺失字段显示“未提供”，损坏元数据安全降级；完整路径默认隐藏，只有用户主动展开才显示。

### 16. 直方图

完成代理图RGB及可选亮度直方图。后台计算、可取消、旧请求不可回写；界面明确标注“基于监看代理图；不是RAW显影结果”。

### 17. 高光和阴影警告

完成独立开关和阈值控制，默认阈值约250/5。遮罩只基于当前显示代理图，不修改、不写入源文件，并明确LUT尚未实现。

### 18. 并排比较

完成两图左右并排、同步缩放/拖动开关、交换、将第二张设为主选中和退出恢复原选中。比较期间新照片不打断。

### 19. 重叠比较

完成中心对齐、透明度、闪烁、同步缩放/拖动和上下层交换。比较不修改评级或源文件。

### 20. 参考图

完成本地参考图仅关联、显示/隐藏、透明度、位置、缩放、左右翻转、锁定、重新定位和清除引用。参考图不加入 `TetherAssets`，不写入照片，丢失时给出重新定位状态。

### 21. 构图辅助线

完成三分法、中心十字、方形、4:5、3:4、2:3、16:9、9:16和安全边界；只影响显示，会话显示设置保存在独立本地配置，不写照片、不生成裁切文件。

### 22. 星级和颜色标签

完成0–5星、数字键0–5、无/红/黄/绿/蓝/紫颜色标签，列表与检查器同步，保存到现有 `TetherAnnotations`。

### 23. 摄影师备注

完成本地文本和显式保存。备注最长4000字符并去除空字符；数据库失败返回明确失败且不伪装成功，备注正文不进入日志或诊断包。

### 24. 客户收藏和备注

完成客户收藏布尔切换、列表图标和与摄影师备注分离的客户备注。客户备注不写日志，诊断包不包含备注或缓存图。

### 25. 快速拒绝

完成，只设置/取消 `IsRejected`。实现和测试均确认不调用删除、移动、回收站或 `UndoJournal`，不改变源文件。

### 26. 全屏

完成当前主显示器全屏，F11进入/退出、Escape退出，隐藏顶部菜单、主侧栏、状态栏和任务中心；切图快捷键仍工作，不停止或重启Watch Folder会话。未开发第二显示器窗口。

### 27. 内存与取消

建立并使用 `IPreviewImageLoader`、`IFullResolutionImageLoader`、`IHistogramService`、`IClippingOverlayService`、`IPreviewRequestCoordinator`、`IPreviewMemoryManager`。请求携带AssetId/版本，切换即取消旧请求，回写前复核当前版本；100%缓存有限LRU，离开释放，文件流及时关闭，位图冻结后跨线程使用。没有伪造硬件毫秒数，性能结果为合成数据下的相对/行为门禁。

### 28. 隐私

审计只记录AssetId、操作和结果等标识，不记录完整路径、文件名、备注正文、客户/模特姓名、图片内容、EXIF转储、参考图路径或哈希。诊断维护不收集代理图、100%缓存和备注。UI验收使用专用 `KitaoPhotoSelector.UiReview` 数据目录及合成测试图，不读取用户正式数据库或真实照片。

### 29. 修改和新增文件

功能提交共21个文件：

- Core新增：`TetherMonitoringModels.cs`、`TetherMonitoringServices.cs`；修改 `SqliteTetherRepositories.cs`、`AppDataPaths.cs`。
- WPF新增：`TetherMonitoringImageServices.cs`、`CompositionGuideOverlay.cs`、`TetherHistogramView.cs`；修改 `App.xaml.cs`、`MainWindow.xaml`、`MainWindow.xaml.cs`、`MainWindow.AutomatedDpiAcceptance.cs`、`TetherCaptureViewModel.cs`、`TetherCaptureView.xaml`、`TetherCaptureView.xaml.cs`。
- 测试新增：Core、WPF、DPI各一份阶段C专项测试；更新阶段B WPF/DPI门禁以适配正式监看页。
- 取证工具新增：`tools/StageCLiveMonitorReview/Invoke-StageCLiveMonitorReview.ps1`、`create_contact_sheet.py`。
- 证据提交另含18张PNG、联系表、18份布局元数据、`evidence-index.json`、`source-integrity.json`及本报告。

### 30. 新增测试数

在996项基础上净增93项：Core 874→896（+22），WPF 77→133（+56），DPI 45→60（+15）。测试总数只增加、未减少、未禁用、未跳过。

### 31. 最终测试总数

`1089/1089`：Core 896 + WPF x64 133 + DPI 60。

### 32. Debug结果

Debug构建成功，0警告、0错误；Core 896/896、WPF x64 133/133、DPI 60/60，总计1089/1089，0失败、0跳过。

### 33. Release结果

Release构建成功，0警告、0错误；Core 896/896、WPF x64 133/133、DPI 60/60，总计1089/1089，0失败、0跳过。Release仍为WinExe、Provider默认None、未注册Fake Camera、无localhost。

### 34. 原996项是否全部保留

是。原Core 874、WPF 77、DPI 45全部保留并通过；专项测试是在其上新增。

### 35. UI截图目录

`D:\AI AGENT\RAWSelectionAssistant\artifacts\ui-review\2.3.0-stage-c\`

包含要求的18个命名场景，均由当前阶段C真实WPF界面通过 `RenderTargetBitmap` 生成，使用合成图片；18个截图哈希全部唯一，18/18布局检查通过，源图前后SHA-256一致。

### 36. UI总览路径

`D:\AI AGENT\RAWSelectionAssistant\artifacts\ui-review\2.3.0-stage-c\像素蛋挞_2.3.0阶段C现场监看UI总览.png`

### 37. 是否下载厂商SDK

否。未下载Sony、Canon、Nikon、Fujifilm或其他相机厂商SDK。

### 38. 是否Publish

否。

### 39. 是否生成安装包

否。

### 40. 是否合并main

否。阶段C期间没有merge提交，也未执行main合并。

### 41. 是否创建Tag

否。未创建 `v2.3.0` Tag；现有正式Tag仍为 `v2.2.0`。

### 42. 是否建议进入阶段D

否。阶段C已完成并按要求立即停止；应先进行人工验收，只有收到新的明确授权后才可规划或开发阶段D。

## 结论

像素蛋挞2.3.0阶段C“现场监看工作区”已完成。功能、文件安全、数据库边界、隐私、主题/DPI、真实WPF取证及Debug/Release全量门禁均达到本阶段要求；未触碰禁止范围。
