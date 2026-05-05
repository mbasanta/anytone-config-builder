namespace AnytoneConfigBuilder.Core.Models;

public sealed record ChannelFieldDefault(
    int Index,
    string FieldName,
    string? DefaultValue
);
