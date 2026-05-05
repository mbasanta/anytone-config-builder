namespace AnytoneConfigBuilder.Core.Constants;

public static class ChannelValues
{
    public const string Digital = "D-Digital";
    public const string Analog = "A-Analog";
    public const string NoTimeSlot = "-";

    public const string TxPermitChannelFree = "ChannelFree";
    public const string TxPermitSameColorCode = "Same Color Code";
    public const string TxPermitAlways = "Always";

    public const string CallTypeGroup = "Group Call";
    public const string CallTypePrivate = "Private Call";

    public const string SquelchCtcssDcs = "CTCSS/DCS";

    public const int DmrModeSimplex = 0;
    public const int DmrModeRepeater = 1;

    public const int ChannelNameMaxLength = 16;
}
