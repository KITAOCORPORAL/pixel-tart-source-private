using System.IO;
using RAWSelectionAssistant.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.WpfTests;
internal static class PlanningRichFlowFixture
{
    internal static readonly string[] Scenes=["玻璃温室逆光入口与悬挂白纱", "旧厂房楼梯长廊与蓝色反光幕", "雨后街角夜景与移动霓虹灯"];
    internal static byte[] ReferencePng(int index)
    {
        var visual=new DrawingVisual();using(var c=visual.RenderOpen())
        { c.DrawRectangle(new SolidColorBrush(Color.FromRgb((byte)(38+index*45),(byte)(108+index*18),(byte)(146-index*23))),null,new Rect(0,0,480,320));
          c.DrawEllipse(Brushes.Coral,null,new Point(100+index*95,120),55,55);c.DrawRectangle(Brushes.Wheat,null,new Rect(40+index*30,220,260,30)); }
        var bmp=new RenderTargetBitmap(480,320,96,96,PixelFormats.Pbgra32);bmp.Render(visual);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bmp));using var stream=new MemoryStream();png.Save(stream);return stream.ToArray();
    }
    internal static async Task Populate(PlanningFoundationWpfTests.Fixture f)
    {
        f.Vm.NewPlanningName="合成摄影策划｜夜航者的玻璃花园与城市回声｜三日二十四镜执行提案";
        f.Vm.NewLocation="合成摄影棚 A／旧厂房 B／雨后街角 C";f.Vm.NewShootDate=new DateTime(2026,10,20);
        await f.Vm.CreatePlanningCommand.ExecuteAsync(null);Assert.IsTrue(f.Vm.HasProject);f.Id=f.Vm.ProjectId!.Value;
        f.Vm.DocumentPeople="林摄影（虚构）／陈灯光（虚构）／周造型（虚构）／演员甲乙（虚构）";
        f.Vm.ShootGoal="在三天内完成二十四个叙事镜头，保留长名称、跨行行动作和真实关联身份。";
        f.Vm.Keywords="冷暖对照、玻璃折射、人物停顿、连续运动、留白";
        f.Vm.ClientRequirements="全部人物、地址、设备与预算均为合成；不访问任何私人照片与正式项目。";
        f.Vm.MustCapture="温室逆光、长廊追踪、街角双人对手戏、道具特写";
        f.Vm.PlanningNotes="夜间安全区需隔离；跨日拆段，不把午夜后时间记在前一日。";
        f.Vm.OutputPurpose="离屏回归与公司审核；不是拍摄授权或真实支出。";
        var refs=new List<ProjectShotReference>();
        for(var i=0;i<3;i++) {var path=Path.Combine(f.Root,$"synthetic-reference-{i}.png");await File.WriteAllBytesAsync(path,ReferencePng(i));refs.Add(new(Guid.Parse($"20261014-0000-0000-0000-{i+1:000000000000}"),ShotReferenceKind.Storyboard,ExternalReference:path,Title:$"合成参考 {i+1} · {Scenes[i]}"));}
        await f.Vm.AddDocumentReferencesAsync(refs);
        for(var i=0;i<24;i++)
        {
            await f.Vm.NewShotCommand.ExecuteAsync(null);var shot=f.Vm.SelectedShot!;
            f.Vm.StageShot(shot with{Name=$"镜头 {i+1:00}｜{Scenes[i/8]}｜人物穿越光影边界后回眸并保留环境空间的超长分镜名称",Scene=Scenes[i/8],EstimatedMinutes=12+i%6,
                Action=$"第一拍：演员从画面左侧缓步进入，手持透明伞。\n第二拍：停顿两秒，抬眼看向{(i%2==0?"窗外":"镜头")}；肩部转动。\n第三拍：向后退半步，保持视线，等待灯光移动结束。",Props="透明伞／折叠椅／旧报纸",Lighting="主灯柔光45°＋轮廓灯蓝色＋反光板",ShotSize=i%2==0?"中景 → 近景":"全景 → 中景",CameraAndLens="合成相机 A · 50mm / 85mm · 稳定器低位",Remarks="对白与运动同步；留三秒空镜；同组参考必须匹配场景。",References=[refs[i/8]],PlanningFieldsVersion=1});
            Assert.IsTrue(await f.Vm.FlushAsync());
        }
        var ids=f.Vm.Shots.Select(s=>s.ShotId).ToArray();
        PlanningResource[] resources=[
            new PlanningProp{Name="透明长柄雨伞与备用伞面（合成）",Quantity=3,Category="手持道具",Preparation="提前贴标并擦拭反光面",ShotIds=ids[..8]},
            new PlanningProp{Name="木质折叠椅／旧报纸／便携反光板",Quantity=4,ShotIds=ids[8..16]},
            new PlanningGearItem{Name="主机 A＋定焦镜头组合",Model="SYNTH-A / 50mm / 85mm",Quantity=1,ShotIds=ids},
            new PlanningGearItem{Name="备机 B 与低位三轴稳定器",Model="SYNTH-B / GIMBAL",Quantity=2,ShotIds=ids[8..]},
            new PlanningCrewMember{Name="林摄影（虚构）",Role="摄影指导",Contact="synthetic@example.invalid",ArrivalLocal="2026-10-20 08:00",ShotIds=ids},
            new PlanningCrewMember{Name="陈灯光（虚构）",Role="灯光师",Contact="不含真实电话",ArrivalLocal="2026-10-20 07:30",ShotIds=ids},
            new PlanningCrewMember{Name="周造型（虚构）",Role="造型／场务",ArrivalLocal="2026-10-20 07:00",ShotIds=ids},
            new PlanningBudgetLine{Name="合成设备租赁三日",Planned=3600,Currency="CNY",ShotIds=ids},
            new PlanningBudgetLine{Name="合成场地与道具",Planned=2400,Currency="CNY",ShotIds=ids},
            new PlanningBudgetLine{Name="合成人员交通及夜间安全",Planned=1800,Currency="CNY",ShotIds=ids},
            new PlanningLight{Name="L1 大柔光主灯",X=.2,Y=.35,DirectionDegrees=45,PowerPercent=65,Modifier="120cm八角柔光箱",ShotIds=ids},
            new PlanningLight{Name="L2 蓝色轮廓灯",X=.75,Y=.25,DirectionDegrees=135,PowerPercent=40,Modifier="条形柔光箱＋蓝片",ShotIds=ids},
            new PlanningLight{Name="L3 背景移动灯",X=.65,Y=.8,DirectionDegrees=270,PowerPercent=25,Modifier="蜂巢＋遮扉",ShotIds=ids[16..]},
            new PlanningLight{Name="L4 暖色眼神补光",X=.35,Y=.7,DirectionDegrees=310,PowerPercent=15,Modifier="小型反射伞",ShotIds=ids[..16]}];
        foreach(var r in resources)Assert.IsTrue(await f.Vm.SaveResourceAsync(r));
        for(var day=0;day<3;day++)
        {
            Assert.IsTrue(await f.Vm.SaveResourceAsync(new PlanningVenue{Name=$"场地 {day+1}（合成）",Address=Scenes[day],Date=new DateOnly(2026,10,20+day),TimeZoneId="Asia/Shanghai",AvailableHours="08:00–23:59",Permit="合成测试，不代表实际许可",ShotIds=ids.Skip(day*8).Take(8).ToArray()}));
            Assert.IsTrue(await f.Vm.SaveResourceAsync(new PlanningScheduleSlot{Name=$"第 {day+1} 日主拍",Date=new DateOnly(2026,10,20+day),StartLocal="09:00",EndLocal="18:30",Location=Scenes[day],Owner="林摄影（虚构）",TimeZoneId="Asia/Shanghai",ShotIds=ids.Skip(day*8).Take(8).ToArray()}));
        }
        foreach(var (date,start,end) in new[]{(new DateOnly(2026,10,21),"23:00","23:59"),(new DateOnly(2026,10,22),"00:00","00:45")})
            Assert.IsTrue(await f.Vm.SaveResourceAsync(new PlanningScheduleSlot{Name="跨午夜夜景补拍（拆段）",Date=date,StartLocal=start,EndLocal=end,TimeZoneId="Asia/Shanghai",Location=Scenes[2],Owner="陈灯光（虚构）",ShotIds=ids[16..]}));
        await f.Vm.RefreshDocumentReferencesAsync();
    }
    internal static FlowDocument Proposal()
    {
        var doc=new FlowDocument();
        doc.Blocks.Add(new Paragraph(new Run("夜航者的玻璃花园 · 合成摄影提案")){Tag=new PlanningBlockSemantic(0,"Heading1")});
        doc.Blocks.Add(new Paragraph(new Run("版本 R6／固定数据配方 20261014／24 镜／三日／预算 CNY 7,800。所有人物与影像均为合成。")));
        using var image=new MemoryStream(ReferencePng(0));var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=image;bitmap.EndInit();bitmap.Freeze();doc.Blocks.Add(new BlockUIContainer(new Image{Source=bitmap,Width=400,Height=266}));
        var table=new Table();var group=new TableRowGroup();table.RowGroups.Add(group);
        foreach(var values in new[]{new[]{"日期","场景","镜头","费用 CNY"},new[]{"2026-10-20","温室","01–08","2,600"},new[]{"2026-10-21","旧厂房","09–16","2,600"},new[]{"2026-10-22","街角","17–24","2,600"}})
        {var row=new TableRow();foreach(var value in values)row.Cells.Add(new TableCell(new Paragraph(new Run(value))){BorderBrush=Brushes.Gray,BorderThickness=new Thickness(1)});group.Rows.Add(row);}doc.Blocks.Add(table);
        for(var page=1;page<=4;page++)
        {
            doc.Blocks.Add(new Paragraph(new Run($"第 {page} 章 · 叙事节奏与现场执行")){BreakPageBefore=true,Tag=new PlanningBlockSemantic(doc.Blocks.Count,"Heading2",true)});
            for(var i=0;i<12;i++)doc.Blocks.Add(new Paragraph(new Run($"{page}.{i+1:00} 先检查演员起点与相机机位，再确认主灯、轮廓灯和背景灯关系。长名称与多行动作不能挤进不可编辑的狭窄格，现场调整要回到同一镜头身份。保留停顿、转身和离场三个动作节点；不要将本合成内容当作已批准拍摄安排。")));
        }
        PlanningDocumentLayout.Apply(doc,new(new(),[],[]));return doc;
    }
    internal static void SaveProposal(PlanningCenterViewModel vm,FlowDocument doc)
    {using var stream=new MemoryStream();var text=new TextRange(doc.ContentStart,doc.ContentEnd);text.Save(stream,DataFormats.Rtf);vm.SaveRichText(Convert.ToBase64String(stream.ToArray()),text.Text,PlanningDocumentLayout.Capture(doc,new(),[]));}
}
