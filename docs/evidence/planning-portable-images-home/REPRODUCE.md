# 复现

在N:/pixart/kitao-planning-portable-images-home-20261014，C:/Program Files/dotnet/dotnet.exe / SDK10.0.401。先build两测试项目和tools/VerifiedTestRunner。

verified_runner.py参数 --assembly实际test.dll --output独立目录 --dotnet本机dotnet --sdk本机SDK；Core完整加--single-process，WPF完整逐类。review_gate.py以完整Core/WPF summary和review-classification执行，不修改strict summary。

只有PREPUBLISH_GATE无阻断后，最终源码bbebec8构建Release win-x64自包含新目录home-review-final/win-x64。复制正常testhost到home-final-components，仅覆盖7发布DLL，不混自包含runtime。发布Core全量设置PIXEL_TART_BATCH12_RELEASE_CORE指向实际隔离host中的Core，再单跑Batch12ReleasedAssemblyTests做实际路径证明。WPF准确类见RELEASED_WPF_DISCOVERY/SUMMARY，含3搬迁、6托盘、表格/DOCX/富文本/启动偏好35项。

发布后review_gate --postpublish同时吃源码Core/WPF、发布Core/WPF和身份单项结果。原分类条目全保留，6新增全required。runner门禁7项unittest通过，未改门禁代码。原始包不含照片、数据库、runtime或Release二进制。

KITAO_PORTABLE_IMAGE_EVIDENCE设置独立绝对目录可生成脱离原根后的合成DOCX和JSON摘要。样本根目录仅由每测试临时Fixture拥有，CopyTree复制、Directory.Move撤原根；不使用/移动真实客户文件。
