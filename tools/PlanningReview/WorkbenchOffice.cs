using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using static PlanningReview.Rules;
namespace PlanningReview;
// The desktop adapter consumes a WPF FlowDocument; use a portable WordprocessingML projection here.
internal static class WorkbenchOffice
{
    public const string Mime="application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    static readonly XNamespace W="http://schemas.openxmlformats.org/wordprocessingml/2006/main", R="http://schemas.openxmlformats.org/package/2006/relationships", C="http://schemas.openxmlformats.org/package/2006/content-types";
    static XElement E(string n,params object?[] c)=>new(W+n,c);
    static XAttribute A(string n,object x)=>new(W+n,x);
    static XElement V(string n,object x)=>E(n,A("val",x));
    static string Str(JsonElement j,string k)=>j.TryGetProperty(k,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString()!:"";
    static bool Flag(JsonElement j,string k)=>j.TryGetProperty(k,out var v)&&v.ValueKind==JsonValueKind.True;
    static string Clean(string text)=>string.Concat(text.EnumerateRunes().Where(r=>r.Value is 9 or 10 or 13 || r.Value is >=32 and <=0xd7ff or >=0xe000 and <=0xfffd or >=0x10000 and <=0x10ffff).Select(r=>r.ToString()));
    static XElement Run(string text,JsonElement? style=null)
    {
        var props=E("rPr");if(style is {} s){if(Str(s,"font") is {Length:>0} font){var latin=font switch {"mono"=>"Consolas","serif"=>"Cambria",_=>"Calibri"};var east=font=="serif"?"SimSun":"Microsoft YaHei";props.Add(E("rFonts",A("ascii",latin),A("hAnsi",latin),A("eastAsia",east)));}if(Flag(s,"bold"))props.Add(E("b"));if(Flag(s,"italic"))props.Add(E("i"));if(Str(s,"color") is {Length:7}color)props.Add(V("color",color[1..]));if(s.TryGetProperty("spacing",out var space))props.Add(V("spacing",(int)Math.Round(space.GetDouble()*15)));if(s.TryGetProperty("size",out var z))props.Add(V("sz",Math.Clamp((int)Math.Round(z.GetDouble()*1.5),12,189)));if(Flag(s,"underline"))props.Add(V("u","single"));if(Str(s,"highlight") is {Length:7}hi)props.Add(E("shd",A("val","clear"),A("fill",hi[1..])));}
        var run=E("r",props.HasElements?props:null);var lines=Clean(text).Replace("\r\n","\n").Replace('\r','\n').Split('\n');for(int i=0;i<lines.Length;i++){if(i>0)run.Add(E("br"));run.Add(E("t",new XAttribute(XNamespace.Xml+"space","preserve"),lines[i]));}return run;
    }
    static XElement P(string s,string? style=null)=>E("p",style==null?null:E("pPr",V("pStyle",style)),Run(s));
    static XElement Rich(JsonElement p,string? prefix=null)
    {
        var type=Str(p,"type");if(type=="")type=Str(p,"style");string? heading=type switch{"h1"=>"Heading2","h2" or "h3"=>"Heading3","quote"=>"Quote","caption"=>"Caption",_=>null};var pp=E("pPr");if(heading!=null)pp.Add(V("pStyle",heading));if(Flag(p,"pageBreakBefore"))pp.Add(E("pageBreakBefore"));if(p.TryGetProperty("lineHeight",out var height))pp.Add(E("spacing",A("line",(int)Math.Round(height.GetDouble()*240)),A("lineRule","auto")));if(Str(p,"align") is "right" or "center" )pp.Add(V("jc",Str(p,"align")));
        var para=E("p",pp);prefix??=type switch{"ul" or "li"=>"•  ","check"=>Flag(p,"checked")?"☑  ":"☐  ",_=>null};if(prefix!=null)para.Add(Run(prefix));var text=Str(p,"text");int pos=0;
        if(p.TryGetProperty("marks",out var marks))foreach(var m in marks.EnumerateArray()){int a=m.GetProperty("start").GetInt32(),b=m.GetProperty("end").GetInt32();if(a>pos)para.Add(Run(text[pos..a]));para.Add(Run(text[a..b],m));pos=b;}
        if(pos<text.Length)para.Add(Run(text[pos..]));return para;
    }
    static XElement NativeDocBlock(JsonElement block,string? prefix=null)
    {
        if(block.TryGetProperty("nativeImage",out var inlineImage))return P("参考图  "+Str(inlineImage,"caption"),"Caption");
        if(!block.TryGetProperty("nativeTable",out var table))return Rich(block,prefix);
        var rows=table.GetProperty("rows").EnumerateArray().ToArray();
        var elem=E("tbl",E("tblPr",E("tblW",A("w",0),A("type","auto")),E("tblBorders",new[]{"top","bottom","left","right","insideH","insideV"}.Select(s=>E(s,A("val","single"),A("sz",4),A("color","D4DBD6"))))));
        var active=new Dictionary<int,(int span,int rows)>();
        for(int r=0;r<rows.Length;r++)
        {
            var row=E("tr",r==0&&Flag(table,"repeatHeader")?E("trPr",E("tblHeader")):null);
            var column=0;var existing=new Dictionary<int,(int span,int rows)>(active);
            void ContinueSpan()
            {
                while(existing.Remove(column,out var prior))
                {
                    row.Add(E("tc",E("tcPr",prior.span>1?V("gridSpan",prior.span):null,E("vMerge")),P("")));
                    if(prior.rows<=1)active.Remove(column);else active[column]=(prior.span,prior.rows-1);
                    column+=prior.span;
                }
            }
            foreach(var cell in rows[r].GetProperty("cells").EnumerateArray())
            {
                ContinueSpan();var span=cell.GetProperty("colSpan").GetInt32();var merge=cell.GetProperty("rowSpan").GetInt32();
                if(merge>1)active[column]=(span,merge-1);
                var contents=cell.GetProperty("paragraphs").EnumerateArray().Select(p=>NativeDocBlock(p)).DefaultIfEmpty(P(""));
                row.Add(E("tc",E("tcPr",span>1?V("gridSpan",span):null,merge>1?V("vMerge","restart"):null),contents));column+=span;
            }
            ContinueSpan();elem.Add(row);
        }
        return elem;
    }
    static XElement Tbl(IReadOnlyList<string> heads,IEnumerable<string[]> values)
    {
        int n=heads.Count,w=9360/n;var borders=E("tblBorders");foreach(var side in new[]{"top","left","bottom","right","insideH","insideV"})borders.Add(E(side,A("val","single"),A("sz",4),A("color","D4DBD6")));
        var t=E("tbl",E("tblPr",E("tblW",A("w",9360),A("type","dxa")),borders,E("tblLayout",A("type","fixed"))),E("tblGrid",Enumerable.Range(0,n).Select(_=>E("gridCol",A("w",w)))));
        XElement Row(IEnumerable<string> data,bool head)=>E("tr",head?E("trPr",E("tblHeader")):null,data.Select(s=>E("tc",E("tcPr",E("tcW",A("w",w),A("type","dxa")),head?E("shd",A("val","clear"),A("fill","E5EDE8")):null),P(s,head?"CellHead":"Cell"))));
        t.Add(Row(heads,true));foreach(var row in values)t.Add(Row(row,false));return t;
    }
    static XElement Style(string id,string name,int halfPt,bool bold=false,bool keep=false,int before=0,int after=80)=>E("style",A("type","paragraph"),A("styleId",id),id=="Normal"?A("default",1):null,V("name",name),id=="Normal"?null:V("basedOn","Normal"),E("pPr",keep?E("keepNext"):null,E("spacing",A("before",before),A("after",after))),E("rPr",E("rFonts",A("ascii","Calibri"),A("hAnsi","Calibri"),A("eastAsia","Microsoft YaHei")),bold?E("b"):null,V("color","202421"),V("sz",halfPt)));
    public static byte[] Create(Notebook book)
    {
        var wb=book.Workbench??throw new Problem(404,"此实例未开启工作台");var pages=wb.Pages.Where(p=>!p.Archived).ToList();var blocks=book.Blocks.Where(b=>!b.Removed).ToDictionary(b=>b.Id);var shots=wb.ShotDetails.ToDictionary(s=>s.Id);
        Need(pages.Sum(p=>(long)p.Data.GetRawText().Length)+book.Order.Sum(id=>shots.TryGetValue(id,out var s)?(long)s.Requirements.Length+s.Fields.Values.Sum(v=>v.Length):0)<2_000_000,"项目内容超出单份 Word 审阅稿的导出上限",413);
        var body=E("body",P(book.Title,"Title"),P("拍摄策划审阅稿  ·  修订 "+book.Revision,"Caption"));bool shotList=false;
        string Caption(Guid? a)=>a is {} id&&blocks.TryGetValue(id,out var b)&&b.Kind=="image"?b.Text:"";
        void ShotList(){if(shotList)return;shotList=true;if(book.Order.Count==0){body.Add(P("暂无分镜"));return;}
            var rows=book.Order.Where(blocks.ContainsKey).Select((id,index)=>{var d=shots.GetValueOrDefault(id);return new[]{(index+1)+"  "+blocks[id].Text,d?.Requirements??"",Caption(d?.AssetId)};});body.Add(Tbl(["镜号与名称","拍摄要求","参考图"],rows));}
        int cells=0;
        foreach(var page in pages){if(page.Kind=="shots"&&shotList)continue;body.Add(P(page.Title,"Heading1"));var data=page.Data;
            switch(page.Kind){case "doc":int serial=0;foreach(var para in data.GetProperty("paragraphs").EnumerateArray()){if(Str(para,"type")=="ol")body.Add(NativeDocBlock(para,(++serial)+".  "));else{serial=0;body.Add(NativeDocBlock(para));}}
                if(data.TryGetProperty("assetIds",out var ids))foreach(var a in ids.EnumerateArray()){var title=Caption(a.GetGuid());if(title!="")body.Add(P("参考图  "+title,"Caption"));}break;
            case "table":var cols=data.GetProperty("columns").EnumerateArray().ToArray();var rows=data.GetProperty("rows").EnumerateArray().ToArray();
                cells+=cols.Length*Math.Max(1,rows.Length);Need(cells<=16000,"表格单元格超出单份 Word 审阅稿的导出上限",413);
                if(cols.Length>5)body.Add(P("宽表按每行记录纵向展开，保留原字段顺序。","Caption"));if(rows.Length==0){body.Add(P(string.Join("  /  ",cols.Select(c=>Str(c,"label"))),"Caption"),P("暂无记录"));break;}
                if(cols.Length<=5)body.Add(Tbl(cols.Select(c=>Str(c,"label")).ToArray(),rows.Select(row=>cols.Select(c=>Str(row.GetProperty("cells"),Str(c,"key"))).ToArray())));
                else foreach(var (row,idx) in rows.Select((r,i)=>(r,i))){body.Add(P("记录 "+(idx+1),"Heading3"),Tbl(["栏目","内容"],cols.Select(c=>new[]{Str(c,"label"),Str(row.GetProperty("cells"),Str(c,"key"))})));}break;
            case "canvas":body.Add(P("画布以文字与参考图图注列出。空间布局、裁切和绘图元素请在工作台查看。","Caption"));foreach(var obj in data.GetProperty("objects").EnumerateArray()){if(Str(obj,"kind")=="text")body.Add(Rich(obj));else if(Str(obj,"kind")=="image"){var cap=Caption(obj.GetProperty("assetId").GetGuid());if(cap!="")body.Add(P("参考图  "+cap));}}break;
            case "shots":ShotList();break;}}
        if(!shotList&&book.Order.Count>0){body.Add(P("拍摄分镜","Heading1"));ShotList();}
        body.Add(E("sectPr",E("pgSz",A("w",11906),A("h",16838)),E("pgMar",A("top",1280),A("right",1273),A("bottom",1280),A("left",1273),A("header",480),A("footer",480),A("gutter",0))));
        var styles=E("styles",Style("Normal","Normal",21),Style("Title","Title",42,true,true,0,150),Style("Heading1","heading 1",32,true,true,320,100),Style("Heading2","heading 2",27,true,true,180,60),Style("Heading3","heading 3",24,true,true,140,55),Style("Quote","Quote",22),Style("Caption","Caption",18,false,false,20,90),Style("Cell","Table text",19),Style("CellHead","Table header",19,true));
        using var stream=new MemoryStream();using(var zip=new ZipArchive(stream,ZipArchiveMode.Create,true)){void Part(string path,XElement xml){using var output=zip.CreateEntry(path,CompressionLevel.Fastest).Open();using var writer=XmlWriter.Create(output,new XmlWriterSettings{Encoding=new UTF8Encoding(false)});xml.Save(writer);}
            Part("[Content_Types].xml",new XElement(C+"Types",new XElement(C+"Default",new XAttribute("Extension","rels"),new XAttribute("ContentType","application/vnd.openxmlformats-package.relationships+xml")),new XElement(C+"Default",new XAttribute("Extension","xml"),new XAttribute("ContentType","application/xml")),new[]{("document","document.main"),("styles","styles")}.Select(p=>new XElement(C+"Override",new XAttribute("PartName","/word/"+p.Item1+".xml"),new XAttribute("ContentType","application/vnd.openxmlformats-officedocument.wordprocessingml."+p.Item2+"+xml")))));
            XElement Rel(string type,string target)=>new(R+"Relationships",new XElement(R+"Relationship",new XAttribute("Id","rId1"),new XAttribute("Type","http://schemas.openxmlformats.org/officeDocument/2006/relationships/"+type),new XAttribute("Target",target)));
            Part("_rels/.rels",Rel("officeDocument","word/document.xml"));Part("word/_rels/document.xml.rels",Rel("styles","styles.xml"));Part("word/styles.xml",styles);Part("word/document.xml",E("document",new XAttribute(XNamespace.Xmlns+"w",W),body));}
        return stream.ToArray();
    }
}
