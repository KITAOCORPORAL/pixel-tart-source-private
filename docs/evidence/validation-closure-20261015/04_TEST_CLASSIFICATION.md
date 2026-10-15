# 本轮与继承测试分类

本轮封存发布Core：适配器发现1702，执行1702，1696 Passed、0 Failed、6 NotExecuted；strict FAILED，发现/结果一一匹配，无超时/漏TRX。完整发现、逐名分类、六个Inconclusive原因与原始TRX/配置/stdout保留。六项：GPU parity两项；可选CPU性能一项；V4 benchmark一项；real corpus一项；full RAW spatial effects一项。独立RAW研究与Structure探针不能把这些NotExecuted改Passed。

P3原10000项测试通过，范围切换预热50.573ms；warm61.894/44.814/39.057ms，worst61.894<400ms。原门槛源文件不变，历史673.8/2499.7失败仍在继承归档。PV4/PV5/PV6 72样本golden断言所在测试通过。

新探针：ICC编译/trace通过，16组阶段轨迹（4 profiles×4 intents）；RAW C API三原件成功；独立RT dcraw Canon/Fuji成功、Sony明确拒绝；full/tile hash相同，两个取消实际CANCELLED。构建早期ABI/环境/取消签名错误均保留，不改成从未失败。

本轮WPF/原生路径/DPI/WPS均NOT_RUN_SHARED_DESKTOP_ACTIVE，未让测试窗口抢前台。继承最后封存源码WPF以SEALED_WPF_SUMMARY为准（1603 Passed/23NotExecuted）；FINAL_WPF_SUMMARY实际仍是中间1602 Passed/1Failed/23NotExecuted，不将其当最后通过报告。该旧文件不改，04_INHERITED_TEST_CLASSIFICATION把二者分别列出。历史Release关键WPF36 Passed也只为继承结果，非本轮桌面验收。

产品src/tests相对af78bb完全无diff；现有Release未重建或升级候选。research脚本不是用户可见功能入口。PV7/SyntheticIccMatrixReader研究默认关闭不变；本批不证明一般ICC支持、A7M4绝对色准或胶片精确复刻。
