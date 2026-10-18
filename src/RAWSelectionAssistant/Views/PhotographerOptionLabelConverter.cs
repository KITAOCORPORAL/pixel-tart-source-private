using System.Globalization;using System.Windows.Data;
namespace RAWSelectionAssistant.Views;
/// <summary>Presentation-only labels. Values and persisted recipes stay unchanged.</summary>
public sealed class PhotographerOptionLabelConverter:IValueConverter
{
 public object Convert(object value,Type targetType,object parameter,CultureInfo culture)=>value is string s?s switch
 {
  "NormalizedMix"=>"自然混合","ExposureSum"=>"曝光叠加","Warm"=>"暖白","Neutral"=>"中性白","Cool"=>"冷白",
  "SquareV1"=>"方形成像","LegacyFree"=>"保留原比例","CropFill"=>"裁切填满","FitEntire"=>"完整放入",
  "Plain"=>"素面","Leather"=>"皮纹","Dots"=>"点纹","Grid"=>"网格","None"=>"无缺陷","Unspread"=>"显影不均","Bands"=>"乳剂条带","Fog"=>"雾化","Islands"=>"斑块","Crescent"=>"月牙痕","BlueFailure"=>"偏蓝失效",
  "Base"=>"基础色彩","Original"=>"从原文件开始","CurrentEdits"=>"使用当前编辑",
  "i-Type Color · 创作 V3"=>"经典彩色 · 效果版本","i-Type B&W · 创作 V3"=>"柔调黑白 · 效果版本","SX-70 Color · 创作 V3"=>"复古暖彩 · 效果版本",
  _=>s.StartsWith("Womb01351.Stock",StringComparison.Ordinal)?"实验色彩 "+s[^1]:s
 }:value;
 public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>throw new NotSupportedException();
}
