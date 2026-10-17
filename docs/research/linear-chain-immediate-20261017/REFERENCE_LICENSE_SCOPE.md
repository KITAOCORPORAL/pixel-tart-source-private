# 参考来源与许可边界

Pillow12.3.0固定commit bb1d8e8ab8d29048624d96e3ee53cecf7c13d13d、FFmpeg9.0.2固定commit946fcce07b6dcd0331c8cc609192aeff5e1924f8：已通过GitHub contents API取回原缺失的函数源码/许可证，git blob/sha256见RECOVERED_REFERENCES.json；旧SECOND_INPUT_REFERENCES.json保留fetch失败并追加recovered字段，不删除历史。

源码等于某固定提交不自动证明当前已安装wheel/exe就是该commit构建。已执行Pillow12.3.0、FFmpeg9.0.2版本匹配，缺可复现build provenance，所以不扩大成二进制全来源准入。Pillow MIT-CMU、FFmpeg本机GPL/version3配置只外部参考；不复制、链接或分发到产品。FFmpeg LGPL许可证页不代表本机GPL构建变为LGPL-only。

colour-science0.4.7 BSD3与OCIO2.4.2 BSD3沿前轮固定文件hash，OCIO GammaCPU/GammaData源码与LICENSE本轮取回。GammaOpUtils.cpp API额外取回遇到rate limit，OCIO公开算子文档加已取得源码说明切线参数；不捏造该文件hash。scikit-image0.25.2完整commit已解tag并核colorconv与license原件。外部脚本只提取原函数做transfer比较，没有放进src/或Release。

第三方完整LUT/贴图/其余附件未补，仍缺资源，不宣称全部吸收。
