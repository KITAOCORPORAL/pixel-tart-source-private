# 启动故障修复交付

基准 eae12944c66e9e519fceb0842344f8b0c9038b7b；工作树 D:/AI AGENT/kitao-startup-fix-20261013；分支 fix/kitao-startup-preferences-20261013。
源码 cb55960ec622951877311d1a1cc5aa4a7a0057f8。本次仅修启动偏好生命周期，未合并第十二批测试线。

|项目|结果|
|---|---|
|偏好读取|先恢复独立共享状态，不依赖分组视觉树或选中照片|
|模板生成|主检查器/抽屉各自产生模板后双向绑定同一状态|
|保存|无选中照片、面板折叠、模板移除/未生成均保留状态和有效宽度|
|错误处理|可恢复IO错误记录异常和提示；无空catch，无标题First查找|
|专项|5项新生命周期测试+8项既有联机回归，13/0/0；发布组件同结果|
|完整Core|1660通过/0失败/6跳过|
|完整WPF|1547通过/0失败/23跳过|
|新Release|x64自包含，新EXE哈希；主DLL/Core/模块发布与隔离测试哈希一致|
|实机启动|隔离新目录启动，无错误弹窗，无ERROR日志；旧异常未再现|
|工作台/Color Studio导航|NOT_RUN：前台争抢已观察，按要求停止输入|
|DPI/切图/取消/保存重开/内存回落|实机NOT_RUN；自动偏好保存重开另列通过|

新EXE D:/AI AGENT/kitao-startup-fix-20261013/artifacts/startup-fix/release/win-x64/KitaoPhotoSelector.exe。
新SHA256 D85C8733B24D5BDC96D08BDFDBFB4A08A1B1DA5A65ADFBE75FDF2454BC9C8A91。
旧失败Release保持 D:/AI AGENT/color-studio-closure-20261013/artifacts/spatial-closure/release/win-x64/KitaoPhotoSelector.exe；SHA25629E8D7395306357904ED149D6FE771C30418DBBC480B81B12E339FA3D7C7C119。

主面板/抽屉布局不重排，PV4/PV5/PV6无任何源码改动。eae12944闭环线与5bde6bf测试线共同祖先9e0fc52已核对，但本次没有整合；后续另建集成任务、重新测试发布。不能把本包与测试线sealed release混用。

原始失败与新启动日志、专项失败历史、跳过项均保留。未保存被其他应用遮挡的私人截图，未改系统DPI。发布进程最后观察仍响应，未强制关闭用户桌面应用。其他dirty工作树和超前提交不动。
