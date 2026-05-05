using System.Reflection;
using System.Text.Json;
using AnytoneConfigBuilder.Core.Models;

namespace AnytoneConfigBuilder.Core.Readers;

public sealed class ChannelDefaultsReader
{
    private const string DefaultsResourceName = "AnytoneConfigBuilder.Core.Resources.channel-defaults.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public IReadOnlyList<ChannelFieldDefault> ReadDefaults()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var resourceStream = assembly.GetManifestResourceStream(DefaultsResourceName);
        if (resourceStream is null)
        {
            throw new InvalidOperationException($"Embedded resource '{DefaultsResourceName}' was not found.");
        }

        var defaults = JsonSerializer.Deserialize<List<ChannelFieldDefaultDto>>(resourceStream, JsonOptions);
        if (defaults is null)
        {
            throw new InvalidOperationException("Unable to deserialize embedded channel defaults.");
        }

        return defaults
            .Select(d => new ChannelFieldDefault(d.Index, d.FieldName ?? string.Empty, d.DefaultValue))
            .OrderBy(d => d.Index)
            .ToList();
    }

    private sealed class ChannelFieldDefaultDto
    {
        public int Index { get; set; }

        public string? FieldName { get; set; }

        public string? DefaultValue { get; set; }
    }
}
