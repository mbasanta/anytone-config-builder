using AnytoneConfigBuilder.Core.Constants;

namespace AnytoneConfigBuilder.Core.Validation;

public static class InputValidator
{
    public static string ValidateName(string value)
    {
        return ValidateLength(value, ChannelValues.ChannelNameMaxLength, "Channel Name");
    }

    public static string ValidateZone(string value)
    {
        return ValidateLength(value, 16, "Zone");
    }

    public static string ValidateContact(string value)
    {
        return ValidateLength(value, ChannelValues.ChannelNameMaxLength, "Contact");
    }

    public static string ValidateBandwidth(string value)
    {
        return ValidateMembership(value, ["25K", "12.5K"], "Bandwidth");
    }

    public static string ValidatePower(string value)
    {
        return ValidateMembership(value, ["Low", "Mid", "High", "Turbo"], "Power");
    }

    public static string ValidateCallType(string value)
    {
        return ValidateMembership(value, [ChannelValues.CallTypeGroup, ChannelValues.CallTypePrivate], "Call Type");
    }

    public static string ValidateTxPermit(string value)
    {
        return ValidateMembership(value, ["Always", "ChannelFree", "Same Color Code", "Different Color Code"], "TX Permit");
    }

    public static string ValidateTxProhibit(string value)
    {
        return ValidateMembership(value, ["On", "Off"], "TX Prohibit");
    }

    public static string ValidateTimeslot(string value)
    {
        return ValidateMembership(value, ["1", "2", "-"], "Time Slot");
    }

    public static string ValidateFreq(string value)
    {
        var result = ValidateNumberInRange(value, 0, 500, "Frequency");
        return result;
    }

    public static string ValidateColorCode(string value)
    {
        return ValidateIntInRange(value, 0, 16, "Color Code");
    }

    public static string ValidateCtcss(string value)
    {
        if (string.Equals(value, "Off", StringComparison.OrdinalIgnoreCase))
        {
            return "Off";
        }

        if (value.StartsWith("D", StringComparison.OrdinalIgnoreCase) && value.Length < 10)
        {
            return value;
        }

        return ValidateNumberInRange(value, 0, 300, "CTCSS/DCS");
    }

    private static string ValidateLength(string value, int maxLength, string name)
    {
        if (value.Length > maxLength)
        {
            throw new InvalidOperationException($"{name} '{value}' exceeds max length of {maxLength}.");
        }

        return value;
    }

    private static string ValidateMembership(string value, string[] allowed, string name)
    {
        foreach (var validValue in allowed)
        {
            if (string.Equals(value, validValue, StringComparison.OrdinalIgnoreCase))
            {
                return validValue;
            }
        }

        throw new InvalidOperationException($"Invalid {name} value '{value}'.");
    }

    private static string ValidateIntInRange(string value, int min, int max, string name)
    {
        if (!int.TryParse(value, out var parsed) || parsed < min || parsed > max)
        {
            throw new InvalidOperationException($"{name} '{value}' must be an integer in range [{min}, {max}].");
        }

        return parsed.ToString();
    }

    private static string ValidateNumberInRange(string value, decimal min, decimal max, string name)
    {
        if (!decimal.TryParse(value, out var parsed) || parsed < min || parsed > max)
        {
            throw new InvalidOperationException($"{name} '{value}' must be in range [{min}, {max}].");
        }

        return value;
    }
}
