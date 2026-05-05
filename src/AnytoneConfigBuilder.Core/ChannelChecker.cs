using CsvHelper;
using CsvHelper.Configuration;
using System.Net;
using System.Text;

namespace AnytoneConfigBuilder.Core;

public sealed class ChannelChecker
{
    private const string ColorOk = "#88FF88";
    private const string ColorWarn = "#FFFF88";
    private const string ColorBad = "#FF8888";

    private static readonly CsvConfiguration CsvConfig = new(System.Globalization.CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = true,
        BadDataFound = null,
        MissingFieldFound = null,
    };

    private static readonly string[] ArbitraryFields =
    [
        "Transmit Power",
        "Band Width",
        "CTCSS/DCS Decode",
        "CTCSS/DCS Encode",
        "Contact Call Type",
        "Busy Lock/TX Permit",
        "Squelch Mode",
        "Optional Signal",
        "DTMF ID",
        "2Tone ID",
        "5Tone ID",
        "PTT ID",
        "TX Prohibit",
        "Reverse",
        "Simplex TDMA",
        "TDMA Adaptive",
        "Encryption Type",
        "Digital Encryption",
        "Call Confirmation",
        "Talk Around",
        "Work Alone",
        "Custom CTCSS",
        "Ranging",
        "Through Mode",
        "Digi APRS RX",
        "Analog APRS PTT Mode",
        "Digital APRS PTT Mode",
        "APRS Report Type",
        "Digital APRS Report Channel",
        "Correct Frequency[Hz]",
        "SMS Confirmation",
        "Exclude channel from roaming",
        "Contact TG/DMR ID",
    ];

    public string BuildHtmlReport(Stream channelsCsv)
    {
        if (channelsCsv.CanSeek)
        {
            channelsCsv.Seek(0, SeekOrigin.Begin);
        }

        using var reader = new StreamReader(channelsCsv, leaveOpen: true);
        using var csv = new CsvReader(reader, CsvConfig);

        if (!csv.Read())
        {
            throw new InvalidOperationException("channels.csv appears to be empty.");
        }

        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? [];
        var headerIndex = BuildHeaderIndex(headers);

        RequireColumns(headerIndex, "No.", "Channel Name", "Receive Frequency", "Transmit Frequency", "Channel Type", "Scan List", "Contact", "Slot", "Color Code");

        var rows = new List<ChannelRow>();
        while (csv.Read())
        {
            var values = new string[headers.Length];
            for (var i = 0; i < headers.Length; i++)
            {
                values[i] = csv.GetField(i) ?? string.Empty;
            }

            rows.Add(new ChannelRow(values));
        }

        var sb = new StringBuilder();
        sb.Append("<h2>Anytone Channels Consistency Report</h2>");
        sb.Append("<p>Generated from channels.csv. Highlights likely mismatches on digital repeater channels.</p>");

        AnalyzeKeyValuePairs(
            sb,
            rows,
            row => GetValue(row, headerIndex, "Scan List"),
            row => GetValue(row, headerIndex, "Receive Frequency") + " / " + GetValue(row, headerIndex, "Transmit Frequency"),
            row => IsDigitalRepeater(row, headerIndex),
            row => ChannelDescription(row, headerIndex),
            "Scan List Name",
            "Frequency Pair (RX / TX)",
            "???.????? / ???.?????",
            "Digital Repeater Frequency Pair Consistency Report");

        AnalyzeKeyValuePairs(
            sb,
            rows,
            row => GetValue(row, headerIndex, "Scan List"),
            row => GetValue(row, headerIndex, "Color Code"),
            row => IsDigitalRepeater(row, headerIndex),
            row => ChannelDescription(row, headerIndex),
            "Scan List Name",
            "Color Code",
            "??",
            "Digital Repeater Color Code Consistency Report");

        AnalyzeKeyValuePairs(
            sb,
            rows,
            row => GetValue(row, headerIndex, "Contact"),
            row => GetValue(row, headerIndex, "Slot"),
            row => IsDigitalRepeater(row, headerIndex),
            row => ChannelDescription(row, headerIndex),
            "Talk Group",
            "Time Slot",
            "??",
            "Digital Repeater Talk Group / Time Slot Consistency Report");

        AnalyzeArbitraryFieldPairs(sb, rows, headerIndex);

        return sb.ToString();
    }

    private static void AnalyzeArbitraryFieldPairs(StringBuilder sb, List<ChannelRow> rows, Dictionary<string, int> headerIndex)
    {
        var fields = ArbitraryFields.Where(headerIndex.ContainsKey).ToList();
        foreach (var fieldName in fields)
        {
            AnalyzeKeyValuePairs(
                sb,
                rows,
                _ => fieldName,
                row => GetValue(row, headerIndex, fieldName),
                row => IsDigitalRepeater(row, headerIndex),
                row => ChannelDescription(row, headerIndex),
                "Field",
                "Value",
                "??",
                "Digital Repeater Other Field Consistency Report");
        }
    }

    private static void AnalyzeKeyValuePairs(
        StringBuilder sb,
        List<ChannelRow> rows,
        Func<ChannelRow, string> keySelector,
        Func<ChannelRow, string> valueSelector,
        Func<ChannelRow, bool> filter,
        Func<ChannelRow, string> channelDescription,
        string keyName,
        string valueName,
        string unknownValue,
        string title)
    {
        sb.Append("<h3>").Append(Html(title)).Append("</h3>");
        sb.Append("<table border='1' cellpadding='4' cellspacing='0'>");
        sb.Append("<tr><th>").Append(Html(keyName)).Append("</th><th>Count</th><th>").Append(Html(valueName)).Append("</th></tr>");

        var data = new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (!filter(row))
            {
                continue;
            }

            var key = keySelector(row);
            var value = valueSelector(row);
            if (!data.TryGetValue(key, out var byValue))
            {
                byValue = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                data[key] = byValue;
            }

            if (!byValue.TryGetValue(value, out var channels))
            {
                channels = [];
                byValue[value] = channels;
            }

            channels.Add(channelDescription(row));
        }

        foreach (var key in data.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
        {
            var byValue = data[key];
            var sum = byValue.Values.Sum(v => v.Count);
            var max = byValue.OrderByDescending(kvp => kvp.Value.Count).First();

            var distinctValues = byValue.Count;
            var likelyValue = max.Key;
            var likelyCount = max.Value.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var color = ColorWarn;

            if (distinctValues == 1)
            {
                color = ColorOk;
            }
            else if ((double)max.Value.Count / sum > 0.70)
            {
                color = ColorBad;
            }
            else
            {
                likelyValue = unknownValue;
                likelyCount = "??";
            }

            sb.Append("<tr bgcolor='").Append(color).Append("'><td><b>")
                .Append(Html(key)).Append("</b></td><td>")
                .Append(Html(likelyCount)).Append("</td><td><b>")
                .Append(Html(likelyValue)).Append("</b></td></tr>");

            if (distinctValues != 1)
            {
                foreach (var valueKvp in byValue.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
                {
                    if (string.Equals(valueKvp.Key, likelyValue, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    foreach (var channel in valueKvp.Value)
                    {
                        sb.Append("<tr bgcolor='").Append(color).Append("'><td>&nbsp; &nbsp; &nbsp; ")
                            .Append(Html(channel))
                            .Append("</td><td></td><td>")
                            .Append(Html(valueKvp.Key))
                            .Append("</td></tr>");
                    }
                }
            }
        }

        sb.Append("</table>");
    }

    private static bool IsDigitalRepeater(ChannelRow row, Dictionary<string, int> headerIndex)
    {
        var type = GetValue(row, headerIndex, "Channel Type");
        var rx = GetValue(row, headerIndex, "Receive Frequency");
        var tx = GetValue(row, headerIndex, "Transmit Frequency");
        return string.Equals(type, "D-Digital", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(rx, tx, StringComparison.OrdinalIgnoreCase);
    }

    private static string ChannelDescription(ChannelRow row, Dictionary<string, int> headerIndex)
    {
        return $"Channel {GetValue(row, headerIndex, "No.")} - \"{GetValue(row, headerIndex, "Channel Name")}\"";
    }

    private static Dictionary<string, int> BuildHeaderIndex(string[] headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Length; i++)
        {
            if (!map.ContainsKey(headers[i]))
            {
                map[headers[i]] = i;
            }
        }

        return map;
    }

    private static void RequireColumns(Dictionary<string, int> headerIndex, params string[] names)
    {
        foreach (var name in names)
        {
            if (!headerIndex.ContainsKey(name))
            {
                throw new InvalidOperationException($"channels.csv is missing required column '{name}'.");
            }
        }
    }

    private static string GetValue(ChannelRow row, Dictionary<string, int> headerIndex, string name)
    {
        if (!headerIndex.TryGetValue(name, out var index))
        {
            return string.Empty;
        }

        if (index < 0 || index >= row.Values.Length)
        {
            return string.Empty;
        }

        return row.Values[index];
    }

    private static string Html(string value)
    {
        return WebUtility.HtmlEncode(value ?? string.Empty);
    }

    private sealed record ChannelRow(string[] Values);
}
