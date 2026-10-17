# 第二阶段输入/生命周期

新输入组件2：编码sRGB白底alpha合成，ICC由WIC到sRGB48，方向归一，文件句柄锁+两次完整块SHA，行内取消与额外解码/ICC缓存预算。旧组件1仍原途径/域/解释。双线性配准仍编码sRGB，不改线性。二源选择异步化并绑定照片/activation revision，切图/取消/关闭不会写回新照片。

透明PNG/TIFF、SHORT/LONG方向及sRGB ICC、损坏/缺失/内容变化、SecondHashBefore/Decode/CopyRow/HashAfter阶段取消与租约返回5/5通过。首次方向缺失query过早返回1的真实bug已修；原失败保留。透明测试首次错误比较红色通道（隐藏红本来等白色红），改为真正不同蓝通道，不改变门槛。

Pillow/FFmpeg实际独立alpha参考0误差；完整commit已ls-remote，raw source/API拉取失败如实记录null，不编造逐文件hash；不复制依赖、不声明ICC问题关闭。

PhotoGradePageView使用弱事件，卸载/不可见解绑并取消，载入恢复；StudioToolPanel也改弱事件，未载入控件不被编辑器长期保留。普通步骤同步新增输出像素验证：非恒等曲线变化真实目标输出，未选BasicTone参数JSON完全保留。固定曲线编辑器不是Slider，首轮错误测试假设被修正，原失败保留。

WIC每次CopyPixels/ICC同步不可抢占，只有边界token；预算保守准入不是WIC隐藏分配硬上限。更多真实ICC/关闭原生回落仍不宣称完成。
