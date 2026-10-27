# 家庭 Scene EV 专项
800px快速采样长边最多1600；输入身份/DecodeKey/几何/曝光前定义组成cache key；Scene EV/mask/Locked不使曝光前样本失效。2缓存拥有lease，1inflight+1latest队列，换上下文严格取消，停400ms精细1600。导出原full-res row/region不使用快速图。
变化密集高频源以及GeometryV2回退精细，性能可慢；同机Canon参数测试见PERFORMANCE.json。平均/95/最大固定预算1.5/4/32（RGB8），2x反例失败记录保留；不是所有图保证，也不是精细逐像素相同。
原PV4/5/6 defaults未变；GeometryV1最近源采样不变，V2才double bilinear/±15°/constant black。色彩空间边界COLOR_BOUNDARIES.json。
