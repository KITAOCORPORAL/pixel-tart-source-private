# 暗室 R3 接续开发交付：PARTIAL / R3 未完成

日期：2026-10-20，用户时区 Europe/Berlin。前次会话：01a1570e-8608-7cf0-b972-df1a760d48f2。

## 身份和交付

- 接续起点：dd6f1bb8abc4e8c50c42ad8e0facadfd3f1ed7c5。
- 产品源码：07c11d750d927e8cdd1b5d6f42800fae0bbf0f44。
- 分支：feature/darkroom-lightprint-r3-20261019。
- 新EXE：E:/Kitao-Deliveries/darkroom-r3-company-20261020/app-panel-review/KitaoPhotoSelector.exe。
- 完整290文件清单：RELEASE_MANIFEST.json；发布DLL与测试宿主原样字节绑定：FINAL_BINDINGS.json。
- 实际进程PID 42124：PROCESS_MODULES_NEW.json；启动可访问性观察：NEW_EXE_STARTUP.txt。

## 已实现并经本轮命令行回归的项目

1. 在既有 ColorStudioLayout 增加独立、可选的 DarkroomPanelLayout，保持旧项目缺省可读；不复用旧编辑器312px下限去夹暗室304px宽度。
2. 保存恢复暗室面板宽度280–460、显隐、当前页签、Solo、区块折叠与顺序。388px非默认宽度、隐藏后重开和项目保存重开均有断言。旧项目/未知区块保持安全兼容。没有更改相纸/相机/RAW成像数学。
3. 工具面板菜单移至页签行；变体页隐藏直方图不再隐藏菜单入口。新增区块上移/下移菜单，顺序写入便携项目；这不是页签或区块拖动排序。
4. 显式“全部展开”退出Solo，不再只留下最后一个区块；启用Solo时每个面板保留至多一个展开区块。
5. 初次测试13/14暴露既有变体选择竞争：前次切换未退出时下一选择被丢弃。改成串行处理且保留最后一次有效选择，保留当前照片归属、归档与导出/显影忙检查；未修改图像版本定义。

## 本轮最终发布DLL成绩（不继承旧54/30或16/27）

- WPF：60/60，通过60，失败0，跳过0；按10个测试类独立进程运行。
- Core：30/30，通过30，失败0，跳过0。
- R3专项14/14，含新增3项布局/菜单/旧项目兼容以及授权两照片流程；导入专项3/3。
- 两张授权照片先校验既有manifest SHA-256，使用最长边768px派生输入，不扫描私人目录。结果覆盖相纸与方圆相框像素变化、最后选择、显影一致、锁定变体切换、保存重开、PNG16/TIFF16/JPEG导出；这不是原分辨率/RAW/真实鼠标验收。
- 原始TRX、完整日志与逐类计数见trx/、TEST_RESULTS.json。首轮失败及构建失败日志保留在retained-failures/，没有抹除后改PASS。
- 最终测试宿主构建135项分析器/既有警告、0错误，Core宿主24项警告、0错误；不宣称零警告。

## 实机与视觉仍未完成

恢复旧包观察到2张照片仍载入；新包已另用隔离运行目录实际启动并核对模块。没有关闭旧进程、丢弃其导入状态或替用户选择受限路径。

窗口专用截图返回FrameArrived/window capture超时；真实输入返回coordinate input geometry is unavailable，当前接口不支持列表Select。新包亦无法完成目标窗口截图。本轮未保存含无关应用的全屏截图，未启用安全回退/绕过HTML限制。没有最终1366×768/1920×1080并排对照，没有冒充离屏图为桌面图。

**D02–D14不因CLI通过而自动关闭。** 相纸相框连续真实点击、显影/锁定窗口调色、撤销重做、原生保存重开/导出、物理DPI、动效及拖动排序均需实机闭合。完整历史时间线、全规格相框/纸型、导出尺寸/配方/队列等原剩余规格仍开放。详见DESKTOP_ACCEPTANCE.md。

## 环境故障与保留

恢复开始工作树干净，已审计工作树/ref/remote并fetch；未reset/stash/clean/merge/force-push，没有修改其他工作树。
NETSDK1060/NuGet.Configuration.ConfigurationDefaults path1错误由本进程补足已验证Windows目录及禁用MSBuild节点重用/单节点全新构建后解除；无持久系统配置变更，原失败日志保留。

正常推送及远端读回见PUSH_READBACK.json。产品与文档提交分开，产品仍标R3未完成，不称READY。
