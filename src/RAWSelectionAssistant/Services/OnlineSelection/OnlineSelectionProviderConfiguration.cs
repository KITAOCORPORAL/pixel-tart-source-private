using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.OnlineSelection;
namespace RAWSelectionAssistant.Services.OnlineSelection;
public static class OnlineSelectionProviderConfiguration
{
 public static IOnlineSelectionProvider Create(string dataDirectory)
 {
  var endpoint=Environment.GetEnvironmentVariable("PIXELTART_SELECTION_ENDPOINT");if(string.IsNullOrWhiteSpace(endpoint))return OnlineSelectionProviderFactory.CreateDefault();
  var key=Environment.GetEnvironmentVariable("PIXELTART_SELECTION_OWNER_KEY");
  if(!Uri.TryCreate(endpoint,UriKind.Absolute,out var uri)||string.IsNullOrWhiteSpace(dataDirectory)||key is null||key.Length<32)return new NoneOnlineSelectionProvider("测试服务配置无效；请使用测试启动入口。");
  try{return new HttpOnlineSelectionProvider(uri,Path.Combine(dataDirectory,"http-owner-access.bin"),key);}
  catch(Exception e)when(e is ArgumentException or IOException or UnauthorizedAccessException or CryptographicException or JsonException){return new NoneOnlineSelectionProvider("测试服务配置或本机凭据不可用，原文件已保留。");}
 }
}
