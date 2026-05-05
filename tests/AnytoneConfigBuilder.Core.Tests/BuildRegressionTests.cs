using AnytoneConfigBuilder.Core;
using AnytoneConfigBuilder.Core.Models;

namespace AnytoneConfigBuilder.Core.Tests;

public sealed class BuildRegressionTests
{
    [Fact]
    public void Build_WithSampleInputs_GeneratesExpectedOutputFiles()
    {
        var repoRoot = TestHelpers.FindRepoRoot();
        var inputDir = Path.Combine(repoRoot, "input-csv");
        var outputDir = TestHelpers.CreateTempDirectory();

        try
        {
            using var analog = File.OpenRead(Path.Combine(inputDir, "Analog.csv"));
            using var digitalOthers = File.OpenRead(Path.Combine(inputDir, "Digital-Others.csv"));
            using var digitalRepeaters = File.OpenRead(Path.Combine(inputDir, "Digital-Repeaters.csv"));
            using var talkgroups = File.OpenRead(Path.Combine(inputDir, "TalkGroups.csv"));

            var builder = new ConfigBuilder();
            var result = builder.Build(
                BuildOptions.Default,
                analog,
                digitalOthers,
                digitalRepeaters,
                talkgroups,
                outputDir);

            Assert.True(result.Success, string.Join(Environment.NewLine, result.Errors));
            Assert.Empty(result.Errors);

            TestHelpers.AssertOutputFileExists(outputDir, "channels.csv");
            TestHelpers.AssertOutputFileExists(outputDir, "zones.csv");
            TestHelpers.AssertOutputFileExists(outputDir, "scanlists.csv");
            TestHelpers.AssertOutputFileExists(outputDir, "talkgroups.csv");
        }
        finally
        {
            Directory.Delete(outputDir, recursive: true);
        }
    }
}
