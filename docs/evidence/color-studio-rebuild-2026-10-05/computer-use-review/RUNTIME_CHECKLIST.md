# r1 实际运行巡检清单

2026-10-07，时间索引以UTC记录，可换算Asia/Shanghai（UTC+02）。本文件覆盖本轮实际操作；95个子项均已归档，**不表示95项已完成验证**。复合项任一失败为FAIL；未测组合保留PARTIAL或NOT_RUN。每项CODE/TEST另见末尾，USER_VISUAL_REVIEW始终NOT_APPROVED。

全部截图和会话只来自r1：SourceHead `31d3ac3ae56d6ccd1d9b4c608bf522dbb735d591`。完整身份见 [BUILD_IDENTITY.json](BUILD_IDENTITY.json)，窗口/实际DPI见 [SESSION_IDENTITIES.json](SESSION_IDENTITIES.json)，输入hash见 [INPUT_IDENTITIES.json](INPUT_IDENTITIES.json)。公开证据采用CC0数学图片；真实RAW/JPEG原片与完整截图留本机，见 [私人索引](PRIVATE_EVIDENCE_INDEX.md)。合成图不能替代真人肤发画质判断。

## 记录读法

- 表中“检查点”是原定完整操作；“实际结果”说明真实覆盖、输入值、预期与观察及缺口。PASS仅限该行已明确的行为，不批准整个工具。
- 公开编号可点击全窗；Before/After及连续动作顺序、UTC、PID、原始hash、遮挡矩形在 [SCREENSHOT_MANIFEST.json](SCREENSHOT_MANIFEST.json)。未公开步骤以私有索引动作ID定位。
- 普通调节预期为照片与双图同步可见变化；复位预期回中性；观察预期不写参数或导出；持久化预期重启恢复同状态。没有逐项观察/数值，不以控件存在判PASS。
- 本轮没有生产修复、没有重跑历史Core/WPF全套。全部历史自动结果仍是背景，不能证明当前UI。5份本轮实际UI导出比对与重开文件比较另见 [EXPORT_COMPARISON.json](EXPORT_COMPARISON.json)。
- 001–032进程DPI不可靠，旧96推断作废；033实际144，s02才实读96。301描述“Home”错误，实际Home303；127–244旧helper统记RT11，按实际动作归属回读，不能据旧rt字段算覆盖。
- 旧RT10.1“100%物理1:1失败”、PID47772/s2图09/10引文撤回；没有测屏幕源像素对应关系，本轮仅PARTIAL。源码DIP单位是线索，不是实机FAIL。

## 汇总

| 编号 | RELEASE_RUNTIME | 已确认问题 |
|---|---|---|
| RT01 | PARTIAL | 其余缺口详见下表 |
| RT02 | FAIL | CU-01 |
| RT03 | PARTIAL | 其余缺口详见下表 |
| RT04 | FAIL | CU-05 |
| RT05 | PARTIAL | 其余缺口详见下表 |
| RT06 | PARTIAL | 其余缺口详见下表 |
| RT07 | PARTIAL | 其余缺口详见下表 |
| RT08 | FAIL | CU-02 |
| RT09 | PARTIAL | 其余缺口详见下表 |
| RT10 | PARTIAL | 其余缺口详见下表 |
| RT11 | FAIL | CU-03 |
| RT12 | PARTIAL | 其余缺口详见下表 |
| RT13 | PARTIAL | 其余缺口详见下表 |
| RT14 | FAIL | CU-01 / CU-04 / CU-05 |

## RT01

| 子项 | 检查点 | 本轮实际结果、限制与证据 | RELEASE_RUNTIME |
|---|---|---|---|
| RT01.1 | 校验EXE/DLL/ProductVersion/manifest后启动r1；记录新会话与窗口，确认没有启动异常 | 核验r1哈希、版本、287/287发布文件后启动7次；新PID与实际DPI见SESSION_IDENTITIES。没有观察到启动崩溃。 [061](evidence/061-r1-after.png) [111](evidence/111-r1-after.png) [250](evidence/250-r1-after.png) | PASS |
| RT01.2 | 由一级导航进入参考仿色/Color Studio；核对标题、选中导航、空目标与空参考提示，空直方图不误判缓存故障 | 主界面→Studio空态(002/060私有)；本轮Home→Studio(303/304)批次保留。空图直方图为空符合预期。 [303](evidence/303-r1-after.png) [304](evidence/304-r1-after.png) | PASS |
| RT01.3 | 导入至少2张JPEG和1张RAW；逐张确认图像内容、方向及当前目标身份，记录失败提示而非只看缩略图出现 | UI导入6张公开PNG/JPEG/TIFF及授权R01与2JPEG，逐张切图；实际打开三种UI导出产物。未覆盖所有方向和失败输入。 [061](evidence/061-r1-after.png) [096](evidence/096-r1-after.png) [097](evidence/097-r1-after.png) [098](evidence/098-r1-after.png) | PARTIAL |
| RT01.4 | 由本次会话打开既有测试批次；确认数量、当前图、选择、调整恢复；与RT13新保存重启分开 | 本轮保存的9目标批次在新进程s03重开，首3五星/范围49、羽化29恢复；s07另重开10目标、红过滤、选2。 [111](evidence/111-r1-after.png) [250](evidence/250-r1-after.png) | PASS |
| RT01.5 | 在受控工作文件副本验证缺失源提示和记录保留，不移动用户原件；取消导入/打开不清空当前现场 | 缺失源副本、取消导入/打开完整保护流程未操作；没有证据，不作通过。  | NOT_RUN |

## RT02

| 子项 | 检查点 | 本轮实际结果、限制与证据 | RELEASE_RUNTIME |
|---|---|---|---|
| RT02.1 | 1600×920取得全窗；逐区对照原图八区域，照片完整、3D在左、两图在右顶，底部紧凑 | s02实际DPI96/1600×920：左球体、中央完整照片、右顶双图、紧凑胶片栏符合区域归属；控件风格与图标密度仍有差距，见VISUAL_COMPARISON。 [061](evidence/061-r1-after.png) | PARTIAL |
| RT02.2 | 收展全局导航、左辅助栏、右编辑栏；记录中央照片Fit/矩形是否合理重算，无遮挡、跳离可用视口 | 全局导航收起、窄窗自动折左栏再打开已操作。全部折栏组合和主图变换量未量测。 [061](evidence/061-r1-after.png) [112](evidence/112-r1-after.png) [114](evidence/114-r1-after.png) | PARTIAL |
| RT02.3 | 展开/折叠右上分析组；确认直方图不移回照片上方，中央照片不被遮挡，右侧工具空间返回 | 点击当前预览分析收起/展开(007、320、332)，双图始终位于右侧；工具空间增加，未覆盖主图。仅限定已测窗口。 [017](evidence/017-r1-before.png) [332](evidence/332-r1-after.png) | PASS |
| RT02.4 | 左右独立滚动至底部、返回顶部；关键操作始终可达，滚动不误调主图或邻栏 | 普通窗滚动能到达影调0–X及编辑子组；1180×720DIP的右栏横向裁切，数值/复位/Creative不可达，CU-01。 [065](evidence/065-r1-after.png) [112](evidence/112-r1-after.png) [123](evidence/123-r1-after.png) | FAIL |
| RT02.5 | 收展胶片栏并恢复；观察返还的主图空间、当前选择及可见目标不丢失 | 胶片栏完整收起→展开及选择连续性没有专门完成；按钮存在不作行为证据。  | NOT_RUN |
| RT02.6 | 开始/结束一次预览计算；处理提示和取消按钮出现时主图不突跳，完成恢复正常状态 | 多次工具计算结束busy消失；RAW119→120点击Stop与完成竞态不能证明取消。完整几何稳定性未量测。 [018](evidence/018-r1-after.png) [342](evidence/342-r1-after.png) | PARTIAL |

## RT03

| 子项 | 检查点 | 本轮实际结果、限制与证据 | RELEASE_RUNTIME |
|---|---|---|---|
| RT03.1 | 左侧参考：载入测试参考，来源仅必要一份，Fit/100%/Zoom/Pan有效；参考图只读，目标参数不意外变化 | 参考载入公开竖图；136→137→138→139实际100%、平移77×58、112%、Fit20%；只读来源。此段捕获带任务栏/边界不完整，留本机，不作为公开全窗图证；各参考参数未全验。 [008](evidence/008-r1-after.png) | PARTIAL |
| RT03.2 | 左侧3D：模式切入实际球面/样本内容，当前选中态明确；离开返回不产生第二套编辑状态 | 实际切入3D模式，真实样本3989；回Nodes/Film/Reference再返回未生成第二编辑状态。 [049](evidence/049-r1-before.png) [053](evidence/053-r1-after.png) | PASS |
| RT03.3 | 左侧胶片预设：浏览、hover/离开、应用及取消；与右Film当前参数职责区分，临时预览不永久提交 | 左预设浏览并应用暖调/冷银灰，右Film参数跟随。XMP、hover取消、用户方案保存/重载未完成。 [009](evidence/009-r1-after.png) [084](evidence/084-r1-before.png) [085](evidence/085-r1-after.png) [086](evidence/086-r1-after.png) | PARTIAL |
| RT03.4 | 左侧节点：选择、启停、重命名、重排、复制/删除及undo/redo；右侧参数跟随正确节点 | 实际节点启停、复制、重排、中文English重命名Enter(190–198私有)；删除、全部undo/redo组合未测。 [010](evidence/010-r1-after.png) [069](evidence/069-r1-before.png) [072](evidence/072-r1-after.png) | PARTIAL |
| RT03.5 | 右3D页：打开有实际观察参数内容，修改只影响查看；全部显示控制详测RT08 | 右3D页实际打开，有色度、切片、XYZ和容差控制；观察参数与照片编辑区分；具体组合仍见RT08。 [011](evidence/011-r1-after.png) | PASS |
| RT03.6 | 右色彩页：白平衡、基础/HDR、范围/肤色、平衡父子组可折叠，数值与同节点一致 | 色彩页实际操作WB/基础/HDR/范围/肤色/平衡父子组、折叠、复位；参数与节点同步（限定所测参数）。 [018](evidence/018-r1-after.png) [030](evidence/030-r1-after.png) [070](evidence/070-r1-after.png) [317](evidence/317-r1-after.png) [322](evidence/322-r1-after.png) [326](evidence/326-r1-after.png) | PASS |
| RT03.7 | 右色阶页：RGB/R/G/B父子组可达；右曲线页：真实控制点编辑器可达 | 色阶与曲线实际打开并编辑，RGB与单通道可达；并非仅看标签。端点限制另留RT05未全测。 [012](evidence/012-r1-after.png) [013](evidence/013-r1-after.png) [038](evidence/038-r1-after.png) [043](evidence/043-r1-after.png) [336](evidence/336-r1-after.png) [340](evidence/340-r1-after.png) | PASS |
| RT03.8 | 右细节页：清晰度/锐化/降噪；右Film页：当前胶片参数；右Creative页：实际既有能力，缺能力不计假入口 | 细节/Film/Creative均实际打开；细节和Film产生像素变化，Creative仅发布流程已操作；LUT/色彩过渡等未逐个测试。 [014](evidence/014-r1-after.png) [015](evidence/015-r1-after.png) [016](evidence/016-r1-after.png) [078](evidence/078-r1-after.png) [085](evidence/085-r1-after.png) | PARTIAL |
| RT03.9 | 七页来回切换、组展开折叠、窗口收窄后返回；保留当前参数和合理滚动位置，无空白假页 | 7页反复切换内容真实变化，父组收展可操作；窄窗仍CU-01，不宣称所有页所有窗口通过。 [011](evidence/011-r1-after.png) [012](evidence/012-r1-after.png) [013](evidence/013-r1-after.png) [014](evidence/014-r1-after.png) [015](evidence/015-r1-after.png) [016](evidence/016-r1-after.png) [112](evidence/112-r1-after.png) | PARTIAL |

## RT04

| 子项 | 检查点 | 本轮实际结果、限制与证据 | RELEASE_RUNTIME |
|---|---|---|---|
| RT04.1 | 冷暖和绿/洋红分别调整（例如±20），双击复位及数值输入；一项变化不改另一项数值，灰区/肤色变化可解释 | ChartPNG冷暖0→27.056→精确输入-25 Enter→undo27.056→redo-25→组复位0；灰白冷暖、双图同变。绿洋红-26.525也实测。双击复位、全部正负符号/肤色未全测。 [017](evidence/017-r1-before.png) [018](evidence/018-r1-after.png) [019](evidence/019-r1-after.png) [020](evidence/020-r1-after.png) [021](evidence/021-r1-after.png) [022](evidence/022-r1-after.png) | PARTIAL |
| RT04.2 | 曝光（例如±0.5EV）、亮度、对比分别调整；区分全图增益和中间调变化，检查黑白端是否突跳 | 曝光+1.3/-1.326EV、亮度27.056、对比38.727实际拖动。灰阶/双图可见响应；全边界与所有内容未验。 [022](evidence/022-r1-after.png) [025](evidence/025-r1-after.png) [034](evidence/034-r1-after.png) | PARTIAL |
| RT04.3 | HDR高光、阴影、白场、黑场逐个操作并复位；观察天空/暗部/端点及与曝光的区别 | 从HDR中性依次高光-50.928、阴影49.867、白场-26.525、黑场25.995；纯黑白端保持、附近亮度变化；复位四项0。仅合成图此次顺序/数值，不称传感器高光恢复。 [029](evidence/029-r1-before.png) [030](evidence/030-r1-after.png) [033](evidence/033-r1-after.png) [034](evidence/034-r1-after.png) | PASS |
| RT04.4 | 全局饱和度/自然饱和度分别操作；灰区保持中性，低/高饱和区变化与参数含义一致 | 尝试滚动到全局颜色未完成两个滑块的独立输入/输出；不把尝试滚动算调节通过。  | NOT_RUN |
| RT04.5 | 每组复位、单节点启停、一次完整拖动的undo/redo；数值、照片、缩略图和统计同版本恢复 | WB完整undo/redo、HDR组复位、范围节点禁用恢复图像。未逐组证明一次拖动历史粒度和所有缩略图最终值。 [019](evidence/019-r1-after.png) [020](evidence/020-r1-after.png) [021](evidence/021-r1-after.png) [022](evidence/022-r1-after.png) [034](evidence/034-r1-after.png) [072](evidence/072-r1-after.png) | PARTIAL |
| RT04.6 | 输入边界和非数字（例如abc），按Enter/Esc；无效值拒绝/提示，旧值与图像一致，不写入NaN | 曝光abc Enter拒绝，图像数值未提交；重新聚焦后Esc却返回Home，回Studio仍见警告。CU-05；原301误记Home撤回，实际Home为303。 [299](evidence/299-r1-after.png) [300](evidence/300-r1-after.png) [302](evidence/302-r1-after.png) [303](evidence/303-r1-after.png) [304](evidence/304-r1-after.png) | FAIL |
| RT04.7 | 快速连续拖动再立即切图；最终只显示最新目标最新值，旧图结果不覆盖；耗时见RT10 | 多次拖动/切图最终可操作；未执行严格快速交错、版本断言、时间线，不能判异步竞争通过。 [336](evidence/336-r1-after.png) [337](evidence/337-r1-after.png) [340](evidence/340-r1-after.png) [342](evidence/342-r1-after.png) | PARTIAL |
| RT04.8 | 同一组参数在JPEG和RAW各重放基础范围；明确两种输入合同，不以JPEG通过覆盖RAW | R01实际曝光约1.32和精确0.5EV；RAW原图/结果可见亮度变化、Source→TIFF成功。与JPEG完整同参数矩阵未做。  | PARTIAL |

## RT05

| 子项 | 检查点 | 本轮实际结果、限制与证据 | RELEASE_RUNTIME |
|---|---|---|---|
| RT05.1 | RGB色阶分别操作输入黑/白、中间gamma、输出黑/白；复位后0/1/1/0/1；记录暗亮区变化 | RGB黑0.102、白0.898、gamma1.52，各产生可见变化；输出黑/白未操作。颜色渐变边界需独立判断裁切预期，不凭截图归因。 [035](evidence/035-r1-before.png) [038](evidence/038-r1-after.png) | PARTIAL |
| RT05.2 | R/G/B色阶逐通道调整，未编辑通道参数不改变；组复位不重置其他组 | R/G/B输入黑分别0.15，灰区依次偏青/洋红/黄，其他参数保持，复位。未逐通道全部输入/输出端点。 [039](evidence/039-r1-after.png) [040](evidence/040-r1-after.png) [041](evidence/041-r1-after.png) | PARTIAL |
| RT05.3 | 输入黑≥白、输出黑>白的非法组合应明确拒绝或约束；数值/像素保持一致 | 输入黑≥白、输出黑>白非法组合未完成。  | NOT_RUN |
| RT05.4 | RGB曲线新增点、拖动、读取input/output；删除内点，端点限制合理，Esc取消本次拖动 | RGB点击增加(.369,.532)、拖(.369,.332)、删除内点后回直线；端点拖动046命中不确认；Esc取消拖动未测。 [013](evidence/013-r1-after.png) [043](evidence/043-r1-after.png) [044](evidence/044-r1-after.png) [045](evidence/045-r1-after.png) | PARTIAL |
| RT05.5 | R/G/B曲线逐一编辑并复位；不把RGB曲线当作尚未实现的Luma曲线 | R中点(.499,.651)并复位(047/048本机)；G(.497,.658)灰阶变绿并复位；B(.497,.349)灰阶变黄并复位，照片/统计终帧恢复。只限定这些真实操作，不外推所有曲线形状。 [335](evidence/335-r1-after.png) [336](evidence/336-r1-after.png) [337](evidence/337-r1-after.png) [339](evidence/339-r1-after.png) [340](evidence/340-r1-after.png) [342](evidence/342-r1-after.png) | PASS |
| RT05.6 | 曲线非单调形状、连续拖动、undo/redo和节点启停；无控制点丢失或旧帧覆盖，保存重开见RT13 | 非单调曲线、连续拖动历史、节点启停和曲线重启专项未完成。  | NOT_RUN |

## RT06

| 子项 | 检查点 | 本轮实际结果、限制与证据 | RELEASE_RUNTIME |
|---|---|---|---|
| RT06.1 | 基础颜色范围→当前照片取色，检查选区与真实颜色一致；取样前后数值/节点身份记录 | 红基础范围创建真实节点；照片红取色与球面对应实测。范围节点的每种取色入口归属未逐个验。 [055](evidence/055-r1-after.png) [069](evidence/069-r1-before.png) [070](evidence/070-r1-after.png) | PARTIAL |
| RT06.2 | 高级范围增/减样本、范围半径与羽化、小幅H/S/L调整；同色背景命中如实记录，非硬边 | 红范围12/羽化8→29、hue49；红及邻棕变化，纯绿蓝灰保持；禁用恢复。增减样本、半径、S/L全参数未全测。071收敛帧与过渡帧区分，不据瞬态认定终态串色。 [069](evidence/069-r1-before.png) [070](evidence/070-r1-after.png) [072](evidence/072-r1-after.png) | PARTIAL |
| RT06.3 | 仅显示选区、关闭、离开/切图/Esc；观察状态不写编辑栈，导出隔离交RT12 | 照片/球面临时层Esc清除、alpha观察输出同字节；切节点、离开及迟到事件全组合未验。 [055](evidence/055-r1-after.png) [056](evidence/056-r1-after.png) [057](evidence/057-r1-after.png) [206](evidence/206-r1-after.png) [214](evidence/214-r1-after.png) | PARTIAL |
| RT06.4 | 肤色目标取样，色相/饱和度/明度均匀化分别调整；灰区/天空保护及同色背景影响检查 | 合成棕色块：肤色色相49.603、chroma31.282、luma30.769并复位(074/309/311/312本机)；灰/绿/蓝保持。并非真人肤色/头发效果批准，肤色取样和同色背景未全测。  | PARTIAL |
| RT06.5 | 色彩平衡全局/阴影/中间调/高光各设方向和小强度；各组作用域、过渡和零位检查 | 全局hue0/27.381；阴影20.769、中间20.149、高光20.149，暗/中/亮灰分带红偏各可见，逐组复位。其他方向/过渡范围未测。 [317](evidence/317-r1-after.png) [322](evidence/322-r1-after.png) [326](evidence/326-r1-after.png) | PARTIAL |
| RT06.6 | 上述节点各自启停、复位和undo/redo；切换节点再取色，样本归正确节点，Esc后无迟到样本 | 范围启停及肤色/平衡组复位已操作；逐节点undo/redo和切节点迟到取样未完成。 [072](evidence/072-r1-after.png) [317](evidence/317-r1-after.png) [322](evidence/322-r1-after.png) [326](evidence/326-r1-after.png) | PARTIAL |

## RT07

| 子项 | 检查点 | 本轮实际结果、限制与证据 | RELEASE_RUNTIME |
|---|---|---|---|
| RT07.1 | 清晰度和结构分别正/负值；100%与Fit观察边缘/肤色/细纹；复位恢复，记录完成耗时 | DetailPNG清晰度43.846、结构35.385局部边缘/纹理对比增强；观察halo，未武断当故障；负值/真人肤发/完整计时未验。 [077](evidence/077-r1-before.png) [078](evidence/078-r1-after.png) | PARTIAL |
| RT07.2 | 锐化强度/半径/阈值逐一作用，观察边缘光晕和噪声，不能与降噪混称 | 锐化29.231、半径1/阈值.01，100%与归0对比；半径/阈值独立扫描未做。 [081](evidence/081-r1-after.png) | PARTIAL |
| RT07.3 | 亮度/颜色降噪分别调整；暗部噪声和细节保留，检查与清晰度先后顺序、取消 | 亮度NR49.603可见噪声减少、棋盘变软，大边保留；颜色NR/先后顺序/取消未验。 [081](evidence/081-r1-after.png) [083](evidence/083-r1-after.png) | PARTIAL |
| RT07.4 | 左Film Preset浏览应用→右Film参数对应同一状态；Profile强度、grain/size、halation/bloom/vignette逐项 | 左Warm应用→右Film启用100→45；grain23；冷银灰、halation29.55、bloom/vignette30.30也拖动；未逐个所有符号/尺度与真人副作用。 [084](evidence/084-r1-before.png) [085](evidence/085-r1-after.png) [086](evidence/086-r1-after.png) | PARTIAL |
| RT07.5 | 既有surface/texture及seed/re-roll（仅已有入口）响应；同seed复现、变化仅当前节点，无新增路线开发 | FineFiber/SurfaceOpacity约30和Regenerate实际点击；同seed重复像素及节点隔离未完整量测。  | PARTIAL |
| RT07.6 | Film启停、复位、undo/redo、旧胶片快照及未激活目标输出；空间proxy差异不等于算法无效 | Film启停与非活动目标批量输出有操作；旧快照迁移/各参数undo/redo及空间质量完整覆盖未完成。 [092](evidence/092-r1-before.png) | PARTIAL |
| RT07.7 | Creative页当前已有真实节点逐项小值/复位，缺失能力只记录，不以同名按钮算实现 | Creative真实打开、发布配方UI输出并重新导入(159–166、232–234本机)。色彩过渡、保存色彩资产/LUT未逐项执行。 [016](evidence/016-r1-after.png) | PARTIAL |

## RT08

| 子项 | 检查点 | 本轮实际结果、限制与证据 | RELEASE_RUNTIME |
|---|---|---|---|
| RT08.1 | 小视图与展开视图分别Orbit、Pan、wheel Zoom；Reset/Fit，Fit包住当前云，非固定倍率假Fit | mini Orbit双向、wheel、Reset；expanded Orbit、Pan开关+83/+60、wheel-240、Fit重居中包住当前云。mini Pan、所有Reset/各形态bounds量测未全验。 [049](evidence/049-r1-before.png) [050](evidence/050-r1-after.png) [053](evidence/053-r1-after.png) [105](evidence/105-r1-before.png) [106](evidence/106-r1-after.png) [107](evidence/107-r1-after.png) | PARTIAL |
| RT08.2 | 核对OKLab真实L/a/b与球形sRGB色域归一显示说明；轴/图例可读，浅蓝到蓝过渡连续 | 实际球面平滑渐变，OKLab/sRGB色域归一化说明可见，L/a/b轴随旋转；小视图文字细小，未做屏幕色值定量与超色域专项。 [049](evidence/049-r1-before.png) [050](evidence/050-r1-after.png) [053](evidence/053-r1-after.png) | PARTIAL |
| RT08.3 | 点云/球面组合模式、背景、点尺寸、点/表面透明度、网格、轴、色域边界逐一开关 | 实际原始点云/球面模式、背景中灰、表面.17、grid/axis/gamut、点6/alpha19%、density变化。每种状态和全范围未全验。 [049](evidence/049-r1-before.png) [053](evidence/053-r1-after.png) | PARTIAL |
| RT08.4 | 右3D页色彩空间/表面模式、色度范围、L切片/厚度、XYZ旋转各产生可见对应变化 | 色度上限1→.5样本3989→146；L中心.5/.77厚.25：3989→1387→1022；XYZ实际旋转。Space未提供自由工作空间切换，未把缺控件冒作支持。 [011](evidence/011-r1-after.png) | PARTIAL |
| RT08.5 | 小/展开视图共享设置；展开关闭后主图继续为主体，无覆盖、相机/设置突然重置 | expanded独立窗口遮主图右侧与编辑栏；其Orbit/Pan不回写mini相机。CU-02。显示设置共享不能覆盖遮挡和独立相机问题。 [105](evidence/105-r1-before.png) [106](evidence/106-r1-after.png) [107](evidence/107-r1-after.png) | FAIL |
| RT08.6 | 所有仅观察操作前后调整栈/照片结果保持；空图、灰阶、近黑白和透明测试图按输入类型记录 | 观察动作主图不变；alpha overlay导出同字节；空态/黑白色块已看。所有灰阶/近黑白/超色域及大图性能未专项全验。 [049](evidence/049-r1-before.png) [050](evidence/050-r1-after.png) [053](evidence/053-r1-after.png) [206](evidence/206-r1-after.png) [214](evidence/214-r1-after.png) | PARTIAL |

## RT09

| 子项 | 检查点 | 本轮实际结果、限制与证据 | RELEASE_RUNTIME |
|---|---|---|---|
| RT09.1 | 当前照片吸管采样天空/肤色/暗部；真实样本对应球面点/簇高亮，记录OKLab及容差显示 | ChartPNG红取色→真实红样本青圈和主图红块overlay；有颜色关联。自然天空/肤色/暗部及数值距离读数未全验。 [049](evidence/049-r1-before.png) [055](evidence/055-r1-after.png) | PARTIAL |
| RT09.2 | 球面选色/簇→照片临时overlay；调容差和softness，边界平滑，旋转后拾取仍对应 | 球面洋红点→仅主图洋红块临时层；旋转后alpha图另一色簇也能拾取。容差/softness独立连续调节未完成。 [056](evidence/056-r1-after.png) [057](evidence/057-r1-after.png) [214](evidence/214-r1-after.png) | PARTIAL |
| RT09.3 | 主图缩放/平移后取样、原图/处理结果及对比模式切换；overlay和云随当前阶段更新，不错位 | Fit与100%已操作；全缩放/平移+原图/结果/对比组合的映射定位未测。 [081](evidence/081-r1-after.png) | PARTIAL |
| RT09.4 | Esc、关闭overlay、离开hover与切图各清除相应临时状态，等待后台结束不重现旧选区 | 图片取色/影调Esc可清；切图时当前云更新。离开hover/关闭/迟到任务所有排列未验；数值框Esc另CU-05。 [055](evidence/055-r1-after.png) [056](evidence/056-r1-after.png) [065](evidence/065-r1-after.png) [067](evidence/067-r1-after.png) | PARTIAL |
| RT09.5 | 展开线性Y 0–X条图，逐区hover/离开；检查色块、占比与照片相应像素，黑/白端区分别测试 | 线性Y0–X内滚动全可达，VII/0 hover主图对应亮区/黑块overlay，Esc清。CU-06发现性差；不是摄影曝光Zone。每区/白端未逐个验。 [063](evidence/063-r1-after.png) [065](evidence/065-r1-after.png) [066](evidence/066-r1-after.png) [067](evidence/067-r1-after.png) | PARTIAL |
| RT09.6 | RGB叠加/R/G/B切换、明度图及hover读数；来源标签与当前图/原图结果一致，alpha0不计 | UI实际RGB→R→G→B→亮度→RGB(254–260本机)，明度读数261；双图来源标签随处理变化；alpha图统计可见但未定量排除验证。 [017](evidence/017-r1-before.png) [018](evidence/018-r1-after.png) [206](evidence/206-r1-after.png) | PARTIAL |
| RT09.7 | 快速调参数/undo/同步/切图，等待终帧，云/双图/影调同一版本；统计“线性Y”等分不冒称曝光Zone | 参数/undo/同步/切图后终帧双图与照片更新；严格revision竞态/跨阶段切换没有完整量测。 [018](evidence/018-r1-after.png) [020](evidence/020-r1-after.png) [021](evidence/021-r1-after.png) [336](evidence/336-r1-after.png) [342](evidence/342-r1-after.png) | PARTIAL |

## RT10

| 子项 | 检查点 | 本轮实际结果、限制与证据 | RELEASE_RUNTIME |
|---|---|---|---|
| RT10.1 | JPEG/RAW各Fit→100%→缩放→Pan→Fit；记录真实倍率/尺寸，单一Zoom驱动全部显示 | 合成图Fit54%→100%裁中间，参考图100/Pan/Fit另已操作；RAW100细节可见。主图Space/中键保持拖动工具不支持，Pan未可靠完成。旧“100%物理不正确”撤回：仅源码DIP线索，无物理测量。 [061](evidence/061-r1-after.png) [081](evidence/081-r1-after.png) | PARTIAL |
| RT10.2 | RAW100%按需全尺寸细节，记录源尺寸、加载/处理耗时及完成状态；不同图像方向不二次旋转 | R01实测100全细节加载完成，源7028×4688、横向正确；代理分析仍768。没有可靠开始/结束时点和多EXIF方向重放。  | PARTIAL |
| RT10.3 | RAW复杂栈开始后Stop，等待旧任务退场；busy清除、UI可操作、无迟到detail覆盖 | 119 busy与Stop→120完成；点击时任务可能已经完成，不能证明取消及迟到隔离。此前许可等待不计产品失败。  | PARTIAL |
| RT10.4 | 缓存图A→B→A再100%；缓存复用及相同当前结果，旧图不覆盖新图 | A→B→A100%缓存命中与重复解码/版本量测未完成。  | NOT_RUN |
| RT10.5 | 持续拖动/平移至少记录一段有开始结束时间的运行；响应、取消和内存观察真实量测，不填估算PASS | 长时间连续拖动/平移、响应/内存专项量测未完成。  | NOT_RUN |
| RT10.6 | V4不支持的完整工具栈组合若进入检查，必须明确提示并保留参数；不自动换引擎或丢节点 | V4不支持工具组合UI提示与数据保留本轮未操作；沿用声明边界不升级PASS。  | NOT_RUN |

## RT11

| 子项 | 检查点 | 本轮实际结果、限制与证据 | RELEASE_RUNTIME |
|---|---|---|---|
| RT11.1 | 逐张点击、方向键切图，当前Active与Selected不同视觉可辨；横向滚动后仍有正确目标 | 实际逐张点击、Ctrl+Right当前与Selected分开、Shift范围；横滚和全部键盘边缘未验。 [061](evidence/061-r1-after.png) [089](evidence/089-r1-after.png) | PARTIAL |
| RT11.2 | Ctrl增减、Shift范围、Ctrl+Shift及Ctrl+A；过滤后范围按可见序列，未错误清空隐藏选择 | Ctrl+Right、Shift+Right选3、Ctrl+A9/9；Ctrl+Shift和过滤后全范围未验。 [089](evidence/089-r1-after.png) [100](evidence/100-r1-before.png) | PARTIAL |
| RT11.3 | All/Selected/RAW/JPEG/TIFF/PNG范围、最低评分、色标分别过滤；总数/可见/已选数与列表一致 | 红0/9明确隐藏已选1，导出/同步禁用；清筛恢复；新red2/10重启保留。所有格式范围、最低评分组合未全测。 [104](evidence/104-r1-after.png) [250](evidence/250-r1-after.png) | PARTIAL |
| RT11.4 | 批量评分/色标只作用可见所选；被筛掉目标不误操作，清筛选恢复其选择；不改原片像素 | 右键5星前三张、其他保持0；红色标2图过滤与重开。隐藏选择不参与本次操作，原像素没有编辑动作；全批元数据边界未全测。 [089](evidence/089-r1-after.png) [091](evidence/091-r1-after.png) [104](evidence/104-r1-after.png) [250](evidence/250-r1-after.png) | PARTIAL |
| RT11.5 | 已关联素材的评分/色标在Inspector与批次共用状态；普通文件与DB素材持久化来源区分 | 本轮使用隔离普通文件批次，关联素材DB/Inspector共同状态未验证。  | NOT_RUN |
| RT11.6 | 当前源→可见所选，核对源图/目标数量/类别；只同步选择的调整，不拷评分、色标、文件名、EXIF或关系 | 源为所选9图之一：节点同步弹层写9，执行toast实际8，且弹层无源名，CU-03；只Range同步/源排除与评分保留已看，不以功能成功遮提示错误。 [100](evidence/100-r1-before.png) [101](evidence/101-r1-after.png) | FAIL |
| RT11.7 | 按节点/类别、多个同类节点同步；目标原有其他节点保留，顺序及内容正确，无重复ID异常 | Range-only同步8目标，以及3类别完整同步8目标，原rating5保持；多同类节点合并与ID全组合未验。 [101](evidence/101-r1-after.png) | PARTIAL |
| RT11.8 | 同步后批量undo/redo；A/B分别编辑、切图再undo不串历史；缩略图最终追上各自调整 | More批量undo126及redo后续成功；103 Ctrl+Z属于当前图历史，不当作批量撤销失败。跨目标历史/缩略图终态专项未全验。  | PARTIAL |
| RT11.9 | 右键单张/多选作用域、禁用反馈、菜单分组/子菜单边界；移出只清测试批次且确认范围 | 实际多选右键评分及子菜单键盘进入，分隔线未外溢；popup超主窗但仍屏内。不完整popup截图留本机；移出/禁用全部组合未测。 [091](evidence/091-r1-after.png) | PARTIAL |

## RT12

| 子项 | 检查点 | 本轮实际结果、限制与证据 | RELEASE_RUNTIME |
|---|---|---|---|
| RT12.1 | 顶部快速导出与底部导出所选使用同可见选择；选目录前明确数量/格式/路径，取消不写文件 | 顶部实际选择目录输出；过滤0可见选择时入口禁用。底部同流程、取消无文件/目录前信息完整性未逐一重放。 [092](evidence/092-r1-before.png) [093](evidence/093-r1-after.png) [104](evidence/104-r1-after.png) | PARTIAL |
| RT12.2 | Source默认：JPEG/PNG/TIFF沿可支持源格式，RAW默认TIFF16；显式切JPEG/PNG/TIFF各实际导出 | UI显式PNG/JPEG/TIFF均输出并重开；R01 Source实际TIFF16。所有源格式默认与位深档位未全验。 [093](evidence/093-r1-after.png) [094](evidence/094-r1-after.png) [095](evidence/095-r1-after.png) [096](evidence/096-r1-after.png) [097](evidence/097-r1-after.png) [098](evidence/098-r1-after.png) | PARTIAL |
| RT12.3 | UI导出前保存同一冻结工作文件/参数，记录source/target代号、格式、输出ID；禁止用自检生成文件冒充UI产物 | UI保存冻结工作文件→真实导出→取实际产物做5份比较，smokeCreatedActual=false；输入/session hash未变；UI操作与程序比较严格分开。 [092](evidence/092-r1-before.png) [093](evidence/093-r1-after.png) [094](evidence/094-r1-after.png) [095](evidence/095-r1-after.png) [206](evidence/206-r1-after.png) | PASS |
| RT12.4 | 对真正UI文件核对容器/尺寸/位深/alpha/ICC/方向；PNG/TIFF无损同链，JPEG与独立同质量编码oracle比对 | UI重新打开JPEG/PNG/TIFF与R01派生TIFF，核容器/尺寸/位深/alpha/ICC/orientation；PNG/TIFF/alpha同链mean/max0；JPEG独立quality95 oracle0，有损对未编码mean.350609/max136。限定输入/栈，见EXPORT_COMPARISON。 [096](evidence/096-r1-after.png) [097](evidence/097-r1-after.png) [098](evidence/098-r1-after.png) [206](evidence/206-r1-after.png) | PASS |
| RT12.5 | 输出过程中改当前图/参数/格式；此批使用最初冻结集，目标不串图，源文件前后hash一致 | 导出进行中改图/参数/格式的冻结竞态没有实际重放。  | NOT_RUN |
| RT12.6 | 取消批量、逐张失败、重试；已完成保留、未完成临时文件清理，重试格式沿原冻结值，不覆盖用户文件 | 取消批次/逐张失败/重试/临时文件清理本轮未操作。  | NOT_RUN |
| RT12.7 | overlay开启/关闭分别输出同栈，图像结果不包含观察高亮；只旋转球体不改变输出 | AlphaPNG正常/overlay两次UI输出，观察层照片可见；两文件SHA完全相同，不进入像素。旋转只观察在RT08另有限定观察。 [206](evidence/206-r1-after.png) [214](evidence/214-r1-after.png) | PASS |
| RT12.8 | 发布配方进入独立流程，只有已启用配方覆盖手动设置；实际输出结果/数量与快速输出职责区分 | Creative发布配方实际JPEG产出并UI重新打开(159–166、232–234私有)。全部启用/覆盖规则与各种配方未验。 [016](evidence/016-r1-after.png) | PARTIAL |

## RT13

| 子项 | 检查点 | 本轮实际结果、限制与证据 | RELEASE_RUNTIME |
|---|---|---|---|
| RT13.1 | 保存本轮新工作文件：多目标、非零调整、选择/当前图、过滤；记录本地文件hash与保存时点 | 本轮UI新存9目标和10目标非零节点/评分/红过滤/2选文件；后续比对hash记录，非只读取旧工作文件。 [111](evidence/111-r1-after.png) [250](evidence/250-r1-after.png) | PASS |
| RT13.2 | 正常关闭应用，重新启动同r1核对新会话身份，打开所存工作文件；不以同进程Load代替重启 | 实际退出旧进程再以同r1启动s03/s07，UI重新打开保存文件。不是同进程Load替代。 [111](evidence/111-r1-after.png) [250](evidence/250-r1-after.png) | PASS |
| RT13.3 | 逐图检查Look/Film/节点顺序/启停/参数、选中和过滤，重开不重新解释旧版本参数 | 10目标/选2/red过滤、前三5星、重命名/顺序与非零范围恢复；重新保存hash完全相同。每图Film/全部节点视觉未全验。 [111](evidence/111-r1-after.png) [250](evidence/250-r1-after.png) | PARTIAL |
| RT13.4 | 素材库评分/色标仍以同DB状态恢复，普通文件标记按工作文件；原件hash不变 | 普通文件工作文件rating/color恢复；关联素材DB未验，不能外推Inspector。原件比较hash不变。 [250](evidence/250-r1-after.png) | PARTIAL |
| RT13.5 | 重开后的相同参数导出与重启前对照；undo日志仅进程内，明确不将未保存跨进程日志当功能承诺 | 重开文件规范化JSON与字节hash完全相同；未做同栈跨重启前后两次UI导出专项。撤销日志不承诺跨进程保存。 [250](evidence/250-r1-after.png) | PARTIAL |
| RT13.6 | 无效版本/文件与缺失源副本：失败不替换当前批次、不丢保存调整；保留可理解错误 | 无效版本/缺失源副本失败现场保护未操作。  | NOT_RUN |

## RT14

| 子项 | 检查点 | 本轮实际结果、限制与证据 | RELEASE_RUNTIME |
|---|---|---|---|
| RT14.1 | 分别1180×720、1600×920、1920×1080（实际桌面可支持时）；记录窗口物理尺寸及可用工作区，截八区全图 | 实际1600×920DIP(100/125/150%)、1180×720DIP(125/200%)、1920×1080DIP(150%)均打开；三档均有全窗证据；不是完整3×4组合。 [061](evidence/061-r1-after.png) [111](evidence/111-r1-after.png) [112](evidence/112-r1-after.png) [123](evidence/123-r1-after.png) [332](evidence/332-r1-after.png) | PARTIAL |
| RT14.2 | Windows100/125/150/200%逐档实读后检查关键按钮/文字/工具与照片面积；不可改变或不可用档位写BLOCKED，不用WPF模拟替代 | Windows实际100/125/150/200%且新进程DPI96/120/144/192；1180×720DIP右栏数字/复位/Creative被裁，CU-01。s01早期设置100与进程144不同，已纠正。 [061](evidence/061-r1-after.png) [111](evidence/111-r1-after.png) [112](evidence/112-r1-after.png) [123](evidence/123-r1-after.png) [332](evidence/332-r1-after.png) | FAIL |
| RT14.3 | 简中→英文→繁中→简中：左四模式、右七页、新参数、曲线、3D、分析、格式文本更新；长文不截断 | 简中→英文→繁中→简中实际切换；阶段/分析来源/底栏摘要/旧帮助仍简中，CU-04，窄窗还CU-01。用户节点名应保留，未把它算翻译缺陷。 [114](evidence/114-r1-after.png) [115](evidence/115-r1-after.png) | FAIL |
| RT14.4 | 语言切换不改文件名/用户节点名/参数key/数值解析/当前栈；重启语言设置恢复；旧中文硬编码如实记范围 | 数值和栈在语言切换未见变化；节点用户中文英文名按预期保留。语言设置重启恢复专项未做。 [114](evidence/114-r1-after.png) [115](evidence/115-r1-after.png) | PARTIAL |
| RT14.5 | 顶栏/格式/右键/子菜单在边缘和窄窗打开；不覆盖父项、可滚动、键盘与Esc关闭、无亮白错误主题 | 格式菜单、曲线/直方图下拉、节点menu、胶片右键实际打开；屏幕四角全组合/全部快捷键未测。数值Esc另CU-05。 [100](evidence/100-r1-before.png) [335](evidence/335-r1-after.png) [339](evidence/339-r1-after.png) | PARTIAL |
| RT14.6 | 键盘焦点/Tab/Enter/Esc和数值字段同时检查；Esc清临时层不误退出或丢当前编辑 | 数值框abc Enter拒绝但再次聚焦Esc返回Home，CU-05；临时影调/云Esc能清不覆盖此问题。 [299](evidence/299-r1-after.png) [300](evidence/300-r1-after.png) [302](evidence/302-r1-after.png) [303](evidence/303-r1-after.png) [304](evidence/304-r1-after.png) | FAIL |

## CODE / TEST / RELEASE_RUNTIME / USER_VISUAL_REVIEW

| 列 | 本轮解释 |
|---|---|
| CODE | r1现有实现，巡检未修改生产；CU-01–06保留缺陷，不能记全项完成 |
| TEST | 本轮5份UI实际产物像素/格式比对及1份重开文件比较；历史Core/WPF未重跑，不能用旧计数升级状态 |
| RELEASE_RUNTIME | 5项FAIL、9项PARTIAL；子项各自保留已做、未做、失败；没有全项PASS |
| USER_VISUAL_REVIEW | NOT_APPROVED；VisualApproved=false；UserVerified=false |

本轮结论 **NOT_READY_FOR_USER_RETEST**。入口巡检：Home↔Studio和隔离空素材库↔Studio未观察到崩溃；329/330几何变化且捕获不完整，留本机，不作为全软件视觉通过。关联DB素材、不同照片的所有入口和其它模块仍未经完整验收。
