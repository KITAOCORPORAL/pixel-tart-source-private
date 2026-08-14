using System.Security.Cryptography;
using PixelTart.SelectionApi.Contracts;
using PixelTart.SelectionApi.Server;

namespace PixelTart.SelectionApi.Tests;

[TestClass]
public sealed class SelectionApiContractTests
{
    [TestMethod]
    public void LocalServerContract_IsExplicitlyNonProduction()
    {
        var server = new SelectionApiServerContract();
        Assert.IsFalse(server.IsProductionConfigured);
        Assert.IsFalse(server.StartsListener);
        CollectionAssert.Contains(server.Routes.ToArray(), $"POST {SelectionApiRouteNames.ClientConfirm}");
        CollectionAssert.Contains(server.Routes.ToArray(), $"GET {SelectionApiRouteNames.ClientAssets}");
    }

    [TestMethod]
    public async Task LocalObjectStorage_IsAtomicAndRejectsTraversal()
    {
        using var root = new TempRoot();
        var storage = new LocalSelectionObjectStorage(root.Path);
        var payload = new MemoryStream("proxy-bytes"u8.ToArray());
        var write = await storage.PutAsync("project-a/asset-1.jpg", payload);
        Assert.AreEqual("project-a/asset-1.jpg", write.ObjectKey);
        Assert.AreEqual("proxy-bytes"u8.Length, write.Bytes);
        Assert.AreEqual(Convert.ToHexString(SHA256.HashData("proxy-bytes"u8.ToArray())).ToLowerInvariant(), write.Sha256);
        await using (var read = await storage.OpenReadAsync(write.ObjectKey))
        {
            Assert.IsNotNull(read);
            using var copy = new MemoryStream();
            await read!.CopyToAsync(copy);
            CollectionAssert.AreEqual("proxy-bytes"u8.ToArray(), copy.ToArray());
        }
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => storage.PutAsync("../outside.txt", new MemoryStream([1])));
        Assert.IsTrue(await storage.DeleteAsync(write.ObjectKey));
        Assert.IsFalse(await storage.DeleteAsync(write.ObjectKey));
    }

    [TestMethod]
    public void ContractCarriesStableSourceIdentityAndVersion()
    {
        var source = Guid.NewGuid();
        var request = new CreateAssetUploadRequest(Guid.NewGuid(), "IMG_001.JPG", 12, "image/jpeg") { SourceAssetId = source };
        var choice = new SelectionChoiceRequest(true, true) { ExpectedVersion = 3 };
        Assert.AreEqual(source, request.SourceAssetId);
        Assert.AreEqual(3, choice.ExpectedVersion);
    }

    private sealed class TempRoot : IDisposable
    {
        public TempRoot()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PixelTart.SelectionApi.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try { Directory.Delete(Path, true); } catch { }
        }
    }
}
