using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.ViewModels;
public sealed partial class TetherReferenceModeViewModel
{
    // Numerical/reference and native acceptance are not release approval.
    public bool LocalCompositeEditingAvailable
    {
        get
        {
#if WHOLE_APP_DIAGNOSTIC
            return true;
#else
            return false;
#endif
        }
    }

    public double SelectedLayerOpacity{get=>SelectedAdjustmentNode?.Opacity??1;set{if(!LocalCompositeEditingAvailable||!double.IsFinite(value))return;if(SelectedAdjustmentNode is {} node)ChangeStack(nodes=>nodes.Select(n=>n.Id==node.Id?n with{CompositeVersion=Math.Max(1,n.CompositeVersion),Opacity=Math.Clamp(value,0,1)}:n).ToArray());OnPropertyChanged();}}
    public StudioLayerBlend SelectedLayerBlend{get=>SelectedAdjustmentNode?.Blend??StudioLayerBlend.Normal;set{if(!LocalCompositeEditingAvailable||!LayerBlendModes.Contains(value))return;if(SelectedAdjustmentNode is {} node)ChangeStack(nodes=>nodes.Select(n=>n.Id==node.Id?n with{CompositeVersion=(int)value>=3?2:Math.Max(1,n.CompositeVersion),Blend=value}:n).ToArray());OnPropertyChanged();}}
    public IReadOnlyList<StudioLayerBlend> LayerBlendModes{get;}=[StudioLayerBlend.Normal,StudioLayerBlend.Multiply,StudioLayerBlend.Screen,StudioLayerBlend.Darken,StudioLayerBlend.Lighten,StudioLayerBlend.Overlay,StudioLayerBlend.SoftLight,StudioLayerBlend.Color,StudioLayerBlend.Luminosity];
    public void AddMaskStroke(StudioMaskStroke stroke)
    {
        if(!LocalCompositeEditingAvailable)return;
        if(SelectedAdjustmentNode is not {} selected||!SelectedNodeEditable){StatusText="请选择可编辑图层，锁定层不能绘制蒙版。";return;}
        var mask=selected.LocalMask??new StudioLocalMask([]);if(mask.Strokes.Count>=2000||mask.Strokes.Sum(s=>s.Points.Count)+stroke.Points.Count>100000){StatusText="蒙版已达到2,000笔或100,000点限制，本笔未提交；请撤销或清空后继续。";return;}var changed=mask with{Strokes=mask.Strokes.Append(stroke.Freeze()).ToArray()};
        ChangeStack(nodes=>nodes.Select(n=>n.Id==selected.Id?n with{CompositeVersion=Math.Max(1,n.CompositeVersion),LocalMask=changed.Freeze()}:n).ToArray());OnPropertyChanged(nameof(SelectedAdjustmentNode));
    }
    public void ClearLocalMask(){if(!LocalCompositeEditingAvailable)return;if(SelectedAdjustmentNode is {} node)ChangeStack(nodes=>nodes.Select(n=>n.Id==node.Id?n with{CompositeVersion=Math.Max(1,n.CompositeVersion),LocalMask=new StudioLocalMask([])}:n).ToArray());}
    public void InvertLocalMask(){if(!LocalCompositeEditingAvailable)return;if(SelectedAdjustmentNode is {} node)ChangeStack(nodes=>nodes.Select(n=>n.Id==node.Id?n with{CompositeVersion=Math.Max(1,n.CompositeVersion),LocalMask=(node.LocalMask??new StudioLocalMask([])) with{Inverted=!(node.LocalMask?.Inverted??false)}}:n).ToArray());}
    public void RemoveLocalMask(){if(!LocalCompositeEditingAvailable)return;if(SelectedAdjustmentNode is {} node)ChangeStack(nodes=>nodes.Select(n=>n.Id==node.Id?n with{CompositeVersion=Math.Max(1,n.CompositeVersion),LocalMask=null}:n).ToArray());}
}
