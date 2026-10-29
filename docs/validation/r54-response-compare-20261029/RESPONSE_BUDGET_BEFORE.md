# 响应与误差预算（实现前冻结）
日期2026-10-29 Asia/Shanghai。源HEAD6ad9ce72fd464f48e839f9cb3d213faf37572a6d，产品674aaa1887a03173e439d593e505a95d26a22139。
- 热调节：已有源/相同输入与代理缓存预热后的实际SetToolParameter命令到快速结果发布，P95<=750ms、单次<=1000ms；冷准备/停手精细/full编码分开报告，不拿裸Render代替用户命令。
- 误差沿原可用预算：mean<=1.5 / P95<=4 / max<=32（8bit encoded sRGB）。不追改阈值。快速800长边，停手400ms后1600精细；最终导出full尺寸/位深/数学不变。
- 快速仅已承认点工具/有解释Graph2五混合；空间细节/复杂参考、细mask(radius<.02或feather<.3)、旋转拉直/裁剪风险、强WB/强contrast/高光阴影、高频边界保守回退。每输入预检缓存source hash/DecodeKey/尺寸方向/ICC/几何/base定义，参数不重复解码。
- 验证自然20MPCompanyRAW、合成高频/细蒙版/多分支，完整fine与源基线逐位一致；自然照片代理误差对同分辨率area oracle。风险回退不计“热快速已达标”。
