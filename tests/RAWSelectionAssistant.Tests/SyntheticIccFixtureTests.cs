using System.Buffers.Binary;
using System.Security.Cryptography;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.ColorScience;
namespace RAWSelectionAssistant.Tests;
[TestClass]
public sealed class SyntheticIccFixtureTests
{
 private static byte[] Fixture()
 {
  var bytes=new byte[132+7*12+4*20+3*12];void U(int offset,uint value)=>BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset,4),value);void S(int offset,string value)=>System.Text.Encoding.ASCII.GetBytes(value).CopyTo(bytes,offset);void F(int offset,double value)=>BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(offset,4),(int)Math.Round(value*65536));
  U(0,(uint)bytes.Length);bytes[8]=2;S(12,"mntr");S(16,"RGB ");S(20,"XYZ ");S(36,"acsp");F(68,.9642);F(72,1);F(76,.8249);U(128,7);
  int cursor=216;var xyzes=new[]{new ColorVector3(.7976749,.2880402,0),new ColorVector3(.1351917,.7118741,0),new ColorVector3(.0313534,.0000857,.8249),new ColorVector3(.9642,1,.8249)};var names=new[]{"rXYZ","gXYZ","bXYZ","wtpt","rTRC","gTRC","bTRC"};
  for(int i=0;i<7;i++){S(132+i*12,names[i]);U(136+i*12,(uint)cursor);U(140+i*12,(uint)(i<4?20:12));S(cursor,i<4?"XYZ ":"curv");if(i<4){F(cursor+8,xyzes[i].X);F(cursor+12,xyzes[i].Y);F(cursor+16,xyzes[i].Z);}cursor+=i<4?20:12;}return bytes;
 }
 [TestMethod] public void ActualSyntheticIccBinaryHasKnownD50LinearContractAndComputedReference()
 {
  var bytes=Fixture();var hash=Convert.ToHexString(SHA256.HashData(bytes));var profile=SyntheticIccMatrixReader.Read(bytes,hash,SyntheticIccMatrixReader.FixtureId);var white=SyntheticIccMatrixReader.ToLinearSrgb(new(1,1,1),profile);Assert.AreEqual(1,white.X,.003);Assert.AreEqual(1,white.Y,.003);Assert.AreEqual(1,white.Z,.003);
  // Published-independent decimal Bradford reference for [.2,.2,.2] is neutral .2; gamma is explicitly linear.
  var gray=SyntheticIccMatrixReader.ToLinearSrgb(new(.2,.2,.2),profile);Assert.AreEqual(.2,gray.X,.001);Assert.AreEqual(.2,gray.Y,.001);Assert.AreEqual(.2,gray.Z,.001);
  Assert.Throws<NotSupportedException>(()=>SyntheticIccMatrixReader.Read(bytes,hash,"CameraICC"));Assert.Throws<InvalidDataException>(()=>SyntheticIccMatrixReader.Read(bytes,new string('a',64),SyntheticIccMatrixReader.FixtureId));
  var encoded=(byte[])bytes.Clone();BinaryPrimitives.WriteUInt32BigEndian(encoded.AsSpan(216+80+8,4),1);Assert.Throws<NotSupportedException>(()=>SyntheticIccMatrixReader.Read(encoded,Convert.ToHexString(SHA256.HashData(encoded)),SyntheticIccMatrixReader.FixtureId));
 }
 [TestMethod] public void UnknownLutAndEscapingIccTagsRefuseInsteadOfFallback()
 {
  var bytes=Fixture();System.Text.Encoding.ASCII.GetBytes("A2B0").CopyTo(bytes,132);Assert.Throws<InvalidDataException>(()=>SyntheticIccMatrixReader.Read(bytes,Convert.ToHexString(SHA256.HashData(bytes)),SyntheticIccMatrixReader.FixtureId));
  bytes=Fixture();BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(136,4),uint.MaxValue);Assert.Throws<InvalidDataException>(()=>SyntheticIccMatrixReader.Read(bytes,Convert.ToHexString(SHA256.HashData(bytes)),SyntheticIccMatrixReader.FixtureId));
 }
}
