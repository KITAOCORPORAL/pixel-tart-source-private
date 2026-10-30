## 2026-10-30 家庭当前：策划画布一致性（优先于以下历史）
- 当前产品源码 **69bbf22935f3471013ad6bb3728f95b8b562acfc**；新分支 `home/r55-planning-canvas-20261030`，独立树 `N:/pixart/guangjing-r55-planning-canvas-20261030`。上游灯台eae800e7/产品1ec06eb5冻结，色彩研究未整合；测试支持f720f1a8、文档HEAD另记。
- 旧策划Snapshot独立合成器已替换为CanvasPixelRenderer有界公共入口：Hidden/Opacity/画笔/文字/色板/方向/crop/flip/rotate同合同，固定画板透明裁切。1400边/1.96MP/256MiB估算/1000层，STA串行取消，不为预览分配最大画板float。
- 新快照RenderVersion2；旧缺省1保留PNG不重算。实际落盘原子追加后才发布VM，手动更新保留旧版；项目/策划/操作代次拒绝迟到，缺源/来源变化/保存失败不发布半成品。原.ptplan v1范围、全部模块/策划15/教程退役保持。
- 同尺寸混合快照与旧DLL导出像素和PNG字节相同；缩小采样独立oracle0码；Pillow/FFmpeg两个固定实现alpha最大1码。发布绑定WPF42+独立2+完整宿主1+范围1、Core50+PV6 4通过；去重100PASS/1旧符号链接权限FAIL/1LCMS参考矩阵NOT_RUN，不能称全套PASS或实机。
- 家庭最终完整包：`N:/pixart/Guangjing-R55-PlanningCanvas-20261030/Guangjing-R55-PlanningCanvas-Final-Diagnostic-win-x64.zip`；同名目录Start-Guangjing-Diagnostic.cmd，WholeAppDiagnosticBuild=true / PhotographerTrialBuild=false，win-x64自包含，不Setup。
- 唯一当前收据：`docs/validation/home-r55-planning-canvas-20261030/FINAL_REPORT.md`。6张真实1366/1920生产控件离屏图已逐张打开；实际EXE/CMD OS启动与正常退出0通过。真实鼠标/DPI/ACM/HDR、通用16位/ICC未验，WIC既有RGB48+ICC拒绝保持。总体PARTIAL_DIAGNOSTIC_NOT_FOR_RELEASE。

## 2026-10-30 家庭当前产品（优先于下方历史）
- 当前产品源码 **1ec06eb5edcb0343d8587304e0536b554c3002d1**；家庭分支 `home/r55-lighttable-20261030`，独立树 `N:/pixart/guangjing-r55-lighttable-20261030`。测试支持5ceb1c55（src无差异），文档HEAD另记。
- 公司b1443ed4/产品e0ff14ab为本批上游，家庭旧7a6b98e9为其祖先（公司ahead13/无分叉）；旧树与dirty保全，研究引擎未整合。
- 照片/版本/状态/选框/上下文分层、明确22DIP展开、胶片数值/复位/真实任务、轻量镜边和幻灯条已修；列表/信息控件复用、通知去重、几何操作避免完整配方序列化。渲染数学/RAW/X-Trans/内存门禁不变；灯箱主干冻结。
- 家庭既有100自然RAW同机公司基线→最终94/6不变；warm366→134ms，冷全完成64.805→65.060s，取消退场400→560ms，部分内存上升，不称全面提速。Leica公司已修、六X3F仍OpenFile/-2/FileUnsupported；两Fuji旧native像素重复性OPEN。原片与自然截图只留本地。
- 发布7项目/20共享DLL绑定，84去重方法PASS（86场景执行），非全量/实机。22张完整宿主+8生产控件合成截图逐张查看；OS EXE及实际CMD正常启动退出0。物理鼠标/DPI/ACM/HDR未验，逐像素定稿仍有余项。
- 家庭完整包：`N:/pixart/Guangjing-R55-Home-20261030/Guangjing-R55-Home-Refresh-Diagnostic-win-x64.zip`；同名目录 `Start-Guangjing-Diagnostic.cmd`。WholeAppDiagnosticBuild=true / PhotographerTrialBuild=false，完整模块/策划15与教程退役保留，无Setup/私照/研究二进制。
- 唯一家庭收据：`docs/validation/home-r55-lighttable-20261030/FINAL_REPORT.md`（源码/测试/发布DLL/ZIP/文档HEAD分开）。总体PARTIAL_DIAGNOSTIC_NOT_FOR_RELEASE。下文公司和旧家庭身份仅历史，不覆盖本节。

# 光镜当前产品路线图

## 2026-10-29 当前家庭增量
产品f70ecf7d1179b6f42c721a787a4a2d7a7acc8109，home/r54-design-fixes-20261029。先完成R5.4暗室收尾，再接入基于现有target/variant的WPF灯箱查看状态和胶片任务边界；最小闭环已自动验证。详见LIGHTTABLE_SPEKTRALAB_INTEGRATION.md和../validation/home-lighttable-film-20261029/FINAL_REPORT.md。研究Spektra未通过产品门禁，不纳默认产品。不是R5.4全量/真实DPI/发行验收。

## 历史平台规划
# Pixel Tart platform roadmap

1. Windows Match V4 + GPU: complete high-precision GPU stages, tile/memory acceptance and product wiring.
2. Color Studio Professional: expose v3/v4 Beta engine choice and shared preview/export state; **3D Color Space / Color Map is REQUIRED and DEFERRED_TO_COLOR_STUDIO_PRO_PHASE** (see `docs/color-studio/3d-color-space/3D_COLOR_SPACE_PRODUCT_SPEC.md`).
3. Photography Workflow 1.0.
4. Planning + Asset Library.
5. Tethering + Client Workflow.
6. Windows 1.0 / RC.
7. macOS Desktop: native UX, Metal, ColorSync, Finder integration, Apple Silicon and RAW/ICC/TIFF/EXIF reevaluation.
8. iPad / Tablet: touch-first, Apple Pencil, browsing, rating, Color Studio and desktop sync.

The current Windows phase establishes the compute boundary and guarded product wiring. The
platform-neutral Match V4 transform and future 3D color data model are shared contracts; Windows
uses DX12 while future macOS/iPadOS renderers use Metal. macOS/iPad UI and Metal implementation
are not part of this change.
