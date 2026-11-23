using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.ViewModels;

internal sealed class HolderObservationFinishedException : Exception
{ public HolderObservationFinishedException():base("Observation captured before second ushort allocation; not a capacity test."){} }

internal static class HolderTags
{
    private sealed record Entry(string Role,string Type,int Identity,string WeakHandle,long PayloadBytes,int Width,int Height);
    private static readonly List<Entry> Entries=[];
    private static readonly List<IntPtr> Handles=[];
    private static bool captured;
    private static readonly JsonSerializerOptions Json=new(){WriteIndented=true};
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Add(string role,object value,long bytes=0,int width=0,int height=0)
    {
        var handle=GCHandle.Alloc(value,GCHandleType.Weak);
        var pointer=GCHandle.ToIntPtr(handle);
        lock(Entries){Handles.Add(pointer);Entries.Add(new(role,value.GetType().FullName??value.GetType().Name,RuntimeHelpers.GetHashCode(value),"0x"+pointer.ToInt64().ToString("x"),bytes,width,height));}
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void CaptureReader(ushort[] values)=>Add("first_reader_ushort_array",values,(long)values.Length*2);
    private static object? Field(object value,string name)=>value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(value);
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void CaptureFirst(ReferenceColorWorkspaceViewModel workspace)
    {
        if(captured)return;captured=true;
        Add("first_workspace",workspace);
        Add("first_editor",workspace.Editor);
        var master=workspace.ActiveTarget?.RawMaster?.Image??throw new InvalidOperationException("No first-round legacy master");
        if(master.IsFloatMaterialized)throw new InvalidOperationException("Observation must not expand or silently replace the RGB48 source");
        Add("first_master_wrapper",master,master.StorageBytes,master.Width,master.Height);
        if(Field(master,"ownedRgb48") is not ReadOnlyMemory<ushort> words || !MemoryMarshal.TryGetArray(words,out ArraySegment<ushort> segment) || segment.Array is null)
            throw new InvalidOperationException("Candidate RGB48 backing not available as an existing array");
        Add("first_master_owned_ushort_array",segment.Array,(long)segment.Array.Length*2,master.Width,master.Height);
        foreach(var pair in new[]{("first_original_bitmap","_source"),("first_processed_bitmap","_matchedImage")})
        {
            if(Field(workspace.Editor,pair.Item2) is not BitmapSource image)throw new InvalidOperationException("Expected first full bitmap missing: "+pair.Item2);
            Add(pair.Item1,image,(long)image.PixelWidth*image.PixelHeight*image.Format.BitsPerPixel/8,image.PixelWidth,image.PixelHeight);
        }
        if(master.IsFloatMaterialized)throw new InvalidOperationException("Diagnostic expanded source unexpectedly");
    }
    public static void WriteMap(string root)
    {
        Entry[] entries;lock(Entries)entries=Entries.ToArray();
        if(entries.Length!=7)throw new InvalidOperationException("Expected exactly seven weak observation subjects");
        File.WriteAllText(Path.Combine(root,"WEAK_HANDLE_MAP.json"),JsonSerializer.Serialize(new{
            classification="DIAGNOSTIC_ONLY_BEFORE_SECOND_USHORT",
            handleKind="GCHandleType.Weak; not pinned or strong",
            noTargetDereferenceAtBoundary=true,noRgb32Access=true,
            gcCounts=new[]{GC.CollectionCount(0),GC.CollectionCount(1),GC.CollectionCount(2)},subjects=entries},Json));
    }
    public static void Complete(string root)=>File.WriteAllText(Path.Combine(root,"HOLDER_CAPTURE_COMPLETE.json"),JsonSerializer.Serialize(new{
        classification="DIAGNOSTIC_ONLY_NO_CAPACITY_VERDICT",secondUshortAllocated=false,
        gcCountsAfter=new[]{GC.CollectionCount(0),GC.CollectionCount(1),GC.CollectionCount(2)},forcedGc=false},Json));
    public static void FreeWeakHandles()
    {lock(Entries){foreach(var pointer in Handles)GCHandle.FromIntPtr(pointer).Free();Handles.Clear();}}
}
