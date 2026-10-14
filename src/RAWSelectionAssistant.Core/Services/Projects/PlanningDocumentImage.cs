using RAWSelectionAssistant.Core.Services.AssetLibrary;
namespace RAWSelectionAssistant.Core.Services.Projects;
public sealed record PlanningDocumentImage(Guid DocumentImageId,Guid DocumentId,AssetLibraryStableReference? Source,string CopyPath,string CopyHash,string Placement,string Title,string Caption,double Width,double Height,int Order,Guid? ShotId=null)
{
 [System.Text.Json.Serialization.JsonIgnore] public string? StorageRoot {get;init;}
 public string Position {get;init;}="";
 public string OriginLabel=>Source is null?"文档内图片":"素材库图片副本";
 public void Validate(){if(DocumentImageId==Guid.Empty||DocumentId==Guid.Empty||string.IsNullOrWhiteSpace(CopyPath)||CopyHash is null||CopyHash.Length!=64||!CopyHash.All(Uri.IsHexDigit)||!double.IsFinite(Width)||!double.IsFinite(Height)||Width<=0||Height<=0||Order<0)throw new ArgumentException("文档图片身份或尺寸无效。");}
}
