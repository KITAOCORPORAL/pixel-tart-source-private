# 真实RAW质量覆盖

本批复用batch06本机授权副本，不扩扫私有照片目录，不提交照片、联系表、成片、TRX、生产数据库或凭证。5组Sony ARW/JPEG授权证据和Canon公开fixture来源仍引用batch06/CORPUS_COVERAGE.md；副本源SHA256和实际native相机名核对。原集成工作树及审计目录保留。

| 覆盖 | 类型 | 本批结论 |
|---|---|---|
| Sony A7M4 / Canon EOS5D III | 真实文件 | 两相机完整preview/native tile/快速3格式/Publishing3格式验证；各自源hash一致 |
| 暗人像/火焰/环境混合光 | 真实照片内容 | 像素/格式回读PASS；肤色与高光视觉批准NOT_RUN |
| 中性灰/曝光阶梯 | 合成数学+已有旧合成 | 灰阶/EV定义回归PASS；真实灰卡、实拍标定阶梯本机授权集合未找到，NOT_RUN |
| 细线/棋盘格 | 合成 | 区域积分不会丢一像素细线；alias输入按display-linear面积oracle验证PASS |
| 高饱和边缘 | 实际Canon/JPEG质量 | 默认抽样MAE6.488失败，444后2.947；不放宽MAE5/RMSE15阈值 |
| 方向 | 合成EXIF1–8 + encoder旋转6 | 原像素region坐标和真实encode尺寸/朝向PASS；两真实文件orientation1 |
| 未知相机/已WB小RAW | 无可信标定语料 | 沿PV4既有拒绝条件回归；真实质量NOT_RUN，未借新一轮下载无授权照片 |
| 负值/超范围/saturation | 合成+batch06实文件 | 全尺寸数学未改，边界回归PASS；原uint16裁切限制仍在 |
| C1/灰卡色准 | 无本批授权测量 | NOT_RUN；内置矩阵可用不等于色准批准 |

本批没有为通用RAW质量盖章。预览抗混叠、pixel/native详情与输出有数值证据；显示器/DPI/用户视觉仍NOT_RUN。不会用不同内容照片pixel差评价仿色。所有真实file只在授权副本上，源文件不改动。
