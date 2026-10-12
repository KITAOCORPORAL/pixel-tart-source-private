# Kitao Studio 光镜 · 信息架构

版本：2026-10-12 / 源码审计基线，不覆盖既有设计。代码事实、用户要求、设计建议三者分离。

## 1. 一级导航核对：当前是十项，不是六项

源码：
- src/RAWSelectionAssistant.Core/Models/AssetLibraryWorkspaceSettings.cs::PrimaryNavigationPolicy.OrderedPages
- src/RAWSelectionAssistant/MainWindow.xaml::PrimaryNavigationGroup
- src/RAWSelectionAssistant/ViewModels/MainViewModel.cs::CurrentPage / NavigateToSurface

当前顺序在集成基线 6a27e166、第九批 51b3f4f8、第十批 061c4cf5 一致：

|顺序|真实显示名称|路由|设计职责|
|---|---|---|---|
|1|工作台|Workbench|项目概览、继续工作、今日档期、固定工具|
|2|素材库|AssetLibrary|本地素材组织、检索、标注、引用|
|3|归片工作区|Workflow|编号与原片匹配、歧义核对、安全输出|
|4|工作日历|WorkCalendar|拍摄档期、状态、提醒、关联资料|
|5|策划中心|Planning|项目策划、文档、参考、镜头清单|
|6|联机拍摄|Tether|看守文件夹接收、预览和现场标注；不承诺厂商快门控制|
|7|参考仿色|ReferenceColor|Color Studio，输入、调整、版本、仿色、即时胶片与输出|
|8|在线选片|OnlineSelection|桌面发布/回收壳；默认服务未配置|
|9|摄影收支|Finance|本地项目收支与汇总|
|10|项目历史|History|历史项目回顾与继续工作|

“六项现有导航”的前提与当前源码矛盾，不能给出虚构的六个名称。已追查的历史策略 b4bd38f 为七项，d6cc4f1 为九项，bf1aa29 为十项（完整 SHA 和代码见 SOURCE_AUDIT.navigationHistory）。没有据此断言历史上从未存在六项，只说明本次读取记录未证实。维持现有十项事实；若产品批准收敛为六项，须另立导航迁移决策与用户验证，不在本任务自动落实。

## 2. 全局框架与重复入口

壳：标题/文件项目编辑视图工具帮助菜单 → 左主导航 → 中央工作区 → 次级工具区/任务中心/设置与帮助。关闭工作区与关闭应用必须区分；浮层先处理 Esc，再返回工作区，再触发应用退出保护。

工具箱是次级工具集合。ProductToolboxPolicy.Catalog 排除已是主工作区的路由。工作台四个固定位置默认整理图片、RAW 转 JPG、批量压缩、拼图；固定、取消固定、排序只改变快捷入口，不复制业务状态。旧 ToolRegistry 与当前 ProductToolboxPolicy 的显示/可用性不同，不能用旧注册表恢复隐藏工具。

重复入口应传入同一业务对象：菜单/工作台/侧栏访问同一路由，复用同一个工作区与任务服务；来源仅影响返回上下文。ProjectCenter 是 Workbench 别名，asset-library 归一化为 AssetLibrary。任务中心沿用次级入口，不加入主导航。即时胶片是 Color Studio 内的工具与版本能力，不新增全局一级入口。自由画布由素材库/灵感板/策划引用进入，不假设独立一级入口。

## 3. 页面树

- P-Shell
  - P-Workbench：概览、近期项目、日程摘要、固定工具。
  - P-Workflow + P-LocalSplit：项目/来源、编号解析、匹配表、P-CandidateSelectionWindow、P-MediaDetailsWindow。
  - P-History：项目历史；项目归档/恢复必须核对领域实现，不与档期归档混同。
  - P-AssetLibrary：组织树、查询组合器、智能集合编辑器、标签管理、色彩筛选、检查器、回收站、视觉分析。
    - P-Canvas：画布工作面、素材抽屉、项目面板、对象菜单。
    - P-AssetViewerWindow：大图与原像素检查。
  - P-Planning：文档导航、概览、正文、情绪板、镜头、文件；P-PlanningRichEditor、图片预览、关联档期和新建策划弹层。
  - P-ReferenceColor：目标与参考、中央预览、调整/方案、胶片条、版本/同步/保存/导出浮层。
    - P-InstantFilm：相机/相纸/布局/图像效果/随机/显影。
  - P-Tether：接收状态、预览、标注、对比、参考、P-ClientMonitorWindow。
  - P-OnlineSelection：项目创建、素材上传、发布设置、进度、结果。
  - P-WorkCalendar：P-MonthCalendarView / P-WeekCalendarView / P-DayCalendarView；详情、完整/快速编辑、已归档、提醒、天气、文档与需求面板。
  - P-Finance：筛选摘要、明细、编辑、附件引用。
  - P-Toolbox：压缩、RAW 转 JPG、拼图、整理图片、P-Publishing；独立水印预览能力另标。
  - P-TaskCenter、P-Settings、P-Activation、P-Help、P-Onboarding：次级或系统表面。
- 规划端：P-Extension、P-Mobile、P-WordTarget。未集成实验页面使用 P-BRANCH-*，不得混入产品主树。

完整逐表面布局、状态和动作连接见 PAGE_AND_STATE_MATRIX.csv。矩阵同时包含组件与弹窗，82条表面记录不能当成82个独立业务页面；以 SurfaceType 区分。

## 4. 对象传递与身份规则

|对象|当前证据|设计规则|
|---|---|---|
|项目|Project/PlanningCenter 持久化及 IdentityBridgeRepository|只传稳定ID和明确的项目域；名称可变不是身份。桥接未建立时阻止错误关联，不能临时按同名合并|
|素材|LibraryId + AssetId；AssetLibraryViewModel.Identity|跨库必须带LibraryId；路径仅是可变位置；重新定位保留身份并核对原对象|
|策划/镜头|PlanningCenterViewModel、ShotCaptureRelation|传ProjectId/ShotId；引用素材与拍摄结果分开，不能把参考照片当交付照片|
|画布|CanvasDocument.CanvasId、对象ID、SourcePath/LibraryId/AssetId|保存对象变换和引用；移出画布不删源文件；快照绑定CanvasId与RevisionId|
|编辑版本|StudioEditVersion、StudioVersionRepository|源图身份 + 编辑版本ID + 处理定义；旧版仍可重开，新算法显式新版本，不静默升级|
|标注|PhotographerAnnotationStore、AnnotationCasJournal、TetherAnnotationMerge|摄影师与客户字段区分；并发更新比较基准，冲突不静默覆盖|
|输出|ExportRecipeSnapshot、StudioExportReceipt、ExportRecovery|输出文件与配方/源/版本/任务对应；高级详情显示校验信息，普通界面显示“来自哪个版本/何时导出”|
|档期|ShootBooking、BookingSyncLifecycleSchemaMigration|BookingId与ProjectId区别；本地UTC字段不等于三端同步已完成|

返回上下文包含来源PageID、所选ID集合、排序筛选、滚动锚点、缩放和焦点。跨页打开对象先验证存在/权限/版本；缺失进入可恢复说明，不创建“同名替代品”。

## 5. 建议（未经批准）

- 可研究“项目”为聚合入口，但不自行替换当前十项导航。
- 用户名称可研究把“参考仿色”迁移为“Color Studio 调色”，须保留路由兼容与旧入口说明。
- 项目历史可研究收进项目域；不是本次已批准导航变更。
- 自由画布可作为素材库内明确工作区标签，不必升级一级导航。
- 状态/任务中心统一跨模块任务，不为每个工具重做队列引擎。
