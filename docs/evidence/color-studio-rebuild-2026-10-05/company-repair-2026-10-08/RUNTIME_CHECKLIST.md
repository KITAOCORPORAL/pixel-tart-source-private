# Company Release runtime checklist — 95 original checks

Release SourceHead: `1a9a53913665490298e6de1b5656a33d0efc934b`.

Company acceptance: **1 PASS, 94 FAIL, 0 BLOCKED**. FAIL includes NOT VERIFIED; it is not a claim that all 94 behaviors malfunction. No home PASS was copied. Home historical distribution remains 15 PASS / 7 FAIL / 61 PARTIAL / 12 NOT_RUN. The six-issue native retest prerequisite was not completed, so the requested 73 missing-step follow-ups are still outstanding.

Computer Use capture/input failures are recorded separately in REPAIR_MATRIX. Code gaps are not relabeled environment blockers. VisualApproved=false; UserVerified=false.

| ID | Original check (retained) | Home historical status | Company status | Company observation / missing step |
|---|---|---|---|---|
| RT01.1 | 校验EXE/DLL/ProductVersion/manifest后启动r1；记录新会话与窗口，确认没有启动异常 | PASS | PASS | New production EXE started as PID 45896; SourceHead, version, EXE/DLL hashes and 287-file manifest recorded; STARTUP_OK, actual window capture screenshots/01-company-release-start.png. |
| RT01.2 | 由一级导航进入参考仿色/Color Studio；核对标题、选中导航、空目标与空参考提示，空直方图不误判缓存故障 | PASS | FAIL | Studio empty state observed in new process, no target/no histogram and original-read-only prompt. Navigation was not reproducibly performed by agent; selected-navigation + full entry cycle not verified. screenshots/02-company-studio-observed.png. |
| RT01.3 | 导入至少2张JPEG和1张RAW；逐张确认图像内容、方向及当前目标身份，记录失败提示而非只看缩略图出现 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT01.4 | 由本次会话打开既有测试批次；确认数量、当前图、选择、调整恢复；与RT13新保存重启分开 | PASS | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PASS is historical only. |
| RT01.5 | 在受控工作文件副本验证缺失源提示和记录保留，不移动用户原件；取消导入/打开不清空当前现场 | NOT_RUN | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home NOT_RUN is historical only. |
| RT02.1 | 1600×920取得全窗；逐区对照原图八区域，照片完整、3D在左、两图在右顶，底部紧凑 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT02.2 | 收展全局导航、左辅助栏、右编辑栏；记录中央照片Fit/矩形是否合理重算，无遮挡、跳离可用视口 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT02.3 | 展开/折叠右上分析组；确认直方图不移回照片上方，中央照片不被遮挡，右侧工具空间返回 | PASS | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PASS is historical only. |
| RT02.4 | 左右独立滚动至底部、返回顶部；关键操作始终可达，滚动不误调主图或邻栏 | FAIL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home FAIL is historical only. |
| RT02.5 | 收展胶片栏并恢复；观察返还的主图空间、当前选择及可见目标不丢失 | NOT_RUN | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home NOT_RUN is historical only. |
| RT02.6 | 开始/结束一次预览计算；处理提示和取消按钮出现时主图不突跳，完成恢复正常状态 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT03.1 | 左侧参考：载入测试参考，来源仅必要一份，Fit/100%/Zoom/Pan有效；参考图只读，目标参数不意外变化 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT03.2 | 左侧3D：模式切入实际球面/样本内容，当前选中态明确；离开返回不产生第二套编辑状态 | PASS | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PASS is historical only. |
| RT03.3 | 左侧胶片预设：浏览、hover/离开、应用及取消；与右Film当前参数职责区分，临时预览不永久提交 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT03.4 | 左侧节点：选择、启停、重命名、重排、复制/删除及undo/redo；右侧参数跟随正确节点 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT03.5 | 右3D页：打开有实际观察参数内容，修改只影响查看；全部显示控制详测RT08 | PASS | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PASS is historical only. |
| RT03.6 | 右色彩页：白平衡、基础/HDR、范围/肤色、平衡父子组可折叠，数值与同节点一致 | PASS | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PASS is historical only. |
| RT03.7 | 右色阶页：RGB/R/G/B父子组可达；右曲线页：真实控制点编辑器可达 | PASS | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PASS is historical only. |
| RT03.8 | 右细节页：清晰度/锐化/降噪；右Film页：当前胶片参数；右Creative页：实际既有能力，缺能力不计假入口 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT03.9 | 七页来回切换、组展开折叠、窗口收窄后返回；保留当前参数和合理滚动位置，无空白假页 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT04.1 | 冷暖和绿/洋红分别调整（例如±20），双击复位及数值输入；一项变化不改另一项数值，灰区/肤色变化可解释 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT04.2 | 曝光（例如±0.5EV）、亮度、对比分别调整；区分全图增益和中间调变化，检查黑白端是否突跳 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT04.3 | HDR高光、阴影、白场、黑场逐个操作并复位；观察天空/暗部/端点及与曝光的区别 | PASS | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PASS is historical only. |
| RT04.4 | 全局饱和度/自然饱和度分别操作；灰区保持中性，低/高饱和区变化与参数含义一致 | NOT_RUN | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home NOT_RUN is historical only. |
| RT04.5 | 每组复位、单节点启停、一次完整拖动的undo/redo；数值、照片、缩略图和统计同版本恢复 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT04.6 | 输入边界和非数字（例如abc），按Enter/Esc；无效值拒绝/提示，旧值与图像一致，不写入NaN | FAIL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home FAIL is historical only. |
| RT04.7 | 快速连续拖动再立即切图；最终只显示最新目标最新值，旧图结果不覆盖；耗时见RT10 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT04.8 | 同一组参数在JPEG和RAW各重放基础范围；明确两种输入合同，不以JPEG通过覆盖RAW | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT05.1 | RGB色阶分别操作输入黑/白、中间gamma、输出黑/白；复位后0/1/1/0/1；记录暗亮区变化 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT05.2 | R/G/B色阶逐通道调整，未编辑通道参数不改变；组复位不重置其他组 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT05.3 | 输入黑≥白、输出黑>白的非法组合应明确拒绝或约束；数值/像素保持一致 | NOT_RUN | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home NOT_RUN is historical only. |
| RT05.4 | RGB曲线新增点、拖动、读取input/output；删除内点，端点限制合理，Esc取消本次拖动 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT05.5 | R/G/B曲线逐一编辑并复位；不把RGB曲线当作尚未实现的Luma曲线 | PASS | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PASS is historical only. |
| RT05.6 | 曲线非单调形状、连续拖动、undo/redo和节点启停；无控制点丢失或旧帧覆盖，保存重开见RT13 | NOT_RUN | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home NOT_RUN is historical only. |
| RT06.1 | 基础颜色范围→当前照片取色，检查选区与真实颜色一致；取样前后数值/节点身份记录 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT06.2 | 高级范围增/减样本、范围半径与羽化、小幅H/S/L调整；同色背景命中如实记录，非硬边 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT06.3 | 仅显示选区、关闭、离开/切图/Esc；观察状态不写编辑栈，导出隔离交RT12 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT06.4 | 肤色目标取样，色相/饱和度/明度均匀化分别调整；灰区/天空保护及同色背景影响检查 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT06.5 | 色彩平衡全局/阴影/中间调/高光各设方向和小强度；各组作用域、过渡和零位检查 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT06.6 | 上述节点各自启停、复位和undo/redo；切换节点再取色，样本归正确节点，Esc后无迟到样本 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT07.1 | 清晰度和结构分别正/负值；100%与Fit观察边缘/肤色/细纹；复位恢复，记录完成耗时 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT07.2 | 锐化强度/半径/阈值逐一作用，观察边缘光晕和噪声，不能与降噪混称 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT07.3 | 亮度/颜色降噪分别调整；暗部噪声和细节保留，检查与清晰度先后顺序、取消 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT07.4 | 左Film Preset浏览应用→右Film参数对应同一状态；Profile强度、grain/size、halation/bloom/vignette逐项 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT07.5 | 既有surface/texture及seed/re-roll（仅已有入口）响应；同seed复现、变化仅当前节点，无新增路线开发 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT07.6 | Film启停、复位、undo/redo、旧胶片快照及未激活目标输出；空间proxy差异不等于算法无效 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT07.7 | Creative页当前已有真实节点逐项小值/复位，缺失能力只记录，不以同名按钮算实现 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT08.1 | 小视图与展开视图分别Orbit、Pan、wheel Zoom；Reset/Fit，Fit包住当前云，非固定倍率假Fit | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT08.2 | 核对OKLab真实L/a/b与球形sRGB色域归一显示说明；轴/图例可读，浅蓝到蓝过渡连续 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT08.3 | 点云/球面组合模式、背景、点尺寸、点/表面透明度、网格、轴、色域边界逐一开关 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT08.4 | 右3D页色彩空间/表面模式、色度范围、L切片/厚度、XYZ旋转各产生可见对应变化 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT08.5 | 小/展开视图共享设置；展开关闭后主图继续为主体，无覆盖、相机/设置突然重置 | FAIL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home FAIL is historical only. |
| RT08.6 | 所有仅观察操作前后调整栈/照片结果保持；空图、灰阶、近黑白和透明测试图按输入类型记录 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT09.1 | 当前照片吸管采样天空/肤色/暗部；真实样本对应球面点/簇高亮，记录OKLab及容差显示 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT09.2 | 球面选色/簇→照片临时overlay；调容差和softness，边界平滑，旋转后拾取仍对应 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT09.3 | 主图缩放/平移后取样、原图/处理结果及对比模式切换；overlay和云随当前阶段更新，不错位 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT09.4 | Esc、关闭overlay、离开hover与切图各清除相应临时状态，等待后台结束不重现旧选区 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT09.5 | 展开线性Y 0–X条图，逐区hover/离开；检查色块、占比与照片相应像素，黑/白端区分别测试 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT09.6 | RGB叠加/R/G/B切换、明度图及hover读数；来源标签与当前图/原图结果一致，alpha0不计 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT09.7 | 快速调参数/undo/同步/切图，等待终帧，云/双图/影调同一版本；统计“线性Y”等分不冒称曝光Zone | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT10.1 | JPEG/RAW各Fit→100%→缩放→Pan→Fit；记录真实倍率/尺寸，单一Zoom驱动全部显示 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT10.2 | RAW100%按需全尺寸细节，记录源尺寸、加载/处理耗时及完成状态；不同图像方向不二次旋转 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT10.3 | RAW复杂栈开始后Stop，等待旧任务退场；busy清除、UI可操作、无迟到detail覆盖 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT10.4 | 缓存图A→B→A再100%；缓存复用及相同当前结果，旧图不覆盖新图 | NOT_RUN | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home NOT_RUN is historical only. |
| RT10.5 | 持续拖动/平移至少记录一段有开始结束时间的运行；响应、取消和内存观察真实量测，不填估算PASS | NOT_RUN | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home NOT_RUN is historical only. |
| RT10.6 | V4不支持的完整工具栈组合若进入检查，必须明确提示并保留参数；不自动换引擎或丢节点 | NOT_RUN | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home NOT_RUN is historical only. |
| RT11.1 | 逐张点击、方向键切图，当前Active与Selected不同视觉可辨；横向滚动后仍有正确目标 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT11.2 | Ctrl增减、Shift范围、Ctrl+Shift及Ctrl+A；过滤后范围按可见序列，未错误清空隐藏选择 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT11.3 | All/Selected/RAW/JPEG/TIFF/PNG范围、最低评分、色标分别过滤；总数/可见/已选数与列表一致 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT11.4 | 批量评分/色标只作用可见所选；被筛掉目标不误操作，清筛选恢复其选择；不改原片像素 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT11.5 | 已关联素材的评分/色标在Inspector与批次共用状态；普通文件与DB素材持久化来源区分 | NOT_RUN | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home NOT_RUN is historical only. |
| RT11.6 | 当前源→可见所选，核对源图/目标数量/类别；只同步选择的调整，不拷评分、色标、文件名、EXIF或关系 | FAIL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home FAIL is historical only. |
| RT11.7 | 按节点/类别、多个同类节点同步；目标原有其他节点保留，顺序及内容正确，无重复ID异常 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT11.8 | 同步后批量undo/redo；A/B分别编辑、切图再undo不串历史；缩略图最终追上各自调整 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT11.9 | 右键单张/多选作用域、禁用反馈、菜单分组/子菜单边界；移出只清测试批次且确认范围 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT12.1 | 顶部快速导出与底部导出所选使用同可见选择；选目录前明确数量/格式/路径，取消不写文件 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT12.2 | Source默认：JPEG/PNG/TIFF沿可支持源格式，RAW默认TIFF16；显式切JPEG/PNG/TIFF各实际导出 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT12.3 | UI导出前保存同一冻结工作文件/参数，记录source/target代号、格式、输出ID；禁止用自检生成文件冒充UI产物 | PASS | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PASS is historical only. |
| RT12.4 | 对真正UI文件核对容器/尺寸/位深/alpha/ICC/方向；PNG/TIFF无损同链，JPEG与独立同质量编码oracle比对 | PASS | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PASS is historical only. |
| RT12.5 | 输出过程中改当前图/参数/格式；此批使用最初冻结集，目标不串图，源文件前后hash一致 | NOT_RUN | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home NOT_RUN is historical only. |
| RT12.6 | 取消批量、逐张失败、重试；已完成保留、未完成临时文件清理，重试格式沿原冻结值，不覆盖用户文件 | NOT_RUN | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home NOT_RUN is historical only. |
| RT12.7 | overlay开启/关闭分别输出同栈，图像结果不包含观察高亮；只旋转球体不改变输出 | PASS | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PASS is historical only. |
| RT12.8 | 发布配方进入独立流程，只有已启用配方覆盖手动设置；实际输出结果/数量与快速输出职责区分 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT13.1 | 保存本轮新工作文件：多目标、非零调整、选择/当前图、过滤；记录本地文件hash与保存时点 | PASS | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PASS is historical only. |
| RT13.2 | 正常关闭应用，重新启动同r1核对新会话身份，打开所存工作文件；不以同进程Load代替重启 | PASS | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PASS is historical only. |
| RT13.3 | 逐图检查Look/Film/节点顺序/启停/参数、选中和过滤，重开不重新解释旧版本参数 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT13.4 | 素材库评分/色标仍以同DB状态恢复，普通文件标记按工作文件；原件hash不变 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT13.5 | 重开后的相同参数导出与重启前对照；undo日志仅进程内，明确不将未保存跨进程日志当功能承诺 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT13.6 | 无效版本/文件与缺失源副本：失败不替换当前批次、不丢保存调整；保留可理解错误 | NOT_RUN | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home NOT_RUN is historical only. |
| RT14.1 | 分别1180×720、1600×920、1920×1080（实际桌面可支持时）；记录窗口物理尺寸及可用工作区，截八区全图 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT14.2 | Windows100/125/150/200%逐档实读后检查关键按钮/文字/工具与照片面积；不可改变或不可用档位写BLOCKED，不用WPF模拟替代 | FAIL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home FAIL is historical only. |
| RT14.3 | 简中→英文→繁中→简中：左四模式、右七页、新参数、曲线、3D、分析、格式文本更新；长文不截断 | FAIL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home FAIL is historical only. |
| RT14.4 | 语言切换不改文件名/用户节点名/参数key/数值解析/当前栈；重启语言设置恢复；旧中文硬编码如实记范围 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT14.5 | 顶栏/格式/右键/子菜单在边缘和窄窗打开；不覆盖父项、可滚动、键盘与Esc关闭、无亮白错误主题 | PARTIAL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home PARTIAL is historical only. |
| RT14.6 | 键盘焦点/Tab/Enter/Esc和数值字段同时检查；Esc清临时层不误退出或丢当前编辑 | FAIL | FAIL | NOT VERIFIED on company Release. Complete this original check and its missing steps; home FAIL is historical only. |

## Required continuation

Use the company source-bound launcher, original CC0 inputs and an uninterrupted visible desktop. Reproduce/retest the six issues, then execute each missing step above. Especially: continuous pan; cancel during actual work; cross-photo undo/redo; hidden/filtered selections; frozen sync boundaries; export while changing active/selected/filter state; cancellation/failure/retry; missing-file and invalid-session protection; language restart; linked asset metadata. Complete real 100/125/150/200% system DPI, not RenderTargetBitmap scaling. No image-output comparison is claimed for this company Release.
