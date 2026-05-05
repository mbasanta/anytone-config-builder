using AnytoneConfigBuilder.Core.Constants;
using AnytoneConfigBuilder.Core.Models;
using AnytoneConfigBuilder.Core.Readers;
using AnytoneConfigBuilder.Core.Validation;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

namespace AnytoneConfigBuilder.Core.Processing;

/// <summary>
/// Core build pipeline that transforms input CSV files into Anytone CPS-compatible output files.
/// 
/// The pipeline orchestrates a multi-stage transformation:
/// 1. Read channel defaults and talk group definitions
/// 2. Process analog channels (single frequency, optional CTCSS)
/// 3. Process digital-others channels (simplex/hotspot, color code + call type)
/// 4. Process digital-repeaters matrix (multi-channel repeater configurations)
/// 5. Write output CSVs with proper formatting and zone/scanlist organization
/// 
/// Key features:
/// - Advanced sort modes (alphabetical, repeaters-first, analog-first)
/// - Repeater nickname generation (prefix/suffix for duplicate names)
/// - Private-call detection and marking
/// - Zone and scanlist automatic organization
/// - Input validation and error reporting
/// </summary>
public sealed class BuildPipeline
{
    // CSV parsing configuration for input files (allow missing fields, handle errors gracefully)
    private static readonly CsvConfiguration InputCsvConfig = new(CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = true,
        BadDataFound = null,
        MissingFieldFound = null,
    };

    // CSV output configuration for CPS compatibility (always quote, use CRLF line endings)
    private static readonly CsvConfiguration OutputCsvConfig = new(CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = false,
        ShouldQuote = _ => true,
        NewLine = "\r\n",
    };

    private readonly ChannelDefaultsReader _channelDefaultsReader;
    private readonly TalkgroupsReader _talkgroupsReader;

    public BuildPipeline(ChannelDefaultsReader channelDefaultsReader, TalkgroupsReader talkgroupsReader)
    {
        _channelDefaultsReader = channelDefaultsReader;
        _talkgroupsReader = talkgroupsReader;
    }

    /// <summary>
    /// Orchestrates the entire build transformation from input CSVs to output CPS-compatible files.
    /// 
    /// Process flow:
    /// 1. Reads channel-defaults.csv and talkgroups.csv for reference data
    /// 2. Processes analog.csv: creates channels with frequency + optional CTCSS
    /// 3. Processes digital-others.csv: creates digital channels (simplex/hotspot)
    /// 4. Processes digital-repeaters.csv: expands repeater matrix into individual channels
    /// 5. Organizes channels into zones and scanlists based on BuildOptions
    /// 6. Writes four output CSVs in CPS format
    /// 
    /// Advanced sort behavior:
    /// - SortMode.Alpha: Alphabetically sort zones and talkgroups (ignores input order)
    /// - SortMode.RepeatersFirst: Maintain input order; repeaters appear first in each zone
    /// - SortMode.AnalogFirst: Maintain input order; analog channels appear first
    /// </summary>
    public BuildResult Build(
        BuildOptions options,
        Stream analogCsv,
        Stream digitalOthersCsv,
        Stream digitalRepeatersCsv,
        Stream talkgroupsCsv,
        string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        var warnings = new List<string>();
        var errors = new List<string>();

        var defaults = _channelDefaultsReader.ReadDefaults();
        var (talkgroupMapping, talkgroupOrder) = _talkgroupsReader.Read(talkgroupsCsv);

        var channelsPath = Path.Combine(outputDirectory, "channels.csv");
        var zonePath = Path.Combine(outputDirectory, "zones.csv");
        var scanlistPath = Path.Combine(outputDirectory, "scanlists.csv");
        var talkgroupsPath = Path.Combine(outputDirectory, "talkgroups.csv");

        // Collections to track channels organized by zone for output
        var zoneEntries = new Dictionary<string, List<ChannelRef>>(StringComparer.OrdinalIgnoreCase);
        var scanlistEntries = new Dictionary<string, List<ChannelRef>>(StringComparer.OrdinalIgnoreCase);
        var usedTalkgroups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        // For sort modes that preserve input order: track zone/scanlist first-appearance index
        var zoneOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var analogChannelIndex = 0;
        var zoneOrderDefault = options.SortMode == SortMode.AnalogFirst ? 0 : 9999;

        var channelNumber = 1;

        try
        {
            using (var writer = new StreamWriter(channelsPath))
            using (var csv = new CsvWriter(writer, OutputCsvConfig))
            {
                foreach (var field in defaults)
                {
                    csv.WriteField(field.FieldName);
                }
                csv.NextRecord();

                ProcessDigitalOthers(csv, defaults, digitalOthersCsv, talkgroupMapping, talkgroupOrder, options, zoneOrderDefault, ref analogChannelIndex, ref channelNumber, zoneEntries, scanlistEntries, zoneOrder, usedTalkgroups);
                ProcessDigitalRepeaters(csv, defaults, digitalRepeatersCsv, talkgroupMapping, talkgroupOrder, options, ref analogChannelIndex, ref channelNumber, zoneEntries, scanlistEntries, zoneOrder, usedTalkgroups);
                ProcessAnalog(csv, defaults, analogCsv, options, zoneOrderDefault, ref analogChannelIndex, ref channelNumber, zoneEntries, scanlistEntries, zoneOrder);
            }

            WriteZones(zonePath, zoneEntries, zoneOrder, options, warnings);
            WriteScanlists(scanlistPath, scanlistEntries, warnings);
            WriteTalkgroups(talkgroupsPath, usedTalkgroups, talkgroupMapping);
        }
        catch (Exception ex)
        {
            errors.Add(ex.Message);
        }

        return new BuildResult
        {
            Success = errors.Count == 0,
            Warnings = warnings,
            Errors = errors,
        };
    }

    private static void ProcessAnalog(
        CsvWriter outCsv,
        IReadOnlyList<ChannelFieldDefault> defaults,
        Stream analogCsv,
        BuildOptions options,
        int zoneOrderDefault,
        ref int analogChannelIndex,
        ref int channelNumber,
        Dictionary<string, List<ChannelRef>> zoneEntries,
        Dictionary<string, List<ChannelRef>> scanlistEntries,
        Dictionary<string, int> zoneOrder)
    {
        Reset(analogCsv);
        using var reader = new StreamReader(analogCsv, leaveOpen: true);
        using var csv = new CsvReader(reader, InputCsvConfig);

        csv.Read();
        csv.ReadHeader();

        while (csv.Read())
        {
            var zone = InputValidator.ValidateZone((csv.GetField("Zone") ?? string.Empty).Trim());
            var name = InputValidator.ValidateName((csv.GetField("Channel Name") ?? string.Empty).Trim());
            var bandwidth = InputValidator.ValidateBandwidth((csv.GetField("Bandwidth") ?? string.Empty).Trim());
            var power = InputValidator.ValidatePower((csv.GetField("Power") ?? string.Empty).Trim());
            var rx = InputValidator.ValidateFreq((csv.GetField("RX Freq") ?? string.Empty).Trim());
            var tx = InputValidator.ValidateFreq((csv.GetField("TX Freq") ?? string.Empty).Trim());
            var ctcssDec = InputValidator.ValidateCtcss((csv.GetField("CTCSS Decode") ?? string.Empty).Trim());
            var ctcssEnc = InputValidator.ValidateCtcss((csv.GetField("CTCSS Encode") ?? string.Empty).Trim());
            var txProhibit = InputValidator.ValidateTxProhibit((csv.GetField("TX Prohibit") ?? string.Empty).Trim());

            var fields = new Dictionary<int, string>
            {
                [ChannelFields.Name] = name,
                [ChannelFields.ReceiveFrequency] = rx,
                [ChannelFields.TransmitFrequency] = tx,
                [ChannelFields.ChannelType] = ChannelValues.Analog,
                [ChannelFields.TransmitPower] = power,
                [ChannelFields.BandWidth] = bandwidth,
                [ChannelFields.CtcssDcsDecode] = ctcssDec,
                [ChannelFields.CtcssDcsEncode] = ctcssEnc,
                [ChannelFields.ScanList] = zone,
                [ChannelFields.TxProhibit] = txProhibit,
                [ChannelFields.PttProhibit] = txProhibit,
            };

            if (!string.Equals(ctcssDec, "Off", StringComparison.OrdinalIgnoreCase))
            {
                fields[ChannelFields.SquelchMode] = ChannelValues.SquelchCtcssDcs;
            }

            WriteChannel(outCsv, defaults, fields, ref channelNumber);
            var sortKey = BuildChannelSortKey(options, talkgroupOrder: null, contact: null, name, isAnalog: true, ref analogChannelIndex);
            TrackZoneOrder(zoneOrder, zone, zoneOrderDefault);
            AddChannelRef(zoneEntries, zone, name, rx, tx, sortKey);
            AddChannelRef(scanlistEntries, zone, name, rx, tx, sortKey);
        }
    }

    private static void ProcessDigitalOthers(
        CsvWriter outCsv,
        IReadOnlyList<ChannelFieldDefault> defaults,
        Stream digitalOthersCsv,
        Dictionary<string, string> talkgroupMapping,
        Dictionary<string, int> talkgroupOrder,
        BuildOptions options,
        int zoneOrderDefault,
        ref int analogChannelIndex,
        ref int channelNumber,
        Dictionary<string, List<ChannelRef>> zoneEntries,
        Dictionary<string, List<ChannelRef>> scanlistEntries,
        Dictionary<string, int> zoneOrder,
        Dictionary<string, string> usedTalkgroups)
    {
        Reset(digitalOthersCsv);
        using var reader = new StreamReader(digitalOthersCsv, leaveOpen: true);
        using var csv = new CsvReader(reader, InputCsvConfig);

        csv.Read();
        csv.ReadHeader();

        while (csv.Read())
        {
            var zone = InputValidator.ValidateZone((csv.GetField("Zone") ?? string.Empty).Trim());
            var name = InputValidator.ValidateName((csv.GetField("Channel Name") ?? string.Empty).Trim());
            var power = InputValidator.ValidatePower((csv.GetField("Power") ?? string.Empty).Trim());
            var rx = InputValidator.ValidateFreq((csv.GetField("RX Freq") ?? string.Empty).Trim());
            var tx = InputValidator.ValidateFreq((csv.GetField("TX Freq") ?? string.Empty).Trim());
            var colorCode = InputValidator.ValidateColorCode((csv.GetField("Color Code") ?? string.Empty).Trim());
            var talkgroup = InputValidator.ValidateContact((csv.GetField("Talk Group") ?? string.Empty).Trim());
            var timeslot = InputValidator.ValidateTimeslot((csv.GetField("TimeSlot") ?? string.Empty).Trim());
            var callType = InputValidator.ValidateCallType((csv.GetField("Call Type") ?? string.Empty).Trim());
            var txPermit = InputValidator.ValidateTxPermit((csv.GetField("TX Permit") ?? string.Empty).Trim());

            if (!talkgroupMapping.TryGetValue(talkgroup, out var talkgroupId))
            {
                throw new InvalidOperationException($"Talkgroup '{talkgroup}' is referenced but not present in TalkGroups.csv.");
            }

            if (usedTalkgroups.TryGetValue(talkgroup, out var previousCallType) && !string.Equals(previousCallType, callType, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Talkgroup '{talkgroup}' is used as both '{previousCallType}' and '{callType}'.");
            }

            usedTalkgroups[talkgroup] = callType;

            var fields = new Dictionary<int, string>
            {
                [ChannelFields.Name] = name,
                [ChannelFields.ReceiveFrequency] = rx,
                [ChannelFields.TransmitFrequency] = tx,
                [ChannelFields.ChannelType] = ChannelValues.Digital,
                [ChannelFields.TransmitPower] = power,
                [ChannelFields.ColorCode] = colorCode,
                [ChannelFields.Contact] = talkgroup,
                [ChannelFields.ContactCallTypeOld] = callType,
                [ChannelFields.ContactCallTypeNew] = callType,
                [ChannelFields.ContactTgDmrId] = talkgroupId,
                [ChannelFields.Slot] = timeslot,
                [ChannelFields.BusyLockTxPermit] = txPermit,
                [ChannelFields.ScanList] = zone,
                [ChannelFields.DmrMode] = string.Equals(rx, tx, StringComparison.OrdinalIgnoreCase)
                    ? ChannelValues.DmrModeSimplex.ToString()
                    : ChannelValues.DmrModeRepeater.ToString(),
            };

            WriteChannel(outCsv, defaults, fields, ref channelNumber);
            var sortKey = BuildChannelSortKey(options, talkgroupOrder, talkgroup, name, isAnalog: false, ref analogChannelIndex);
            TrackZoneOrder(zoneOrder, zone, zoneOrderDefault);
            AddChannelRef(zoneEntries, zone, name, rx, tx, sortKey);
            AddChannelRef(scanlistEntries, zone, name, rx, tx, sortKey);
        }
    }

    private static void ProcessDigitalRepeaters(
        CsvWriter outCsv,
        IReadOnlyList<ChannelFieldDefault> defaults,
        Stream digitalRepeatersCsv,
        Dictionary<string, string> talkgroupMapping,
        Dictionary<string, int> talkgroupOrder,
        BuildOptions options,
        ref int analogChannelIndex,
        ref int channelNumber,
        Dictionary<string, List<ChannelRef>> zoneEntries,
        Dictionary<string, List<ChannelRef>> scanlistEntries,
        Dictionary<string, int> zoneOrder,
        Dictionary<string, string> usedTalkgroups)
    {
        Reset(digitalRepeatersCsv);
        using var reader = new StreamReader(digitalRepeatersCsv, leaveOpen: true);
        using var csv = new CsvReader(reader, InputCsvConfig);

        csv.Read();
        csv.ReadHeader();

        var headers = csv.HeaderRecord ?? [];
        if (headers.Length < 7)
        {
            return;
        }

        var zoneOrderIndex = 1;
        while (csv.Read())
        {
            var (zoneFull, zoneNickname) = SplitNicknameValue((csv.GetField("Zone Name") ?? string.Empty).Trim());
            var zone = InputValidator.ValidateZone(zoneFull);
            var power = InputValidator.ValidatePower((csv.GetField("Power") ?? string.Empty).Trim());
            var rx = InputValidator.ValidateFreq((csv.GetField("RX Freq") ?? string.Empty).Trim());
            var tx = InputValidator.ValidateFreq((csv.GetField("TX Freq") ?? string.Empty).Trim());
            var colorCode = InputValidator.ValidateColorCode((csv.GetField("Color Code") ?? string.Empty).Trim());

            for (var col = 6; col < headers.Length; col++)
            {
                var (talkgroupFull, talkgroupNickname) = SplitNicknameValue((headers[col] ?? string.Empty).Trim());
                var matrixValue = (csv.GetField(col) ?? string.Empty).Trim();

                var (timeslot, callType, shouldCreate) = ParseRepeaterMatrixValue(matrixValue);
                if (!shouldCreate)
                {
                    continue;
                }

                var talkgroup = InputValidator.ValidateContact(talkgroupFull);
                if (!talkgroupMapping.TryGetValue(talkgroup, out var talkgroupId))
                {
                    throw new InvalidOperationException($"Talkgroup '{talkgroup}' is referenced but not present in TalkGroups.csv.");
                }

                if (usedTalkgroups.TryGetValue(talkgroup, out var previousCallType) && !string.Equals(previousCallType, callType, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Talkgroup '{talkgroup}' is used as both '{previousCallType}' and '{callType}'.");
                }

                usedTalkgroups[talkgroup] = callType;

                var channelName = BuildRepeaterChannelName(options, zoneNickname, talkgroup, talkgroupNickname);
                var txPermit = BuildRepeaterTxPermit(options, rx, tx);

                var fields = new Dictionary<int, string>
                {
                    [ChannelFields.Name] = channelName,
                    [ChannelFields.ReceiveFrequency] = rx,
                    [ChannelFields.TransmitFrequency] = tx,
                    [ChannelFields.ChannelType] = ChannelValues.Digital,
                    [ChannelFields.TransmitPower] = power,
                    [ChannelFields.ColorCode] = colorCode,
                    [ChannelFields.Contact] = talkgroup,
                    [ChannelFields.ContactCallTypeOld] = callType,
                    [ChannelFields.ContactCallTypeNew] = callType,
                    [ChannelFields.ContactTgDmrId] = talkgroupId,
                    [ChannelFields.Slot] = timeslot,
                    [ChannelFields.BusyLockTxPermit] = txPermit,
                    [ChannelFields.ScanList] = talkgroup,
                    [ChannelFields.DmrMode] = string.Equals(rx, tx, StringComparison.OrdinalIgnoreCase)
                        ? ChannelValues.DmrModeSimplex.ToString()
                        : ChannelValues.DmrModeRepeater.ToString(),
                };

                WriteChannel(outCsv, defaults, fields, ref channelNumber);
                var sortKey = BuildChannelSortKey(options, talkgroupOrder, talkgroup, channelName, isAnalog: false, ref analogChannelIndex);
                TrackZoneOrder(zoneOrder, zone, zoneOrderIndex);
                AddChannelRef(zoneEntries, zone, channelName, rx, tx, sortKey);
                AddChannelRef(scanlistEntries, talkgroup, channelName, rx, tx, sortKey);
            }

            zoneOrderIndex++;
        }
    }

    private static string BuildChannelSortKey(
        BuildOptions options,
        Dictionary<string, int>? talkgroupOrder,
        string? contact,
        string channelName,
        bool isAnalog,
        ref int analogChannelIndex)
    {
        var index1 = 9999;
        var index2 = 0;

        if (options.SortMode != SortMode.Alpha)
        {
            if (isAnalog)
            {
                index2 = analogChannelIndex;
                analogChannelIndex++;
            }
            else if (!string.IsNullOrWhiteSpace(contact)
                && talkgroupOrder is not null
                && talkgroupOrder.TryGetValue(contact, out var talkgroupIndex))
            {
                index1 = talkgroupIndex;
            }
        }

        return string.Create(CultureInfo.InvariantCulture, $"{index1:D4}{index2:D4}{channelName}");
    }

    private static void TrackZoneOrder(Dictionary<string, int> zoneOrder, string zoneName, int order)
    {
        zoneOrder[zoneName] = order;
    }

    private static (string Timeslot, string CallType, bool ShouldCreate) ParseRepeaterMatrixValue(string matrixValue)
    {
        if (string.IsNullOrWhiteSpace(matrixValue) || matrixValue == ChannelValues.NoTimeSlot)
        {
            return (ChannelValues.NoTimeSlot, ChannelValues.CallTypeGroup, false);
        }

        var parts = matrixValue.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var timeslot = InputValidator.ValidateTimeslot(parts[0]);
        if (timeslot == ChannelValues.NoTimeSlot)
        {
            return (timeslot, ChannelValues.CallTypeGroup, false);
        }

        var callType = ChannelValues.CallTypeGroup;
        foreach (var part in parts.Skip(1))
        {
            if (string.Equals(part, "P", StringComparison.OrdinalIgnoreCase))
            {
                callType = ChannelValues.CallTypePrivate;
            }
        }

        return (timeslot, callType, true);
    }

    private static string BuildRepeaterTxPermit(BuildOptions options, string rx, string tx)
    {
        if (options.HotspotTxPermitMode == HotspotTxPermitMode.Always && string.Equals(rx, tx, StringComparison.OrdinalIgnoreCase))
        {
            return ChannelValues.TxPermitAlways;
        }

        return ChannelValues.TxPermitSameColorCode;
    }

    private static string BuildRepeaterChannelName(BuildOptions options, string zoneNickname, string talkgroupFull, string talkgroupNickname)
    {
        if (options.NicknameMode == NicknameMode.Off || string.IsNullOrWhiteSpace(zoneNickname))
        {
            return InputValidator.ValidateName(talkgroupFull);
        }

        var tgNick = string.IsNullOrWhiteSpace(talkgroupNickname) ? talkgroupFull : talkgroupNickname;
        var useForced = options.NicknameMode is NicknameMode.PrefixForced or NicknameMode.SuffixForced;
        var talkgroupBase = useForced ? tgNick : talkgroupFull;

        string selectedName;
        var separator = " ";
        if (zoneNickname.Length + talkgroupBase.Length + 1 <= ChannelValues.ChannelNameMaxLength)
        {
            selectedName = talkgroupBase;
        }
        else if (zoneNickname.Length + tgNick.Length + 1 <= ChannelValues.ChannelNameMaxLength)
        {
            selectedName = tgNick;
        }
        else if (zoneNickname.Length + tgNick.Length <= ChannelValues.ChannelNameMaxLength)
        {
            selectedName = tgNick;
            separator = string.Empty;
        }
        else
        {
            throw new InvalidOperationException($"Cannot fit nickname combination '{zoneNickname}' and '{tgNick}' into 16 characters.");
        }

        if (!char.IsLetterOrDigit(zoneNickname[0]))
        {
            separator = string.Empty;
        }

        var value = options.NicknameMode is NicknameMode.Prefix or NicknameMode.PrefixForced
            ? zoneNickname + separator + selectedName
            : selectedName + separator + zoneNickname;

        return InputValidator.ValidateName(value);
    }

    private static (string Full, string Nickname) SplitNicknameValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return (string.Empty, string.Empty);
        }

        var parts = value.Split(';', StringSplitOptions.TrimEntries);
        var full = parts[0];
        var nickname = parts.Length > 1 ? parts[^1] : string.Empty;
        return (full, nickname);
    }

    private static void WriteChannel(
        CsvWriter outCsv,
        IReadOnlyList<ChannelFieldDefault> defaults,
        Dictionary<int, string> values,
        ref int channelNumber)
    {
        var row = new List<string>(defaults.Count);

        foreach (var field in defaults)
        {
            string? value;
            if (field.Index == ChannelFields.Number)
            {
                value = channelNumber.ToString(CultureInfo.InvariantCulture);
                channelNumber++;
            }
            else if (values.TryGetValue(field.Index, out var assignedValue))
            {
                value = assignedValue;
            }
            else
            {
                value = field.DefaultValue;
            }

            if (value is null)
            {
                throw new InvalidOperationException($"Missing required field '{field.FieldName}' (index {field.Index}).");
            }

            row.Add(value);
        }

        WriteRow(outCsv, row);
    }

    private static void WriteZones(
        string filePath,
        Dictionary<string, List<ChannelRef>> zoneEntries,
        Dictionary<string, int> zoneOrder,
        BuildOptions options,
        List<string> warnings)
    {
        using var writer = new StreamWriter(filePath);
        using var csv = new CsvWriter(writer, OutputCsvConfig);

        WriteRow(csv, new[]
        {
            "No.", "Zone Name",
            "Zone Channel Member", "Zone Channel Member RX Frequency", "Zone Channel Member TX Frequency",
            "A Channel", "A Channel RX Frequency", "A Channel TX Frequency",
            "B Channel", "B Channel RX Frequency", "B Channel TX Frequency",
        });

        var rowNum = 1;
        foreach (var zone in zoneEntries.OrderBy(k => k.Key, ZoneKeyComparer(zoneOrder, options)))
        {
            var channels = zone.Value.OrderBy(c => c.SortKey, StringComparer.OrdinalIgnoreCase).ToList();
            if (channels.Count > 250)
            {
                warnings.Add($"Zone '{zone.Key}' exceeds 250 channels and was truncated.");
                channels = channels.Take(250).ToList();
            }

            var first = channels.First();
            WriteRow(csv, new[]
            {
                rowNum.ToString(CultureInfo.InvariantCulture),
                zone.Key,
                string.Join("|", channels.Select(c => c.Name)),
                string.Join("|", channels.Select(c => c.Rx)),
                string.Join("|", channels.Select(c => c.Tx)),
                first.Name,
                first.Rx,
                first.Tx,
                first.Name,
                first.Rx,
                first.Tx,
            });
            rowNum++;
        }
    }

    private static void WriteScanlists(string filePath, Dictionary<string, List<ChannelRef>> scanlistEntries, List<string> warnings)
    {
        using var writer = new StreamWriter(filePath);
        using var csv = new CsvWriter(writer, OutputCsvConfig);

        WriteRow(csv, new[]
        {
            "No.", "Scan List Name",
            "Scan Channel Member", "Scan Channel Member RX Frequency", "Scan Channel Member TX Frequency",
            "Scan Mode", "Priority Channel Select",
            "Priority Channel 1", "Priority Channel 1 RX Frequency", "Priority Channel 1 TX Frequency",
            "Priority Channel 2", "Priority Channel 2 RX Frequency", "Priority Channel 2 TX Frequency",
            "Revert Channel", "Look Back Time A[s]", "Look Back Time B[s]", "Dropout Delay Time[s]", "Dwell Time[s]",
        });

        var rowNum = 1;
        foreach (var scanlist in scanlistEntries.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            var channels = scanlist.Value.OrderBy(c => c.SortKey, StringComparer.OrdinalIgnoreCase).ToList();
            if (channels.Count > 50)
            {
                warnings.Add($"Scanlist '{scanlist.Key}' exceeds 50 channels and was truncated.");
                channels = channels.Take(50).ToList();
            }

            WriteRow(csv, new[]
            {
                rowNum.ToString(CultureInfo.InvariantCulture),
                scanlist.Key,
                string.Join("|", channels.Select(c => c.Name)),
                string.Join("|", channels.Select(c => c.Rx)),
                string.Join("|", channels.Select(c => c.Tx)),
                "Off", "Off",
                string.Empty, string.Empty, "Off",
                string.Empty, string.Empty, "Selected",
                "0.5", "0.5", "0.1", "0.1",
            });
            rowNum++;
        }
    }

    private static void WriteTalkgroups(string filePath, Dictionary<string, string> usedTalkgroups, Dictionary<string, string> talkgroupMapping)
    {
        using var writer = new StreamWriter(filePath);
        using var csv = new CsvWriter(writer, OutputCsvConfig);

        WriteRow(csv, new[] { "No.", "Radio ID", "Name", "Country", "Remarks", "Call Type", "Call Alert" });

        var rowNum = 1;
        foreach (var talkgroup in usedTalkgroups.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            WriteRow(csv, new[]
            {
                rowNum.ToString(CultureInfo.InvariantCulture),
                talkgroupMapping[talkgroup.Key],
                talkgroup.Key,
                string.Empty,
                string.Empty,
                talkgroup.Value,
                "None",
            });
            rowNum++;
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

    private static IComparer<string> ZoneKeyComparer(Dictionary<string, int> zoneOrder, BuildOptions options)
    {
        return Comparer<string>.Create((a, b) =>
        {
            var aIndex = zoneOrder.TryGetValue(a, out var ai) ? ai : int.MaxValue;
            var bIndex = zoneOrder.TryGetValue(b, out var bi) ? bi : int.MaxValue;

            if (options.SortMode == SortMode.Alpha || aIndex == bIndex)
            {
                return StringComparer.OrdinalIgnoreCase.Compare(a, b);
            }

            return aIndex.CompareTo(bIndex);
        });
    }

    private static void AddChannelRef(Dictionary<string, List<ChannelRef>> destination, string group, string name, string rx, string tx, string sortKey)
    {
        if (!destination.TryGetValue(group, out var entries))
        {
            entries = [];
            destination[group] = entries;
        }

        entries.Add(new ChannelRef(name, rx, tx, sortKey));
    }

    private static void Reset(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Seek(0, SeekOrigin.Begin);
        }
    }

    private sealed record ChannelRef(string Name, string Rx, string Tx, string SortKey);
}
