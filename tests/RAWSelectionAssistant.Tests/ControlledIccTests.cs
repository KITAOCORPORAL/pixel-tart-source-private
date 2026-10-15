using System.Buffers.Binary;
using System.Security.Cryptography;
using RAWSelectionAssistant.Core.Services.Color.ControlledIcc;
namespace RAWSelectionAssistant.Tests;
[TestClass]
public sealed class ControlledIccTests
{
    private static byte[] Profile()
    {
        var names=new[]{"rXYZ","gXYZ","bXYZ","wtpt","rTRC","gTRC","bTRC"};var b=new byte[216+80+36];
        void U(int o,uint v)=>BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(o),v);
        void S(int o,string v)=>System.Text.Encoding.ASCII.GetBytes(v).CopyTo(b,o);
        U(0,(uint)b.Length);b[8]=2;S(12,"mntr");S(16,"RGB ");S(20,"XYZ ");S(36,"acsp");U(128,7);
        int pos=216;for(int i=0;i<7;i++){S(132+i*12,names[i]);U(136+i*12,(uint)pos);U(140+i*12,(uint)(i<4?20:12));S(pos,i<4?"XYZ ":"curv");pos+=i<4?20:12;}return b;
    }
    static void Refuses(byte[] b,string code){var e=Assert.Throws<ControlledIccException>(()=>ControlledIccProfile.Read(b));Assert.AreEqual(code,e.Code);}
    [TestMethod] public void StrictProfileRejectsLengthsBoundsUnknownVersionIntentAndLut()
    {
        var b=Profile();var p=ControlledIccProfile.Read(b);Assert.AreEqual(Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant(),p.Sha256);var copy=p.CopyBytes();copy[0]=255;Assert.AreNotEqual(copy[0],p.CopyBytes()[0]);
        b=Profile();BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(),uint.MaxValue);Refuses(b,"ICC_LENGTH");
        b=Profile();BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(136),uint.MaxValue);Refuses(b,"ICC_TAG_BOUNDS");
        b=Profile();b[8]=5;Refuses(b,"ICC_VERSION");b=Profile();BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(64),9);Refuses(b,"ICC_INTENT");
        b=Profile();"A2B0"u8.CopyTo(b.AsSpan(132));Refuses(b,"ICC_UNSUPPORTED_PROFILE");Refuses(new byte[ControlledIccProfile.MaximumBytes+1],"ICC_TOO_LARGE");
        using var c=new CancellationTokenSource();c.Cancel();Assert.Throws<OperationCanceledException>(()=>ControlledIccProfile.Read(Profile(),c.Token));
    }
    [TestMethod] public void DefaultOffAndRecipeCannotMigrateLegacyVersions()
    {
        Assert.AreEqual("ICC_DISABLED",Assert.Throws<ControlledIccException>(()=>new ControlledIccTransform(new(false,"unused","",[]))).Code);
        Assert.AreEqual("ICC_INTENT",Assert.Throws<ControlledIccException>(()=>new ControlledIccTransform(new(true,"unused","",[],Intent:3))).Code);
        var h=new string('a',64);var recipe=new ControlledIccRecipe(ControlledIccTransform.AlgorithmVersion,h,h,h,h,-1);
        CollectionAssert.AreEqual(new float[]{0,.125f,.5f},recipe.ApplyFrozenEdit(new float[]{0,.25f,1}));
        Assert.Throws<ControlledIccException>(()=>(recipe with{AlgorithmVersion="PV4"}).Validate());Assert.Throws<ControlledIccException>(()=>recipe.ApplyFrozenEdit(new float[]{-1,0,0}));
    }
    [TestMethod] public void EmbeddedInspectorDistinguishesMissingAndCorruptProfileWithoutFallback()
    {
        string root=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try{string path=Path.Combine(root,"a.jpg");File.WriteAllBytes(path,[255,216,255,217]);Assert.AreEqual("ICC_MISSING",Assert.Throws<ControlledIccException>(()=>EmbeddedIccInspector.Read(path)).Code);
        File.WriteAllBytes(path,[255,216,255,226,255,255]);Assert.AreEqual("ICC_CONTAINER",Assert.Throws<ControlledIccException>(()=>EmbeddedIccInspector.Read(path)).Code);
        }finally{Directory.Delete(root,true);}
    }
}
