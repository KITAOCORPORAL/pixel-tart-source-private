# 源码审查与修复边界

状态：**R3 未完成；待桌面验收的开发版本**。

## 本轮确定修复（不是视觉验收关闭）

1. 变体模板 UpdatedAtUtc 在 StudioEditVersion 中不存在，改绑实际 CreatedAt。模型仍保留原 CreatedAt/RawDefinition/RawInput/OriginVersionId/OriginIdentity/Archived 结构，未伪造更新时间字段。
2. 原新增 PreferDisplayedDimensions 会以代理像素替代原输出尺寸，破坏 100% 语义；改为依赖属性并立即重新配置，采用显示成片比例但保留全尺寸输出单位、平移及坐标尺度。全尺寸相框输出由既有 ProfessionalRawPipeline.OutputSize 提供；未改 Core 成像数学。
3. 克隆按钮改为真实 CreateEditVersionCommand 绑定；变体选择事件增加重入、加载、导出、显影及所属照片检查。克隆/重命名后目录版本仓储及便携项目均重开验证。
4. 停止按钮绑定已存在 StopExportCommand。该命令只取消当前 CTS；ExportAsync 捕获 OperationCanceledException，删除未完成 stage，保留已完成文件，finally 清理 CTS 并更新命令状态。新增三格式取消后重试测试，另跑既有 PixelRender/EncodeStrip/OutputValidation 取消回归。
5. 导出窗口改用 ExportStatus 和 ExportFailureSummary，详情可滚动；导出执行期间禁用格式区，关闭仍由 IsExporting 拦截。
6. 参考线增加空尺寸检查。Solo 和绑定变体选择有不显示窗口的控件行为测试，但真实菜单/鼠标仍未验收。

## 上一轮表述纠正与明确剩余

- 实际保留的是 StudioExportFormatSelector 下拉框，不存在已实现的三格式“配方列表”。自定义导出配方、尺寸管理、完整队列 UI 仍未完成；本轮没有伪造。
- 实际 App.xaml.cs 只改了“R3 未完成”标题，未写入上一轮提到的显式照片启动参数，故不得宣传该入口。
- 历史区域目前是状态文字及真实撤销/重做入口，不是完整可跳转历史时间线。
- 相框、照片信息以及导航已做的代码修改均保留；页签/区块拖动排序、持久化面板顺序、完整导航功能、裁切/取色和动效完整规格仍不宣称完成。
- 14 张 HTML 参考 PNG 属“前轮生成”。原路径 N:/pixart/... 当前不可用；本轮未重跑浏览器、未建立临时服务器、未生成桌面或离屏图作为视觉证据。

## 测试范围

新增回归只构造与测量 WPF 控件，不调用 Window.Show/ShowDialog，不注入桌面输入。两张照片严格来自已授权 manifest，使用最长边 768 的派生图测试；不是新 RAW 解码，也不是原分辨率照片验收。PNG16/TIFF16 回读误差0，JPEG有损，不宣称逐码相等。历史 WPF16/Core27 不计入本轮结果。

## 保留与身份

起点 755b1d80ead4534665373cd54dcd8353a4f1288b；所有接续修改的原始字节及哈希保留在本地 cli-review-20261020/preserved-group2、PRESERVED_GROUP2.json。未 reset/stash/clean/merge/main/force-push，未覆盖其他工作区。
产品源码冻结：d2e52278841b797744af1ce749e2a5e9b170fed1。
最终测试实际装载发布包原样 DLL（见 FINAL_BINDINGS.json），EXE apphost 可能与基线相同，必须以 DLL、Core 和完整包清单联合标识，不以 EXE 单一哈希证明改动。
