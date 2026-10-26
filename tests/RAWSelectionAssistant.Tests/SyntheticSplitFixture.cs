using RAWSelectionAssistant.Core.Models;
namespace RAWSelectionAssistant.Tests;
internal static class SyntheticSplitFixture
{
    public static async Task<TutorialSandboxPaths> CreateAsync(string root)
    {
        var paths = new TutorialSandboxPaths(root, Path.Combine(root,"Source"), Path.Combine(root,"Source/JPG"),
            Path.Combine(root,"Source/RAW"), Path.Combine(root,"CustomerSelection"), Path.Combine(root,"Output"),
            Path.Combine(root,"CustomerSelection/DSC01234.JPG"), Path.Combine(root,"CustomerSelection/选片编号.txt"));
        Directory.CreateDirectory(paths.JpegSource); Directory.CreateDirectory(paths.RawSource);
        Directory.CreateDirectory(paths.CustomerSelection); Directory.CreateDirectory(paths.Output);
        var jpeg = Convert.FromBase64String("/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAP//////////////////////////////////////////////////////////////////////////////////////2wBDAf//////////////////////////////////////////////////////////////////////////////////////wAARCAAMABADASIAAhEBAxEB/8QAFQABAQAAAAAAAAAAAAAAAAAAAAf/xAAUEAEAAAAAAAAAAAAAAAAAAAAA/9oADAMBAAIQAxAAAAFQAP/EABQQAQAAAAAAAAAAAAAAAAAAABD/2gAIAQEAAQUCf//EABQRAQAAAAAAAAAAAAAAAAAAABD/2gAIAQMBAT8Bf//EABQRAQAAAAAAAAAAAAAAAAAAABD/2gAIAQIBAT8Bf//EABQQAQAAAAAAAAAAAAAAAAAAABD/2gAIAQEABj8Cf//EABQQAQAAAAAAAAAAAAAAAAAAABD/2gAIAQEAAT8hf//aAAwDAQACAAMAAAAQAP/EABQRAQAAAAAAAAAAAAAAAAAAABD/2gAIAQMBAT8Qf//EABQRAQAAAAAAAAAAAAAAAAAAABD/2gAIAQIBAT8Qf//EABQQAQAAAAAAAAAAAAAAAAAAABD/2gAIAQEAAT8Qf//Z");
        foreach (var number in new[]{"01234","01235","01236"})
        {
            await File.WriteAllBytesAsync(Path.Combine(paths.JpegSource,$"DSC{number}.JPG"),jpeg);
            await File.WriteAllTextAsync(Path.Combine(paths.RawSource,$"DSC{number}.ARW"),"synthetic match-only placeholder, not decoded RAW");
        }
        File.Copy(Path.Combine(paths.JpegSource,"DSC01234.JPG"),paths.CustomerJpeg);
        await File.WriteAllTextAsync(paths.SelectionText,"1235、DSC01236.JPG");
        return paths;
    }
}
