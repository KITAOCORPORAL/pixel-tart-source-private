# Pixel Tart Motion System v1.0

## 1. 关键词

Pixel Tart 的动效是：安静、克制、像摄影显影。动效只解释状态、层级和空间关系，不庆祝用户点击，不用运动证明“智能”。

## 2. 时长 Token

| Token | Duration | 用途 |
| --- | ---: | --- |
| `Motion.Instant` | 0 ms | 直接操作反馈、Reduced Motion |
| `Motion.Fast` | 100 ms | Hover、Focus、按钮状态 |
| `Motion.Standard` | 160 ms | Panel、Menu、Tooltip、选择变化 |
| `Motion.Slow` | 240 ms | Viewer Chrome、Drawer、页面内重要过渡 |
| `Motion.Reveal` | 320 ms max | 首次图片渐进显影；只改变透明度 |

单一交互不得串联超过 400 ms。持续任务不使用无限装饰动画。

## 3. Easing

- 进入：`CubicBezier(0.2, 0.0, 0.0, 1.0)`，快速安定；
- 离开：`CubicBezier(0.4, 0.0, 1.0, 1.0)`，不拖延；
- 位移：`CubicBezier(0.2, 0.0, 0.2, 1.0)`；
- Hover 可线性或标准曲线，不使用弹簧、回弹和 Overshoot。

## 4. 允许

- Fade：Menu、Tooltip、Toast、Viewer Chrome；
- Smooth transition：侧栏宽度、Drawer、选择轮廓、画布平移/缩放；
- Progressive loading：低清 Preview 到高质量 Thumbnail / Viewer；
- Skeleton：保持图片比例与文字行，低对比、无闪烁；
- 轻微 4–8 DIP 位移：仅用于说明 Drawer/Panel 从何处出现；
- Crossfade：同一位置的图片质量升级或同一视图模式切换。

## 5. 图片加载

1. 立即保留真实比例；
2. 显示中性占位或可用嵌入式 Preview；
3. 解码完成后 160–320 ms Crossfade；
4. 不从 80% 缩放到 100%，不使用模糊大幅变化；
5. Gallery 批量加载按可视区优先，不做多米诺瀑布动画；
6. 错误替换占位时不闪烁，并提供重试/重定位。

## 6. 组件行为

- Button：颜色/边界 100 ms；Pressed 不缩放、不弹跳；
- Thumbnail：Hover 100–160 ms，操作层 Fade；Selected ring 即时或 100 ms；
- Context Menu：100–160 ms Fade；位置立即准确，不从远处飞入；
- Dialog：遮罩 Fade + 内容最多 8 DIP 位移，160–200 ms；
- Toast：Fade + 8 DIP，160 ms；离开 120 ms；
- Navigation/Inspector：宽度 160–240 ms，Canvas 同步布局，避免先后跳动；
- Viewer：Chrome 240 ms Fade；图片缩放跟随输入并在结束时轻微缓和；
- Moodboard：拖动一比一跟手，吸附仅在接近目标时出现，不自动回弹。

## 7. Loading 与进度

不确定时长使用低速中性 ProgressRing；可计算任务使用确定进度与剩余项。超过约 2 秒的操作进入全局任务中心；Toast 只报告结果。禁止用彩色呼吸光表示“AI 思考”，禁止为每个 Thumbnail 单独显示抢眼 Spinner。

## 8. Reduced Motion

尊重 Windows 动画/辅助功能设置。Reduced Motion 下：位移、缩放、视差、自动滚动过渡全部关闭；Fade 缩短至 0–80 ms；Progress 仍必须可理解；功能、焦点与完成反馈不能依赖动画。

## 9. 禁止

- 弹跳、回弹、Overshoot；
- 大幅缩放、翻转、旋转和卡片飞入；
- 游戏化升级、粒子、彩纸、发光扫描；
- 频繁脉冲的 Accent / Status；
- 自动轮播作品；
- 图片加载时改变裁切和布局；
- 同一状态同时动画颜色、位移、缩放和阴影；
- 动画阻塞输入或超过实际任务完成时间。

## 10. 验收

在 60 Hz、低性能设备、100%/200% DPI、Reduced Motion 下检查；快速连续选择 20 张图片不应积压动画；打开/关闭 Panel 后焦点与滚动锚点稳定；任何动画都能用一句话解释它传达的状态或空间关系，否则删除。
