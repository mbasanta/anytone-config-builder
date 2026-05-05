using AnytoneConfigBuilder.Core.Models;
using AnytoneConfigBuilder.Core.Processing;
using AnytoneConfigBuilder.Core.Readers;

namespace AnytoneConfigBuilder.Core;

public sealed class ConfigBuilder
{
    private readonly ChannelDefaultsReader _channelDefaultsReader;
    private readonly TalkgroupsReader _talkgroupsReader;

    public ConfigBuilder()
        : this(new ChannelDefaultsReader(), new TalkgroupsReader())
    {
    }

    public ConfigBuilder(ChannelDefaultsReader channelDefaultsReader, TalkgroupsReader talkgroupsReader)
    {
        _channelDefaultsReader = channelDefaultsReader;
        _talkgroupsReader = talkgroupsReader;
    }

    public IReadOnlyList<ChannelFieldDefault> GetChannelDefaults()
    {
        return _channelDefaultsReader.ReadDefaults();
    }

    public BuildResult Build(
        BuildOptions options,
        Stream analogCsv,
        Stream digitalOthersCsv,
        Stream digitalRepeatersCsv,
        Stream talkgroupsCsv,
        string outputDirectory)
    {
        var pipeline = new BuildPipeline(_channelDefaultsReader, _talkgroupsReader);
        return pipeline.Build(options, analogCsv, digitalOthersCsv, digitalRepeatersCsv, talkgroupsCsv, outputDirectory);
    }
}
