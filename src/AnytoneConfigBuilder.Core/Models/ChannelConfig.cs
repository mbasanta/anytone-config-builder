namespace AnytoneConfigBuilder.Core.Models;

public sealed class ChannelConfig
{
    private readonly Dictionary<int, string> _values = [];

    public string ZoneNickname { get; set; } = string.Empty;

    public IReadOnlyDictionary<int, string> Values => _values;

    public string? GetValue(int index)
    {
        return _values.TryGetValue(index, out var value) ? value : null;
    }

    public void SetValue(int index, string value)
    {
        _values[index] = value;
    }
}
