using System;
using System.IO;

/// <summary>Explicit source-resource root, never inferred by walking assembly ancestors.</summary>
internal static class TestResources
{
    public static string Root => Resolve(Environment.GetEnvironmentVariable("KITAO_TEST_RESOURCE_ROOT"), AppContext.BaseDirectory);
    internal static string Resolve(string? configured,string assemblyDirectory)
    {
        string root;
        if(!string.IsNullOrWhiteSpace(configured))
        {
            if(!Path.IsPathFullyQualified(configured))throw new InvalidDataException("KITAO_TEST_RESOURCE_ROOT must be absolute.");
            root=Path.GetFullPath(configured);
        }
        else
        {
            var config=Path.Combine(assemblyDirectory,"test-resource-root.txt");
            if(!File.Exists(config))throw new FileNotFoundException("Set KITAO_TEST_RESOURCE_ROOT or supply test-resource-root.txt beside test assembly.",config);
            root=Path.GetFullPath(File.ReadAllText(config).Trim(),assemblyDirectory);
        }
        if(!File.Exists(Path.Combine(root,"RAWSelectionAssistant.sln"))||!File.Exists(Path.Combine(root,"src","RAWSelectionAssistant","App.xaml")))
            throw new DirectoryNotFoundException("Configured test resources are incomplete: "+root);
        return root;
    }
}
