using DocumentFormat.OpenXml.Packaging;
using RAWSelectionAssistant.Core.Services.Projects;
using W=DocumentFormat.OpenXml.Wordprocessing;
namespace RAWSelectionAssistant.Services;
public static partial class PlanningDocxAdapter
{
 private static void WritePageExtras(MainDocumentPart main,W.SectionProperties section,PlanningHeaderFooter value)
 {
  value.Validate();
  W.Paragraph Paragraph(string text,bool page){var p=new W.Paragraph(new W.Run(new W.Text(text){Space=DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve}));if(page){p.Append(new W.Run(new W.Text("    第")),new W.SimpleField(new W.Run(new W.Text(value.StartNumber.ToString()))){Instruction=" PAGE "},new W.Run(new W.Text("页/共")),new W.SimpleField(new W.Run(new W.Text("1"))){Instruction=" NUMPAGES "},new W.Run(new W.Text("页")));}return p;}
  void Header(string text,W.HeaderFooterValues type){var part=main.AddNewPart<HeaderPart>();part.Header=new W.Header(Paragraph(text,false));part.Header.Save();section.PrependChild(new W.HeaderReference{Type=type,Id=main.GetIdOfPart(part)});}
  void Footer(string text,bool page,W.HeaderFooterValues type){var part=main.AddNewPart<FooterPart>();part.Footer=new W.Footer(Paragraph(text,page));part.Footer.Save();var reference=new W.FooterReference{Type=type,Id=main.GetIdOfPart(part)};var lastHeader=section.Elements<W.HeaderReference>().LastOrDefault();if(lastHeader is null)section.PrependChild(reference);else section.InsertAfter(reference,lastHeader);}
  if(value==new PlanningHeaderFooter())return;
  if(section.GetFirstChild<W.PageMargin>() is {} margin){margin.Header=300;margin.Footer=300;if(value.Header.Length>0||value.FirstHeader.Length>0)margin.Top=Math.Max(810,margin.Top?.Value??720);if(value.PageNumbers||value.Footer.Length>0||value.FirstFooter.Length>0)margin.Bottom=Math.Max(810,margin.Bottom?.Value??720);}
  Header(value.Header,W.HeaderFooterValues.Default);Footer(value.Footer,value.PageNumbers,W.HeaderFooterValues.Default);
  if(value.DifferentFirstPage||value.HideFirstPageNumber){Header(value.DifferentFirstPage?value.FirstHeader:value.Header,W.HeaderFooterValues.First);Footer(value.DifferentFirstPage?value.FirstFooter:value.Footer,value.PageNumbers&&!value.HideFirstPageNumber,W.HeaderFooterValues.First);section.Append(new W.TitlePage());}
  if(value.PageNumbers){var number=new W.PageNumberType{Start=value.StartNumber};var title=section.GetFirstChild<W.TitlePage>();if(title is null)section.Append(number);else section.InsertBefore(number,title);}
 }
 private static PlanningHeaderFooter ReadPageExtras(MainDocumentPart main,W.SectionProperties? section,System.Collections.Generic.List<string> losses)
 {
  string Text(DocumentFormat.OpenXml.OpenXmlElement? e)=>e is null?"":string.Concat(e.Elements<W.Paragraph>().SelectMany(p=>p.Elements<W.Run>()).SelectMany(r=>r.Elements<W.Text>()).Select(t=>t.Text));
  DocumentFormat.OpenXml.OpenXmlElement? Part(bool header,W.HeaderFooterValues type)
  {var id=header?section?.Elements<W.HeaderReference>().FirstOrDefault(x=>x.Type?.Value==type)?.Id?.Value:section?.Elements<W.FooterReference>().FirstOrDefault(x=>x.Type?.Value==type)?.Id?.Value;if(id is null)return null;return main.GetPartById(id) switch{HeaderPart h=>h.Header,FooterPart f=>f.Footer,_=>null};}
  var h=Part(true,W.HeaderFooterValues.Default);var f=Part(false,W.HeaderFooterValues.Default);var fh=Part(true,W.HeaderFooterValues.First);var ff=Part(false,W.HeaderFooterValues.First);
  bool Numbers(DocumentFormat.OpenXml.OpenXmlElement? e)=>e?.Descendants<W.SimpleField>().Any(x=>x.Instruction?.Value?.Trim()=="PAGE")==true;
  string Plain(DocumentFormat.OpenXml.OpenXmlElement? e){var text=Text(e);return Numbers(e)?text.Replace("    第页/共页",""):text;}
  if(section?.Elements<W.HeaderReference>().Any(x=>x.Type?.Value==W.HeaderFooterValues.Even)==true||section?.Elements<W.FooterReference>().Any(x=>x.Type?.Value==W.HeaderFooterValues.Even)==true)losses.Add("奇偶页独立页眉页脚尚未支持，仅保留默认和首页。");
  if(new[]{h,f,fh,ff}.Where(e=>e is not null).Any(e=>e!.Descendants<W.Drawing>().Any()||e.Descendants<W.FieldChar>().Any()))losses.Add("页眉页脚图片和复杂域不保留；仅支持纯文本及简单 PAGE/NUMPAGES 域。");
  var result=new PlanningHeaderFooter{Header=Plain(h),Footer=Plain(f),FirstHeader=Plain(fh),FirstFooter=Plain(ff),DifferentFirstPage=section?.GetFirstChild<W.TitlePage>() is not null,PageNumbers=Numbers(f),HideFirstPageNumber=section?.GetFirstChild<W.TitlePage>() is not null&&!Numbers(ff),StartNumber=section?.GetFirstChild<W.PageNumberType>()?.Start?.Value??1};result.Validate();return result;
 }
}
