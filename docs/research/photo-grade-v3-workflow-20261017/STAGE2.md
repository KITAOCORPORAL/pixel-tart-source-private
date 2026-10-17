# 第二阶段输入/生命周期

新输入组件2：编码sRGB白底alpha合成，ICC由WIC到sRGB48，方向归一，文件句柄锁+两次完整块SHA，行内取消与额外解码/ICC缓存预算。旧组件1仍原途径/域/解释。双线性配准仍编码sRGB，不改线性。二源选择异步化并绑定照片/activation revision，切图/取消/关闭不会写回新照片。

透明PNG/TIFF、SHORT/LONG方向及sRGB ICC、损坏/缺失/内容变化、SecondHashBefore/Decode/CopyRow/HashAfter阶段取消与租约返回5/5通过。首次方向缺失query过早返回1的真实bug已修；原失败保留。透明测试首次错误比较红色通道（隐藏红本来等白色红），改为真正不同蓝通道，不改变门槛。

Pillow/FFmpeg实际独立alpha参考0误差；完整commit已ls-remote，raw source/API拉取失败如实记录null，不编造逐文件hash；不复制依赖、不声明ICC问题关闭。

PhotoGradePageView使用弱事件，卸载/不可见解绑并取消，载入恢复；StudioToolPanel也改弱事件，未载入控件不被编辑器长期保留。普通步骤同步新增输出像素验证：非恒等曲线变化真实目标输出，未选BasicTone参数JSON完全保留。固定曲线编辑器不是Slider，首轮错误测试假设被修正，原失败保留。

WIC每次CopyPixels/ICC同步不可抢占，只有边界token；预算保守准入不是WIC隐藏分配硬上限。更多真实ICC/关闭原生回落仍不宣称完成。

追加实际生命周期12次解绑/恢复、200次草稿拖动（不启动像素任务）、卸载控件WeakReference GC可回收，关闭workspace租约81792→0；进程private仍有托管缓存，不将台账释放等同原生完全回落。测量含4秒显影展示动画；区分渲染阶段后续证据。取消按钮补接实际显影和二源选择取消。

受控linear-sRGB合成ICC被WIC拒绝：它是research-only子集，不把它当真实ICC准入。保留原失败，专项改为确认拒绝且台账恢复；未悄悄用原RGB换标签。嵌入实际sRGB ICC已通过，但其他真实ICC广泛覆盖仍未验收。

最终复核补齐：重置覆盖共享amount/边距/输入来源，不仅V3前缀字段；保存配方中的Original/CurrentEdits切图正确恢复；PNG/TIFF不支持Original时选项明确禁用，JPEG/RAW仍可选；选中调整步骤改变时同步可用性通知更新。因这些src更改，先前04-final-delivery也标SUPERSEDED并停止本任务子进程，重新冻结后构建全回归，不拿旧包冒充最新。

完整WPF暴露历史V2回归：普通照片初始版本捕获错误扩大到V2，旧CurrentEdits配方OriginVersionId从null变成新ID。修复仅允许V3普通照片新捕获（RAW历史原行为保留），不改旧V2语义，不改旧断言。原BatchNineCurrentJpegEditsSurviveGenerationAndReopenWithoutProxy失败完整保留，重新冻包验证。
