namespace AnytoneConfigBuilder.Core.Models;

public enum SortMode
{
    Alpha,
    RepeatersFirst,
    AnalogFirst
}

public enum HotspotTxPermitMode
{
    SameColorCode,
    Always
}

public enum NicknameMode
{
    Off,
    Prefix,
    Suffix,
    PrefixForced,
    SuffixForced
}

public sealed record BuildOptions(
    SortMode SortMode,
    HotspotTxPermitMode HotspotTxPermitMode,
    NicknameMode NicknameMode
)
{
    public static BuildOptions Default { get; } = new(
        SortMode.Alpha,
        HotspotTxPermitMode.SameColorCode,
        NicknameMode.Off
    );
}
