namespace AnytoneConfigBuilder.Core.Tests;

internal static class TestHelpers
{
    public static void AssertOutputFileExists(string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);
        Assert.True(File.Exists(path), $"Expected output file '{fileName}' was not generated.");
        Assert.True(new FileInfo(path).Length > 0, $"Expected output file '{fileName}' to be non-empty.");
    }

    public static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "acb-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    public static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var inputCsv = Path.Combine(dir.FullName, "input-csv");
            var src = Path.Combine(dir.FullName, "src");
            if (Directory.Exists(inputCsv) && Directory.Exists(src))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from test execution directory.");
    }
}
