# 人像亮部第一轮：图像尺寸及内存契约

来源：6e57eb26；实际修复 a42cb7cd9633ad065f632172ee137614af151324、b86d02455be77e4ed0f44ddd5b9816681979746e。已读取原报告及两个修复的实际 diff。本分支未合并这些无关产品提交；亮部工具直接调用 Core，不创建 WPF 页或请求视口。

- 隐藏旧工作区：RequestImageDetail 必须满足 IsVisible、IsLoaded、视口宽高>0。Collapsed 页仍订阅 SourcePixelSize 事件，不得利用默认 zoom=1 意外升级全图。
- 可见照片页：自身监听 State.Changed，在同样的可见/加载/有效尺寸检查之后，以自身 VisibleSourceRegion、Zoom 和 DPI 请求；100% 是用户视口行为，不能由隐藏页代理触发。
- RGB24 适配：仅完整 backing array、offset=0、Count=Array.Length 可直接给 WIC Create。切片必须按真实 offset/length 复制；WIC 自有副本仍存在。不要把零托管副本误写为零复制。
- 关闭释放：清空 _fullRawDisplay、分析图及派生颜色/像素引用。不可在研究器中持有关闭后的窗口/视图/位图，不加生产强制 GC。

原 7752×5184 = 40,186,368 像素：RGB16 241,118,208 B，float RGB 482,236,416 B，RGB24 120,559,104 B；旧适配器还额外复制一个 RGB24 全帧。缓存、float master、WIC、分析、图形内存不可只算一项。原 2 GiB 守卫是进程上限，缓存重放通过不等于真实整应用资源验收。

本轮固定输入：7028×4688 存储、EXIF8 后4688×7028的授权 sRGB16 TIFF。只读映射不申请整图 float，不调用 TIFF asarray/全图 decode。哈希分块1 MiB；切片仅复制384×384；裁片 RGB16 884,736 B、float32 RGB 1,769,472 B、float64 RGB 3,538,944 B。旋转是 stride view，裁片转正只复制 ROI。

研究 CLI 单边≤512、面积≤512×512，先验证尺寸、文件长度、domain 和 finite，再处理。每个 double 阶段独立 ROI 数组，7个阶段上界44,040,192 B，另加有界输入/输出/临时 float 写出；工具不会输出整幅源图。Python父进程监测子进程 PrivateBytes，每10 ms采样，超过512 MiB杀停；该采样峰值是下界，并非精密 OS 生命周期峰值。外部参考逐次子进程结束即释放。

未触及公司的 NEW_BATCH_DRAFT、授权、ledger、STOP 或原生 worker。新增 RAW 解码=0；没有借用旧批次剩余额度。
