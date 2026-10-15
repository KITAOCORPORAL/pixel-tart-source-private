# 二进制ICC合成子集补充

增加真实bytes ICC v2 RGB/XYZ matrix/linear curv(0) fixture parser，hash、长度、header D50、7已知tag、边界/重叠/白轴一致全检查；unknown A2B/LUT/曲线/设备声明拒绝。不是通用ICC文件加载器，不改变StudioQuickExport或输入生产profile准入。

合成数据由独立生成器程序构造，无厂商原件或授权疑义；D50 PCS通过Bradford到D65工作。单测灰/白和未知/越界/曲线拒绝2项通过。当前仍不是完整输出ICC编码器/设备测量，真实camera accuracy未证明。
