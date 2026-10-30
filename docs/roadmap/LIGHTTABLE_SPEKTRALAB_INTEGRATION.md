## 2026-10-30 家庭当前产品（优先于下方历史）
- 当前产品源码 **1ec06eb5edcb0343d8587304e0536b554c3002d1**；家庭分支 `home/r55-lighttable-20261030`，独立树 `N:/pixart/guangjing-r55-lighttable-20261030`。测试支持5ceb1c55（src无差异），文档HEAD另记。
- 公司b1443ed4/产品e0ff14ab为本批上游，家庭旧7a6b98e9为其祖先（公司ahead13/无分叉）；旧树与dirty保全，研究引擎未整合。
- 照片/版本/状态/选框/上下文分层、明确22DIP展开、胶片数值/复位/真实任务、轻量镜边和幻灯条已修；列表/信息控件复用、通知去重、几何操作避免完整配方序列化。渲染数学/RAW/X-Trans/内存门禁不变；灯箱主干冻结。
- 家庭既有100自然RAW同机公司基线→最终94/6不变；warm366→134ms，冷全完成64.805→65.060s，取消退场400→560ms，部分内存上升，不称全面提速。Leica公司已修、六X3F仍OpenFile/-2/FileUnsupported；两Fuji旧native像素重复性OPEN。原片与自然截图只留本地。
- 发布7项目/20共享DLL绑定，84去重方法PASS（86场景执行），非全量/实机。22张完整宿主+8生产控件合成截图逐张查看；OS EXE及实际CMD正常启动退出0。物理鼠标/DPI/ACM/HDR未验，逐像素定稿仍有余项。
- 家庭完整包：`N:/pixart/Guangjing-R55-Home-20261030/Guangjing-R55-Home-Refresh-Diagnostic-win-x64.zip`；同名目录 `Start-Guangjing-Diagnostic.cmd`。WholeAppDiagnosticBuild=true / PhotographerTrialBuild=false，完整模块/策划15与教程退役保留，无Setup/私照/研究二进制。
- 唯一家庭收据：`docs/validation/home-r55-lighttable-20261030/FINAL_REPORT.md`（源码/测试/发布DLL/ZIP/文档HEAD分开）。总体PARTIAL_DIAGNOSTIC_NOT_FOR_RELEASE。下文公司和旧家庭身份仅历史，不覆盖本节。

> 当前公司状态 2026-10-30 Europe/Helsinki：R5.5产品e0ff14ab57316fe47d112743f56add7d6ba26e0e，实施大纲R55_IMPLEMENTATION.md，当前唯一证据company-r55-interactions-20261030/FINAL_REPORT.md。旧9f7ef4e4/f7b53078与下文交付为历史。本批具体交互/真实控件/完整包已交，68唯一自动PASS，未改RAW数学或预算；产品仍PARTIAL，物理/DPI/ACM/HDR未验。

## 历史大纲

# 当前公司灯箱接续 2026-10-30 Europe/Berlin
当前产品9f7ef4e45c383b66b048d1a183bc41d5e0a61d6d，company/lighttable-lifecycle-20261030。已修detail/compare/slide/refresh/back/close统一代际，渐进发布/占位/视区虚拟化/逐项重试/滚动取消，可靠源变化检测和有界native代理复制。固定快照、8位已有胶片任务和独立项目重开保持。
公司输入与性能：100自建JPEG非家庭自然图；5CompanyRAW最终3成功/1不支持/1预算拒绝。家庭7份源不可用不能声称恢复；Fuji原生重复性/超大RAW/100自然样本准入仍OPEN。报告../validation/company-lighttable-lifecycle-20261030/FINAL_REPORT.md为当前唯一收据。
SpektraLab仍未加载、未打包，研究另线；不增加任何RAW/16位/宽域胶片后端、ROI或跨崩溃任务系统。完整模块/策划15/旧教程退役保留。下文家庭结果为历史参考，未当公司交付。

## 家庭开发记录
# 光镜灯箱与胶片后端联动开发大纲
当前：2026-10-29 America/Los_Angeles；家庭产品f70ecf7d1179b6f42c721a787a4a2d7a7acc8109，总体诊断版，非完整LightTable复刻。

## 产品边界和导航
复用现有暗室比较/所选照片入口，不建第二图库、不嵌WinUI。R5.4第9章仅覆盖简易/专业节点；外壳/即时胶片/变体/3D/自由画布/策划仍按R5.3。灯箱是ViewingTableState v1独立查看摆放，拼贴是作品编辑，两者共享target/variant但不混布局。

## 实施顺序和当前结果
1. 暗室收尾：已有DevelopedStageProjection改系统执行视图，来源/配方hash、只读查看和切换失效保护；同固定输入旧包A/B预览与三格式像素一致。四色轮完整单格139×186 DIP、两行382，窄窗需滚动；专业图仍在照片与胶片条之间。已实现，物理验收未做。
2. 灯箱首批：选中照片/固定变体→中性灯箱摆放/缩放/90°旋转→带真实代理分辨率的局部放大→共享比较/返回暗室→保存→独立进程重开，已实现并自动测。展示灯光只改台面背景。不引入SHARP、专有EDR、音频、遥测或上传。
3. 后端适配首批：FilmBackendBindingV1和任务中心桥，固定源/域/profile/hash/engine/resource/参数/seed/输出位深/alpha/取消错误。已有创意胶片真实计算后生成新版本→灯箱→暗室→保存重开已测。首批只原生8位不透明JPEG/PNG/BMP和非复杂图；不降RAW/16位。不是实测负片/反转片，不与宝丽来混类。
4. SpektraLab产品接入：BLOCKED_BY_ACCEPTANCE，未实现真实engine加载。研究ce292f2a固定c1447c6；构建/渲染可运行但BT失败、任意grain_seed不支持、GPL及数据权利未批准。本产品只记录禁用入口与合同，独立研究另线；不合研究分支，不修改其解码/引擎。

## 固定参考和许可
LightTable-Windows cb8f4c89d845634688627f1c01225cb5d851248f；原LightTable 8e1868df40e69e6b1a08e207d7ab05cf463532ab；SpektraLab c1447c6ab618fa4b1cb11fd2fe12826855007059。文件级hash/许可与依赖读回见../validation/home-lighttable-film-20261029/REFERENCE_FILES.json及LICENSE_REVIEW.md。Root MIT不替模型/EDR/音频授权，进程边界不自动解决GPL。没有新增这些第三方依赖到产品。

## 下一批有界任务
- 优化RAW观片解码和进程工作集，补虚拟化/渐进显示、失败重试；100张当前93成功7失败、首次约49s、采样工作集约3.66GiB，不宣称已达流畅目标。
- 局部原图细节流式接口、任意角度拖柄/框选/平移台面及精美玻璃镜片/幻灯动画。当前仅代理view，非原片细节。
- 带可持久恢复输出的任务提交、跨崩溃恢复与外部引擎进程资源释放；当前无半成品变体，崩溃前未提交需显式重试。
- 普通胶片RAW/16位/宽域输入和三格式完整独立输出域门禁；已有8位适配不是未来Spektra色彩合同。
- Spektra许可批准、seed能力和BT/自然保留集/跨oracle门禁后，显式新engine版本接入候选变体；不能默认覆盖PV4/5/6或宝丽来。研究负责build/color/backend contract，产品负责adapter/variants/project/error recovery，写入范围不重叠。
- 完整鼠标/真实DPI/ACM/HDR与Grok美术验收，独立于自动/离屏/OS启动。

本轮身份、实测、完整包和所有限制见../validation/home-lighttable-film-20261029/FINAL_REPORT.md，不把历史路线图待实现状态盖过当前结果。
