using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Bookings;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class ClientDisplayResolverTests
{
    [TestMethod]
    public void ResolverUsesExistingBookingAndProjectNamesWithoutSilentlyPickingOne()
    {
        var resolver = new ClientDisplayResolver();
        Assert.AreEqual("未关联", resolver.Resolve([], []));
        Assert.AreEqual("Alice", resolver.Resolve(["Alice"], []));
        Assert.AreEqual("多个客户（Alice、Bob）", resolver.Resolve(["Alice"], ["Bob", "Alice"]));
        Assert.AreEqual("真实客户", resolver.Resolve(["真实客户"], []));
    }
}
