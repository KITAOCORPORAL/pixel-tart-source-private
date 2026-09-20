# Pixel Tart Studio UI — 字体

Studio 控件以 Segoe UI 为主，中文回退 Microsoft YaHei UI。Display 32/40、PageTitle 24/32、SectionTitle 16/24、Body 14/22、BodyStrong 同正文加粗、Caption 12/16、Metadata 12/18、Micro 11/16（字号／行高）。中文正文 Wrap；路径 Ellipsis 加 Tooltip。标题强调，正文不全面 SemiBold。既有静态字体引用仍保留 Microsoft YaHei UI，不能把新 token 等同于全应用字体已切换。

实施资源：`src/RAWSelectionAssistant/Resources/DesignSystem/Studio.Controls.xaml`、`Studio.Symbols.xaml` 及现有兼容字典。
状态：v1 实施中，用户确认待完成。全应用推广为 PARTIAL；本文件不是验收 PASS。
