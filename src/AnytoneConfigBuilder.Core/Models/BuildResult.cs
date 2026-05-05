namespace AnytoneConfigBuilder.Core.Models;

public sealed class BuildResult
{
    public bool Success { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public IReadOnlyList<string> Errors { get; init; } = [];
}
