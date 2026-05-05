using AnytoneConfigBuilder.Core;
using AnytoneConfigBuilder.Core.Models;

namespace AnytoneConfigBuilder.Core.Tests;

public sealed class UndoRegressionTests
{
    [Fact]
    public void Undo_WithGeneratedOutputs_ReconstructsExpectedInputShape()
    {
        var repoRoot = TestHelpers.FindRepoRoot();
        var inputDir = Path.Combine(repoRoot, "input-csv");
        var buildOutputDir = TestHelpers.CreateTempDirectory();
        var undoOutputDir = TestHelpers.CreateTempDirectory();

        try
        {
            using (var analog = File.OpenRead(Path.Combine(inputDir, "Analog.csv")))
            using (var digitalOthers = File.OpenRead(Path.Combine(inputDir, "Digital-Others.csv")))
            using (var digitalRepeaters = File.OpenRead(Path.Combine(inputDir, "Digital-Repeaters.csv")))
            using (var talkgroups = File.OpenRead(Path.Combine(inputDir, "TalkGroups.csv")))
            {
                var builder = new ConfigBuilder();
                var result = builder.Build(
                    BuildOptions.Default,
                    analog,
                    digitalOthers,
                    digitalRepeaters,
                    talkgroups,
                    buildOutputDir);

                Assert.True(result.Success, string.Join(Environment.NewLine, result.Errors));
            }

            var undoBuilder = new UndoBuilder();
            undoBuilder.Run(
                Path.Combine(buildOutputDir, "channels.csv"),
                Path.Combine(buildOutputDir, "zones.csv"),
                Path.Combine(buildOutputDir, "talkgroups.csv"),
                undoOutputDir);

            var analogOut = Path.Combine(undoOutputDir, "analog.csv");
            var digitalOthersOut = Path.Combine(undoOutputDir, "digital-others.csv");
            var digitalRepeatersOut = Path.Combine(undoOutputDir, "digital-repeaters.csv");
            var talkgroupsOut = Path.Combine(undoOutputDir, "talkgroups.csv");

            TestHelpers.AssertOutputFileExists(undoOutputDir, "analog.csv");
            TestHelpers.AssertOutputFileExists(undoOutputDir, "digital-others.csv");
            TestHelpers.AssertOutputFileExists(undoOutputDir, "digital-repeaters.csv");
            TestHelpers.AssertOutputFileExists(undoOutputDir, "talkgroups.csv");

            Assert.Contains("\"Zone\"", File.ReadLines(analogOut).First());
            Assert.Contains("\"Zone\"", File.ReadLines(digitalOthersOut).First());
            Assert.Contains("\"Zone Name\"", File.ReadLines(digitalRepeatersOut).First());

            Assert.Equal(File.ReadLines(Path.Combine(inputDir, "Analog.csv")).Count(), File.ReadLines(analogOut).Count());
            Assert.Equal(File.ReadLines(Path.Combine(inputDir, "Digital-Others.csv")).Count(), File.ReadLines(digitalOthersOut).Count());
            Assert.Equal(File.ReadLines(Path.Combine(inputDir, "Digital-Repeaters.csv")).Count(), File.ReadLines(digitalRepeatersOut).Count());

            var sourceTalkgroupLines = File.ReadLines(Path.Combine(inputDir, "TalkGroups.csv")).Count();
            var rebuiltTalkgroupLines = File.ReadLines(talkgroupsOut).Count();
            Assert.InRange(rebuiltTalkgroupLines, 1, sourceTalkgroupLines);
        }
        finally
        {
            Directory.Delete(buildOutputDir, recursive: true);
            Directory.Delete(undoOutputDir, recursive: true);
        }
    }

    [Fact]
    public void Undo_RepeaterPrivateCall_WritesMatrixCellWithPrivateMarker()
    {
        var tempDir = TestHelpers.CreateTempDirectory();

        try
        {
            var channelsPath = Path.Combine(tempDir, "channels.csv");
            var zonesPath = Path.Combine(tempDir, "zones.csv");
            var talkgroupsPath = Path.Combine(tempDir, "talkgroups.csv");
            var outputDir = Path.Combine(tempDir, "out");
            Directory.CreateDirectory(outputDir);

            File.WriteAllText(channelsPath,
                "\"Channel Name\",\"Receive Frequency\",\"Transmit Frequency\",\"Channel Type\",\"Transmit Power\",\"Band Width\",\"CTCSS/DCS Decode\",\"CTCSS/DCS Encode\",\"Contact\",\"Contact Call Type\",\"Busy Lock/TX Permit\",\"Color Code\",\"Slot\",\"TX Prohibit\"\r\n" +
                "\"TG 1\",\"440.000\",\"445.000\",\"D-Digital\",\"High\",\"12.5K\",\"Off\",\"Off\",\"TG1\",\"Private Call\",\"Same Color Code\",\"1\",\"2\",\"Off\"\r\n" +
                "\"TG 2\",\"440.000\",\"445.000\",\"D-Digital\",\"High\",\"12.5K\",\"Off\",\"Off\",\"TG2\",\"Group Call\",\"Same Color Code\",\"1\",\"1\",\"Off\"\r\n" +
                "\"TG 3\",\"440.000\",\"445.000\",\"D-Digital\",\"High\",\"12.5K\",\"Off\",\"Off\",\"TG3\",\"Group Call\",\"Same Color Code\",\"1\",\"1\",\"Off\"\r\n" +
                "\"TG 4\",\"440.000\",\"445.000\",\"D-Digital\",\"High\",\"12.5K\",\"Off\",\"Off\",\"TG4\",\"Group Call\",\"Same Color Code\",\"1\",\"1\",\"Off\"\r\n" +
                "\"TG 5\",\"440.000\",\"445.000\",\"D-Digital\",\"High\",\"12.5K\",\"Off\",\"Off\",\"TG5\",\"Group Call\",\"Same Color Code\",\"1\",\"1\",\"Off\"\r\n" +
                "\"TG 6\",\"440.000\",\"445.000\",\"D-Digital\",\"High\",\"12.5K\",\"Off\",\"Off\",\"TG6\",\"Group Call\",\"Same Color Code\",\"1\",\"1\",\"Off\"\r\n");

            File.WriteAllText(zonesPath,
                "\"Zone Name\",\"Zone Channel Member\",\"Zone Channel Member RX Frequency\",\"Zone Channel Member TX Frequency\"\r\n" +
                "\"Repeater One\",\"TG 1|TG 2|TG 3|TG 4|TG 5|TG 6\",\"440.000|440.000|440.000|440.000|440.000|440.000\",\"445.000|445.000|445.000|445.000|445.000|445.000\"\r\n");

            File.WriteAllText(talkgroupsPath,
                "\"No.\",\"Radio ID\",\"Name\",\"Country\",\"Remarks\",\"Call Type\",\"Call Alert\"\r\n" +
                "\"1\",\"1001\",\"TG1\",\"\",\"\",\"Private Call\",\"None\"\r\n");

            var undo = new UndoBuilder();
            undo.Run(channelsPath, zonesPath, talkgroupsPath, outputDir);

            var digitalRepeatersOut = Path.Combine(outputDir, "digital-repeaters.csv");
            Assert.True(File.Exists(digitalRepeatersOut));

            var content = File.ReadAllText(digitalRepeatersOut);
            Assert.Contains("\"2;P\"", content);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Undo_RepeaterMatrix_MixesOffGroupAndPrivateValues()
    {
        var tempDir = TestHelpers.CreateTempDirectory();

        try
        {
            var channelsPath = Path.Combine(tempDir, "channels.csv");
            var zonesPath = Path.Combine(tempDir, "zones.csv");
            var talkgroupsPath = Path.Combine(tempDir, "talkgroups.csv");
            var outputDir = Path.Combine(tempDir, "out");
            Directory.CreateDirectory(outputDir);

            File.WriteAllText(channelsPath,
                "\"Channel Name\",\"Receive Frequency\",\"Transmit Frequency\",\"Channel Type\",\"Transmit Power\",\"Band Width\",\"CTCSS/DCS Decode\",\"CTCSS/DCS Encode\",\"Contact\",\"Contact Call Type\",\"Busy Lock/TX Permit\",\"Color Code\",\"Slot\",\"TX Prohibit\"\r\n" +
                "\"RA TG-A\",\"440.000\",\"445.000\",\"D-Digital\",\"High\",\"12.5K\",\"Off\",\"Off\",\"TG-A\",\"Group Call\",\"Same Color Code\",\"1\",\"1\",\"Off\"\r\n" +
                "\"RA TG-B\",\"440.000\",\"445.000\",\"D-Digital\",\"High\",\"12.5K\",\"Off\",\"Off\",\"TG-B\",\"Private Call\",\"Same Color Code\",\"1\",\"2\",\"Off\"\r\n" +
                "\"RA TG-C\",\"440.000\",\"445.000\",\"D-Digital\",\"High\",\"12.5K\",\"Off\",\"Off\",\"TG-C\",\"Group Call\",\"Same Color Code\",\"1\",\"1\",\"Off\"\r\n" +
                "\"RA TG-D\",\"440.000\",\"445.000\",\"D-Digital\",\"High\",\"12.5K\",\"Off\",\"Off\",\"TG-D\",\"Group Call\",\"Same Color Code\",\"1\",\"1\",\"Off\"\r\n" +
                "\"RA TG-E\",\"440.000\",\"445.000\",\"D-Digital\",\"High\",\"12.5K\",\"Off\",\"Off\",\"TG-E\",\"Group Call\",\"Same Color Code\",\"1\",\"1\",\"Off\"\r\n" +
                "\"RA TG-F\",\"440.000\",\"445.000\",\"D-Digital\",\"High\",\"12.5K\",\"Off\",\"Off\",\"TG-F\",\"Group Call\",\"Same Color Code\",\"1\",\"1\",\"Off\"\r\n" +
                "\"RB TG-C\",\"441.000\",\"446.000\",\"D-Digital\",\"High\",\"12.5K\",\"Off\",\"Off\",\"TG-C\",\"Group Call\",\"Same Color Code\",\"1\",\"1\",\"Off\"\r\n" +
                "\"RB TG-D\",\"441.000\",\"446.000\",\"D-Digital\",\"High\",\"12.5K\",\"Off\",\"Off\",\"TG-D\",\"Group Call\",\"Same Color Code\",\"1\",\"1\",\"Off\"\r\n" +
                "\"RB TG-E\",\"441.000\",\"446.000\",\"D-Digital\",\"High\",\"12.5K\",\"Off\",\"Off\",\"TG-E\",\"Group Call\",\"Same Color Code\",\"1\",\"1\",\"Off\"\r\n" +
                "\"RB TG-F\",\"441.000\",\"446.000\",\"D-Digital\",\"High\",\"12.5K\",\"Off\",\"Off\",\"TG-F\",\"Group Call\",\"Same Color Code\",\"1\",\"1\",\"Off\"\r\n" +
                "\"RB TG-G\",\"441.000\",\"446.000\",\"D-Digital\",\"High\",\"12.5K\",\"Off\",\"Off\",\"TG-G\",\"Group Call\",\"Same Color Code\",\"1\",\"1\",\"Off\"\r\n" +
                "\"RB TG-H\",\"441.000\",\"446.000\",\"D-Digital\",\"High\",\"12.5K\",\"Off\",\"Off\",\"TG-H\",\"Group Call\",\"Same Color Code\",\"1\",\"1\",\"Off\"\r\n");

            File.WriteAllText(zonesPath,
                "\"Zone Name\",\"Zone Channel Member\",\"Zone Channel Member RX Frequency\",\"Zone Channel Member TX Frequency\"\r\n" +
                "\"Repeater A\",\"RA TG-A|RA TG-B|RA TG-C|RA TG-D|RA TG-E|RA TG-F\",\"440.000|440.000|440.000|440.000|440.000|440.000\",\"445.000|445.000|445.000|445.000|445.000|445.000\"\r\n" +
                "\"Repeater B\",\"RB TG-C|RB TG-D|RB TG-E|RB TG-F|RB TG-G|RB TG-H\",\"441.000|441.000|441.000|441.000|441.000|441.000\",\"446.000|446.000|446.000|446.000|446.000|446.000\"\r\n");

            File.WriteAllText(talkgroupsPath,
                "\"No.\",\"Radio ID\",\"Name\",\"Country\",\"Remarks\",\"Call Type\",\"Call Alert\"\r\n" +
                "\"1\",\"2001\",\"TG-A\",\"\",\"\",\"Group Call\",\"None\"\r\n" +
                "\"2\",\"2002\",\"TG-B\",\"\",\"\",\"Private Call\",\"None\"\r\n");

            var undo = new UndoBuilder();
            undo.Run(channelsPath, zonesPath, talkgroupsPath, outputDir);

            var digitalRepeatersOut = Path.Combine(outputDir, "digital-repeaters.csv");
            Assert.True(File.Exists(digitalRepeatersOut));

            var lines = File.ReadAllLines(digitalRepeatersOut);
            Assert.True(lines.Length >= 3);

            var header = lines[0];
            var repeaterARow = lines.First(l => l.Contains("\"Repeater A\"", StringComparison.Ordinal));

            Assert.Contains("\"TG-A\"", header);
            Assert.Contains("\"TG-B\"", header);
            Assert.Contains("\"TG-H\"", header);

            Assert.Contains("\"1\"", repeaterARow);
            Assert.Contains("\"2;P\"", repeaterARow);
            Assert.Contains("\"-\"", repeaterARow);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
