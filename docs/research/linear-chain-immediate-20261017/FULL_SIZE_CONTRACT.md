# 原尺寸双照片与预算

第一Canon5496×3670 landscape，第二Sony原冻结方向归一后4688×7028 portrait。来源/hash既有授权manifest，每次核原件和冻结文件，不扫描私人目录。不同内容/尺寸/宽高比，在真实新页控件设置V3、第二权重.37/crop.2,.7/zoom1.21，undo/redo像素、切图、session重开、实际ExportSelected三格式完成。首次局部选在圆窗外纸基，变化测试正确拒绝；改选照片内部实际坐标，三个参数均确实改变ROI，不改算法让纸面受影响。

原WIC预算76N+文件长度导致二源Sony需>2.50GB、Canon>1.53GB超1GiB准入，不扩大预算。TIFF3只认产品单strip RGB16精确sRGB ICC，以row流读，最终12N，1/2历史不改，WIC独立逐码对照通过。

初轮完整原尺寸输出约4530×4530（以格式回执为准），预览512对完整TIFF线性面积oracle最大2.165012e-6 <=2/65535，TIFF/PNG逐码0，JPEG MAE.519；不是重复输出当预览对照。实测进程working-set峰4.579GB、private结束2.981GB，包含反复全尺寸新旧工作区/GC；不能声称2GiB台账是进程上限。workspace dispose后仍有preview lease，驱动现显式释放再测，最终交付绑定重跑。

测试结果照片/项目在忽略目录，仅脱敏数值JSON提交。Fuji冻结只数学参考，不新解码。
