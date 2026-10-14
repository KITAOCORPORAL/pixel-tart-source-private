using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Windows.Documents;
using RAWSelectionAssistant.Services;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass]
public sealed class PlanningDocxAtomicReplacementTests
{
 public TestContext TestContext {get;set;}=null!;
 private void Record(string path,string phase)
 {
  var folder=Path.GetDirectoryName(path)!;
  TestContext.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{phase,path,attributes=File.GetAttributes(path).ToString(),fileAcl=new FileInfo(path).GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.All),directoryAcl=new DirectoryInfo(folder).GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.All),identity=WindowsIdentity.GetCurrent().Name,sid=WindowsIdentity.GetCurrent().User?.Value,process=Environment.ProcessId,cwd=Environment.CurrentDirectory,temp=Path.GetTempPath(),framework=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,assembly=typeof(PlanningDocxAdapter).Assembly.Location,sha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(PlanningDocxAdapter).Assembly.Location)))}));
 }
 [TestMethod]
 public Task ExistingDestinationWithDeleteSharingReaderCanBeReplaced()=>RunSta(async()=>
 {
  var root=Path.Combine(Path.GetTempPath(),"docx-atomic-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var path=Path.Combine(root,"existing.docx");var renamed=Path.Combine(root,"rename.tmp");
  try{
   await File.WriteAllTextAsync(path,"old destination");await File.WriteAllTextAsync(renamed,"candidate");
   Record(path,"before controlled reader");
   using(var reader=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
   {
    var denied=Assert.Throws<UnauthorizedAccessException>(()=>File.Move(renamed,path,true),"Control reproduces Windows rename-overwrite denial with a live delete-sharing reader.");
    TestContext.WriteLine($"Owned reader handle={reader.SafeFileHandle.DangerousGetHandle()}; share=ReadWrite|Delete; rename error HResult={denied.HResult:X8}");
    PlanningDocxAdapter.Export(new FlowDocument(new Paragraph(new Run("new document"))),path);
    Record(path,"after File.Replace with same live reader");
    var imported=PlanningDocxAdapter.Import(path).Document;StringAssert.Contains(new TextRange(imported.ContentStart,imported.ContentEnd).Text,"new document");
    using var old=new StreamReader(reader);Assert.AreEqual("old destination",await old.ReadToEndAsync());
   }
  }finally{Directory.Delete(root,true);}
 });
 [TestMethod]
 public Task LockedReadOnlyAndAclDeniedPreserveOriginalAndCleanTemporary()=>RunSta(async()=>
 {
  var root=Path.Combine(Path.GetTempPath(),"docx-denied-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var path=Path.Combine(root,"existing.docx");var bytes=System.Text.Encoding.UTF8.GetBytes("old document must survive");await File.WriteAllBytesAsync(path,bytes);var doc=new FlowDocument(new Paragraph(new Run("new")));
  void Preserved(){CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));Assert.IsEmpty(Directory.GetFiles(root,"*.tmp"));}
  try{
   using(var held=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){Assert.Throws<IOException>(()=>PlanningDocxAdapter.Export(doc,path));}Preserved();
   File.SetAttributes(path,FileAttributes.ReadOnly);try{Assert.Throws<UnauthorizedAccessException>(()=>PlanningDocxAdapter.Export(doc,path));}finally{File.SetAttributes(path,FileAttributes.Normal);}Preserved();
   var dir=new DirectoryInfo(root);var original=dir.GetAccessControl();var denied=dir.GetAccessControl();var rule=new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!,FileSystemRights.CreateFiles,AccessControlType.Deny);denied.AddAccessRule(rule);dir.SetAccessControl(denied);
   try{Assert.Throws<UnauthorizedAccessException>(()=>PlanningDocxAdapter.Export(doc,path));}finally{dir.SetAccessControl(original);}Preserved();
  }finally{File.SetAttributes(path,FileAttributes.Normal);Directory.Delete(root,true);}
 });
}
