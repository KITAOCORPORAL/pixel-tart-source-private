# 随机快照规范

- UI只有“稳定出片 / 轻微偶然 / 实验胶片”三档；黑白骰子为关闭，彩色为开启。关闭不是第四档。开启与“再掷骰”都是显式新seed操作，普通生成不会隐式重采样。
- 使用64-bit root seed，PRNG标识SplitMix64SubstreamsV1。颜色、铺展、滚轴、纹理分别异或固定域常量建立子流。渲染不访问时钟/RNG。
- 持久化root seed、PRNG版本、profile id/version、tier、ColorShift、SpreadScale、TextureScale及全部Spread/Corner/Roller归一化几何（x/y/radius/strength/period）。快照冻结复制为只读数组；导出入口再冻结一次。
- 显影不均、边角未铺展、滚轴重复污点各有手动强度；美术漏光另列。关骰子忽略snapshot，恢复手动场。切相纸/档位若旧快照不匹配，普通生成明确拒绝并要求显式掷骰，不暗中换纸/曝光/机型。
- 存档未知算法/profile/PRNG、非有限值、非法几何、零滚轴周期均拒绝。概率/幅度/温度影响是创作值，不代表实测统计。
- 照明色温K不在本层；基础WB在输入管线；EnvironmentCelsius仅显影创作近似。i-Type与600共享创作基础明确声明；SX70独立暖度系数；Reclaimed Blue白蓝端与Blue Duochrome黑蓝端分别实现。

测试：八预设×八EXIF完整图/分块/JSON重开；独立子流复现；不同分辨率同归一化位置；关闭恢复手动；实际JPEG会话同seed保存重开；非法版本与几何拒绝。
