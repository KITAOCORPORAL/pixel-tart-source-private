# 家里批次3 · 策划十五分区业务闭环

2026-10-20T23:50:26.895466+08:00（Asia/Shanghai；主机原始日志时区不变）。**诊断版、不能发行。策划限定范围自动验证PASS/PARTIAL；整体FAIL/PARTIAL，发行BLOCKED。**

## 起点、安全与实现范围

Fetch/独立ls-remote核对起点`56633a72510dc8057633767790c6ac3b6e7199d1`，无新增远端提交。已读前批FINAL_REPORT/DESKTOP_MATRIX及现有策划Foundation/R7/图片搬迁状态报告；旧报告可能过时，仅用当前数据/组件实证判断。
原16工作树、211未跟踪文件HEAD/状态/内容hash保持；批次1包和DPI原始文件hash复验不变。未reset/clean/stash/强推/合并main/其他功能分支，没改色彩、Fuji或显示算法。

新增服务层1测试、WPF连续业务/格式2测试及分类回归2个DataRow。产品仅修当前灯位/服化道分类和筛选三行，详情见BEFORE_AFTER.md。不是界面改版或功能重写。

## 新合成策划案身份与真实闭环

- ProjectId `5c35c6bc-3aa7-4ad6-9a4f-5d60c5193db4`
- DocumentId `0888c455-e25d-5f7e-6403-a2bfd4b9fe76`
- ShotId `ecdcaceb-14e3-41ba-ad9e-b2bd3d165ba6`
- ReferenceId `2342446d-6fd8-4f75-b34e-d03e8ccd3ec9`
- BookingId `8be15951-b883-4aaf-96ea-f96d0c933973`
- ExpenseId `3d56916d-0a0a-4de4-92d1-cffb78898633`

资源7类各有新Guid，见SYNTHETIC_PLAN_IDENTITY.json。WPF新增modal创建、行选择→检查器modal编辑各类名称/部分真实参数及ShotIds；正文是真实RichTextBox保存，镜头是真实DataGrid绑定事务。调用真实CreatePlanningCommand新项目、生产JSON stores、隔离SQLite项目/档期/账本，不使用mock存储。

跨区包括镜头↔参考图、道具/设备/人员/时间表的ShotIds；情绪板共用ReferenceId；预算只读读取FinanceService实际隔离支出，读取前后账本JSON相同。实际引用方向是资源ShotIds，不声称已有通用双向引用图谱。复制/归档/进程内撤销重做验证资源稳定身份；镜头复制/归档等既有专项另列，不能借此说新单案所有类型都有同一撤销机制。

失败恢复通过锁定自有planning JSON触发真实保存失败，原文件字节不变、draft落盘，新VM读取待保存正文并重试成功。搬迁只移动这份合成planning根和shots根，原隔离SQLite保留；文档自有图片相对CopyPath与SHA验证仍可读取；项目/文档/镜头/资源/档期引用身份保持。

**保存/恢复限定范围可用，但不是完整便携提案发行验收**：外部参考图与普通附件仍是绝对原位置引用，不自动随项目打包；SQLite项目/档期/财务跨机迁移没验证。每区创建/编辑/关联/重开/导出/搬迁的实际支持、ID和缺项见PLANNING_15_MATRIX.csv/md，物理鼠标列统一NOT_RUN。15个入口构造只作补充，不能替代前面真实数据变更断言。

## 文档格式边界

- RTF：使用产品真实持久RTF编码/PlanningRichEditor回读，验证段落、图片、基础表格；页眉页脚依赖伴随PlanningRichLayout，不声称纯RTF单文件可完整携带。没有独立RTF导入按钮，不伪造入口。
- DOCX：生产ExportPlanningDocxAsync→PreviewPlanningDocxImport→ApplyPlanningDocxImportAsync，OpenXML校验零错误；图片、正文基础表、稳定ShotId生成镜头表、首页不同页眉页脚/页码保留。源DOCX和原RTF文件hash不变。复杂分节/样式继承/任意混排、表内复杂内容按既有拒绝/损失报告，外部Word/WPS未运行。
- PDF：生产216DPI图片式PDF；独立Poppler识别A4/1页并渲染检查，页眉、正文、图片、表、页码可见（EXPORTED_PDF_FIRST_PAGE.png）。不是可编辑/可检索PDF，也没有PDF反导入合同。
- 同一业务策划的导出DOCX确有镜头表，但七类资源表不自动包含；富文本分支PDF也不自动追加镜头/全部概览/CustomBody。不能声称15区全部汇总成一份完整导出文档。

## 本轮测试：旧测试/新增测试分开

| 套件 | 发现 | 选择/实际结果 | 通过 | 失败 | 跳过 |
|---|---:|---:|---:|---:|---:|
| 修改前旧策划WPF专项 |1711总发现|60/60|60|0|0|
| 最终策划Core专项 |1714总发现|25/25|25|0|0|
| 最终策划WPF专项 |1715总发现|64/64|64|0|0|
| 最终完整Core |1714|1714/1714|1708|1|5|
| 最终完整WPF |1715|1715/1715|1694|0|21|
| 最终发布模块 |14|14/14|14|0|0|

最终Core专项旧24+新增1全通过；WPF专项旧60+新增4全通过。
完整Core拆分：旧1713→1707通过/1失败/5跳过，新1→1通过。
完整WPF拆分：旧1711→1690通过/0失败/21跳过，新4→4通过。

WPF四片以(testId,FQN,displayName)多重集合独立核对计划/发现/实际，missing0、extra0、timeout0。全部原名、失败/跳过原因、TRX/适配器发现/runsettings/stdout/stderr与组件哈希在ALL_TEST_RESULTS.csv、NONPASS_TEST_RESULTS.csv、RAW_TEST_EVIDENCE.zip，不继承旧成绩。fail-closed驱动对跳过返回FAILED，不把它当实际Failed；表格如实区分。
Core额外失败为AssetLibraryP35AcceptanceTests.Scenario11_TenKSwitchAndHundredKMetadataPreview 32139ms超过30000ms，保留FAIL，后续独立性能批次，不在此范围修。5个Core/21个WPF未执行条件保持（授权RAW/GPU/性能opt-in/视觉等）。DPI旧证据不复制为本轮成绩。

## 发布、桌面、门禁

产品冻结源码`99d7818e035310c9273565e73752ee0359ebb8f1`，完整诊断构建WholeAppDiagnosticBuild=true、PhotographerTrialBuild=false，Release win-x64 self-contained；发布组件47条逐字节绑定至测试目录。

| 组件 | SHA-256 |
|---|---|
| EXE | 77392EC8DF811CB9AA2DCD47BC40FE6D35823FC23D2D726CF7F938796E233E16 |
| WPF | 735FE50A3EF0BD76424ECD079A1ECF7BAFC65CB5C719A45297F3050A5E4788B2 |
| Core | BD89680916DAFF6F964DA129D136C613E18B5827868CF6F0022D9915E336491D |

产品路径 `N:\pixart\Guangjing-Home-Planning3-20261020-225855\Guangjing-Planning3-Diagnostic-win-x64`。未修改Core源码；SourceRevisionId变化导致组件字节hash变化不等于像素更改。旧包均保留。桌面接口按前批已缺失的状态保持，不重新寻找未授权通道；没有输入、没有真实鼠标截图。DESKTOP_MATRIX全部NOT_RUN，等用户结果后才回填，最短人工复验.md另附。

工具箱设备目录缺真实来源BLOCKED；通讯录/地图/自动天象/跨日自动拆段/任意混排/复杂Word/WPS/一键完整项目便携包不在当前已实现范围。策划资料数据闭环是部分可用，不等于完整拍摄案交付/Windows实机发行批准。

阶段提交：`e9b08863`新增测试；`99d7818e`最小产品修复；证据另交提交，正常push原家用分支并独立查远端SHA。没有发布二进制/私人资料入Git。下一批保留性能失败、桌面实操与上述格式/打包范围阻断。
