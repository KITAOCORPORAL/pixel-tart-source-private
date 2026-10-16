using RAWSelectionAssistant.Core.Services.Color.DisplayPreview;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class DisplayPreviewBoundaryTests
{
    private sealed class Transform : IDisplayPreviewTransform
    {
        public string EngineIdentity => "TestDoubleNotAColorReference";
        public int Calls;
        public ushort[] Convert(ReadOnlySpan<ushort> input, ReadOnlySpan<byte> source, ReadOnlySpan<byte> target,
            DisplayPreviewContract contract, CancellationToken token) { Calls++; return input.ToArray().Select(v => (ushort)(65535-v)).ToArray(); }
    }
    [TestMethod]
    public void DisabledMissingAndBudgetNeverPretendCalibration()
    {
        var transform = new Transform();var cache = new DisplayPreviewCache(transform, 6);
        Assert.AreEqual("DisabledOriginalSrgbUnchanged", cache.Convert(false,"monitor",[1,2,3],1,1,[1],[2]).State);
        Assert.AreEqual("MissingProfileUncalibrated", cache.Convert(true,"monitor",[1,2,3],1,1,[],[2]).State);
        Assert.AreEqual("DisplayBudgetRefused", cache.Convert(true,"monitor",[1,2,3,4,5,6],2,1,[1],[2]).State);
        Assert.AreEqual(0,transform.Calls);
    }
    [TestMethod]
    public void DeviceProfileSourceAndExplicitInvalidationCannotReuseStalePixels()
    {
        var transform = new Transform();var cache = new DisplayPreviewCache(transform);ushort[] source=[1,2,3];
        var a=cache.Convert(true,"one",source,1,1,[1],[2]);
        Assert.AreEqual("ExperimentalCacheHit",cache.Convert(true,"one",source,1,1,[1],[2]).State);
        Assert.AreEqual(1,transform.Calls);
        _=cache.Convert(true,"two",source,1,1,[1],[2]);
        _=cache.Convert(true,"two",source,1,1,[1],[3]);
        _=cache.Convert(true,"two",[1,2,4],1,1,[1],[3]);
        _=cache.Convert(true,"two",[1,2,4],1,1,[4],[3]);
        Assert.AreEqual(5,transform.Calls);
        cache.Invalidate();_=cache.Convert(true,"two",[1,2,4],1,1,[4],[3]);Assert.AreEqual(6,transform.Calls);
        CollectionAssert.AreEqual(new ushort[]{1,2,3},source);
        Assert.IsFalse(a.Pixels.Span.SequenceEqual(source));
    }
    [TestMethod]
    public void RejectUnreviewedContractAndObserveCancellation()
    {
        var cache=new DisplayPreviewCache(new Transform());
        Assert.Throws<NotSupportedException>(()=>cache.Convert(true,"one",[1,2,3],1,1,[1],[2],new(AlphaPolicy:"Premultiplied")));
        Assert.Throws<ArgumentException>(()=>cache.Convert(true,"one",[1,2],1,1,[1],[2]));
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(()=>cache.Convert(true,"one",[1,2,3],1,1,[1],[2],token:cancellation.Token));
    }
}
