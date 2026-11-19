# 光镜433最终验收短交接

功能源码433a1d8e88d839839e4f21341e3e2776696d33a5，分支fix/root-cause-b0-b1-b2-20261117。既有Full产品包和原ZIP不变；后续只提交验收文档/回执。原始根E:/Kitao-Deliveries/root-cause-fixes-20261117。

## 当前结论

- **普通JPEG同项目主线PASS**：项目08d18374…、相册42af2986…、两原始JPEG、曝光0.5、真实浏览器锁定v1/r6、PC收回、实际JPEG2、保存exit0、独立25256重开、本地锁定回执及原来源重扫保持。先读final-acceptance-20261119/RESULTS.md和JPEG_MAINLINE_PASS.json。
- 输出均1200×800/8位/ICC3144；源hash/编辑版本/ImageId/Completed侧车/OutputHash匹配。原工作文件和导出哈希重开保持；原始来源目录Count1。来源子行GUID因现有保存实现会重建，不保证稳定，已单列。
- 匹配0完整/2部分/0冲突/0未找到，原因现有类别JpegAndRaw而批次只有JPG；两原始JPG均找到，不伪称完整2。
- **产品PARTIAL**：普通空名发送、取消/重复关闭、两最终root并存未全部验收；原52/17受控回归与B0探针保持独立证据，不借JPEG成功提升。

## 下一步TIFF，需要当前系统窗口选择一次

JPEG会话已正常关闭保全。新TIFF独立运行根 E:/Kitao-Deliveries/root-cause-fixes-20261117/tiff-433a1d8e-20261119，仍用最终433 Full包/App/KitaoPhotoSelector.exe；当前PID21496只作线索，继续前重查LAUNCH的FILETIME与路径。

当前窗口“打开 Color Studio 工作文件”，需要打开：
E:\Kitao-Deliveries\root-cause-fixes-20261117\tiff-433a1d8e-20261119\output\tiff-input-copy.ptstudio.json

原授权7752×5184 TIFF hash0da1bf38…未变，副本工作文件字节与旧授权ordinary-tiff一致，未改旧文件/原根、不新解码RAW。当前尚未载入，接续普通编辑、保存、正常关、独立重开和实际两次TIFF导出/全行像素ICC比较；不要借原e818证据改标433通过。2GiB/50ms、无强制GC守卫不放宽。

手机此前token已过期401，TailscaleNeedsLogin；真机NOT_RUN，不用旧链接或历史PID当当前证据。历史逐阶段交接完整保留history/HANDOFF_before_jpeg_mainline_pass_20261119.md及各原回执。
