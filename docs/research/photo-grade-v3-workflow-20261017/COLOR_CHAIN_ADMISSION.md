# 第三阶段：最小色彩链预准入失败，未进入生产

拟定最小链：文件ICC→渲染sRGB D65→EOTF→相对线性sRGB曝光→OETF/输出裁切→sRGB文件；不等同RAW场景输入。继承PhotoWideV1固定colour0.4.7提交a3bfe349685f528100672e5c8ca2dfeeef64a273、OCIO2.4.2提交6918fad3f5d22ac3ef2397c754bf4268c2b58dd0与BSD3外部参考，文件hash在上一轮REFERENCE_PINS.json。

实际在编码负值/0/.003/.04045/.5/1/>1以及EV-2/0/.7/2运行两独立CPU参考。colour最大5.272673e-8超过既有2e-10；OCIO的sRGB ExponentWithLinear阴影切点为连续切线，与标准舍入分段不同，最大2.518114e-6超过既有2e-6。LINEAR_CHAIN_PRE_ADMISSION.json保留失败，不放宽阈值，不用自身往返替代。失败集中分段阈值与近黑，不是一般曝光乘法失效。

因此本轮没有将这个候选接入生产，没有为完成清单而加假入口。PhotoWideV1仍公式库；新页继续已准入V3/普通照片现有工具。下一阶段需先制定独立明确转移函数版本、阈值相等时规范解释，以及OCIO连续近似和规范公式的对照边界；保持原门槛不以“看起来一样”通过。

RAW最小生产链还需要独立场景输入profile/矩阵准入，不能拿LibRaw已剪裁display输出冒充scene-linear，显示器ICC分歧仍独立。输出sRGB文件转换、WPF显示和系统显示ICC不是同一验收。原PV4/5/6与已存V3未改解释。

本阶段未达到用户要求的最小生产接入，是剩余任务而非产品完成项。
