namespace RAWSelectionAssistant.Core.Services.Projects;
public sealed record PlanningHeaderFooter
{
 public string Header {get;init;}="";
 public string Footer {get;init;}="";
 public bool DifferentFirstPage {get;init;}
 public string FirstHeader {get;init;}="";
 public string FirstFooter {get;init;}="";
 public bool PageNumbers {get;init;}
 public bool HideFirstPageNumber {get;init;}
 public int StartNumber {get;init;}=1;
 public void Validate(){if(StartNumber<1||StartNumber>1000000||new[]{Header,Footer,FirstHeader,FirstFooter}.Any(x=>x is null||x.Length>300||x.Contains('\n')||x.Contains('\r')))throw new ArgumentException("页眉页脚限单行300字，起始页码为1到1000000。");}
}
