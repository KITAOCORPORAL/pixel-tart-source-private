using RAWSelectionAssistant.Core.Services.RawToJpeg;
namespace RAWSelectionAssistant.Tests;
[TestClass]public sealed class RawDiagnosticTaskAuthorizationTests
{
 [TestMethod]public void LegacyVersionOneAdmissionRemainsCompatible()=>RawDiagnosticTaskAuthorization.Validate(1,RawDiagnosticTaskAuthorization.LegacyTask,false);
 [TestMethod]public void NewTaskWithoutNewAuthorizationFailsBeforeAnyDecode()=>Assert.Throws<InvalidDataException>(()=>RawDiagnosticTaskAuthorization.Validate(2,RawDiagnosticTaskAuthorization.MemoryFixTask,false));
 [TestMethod]public void ExplicitAuthorizationPassesSuccessorAdmission()=>RawDiagnosticTaskAuthorization.Validate(2,RawDiagnosticTaskAuthorization.MemoryFixTask,true);
 [TestMethod][DataRow(1,"RAW-XTRANS-MEMORYFIX-PRODUCT-REGRESSION-20261111")][DataRow(2,"RAW-XTRANS-STABLE-V1-PRODUCT-REGRESSION-20261110")][DataRow(2,"unrecognized-task")][DataRow(0,"RAW-XTRANS-MEMORYFIX-PRODUCT-REGRESSION-20261111")]
 public void TaskNamesAndVersionsCannotBeSubstituted(int version,string task)=>Assert.Throws<InvalidDataException>(()=>RawDiagnosticTaskAuthorization.Validate(version,task,true));
}
