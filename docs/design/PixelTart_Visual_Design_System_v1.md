# Pixel Tart Visual Design System v1.0

状态：已锁定，作为像素蛋挞 2.3.x 产品重构的视觉唯一依据。

## 1. 设计方向

Pixel Tart A-v2 的产品气质是“摄影师的数字暗房工作台”：安静、克制、精确、专业，优先服务照片、拍摄排期和交付工作流。

禁止将产品设计成 ERP、SaaS 管理后台、数据大屏、游戏界面、赌场界面或卡片墙。任何新增视觉表达都必须先归入本规范的既有 token、组件和容器角色；不得由实现者临时发明相近样式。

## 2. 色彩系统

### 2.1 深色基础色

| Token | 色值 | 用途 |
| --- | --- | --- |
| AppBackground | #0D0F12 | 应用最底层背景 |
| Surface01 | #121519 | 主工作区表面 |
| Surface02 | #171B20 | 局部面板和输入背景 |
| Surface03 | #1D2228 | 悬停、分组和次级层 |
| SurfaceElevated | #222830 | 浮层、菜单、弹窗 |
| BorderSubtle | #292F37 | 安静分隔线 |
| BorderStrong | #3A424C | 聚焦和明确边界 |
| TextPrimary | #F2F4F6 | 主文本 |
| TextSecondary | #B2B8C0 | 次文本 |
| TextMuted | #747C86 | 说明和弱信息 |
| TextDisabled | #555C65 | 禁用内容 |

### 2.2 操作色与摄影识别色

| Token | 色值 | 用途 |
| --- | --- | --- |
| Primary | #18A88C | 当前工作区唯一主要操作 |
| PrimaryHover | #20B89B | 主要操作悬停 |
| PrimaryPressed | #128671 | 主要操作按下 |
| PhotographyGold | #D79A32 | Pin、摄影身份和重要提示 |

PhotographyGold 不得用于大面积填充，也不得与 Primary 竞争主要操作权重。

### 2.3 语义色

Success #42B883、Warning #E0AD43、Danger #D85A5A、Info #4E8FD4。语义色优先用于文本、图标、细边框和小型状态标记，不填满整张卡片。

### 2.4 日历五色

空闲 #59616B；有拍摄 #E05252；已拍摄 #3DB879；待返图 #DDAF32；已返图 #3E8ED0。

关闭档期不是第六种业务颜色。关闭状态必须保留原业务状态色，并通过锁图标、背景降亮和辅助文本表达。

## 3. 字体层级

中文使用 Microsoft YaHei UI，拉丁字符和数字优先使用 Segoe UI Variable。

| 层级 | 大小 | 字重 |
| --- | --- | --- |
| PageTitle | 22 | SemiBold |
| Hero | 20 | SemiBold |
| Section | 16 | SemiBold |
| Card | 14 | SemiBold |
| Body | 13 | Normal |
| Secondary | 12 | Normal |
| Caption | 11 | Normal |
| NumericLarge | 24 | SemiBold |

普通文本不得小于 11 DIP。空间不足时应调整布局、换行、折叠次要内容或增加滚动，不得通过缩小字体解决。

## 4. 间距与圆角

合法间距只有 4、8、12、16、24、32、48 DIP。页面和组件不得出现未经记录的任意间距。

圆角：小型标记 4；输入框和按钮 6；卡片 8；抽屉 10；模态框 12。禁止手机应用式的大圆角和胶囊化滥用。

## 5. 层级

- L0：App，应用背景和全局壳层。
- L1：Workspace，长期工作的主页面。
- L2：Local Panel，页面内的详情、过滤和局部面板。
- L3：Floating，Modal、Drawer、Tooltip、ContextMenu。

层级主要依靠留白、亮度和排版建立，不依靠卡片套卡片、粗边框或大量徽章。

## 6. 操作层级

一个工作区同一时刻最多一个 Primary。Secondary 使用深色表面和细边框；Ghost 保持透明；Danger 默认使用红色文本或红色描边，只有不可逆确认才允许更强强调。

Hover 必须安静，不能产生大面积颜色跳变。常规反馈时长为 120–180ms；减少动效模式下应取消非必要位移与渐变。

## 7. 信息密度

摄影内容优先于统计。仅当数字直接帮助下一步行动时才显示指标。空状态必须说明当前状态、下一步动作和数据安全边界，不能用无意义的大面积黑区填充。

## 8. 主题与兼容

Dark 是 A-v2 主设计稿。Light 保持同一结构与层级；HighContrast 使用系统色并保留状态的非颜色通道。旧资源键可作为兼容别名存在，但新页面只能引用本规范的语义 token。

## 9. 变更治理

新增颜色、字号、间距、圆角、图标或组件前，必须先更新本文件和组件目录。任何例外必须记录适用页面、原因、期限和替代方案。未记录例外视为缺陷。
