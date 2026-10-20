# 续接交付结果

状态：**R3 布局未完成**，可启动开发预览，不是完整R3交付。

## 本地入口

- EXE：N:/pixart/Darkroom-R3-Resume-20261020/app/KitaoPhotoSelector.exe
- 启动脚本：N:/pixart/Darkroom-R3-Resume-20261020/启动暗室预览.cmd（独立UserData，不打开浏览器）
- 手动评审：N:/pixart/Darkroom-R3-Resume-20261020/视觉对照.html
- 14张同尺寸并排图：N:/pixart/Darkroom-R3-Resume-20261020/comparison
- 双照片显影/重开/导出：N:/pixart/Darkroom-R3-Resume-20261020/wpf-shipped/two-photo

## 构建与测试

产品源码冻结：2c4ce66b73b91316e7fa8eaa997f92c41a824651。之后仅测试断言/文本证据，不重标产品源码。
发布EXE SHA-256：E9A219E85F26DC594C85531D0FF016D84B32E65D80097991D29D4AD9AE5068E7。
WPF DLL SHA-256：3AAB4063F321618B82DB606A03207C80D5E6774FDC8FF32E5D742F60495FC23E。
Core DLL SHA-256：AF418F8371976A99BC408BA384E55DBF2F9B13B51EC04081889E858BF6A52385。

- 发布成功；审计工具构建0警告/0错误。测试工程存在既有MSTest风格建议警告。
- 独立EXE启动获得真实主窗口句柄和标题，18/15秒观测未退出，正常关闭；最终15秒结果见STARTUP.json。不算视觉验收。
- 同发布WPF/Core DLL实际替换进测试目录：WPF 16通过、1跳过；Core 27通过、0跳过。
- 跳过项需显式授权照片manifest，未为运行它而搜索用户目录；独立R3双照片流程使用前轮已授权的两张1000边长输入，均成功。
- 组合框/分组列表绑定测试实际选择相纸、相框并等待预览；没有物理鼠标点击声称。
- 双照片相纸变化/相框变化/最后选择优先/显影像素等于最终预览/旧变体切回/撤销重做/保存重开，均断言通过。
- 两张照片各导出PNG16/TIFF16/JPEG8。PNG16/TIFF16独立回读最大误差0；JPEG为有损结果，不宣称逐码相等，数值见TWO_PHOTO_SUMMARY.json。
- 关闭动画，双照片显影演示时间均小于0.01ms；不代表全程渲染无等待。
- 全覆盖清单155行，按当前新页入口保守登记。不继承原型CSV的“已放入”为实际可用证明。

## 未完成

详见VISUAL_DIFFERENCES.md和STATUS.md。无浏览器重跑、无EXE桌面同尺寸截图、无真实点击录屏、无物理DPI验收。离屏图和导出内容叠加均显式标注，不冒充桌面证据。

本轮没有改动Core色彩数学或扩充第三方角色资产，没有合并main，不强推。源码与证据正常推送原开发分支；最终SHA/远端核对另行登记。
