# SecondExposureInput2节点合同与准入

位置：二源文件→WIC解释→方向/透明→编码sRGB双线性配准→线性混合→旧V3。输入PNG/TIFF/JPEG，ICC存在由WIC转sRGB D65 RGB48，未标记假设sRGB；alpha独立，SHORT/LONG等整数方向1..8只归一一次。新2在编码sRGB域白底straight-alpha合成C=rgb*a+1*(1-a)，输出0..1 float32 RGB。组件1旧解释不改，新选择用2。双线性仍编码sRGB域，不改为线性插值，不声称scene-linear RAW。

两个成熟独立参考已实际运行Pillow12.3.0 ImagingAlphaComposite与FFmpeg9.0.2 overlay straight/RGB；完整commit、许可证和入口见SECOND_INPUT_REFERENCES.json；固定RGBA0/64/128/255 alpha/hidden RGB最大8bit误差均0，门槛1。SECOND_ALPHA_REFERENCE.json保留hash与实际版本。只研究执行，不复制或分发GPL源码/工具。ICC仅复用现有WIC，不宣称通用ICC分歧解决；产品再测透明PNG/TIFF与代表ICC。

SHA按1MiB块取消，头/WIC CopyPixels/ICC不可抢占只能前后检查；像素行内每4096检查。同次输入持FileShare.Read读句柄禁止普通写入替换，前后两次全SHA，避免Core/decoder原先重复4次。组件1保留旧途径。预算最终12N由Core租约，另外64N+文件长度为解码/ICC潜在缓存预算、逐行临时另计；预算是保守准入而非WIC分配上限保证。

适用范围：渲染sRGB主链副输入，不恢复已剪裁RAW信息。真实照片沿用授权冻结RGB16与本轮页面派生身份；精度、取消、损坏/缺失/变更与租约释放专项独立验证。历史组件/原V3像素仍回归。

可复现参考驱动tools/photo-color-v1/check_second_alpha.py，显式新output-dir，合成字节输入、外部版本、逐码差与退出码；本轮再次运行0。Pillow/FFmpeg源文件网络hash失败记录保留，本次未复制或引入其实现；通用ICC引擎准入未完成。
