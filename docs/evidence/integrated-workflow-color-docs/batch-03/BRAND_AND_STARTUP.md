# 品牌与启动

显示名称改为 Kitao Studio 光镜：Branding.ProductName、主窗口标题、工具说明、版本页、README 顶部和正式安装器显示名。InternalProductName=KitaoPhotoSelector、程序集、数据库路径、会话格式、AppId、安装目录及升级身份未改。未制作 Installer。

StudioStartupWindow 在单实例检查后创建，黑底彩色三角粒子缓慢漂浮，显示 Kitao Studio / 光镜。App.StartupStage 提供真实状态；主窗口 ContentRendered 后立即触发 150ms 淡出，不等待动画循环。失败关闭窗口，Closed/Unloaded 解绑 CompositionTarget.Rendering。SystemParameters.ClientAreaAnimation=false 或 KITAO_REDUCE_MOTION=1 时静态显示。

本机仓库/工作区未找到 DESIGN.md，未声称读取或复刻原动画；当前为按用户文字要求实现的抽象形态，不是定稿 Logo。CODE=PASS（实现范围），TEST=PARTIAL，RELEASE_RUNTIME=NOT_RUN，USER_ACCEPTANCE=NOT_RUN。实际帧率、视觉、无障碍/减少动态效果需人工检查。
