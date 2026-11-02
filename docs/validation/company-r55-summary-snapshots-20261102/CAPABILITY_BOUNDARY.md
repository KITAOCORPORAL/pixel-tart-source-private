# 策划汇总固定快照能力边界

15区汇总DOCX现在可按保存顺序呈现已保存CanvasRevision.PreviewPngBase64；没有明确位置时位于文档内容，存在正文段落或灯位的显式revision关联则在该位置呈现，文档内容保留版本索引。不会猜测情绪板归属。

只读阅读文档，不是可编辑自由画布、完整策划中心验收或跨机项目包；.ptplan v1范围不变。不访问当前画布或照片重渲染，不升级v1/v2/v3，不修改旧PNG和revision。

编码限32MiB Base64字符、24MiB编码字节；8位单帧静态PNG，最长边16384、总像素4194304；按16B/像素估算单图解码工作64MiB，不含编码输入/编码器/完整进程。原画布256MiB预算不变。复用现有PNG解码和DOCX编码，逐图延迟解码/写入并解除解码源引用，不缓存所有版本的大图像素。不是整个进程硬内存上限。

缺失、损坏、unsupported或超预算PNG在相应位置标出原因，同时返回导出Warnings，不偷换当前画布或别的版本。保留富文本、参考图、普通图片既有导出规则；已有DOCX不被覆盖，失败清理staging。

已完成发布DLL绑定的合成输入内容/alpha、位置/比例、15区、原子失败/项目切换、资源不变和上一轮保存失败恢复回归；实际DOCX通过产品适配器读回并逐页离屏检查。独立Word/WPS/LibreOffice排版签收未完成：本机无可用soffice.exe，render_docx.py失败日志保留。产品适配器读回不等于独立Office验收。

鼠标/DPI/ACM/HDR未执行，禁用ComputerUse/Browser/WebView/CDP未使用；JPEG/ICC原数值FAIL与研究边界保持。整体仍PARTIAL_DIAGNOSTIC_NOT_FOR_RELEASE，不称整个策划中心全部验收。
