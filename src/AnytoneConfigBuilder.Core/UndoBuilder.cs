using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

namespace AnytoneConfigBuilder.Core;

public sealed class UndoBuilder
{
    private static readonly CsvConfiguration InputCsvConfig = new(CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = true,
        BadDataFound = null,
        MissingFieldFound = null,
    };

    private static readonly CsvConfiguration OutputCsvConfig = new(CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = false,
        ShouldQuote = _ => true,
        NewLine = "\r\n",
    };

    public void Run(string channelsFilePath, string zonesFilePath, string talkgroupsFilePath, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        var analogOut = Path.Combine(outputDirectory, "analog.csv");
        var digitalOthersOut = Path.Combine(outputDirectory, "digital-others.csv");
        var digitalRepeatersOut = Path.Combine(outputDirectory, "digital-repeaters.csv");
        var talkgroupsOut = Path.Combine(outputDirectory, "talkgroups.csv");

        var chanToZones = new Dictionary<ChannelKey, List<string>>();
        var zoneFreqs = new Dictionary<string, Dictionary<FreqPair, int>>(StringComparer.OrdinalIgnoreCase);

        ReadZones(zonesFilePath, chanToZones, zoneFreqs);

        var digitalRepeaterZoneFreqs = new HashSet<ZoneFreq>();
        foreach (var zone in zoneFreqs)
        {
            foreach (var pair in zone.Value)
            {
                if (pair.Value > 5)
                {
                    digitalRepeaterZoneFreqs.Add(new ZoneFreq(zone.Key, pair.Key.Rx, pair.Key.Tx));
                }
            }
        }

        var talkgroupsById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var talkgroupRows = new List<string[]>();
        ReadTalkgroups(talkgroupsFilePath, talkgroupsById, talkgroupRows);

        var analogRows = new List<string[]>();
        var digitalOthersRows = new List<string[]>();
        var digitalRepeaterColorCode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var digitalRepeaterPower = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var digitalRepeaterAllTgs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var digitalRepeaterTgs = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        ReadChannels(
            channelsFilePath,
            chanToZones,
            digitalRepeaterZoneFreqs,
            analogRows,
            digitalOthersRows,
            digitalRepeaterColorCode,
            digitalRepeaterPower,
            digitalRepeaterAllTgs,
            digitalRepeaterTgs);

        analogRows.Sort(RowComparer.Instance);
        digitalOthersRows.Sort(RowComparer.Instance);

        WriteAnalog(analogOut, analogRows);
        WriteDigitalOthers(digitalOthersOut, digitalOthersRows);
        WriteDigitalRepeaters(digitalRepeatersOut, digitalRepeaterZoneFreqs, digitalRepeaterPower, digitalRepeaterColorCode, digitalRepeaterAllTgs, digitalRepeaterTgs);
        WriteTalkgroups(talkgroupsOut, talkgroupRows);
    }

    private static void ReadZones(
        string zonesFilePath,
        Dictionary<ChannelKey, List<string>> chanToZones,
        Dictionary<string, Dictionary<FreqPair, int>> zoneFreqs)
    {
        using var reader = new StreamReader(zonesFilePath);
        using var csv = new CsvReader(reader, InputCsvConfig);

        csv.Read();
        csv.ReadHeader();

        while (csv.Read())
        {
            var zoneName = (csv.GetField("Zone Name") ?? string.Empty).Trim();
            var channelMembers = (csv.GetField("Zone Channel Member") ?? string.Empty).Split('|');
            var rxMembers = (csv.GetField("Zone Channel Member RX Frequency") ?? string.Empty).Split('|');
            var txMembers = (csv.GetField("Zone Channel Member TX Frequency") ?? string.Empty).Split('|');

            var memberCount = Math.Min(channelMembers.Length, Math.Min(rxMembers.Length, txMembers.Length));
            for (var i = 0; i < memberCount; i++)
            {
                var name = channelMembers[i].Trim();
                var rx = rxMembers[i].Trim();
                var tx = txMembers[i].Trim();

                var channelKey = new ChannelKey(name, rx, tx);
                if (!chanToZones.TryGetValue(channelKey, out var zones))
                {
                    zones = [];
                    chanToZones[channelKey] = zones;
                }

                zones.Add(zoneName);

                if (!zoneFreqs.TryGetValue(zoneName, out var byPair))
                {
                    byPair = new Dictionary<FreqPair, int>();
                    zoneFreqs[zoneName] = byPair;
                }

                var pair = new FreqPair(rx, tx);
                byPair.TryGetValue(pair, out var count);
                byPair[pair] = count + 1;
            }
        }
    }

    private static void ReadTalkgroups(string talkgroupsFilePath, Dictionary<string, string> talkgroupsById, List<string[]> outputRows)
    {
        using var reader = new StreamReader(talkgroupsFilePath);
        using var csv = new CsvReader(reader, InputCsvConfig);

        csv.Read();
        csv.ReadHeader();

        while (csv.Read())
        {
            var radioId = (csv.GetField("Radio ID") ?? string.Empty).Trim();
            var name = (csv.GetField("Name") ?? string.Empty).Trim();

            talkgroupsById[radioId] = name;
            outputRows.Add([name, radioId]);
        }
    }

    private static void ReadChannels(
        string channelsFilePath,
        Dictionary<ChannelKey, List<string>> chanToZones,
        HashSet<ZoneFreq> digitalRepeaterZoneFreqs,
        List<string[]> analogRows,
        List<string[]> digitalOthersRows,
        Dictionary<string, string> digitalRepeaterColorCode,
        Dictionary<string, string> digitalRepeaterPower,
        HashSet<string> digitalRepeaterAllTgs,
        Dictionary<string, Dictionary<string, string>> digitalRepeaterTgs)
    {
        using var reader = new StreamReader(channelsFilePath);
        using var csv = new CsvReader(reader, InputCsvConfig);

        csv.Read();
        csv.ReadHeader();

        while (csv.Read())
        {
            var chanName = (csv.GetField("Channel Name") ?? string.Empty).Trim();
            var chanRx = (csv.GetField("Receive Frequency") ?? string.Empty).Trim();
            var chanTx = (csv.GetField("Transmit Frequency") ?? string.Empty).Trim();
            var chanType = (csv.GetField("Channel Type") ?? string.Empty).Trim();
            var chanPower = (csv.GetField("Transmit Power") ?? string.Empty).Trim();
            var chanBandwidth = (csv.GetField("Band Width") ?? string.Empty).Trim();
            var chanRxTone = (csv.GetField("CTCSS/DCS Decode") ?? string.Empty).Trim();
            var chanTxTone = (csv.GetField("CTCSS/DCS Encode") ?? string.Empty).Trim();
            var chanContact = (csv.GetField("Contact") ?? string.Empty).Trim();
            var chanCallType = (csv.GetField("Contact Call Type") ?? string.Empty).Trim();
            var chanTxPermit = (csv.GetField("Busy Lock/TX Permit") ?? string.Empty).Trim();
            var chanColorCode = (csv.GetField("Color Code") ?? string.Empty).Trim();
            var chanTimeslot = (csv.GetField("Slot") ?? string.Empty).Trim();
            var chanTxProhibit = (csv.GetField("TX Prohibit") ?? string.Empty).Trim();
            var repeaterMatrixValue = BuildRepeaterMatrixValue(chanTimeslot, chanCallType);

            var key = new ChannelKey(chanName, chanRx, chanTx);
            if (!chanToZones.TryGetValue(key, out var zoneNames) || zoneNames.Count == 0)
            {
                continue;
            }

            foreach (var zoneName in zoneNames)
            {
                if (string.Equals(chanType, "A-Analog", StringComparison.OrdinalIgnoreCase))
                {
                    analogRows.Add([zoneName, chanName, chanBandwidth, chanPower, chanRx, chanTx, chanRxTone, chanTxTone, chanTxProhibit]);
                    continue;
                }

                var zoneFreq = new ZoneFreq(zoneName, chanRx, chanTx);
                if (digitalRepeaterZoneFreqs.Contains(zoneFreq))
                {
                    if (digitalRepeaterColorCode.TryGetValue(zoneName, out var existingColor)
                        && !string.Equals(existingColor, chanColorCode, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException($"Channel '{chanName}' has color code '{chanColorCode}', which doesn't match other channels on repeater '{zoneName}'.");
                    }

                    digitalRepeaterColorCode[zoneName] = chanColorCode;
                    digitalRepeaterPower[zoneName] = chanPower;
                    digitalRepeaterAllTgs.Add(chanContact);

                    if (!digitalRepeaterTgs.TryGetValue(zoneName, out var byTalkgroup))
                    {
                        byTalkgroup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        digitalRepeaterTgs[zoneName] = byTalkgroup;
                    }

                    byTalkgroup[chanContact] = repeaterMatrixValue;
                }
                else
                {
                    digitalOthersRows.Add([zoneName, chanName, chanPower, chanRx, chanTx, chanColorCode, chanContact, chanTimeslot, chanCallType, chanTxPermit]);
                }
            }
        }
    }

    private static void WriteAnalog(string analogOut, List<string[]> analogRows)
    {
        using var writer = new StreamWriter(analogOut);
        using var csv = new CsvWriter(writer, OutputCsvConfig);

        WriteRow(csv, ["Zone", "Channel Name", "Bandwidth", "Power", "RX Freq", "TX Freq", "CTCSS Decode", "CTCSS Encode", "TX Prohibit"]);
        foreach (var row in analogRows)
        {
            WriteRow(csv, row);
        }
    }

    private static void WriteDigitalOthers(string digitalOthersOut, List<string[]> digitalOthersRows)
    {
        using var writer = new StreamWriter(digitalOthersOut);
        using var csv = new CsvWriter(writer, OutputCsvConfig);

        WriteRow(csv, ["Zone", "Channel Name", "Power", "RX Freq", "TX Freq", "Color Code", "Talk Group", "TimeSlot", "Call Type", "TX Permit"]);
        foreach (var row in digitalOthersRows)
        {
            WriteRow(csv, row);
        }
    }

    private static void WriteDigitalRepeaters(
        string digitalRepeatersOut,
        HashSet<ZoneFreq> digitalRepeaterZoneFreqs,
        Dictionary<string, string> digitalRepeaterPower,
        Dictionary<string, string> digitalRepeaterColorCode,
        HashSet<string> digitalRepeaterAllTgs,
        Dictionary<string, Dictionary<string, string>> digitalRepeaterTgs)
    {
        var orderedTalkgroups = digitalRepeaterAllTgs.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        var orderedZones = digitalRepeaterZoneFreqs
            .OrderBy(z => z.ZoneName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(z => z.Rx, StringComparer.OrdinalIgnoreCase)
            .ThenBy(z => z.Tx, StringComparer.OrdinalIgnoreCase)
            .ToList();

        using var writer = new StreamWriter(digitalRepeatersOut);
        using var csv = new CsvWriter(writer, OutputCsvConfig);

        var header = new List<string> { "Zone Name", "Comment", "Power", "RX Freq", "TX Freq", "Color Code" };
        header.AddRange(orderedTalkgroups);
        WriteRow(csv, header);

        foreach (var zone in orderedZones)
        {
            var row = new List<string>
            {
                zone.ZoneName,
                string.Empty,
                digitalRepeaterPower.TryGetValue(zone.ZoneName, out var power) ? power : string.Empty,
                zone.Rx,
                zone.Tx,
                digitalRepeaterColorCode.TryGetValue(zone.ZoneName, out var color) ? color : string.Empty,
            };

            foreach (var talkgroup in orderedTalkgroups)
            {
                var value = "-";
                if (digitalRepeaterTgs.TryGetValue(zone.ZoneName, out var byTg)
                    && byTg.TryGetValue(talkgroup, out var slot)
                    && !string.IsNullOrWhiteSpace(slot))
                {
                    value = slot;
                }

                row.Add(value);
            }

            WriteRow(csv, row);
        }
    }

    private static void WriteTalkgroups(string talkgroupsOut, List<string[]> talkgroupRows)
    {
        using var writer = new StreamWriter(talkgroupsOut);
        using var csv = new CsvWriter(writer, OutputCsvConfig);

        foreach (var row in talkgroupRows)
        {
            WriteRow(csv, row);
        }
    }

    private static void WriteRow(CsvWriter csv, IEnumerable<string> fields)
    {
        foreach (var field in fields)
        {
            csv.WriteField(field);
        }

        csv.NextRecord();
    }

    private static string BuildRepeaterMatrixValue(string timeslot, string callType)
    {
        if (string.IsNullOrWhiteSpace(timeslot) || string.Equals(timeslot, "Off", StringComparison.OrdinalIgnoreCase))
        {
            return timeslot;
        }

        return string.Equals(callType, "Private Call", StringComparison.OrdinalIgnoreCase)
            ? timeslot + ";P"
            : timeslot;
    }

    private sealed class RowComparer : IComparer<string[]>
    {
        public static RowComparer Instance { get; } = new();

        public int Compare(string[]? x, string[]? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x is null)
            {
                return -1;
            }

            if (y is null)
            {
                return 1;
            }

            return StringComparer.Ordinal.Compare(string.Join(",", x), string.Join(",", y));
        }
    }

    private sealed record ChannelKey(string Name, string Rx, string Tx);
    private sealed record FreqPair(string Rx, string Tx);
    private sealed record ZoneFreq(string ZoneName, string Rx, string Tx);
}
