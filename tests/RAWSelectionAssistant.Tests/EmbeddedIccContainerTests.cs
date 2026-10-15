using System.Buffers.Binary;
using RAWSelectionAssistant.Core.Services.Color.ControlledIcc;
namespace RAWSelectionAssistant.Tests;
[TestClass]
public sealed class EmbeddedIccContainerTests
{
    static byte[] Profile()=>File.ReadAllBytes(Path.Combine(TestResources.Root,"tests/RAWSelectionAssistant.Tests/TestData/controlled-linear-srgb.icc"));
    static byte[] Jpeg(byte[] profile,int chunks=1,int omit=0,bool duplicate=false)
    {
        using var s=new MemoryStream();s.Write(new byte[]{255,216});
        for(int c=1;c<=chunks;c++)
        {
            int start=(c-1)*profile.Length/chunks,end=c*profile.Length/chunks;if(c==omit)continue;
            using var seg=new MemoryStream();seg.Write("ICC_PROFILE\0"u8);seg.WriteByte((byte)c);seg.WriteByte((byte)chunks);seg.Write(profile.AsSpan(start,end-start));var b=seg.ToArray();
            for(int k=0;k<(duplicate&&c==1?2:1);k++){s.Write(new byte[]{255,226,(byte)((b.Length+2)>>8),(byte)(b.Length+2)});s.Write(b);}
        }
        s.Write(new byte[]{255,217});return s.ToArray();
    }
    static byte[] Tiff(byte[] profile,bool le,bool duplicate=false,bool next=false)
    {
        int count=duplicate?2:1,data=8+2+12*count+4;var b=new byte[data+profile.Length];b[0]=b[1]=(byte)(le?'I':'M');
        void U16(int o,ushort v){if(le)BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(o),v);else BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(o),v);}
        void U32(int o,uint v){if(le)BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(o),v);else BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(o),v);}
        U16(2,42);U32(4,8);U16(8,(ushort)count);for(int i=0;i<count;i++){int o=10+12*i;U16(o,34675);U16(o+2,7);U32(o+4,(uint)profile.Length);U32(o+8,(uint)data);}U32(10+12*count,next?8u:0);profile.CopyTo(b,data);return b;
    }
    [TestMethod] public void JpegSingleMultiMissingDuplicateAndDamagedProfileHaveStableOutcomes()
    {
        WithFiles((path)=>{var p=Profile();foreach(int n in new[]{1,2,3}){File.WriteAllBytes(path,Jpeg(p,n));Assert.AreEqual(ControlledIccProfile.Read(p).Sha256,EmbeddedIccInspector.Read(path).Sha256);}
        foreach(var b in new[]{Jpeg(p,3,2),Jpeg(p,2,duplicate:true)}){File.WriteAllBytes(path,b);Assert.AreEqual("ICC_CHUNKS",EmbeddedIccInspector.Inspect(path).RejectionCode);}
        p[0]=255;File.WriteAllBytes(path,Jpeg(p));Assert.AreEqual("ICC_LENGTH",EmbeddedIccInspector.Inspect(path).RejectionCode);
        File.WriteAllBytes(path,new byte[]{255,216,255,226,0,14}.Concat("ICC_PROFILE\0"u8.ToArray()).Concat(new byte[]{255,217}).ToArray());Assert.AreEqual("ICC_CHUNKS",EmbeddedIccInspector.Inspect(path).RejectionCode);
        });
    }
    [TestMethod] public void TiffBothEndianDuplicateAndOtherIfdPolicyIsExplicit()
    {
        WithFiles(path=>{foreach(bool le in new[]{true,false}){File.WriteAllBytes(path,Tiff(Profile(),le));Assert.IsNull(EmbeddedIccInspector.Inspect(path).RejectionCode);File.WriteAllBytes(path,Tiff(Profile(),le,duplicate:true));Assert.AreEqual("ICC_DUPLICATE_TAG",EmbeddedIccInspector.Inspect(path).RejectionCode);File.WriteAllBytes(path,Tiff(Profile(),le,next:true));Assert.AreEqual("ICC_CONTAINER",EmbeddedIccInspector.Inspect(path).RejectionCode);}});
    }
    static void WithFiles(Action<string> action){var root=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);try{action(Path.Combine(root,"container.bin"));}finally{Directory.Delete(root,true);}}
}
