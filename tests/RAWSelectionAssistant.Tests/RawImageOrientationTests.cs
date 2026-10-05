using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Tests;
[TestClass]
public sealed class RawImageOrientationTests
{
    [TestMethod]
    public void AllExifTransformsKeepTrueFloatSamplesAndBecomeIdentity()
    {
        var expected = new Dictionary<int,int[]> { [2]=[2,1,0,5,4,3], [3]=[5,4,3,2,1,0], [4]=[3,4,5,0,1,2], [5]=[0,3,1,4,2,5], [6]=[3,0,4,1,5,2], [7]=[5,2,4,1,3,0], [8]=[2,5,1,4,0,3] };
        foreach(var (orientation,indices) in expected)
        {
            var values=Enumerable.Range(0,6).SelectMany(i=>new[]{i/10f+.00001f,i/10f+.00002f,i/10f+.00003f}).ToArray();
            var source=new HighBitDepthImageBuffer(3,2,values,orientation:(ushort)orientation);
            var upright=RawImageOrientation.NormalizePixels(source);
            Assert.AreEqual(orientation>=5?2:3,upright.Width); Assert.AreEqual((ushort)1,upright.Orientation);
            for(var i=0;i<6;i++)for(var c=0;c<3;c++)Assert.AreEqual(values[indices[i]*3+c],upright.Rgb32.Span[i*3+c]);
            Assert.AreSame(upright,RawImageOrientation.NormalizePixels(upright));
        }
    }
}
