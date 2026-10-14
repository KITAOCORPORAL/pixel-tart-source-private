using System.Text.Json;
using Microsoft.TestPlatform.VsTestConsole.TranslationLayer;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
if(args.Length!=3)throw new ArgumentException("usage: runner vstest.console.dll test.dll discovery.json");
var wrapper=new VsTestConsoleWrapper(Path.GetFullPath(args[0]));var handler=new Discovery();
try{wrapper.StartSession();wrapper.DiscoverTests([Path.GetFullPath(args[1])],"<RunSettings/>",new TestPlatformOptions(),handler);}
finally{wrapper.EndSession();}
if(handler.Aborted||handler.Error||handler.Total!=handler.Tests.Count||handler.Tests.Count==0)throw new InvalidOperationException($"Discovery incomplete: reported={handler.Total}, received={handler.Tests.Count}, aborted={handler.Aborted}, error={handler.Error}");
File.WriteAllText(args[2],JsonSerializer.Serialize(handler.Tests.Select(t=>new{ id=t.Id,fullyQualifiedName=t.FullyQualifiedName,displayName=t.DisplayName,source=t.Source,executor=t.ExecutorUri.ToString()}),new JsonSerializerOptions{WriteIndented=true}));
sealed class Discovery:ITestDiscoveryEventsHandler2
{
 public List<TestCase> Tests {get;}=[];public bool Aborted,Error;public long Total;
 public void HandleDiscoveredTests(IEnumerable<TestCase>? tests){if(tests is not null)Tests.AddRange(tests);}
 public void HandleDiscoveryComplete(DiscoveryCompleteEventArgs e,IEnumerable<TestCase>? last){Total=e.TotalCount;Aborted=e.IsAborted;HandleDiscoveredTests(last);}
 public void HandleLogMessage(TestMessageLevel level,string? message){Console.Error.WriteLine(message);if(level==TestMessageLevel.Error)Error=true;}
 public void HandleRawMessage(string rawMessage){}
}

