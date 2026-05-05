using AnytoneConfigBuilder.Core;
using AnytoneConfigBuilder.Core.Models;

if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
{
	PrintUsage();
	return 0;
}

var command = args[0].ToLowerInvariant();
switch (command)
{
	case "defaults":
		return RunDefaultsCommand();
	case "build":
		return RunBuildCommand(args[1..]);
	case "check":
		return RunCheckCommand(args[1..]);
	case "undo":
		return RunUndoCommand(args[1..]);
	default:
		Console.Error.WriteLine($"Unknown command '{command}'.");
		PrintUsage();
		return 1;
}

static int RunDefaultsCommand()
{
	try
	{
		var builder = new ConfigBuilder();
		var defaults = builder.GetChannelDefaults();
		Console.WriteLine($"Loaded {defaults.Count} default channel fields from embedded JSON.");
		foreach (var field in defaults)
		{
			var defaultValue = field.DefaultValue ?? "REQUIRED";
			Console.WriteLine($"{field.Index,2}: {field.FieldName} = {defaultValue}");
		}

		return 0;
	}
	catch (Exception ex)
	{
		Console.Error.WriteLine($"Failed to load embedded defaults: {ex.Message}");
		return 1;
	}
}

static void PrintUsage()
{
	Console.WriteLine("Anytone Config Builder CLI");
	Console.WriteLine();
	Console.WriteLine("Usage:");
	Console.WriteLine("  acb defaults");
	Console.WriteLine("  acb build --analog-csv=PATH --digital-others-csv=PATH --digital-repeaters-csv=PATH --talkgroups-csv=PATH --output-directory=PATH [--sorting=alpha|repeaters-first|analog-first] [--hotspot-tx-permit=same-color-code|always] [--nicknames=off|prefix|suffix|prefix-forced|suffix-forced]");
	Console.WriteLine("  acb check --channels-csv=PATH [--output-html=PATH]");
	Console.WriteLine("  acb undo --channels-csv=PATH --zones-csv=PATH --talkgroups-csv=PATH --output-directory=PATH");
}

static int RunBuildCommand(string[] args)
{
	var options = ParseNamedArgs(args);
	if (!TryGetRequiredOption(options, "analog-csv", out var analogPath)
		|| !TryGetRequiredOption(options, "digital-others-csv", out var digitalOthersPath)
		|| !TryGetRequiredOption(options, "digital-repeaters-csv", out var digitalRepeatersPath)
		|| !TryGetRequiredOption(options, "talkgroups-csv", out var talkgroupsPath)
		|| !TryGetRequiredOption(options, "output-directory", out var outputDirectory))
	{
		Console.Error.WriteLine("Missing one or more required build options.");
		PrintUsage();
		return 1;
	}

	var buildOptions = new BuildOptions(
		ParseSortMode(GetOptionOrDefault(options, "sorting", "alpha")),
		ParseHotspotMode(GetOptionOrDefault(options, "hotspot-tx-permit", "same-color-code")),
		ParseNicknameMode(GetOptionOrDefault(options, "nicknames", "off")));

	try
	{
		using var analogStream = File.OpenRead(analogPath);
		using var digitalOthersStream = File.OpenRead(digitalOthersPath);
		using var digitalRepeatersStream = File.OpenRead(digitalRepeatersPath);
		using var talkgroupsStream = File.OpenRead(talkgroupsPath);

		var builder = new ConfigBuilder();
		var result = builder.Build(
			buildOptions,
			analogStream,
			digitalOthersStream,
			digitalRepeatersStream,
			talkgroupsStream,
			outputDirectory);

		foreach (var warning in result.Warnings)
		{
			Console.WriteLine($"WARNING: {warning}");
		}

		foreach (var error in result.Errors)
		{
			Console.Error.WriteLine($"ERROR: {error}");
		}

		if (!result.Success)
		{
			return 1;
		}

		Console.WriteLine($"SUCCESS: Output files written to '{outputDirectory}'.");
		return 0;
	}
	catch (Exception ex)
	{
		Console.Error.WriteLine($"ERROR: {ex.Message}");
		return 1;
	}
}

static int RunCheckCommand(string[] args)
{
	var options = ParseNamedArgs(args);
	if (!TryGetRequiredOption(options, "channels-csv", out var channelsPath))
	{
		Console.Error.WriteLine("Missing required option '--channels-csv'.");
		PrintUsage();
		return 1;
	}

	var outputPath = GetOptionOrDefault(options, "output-html", Path.Combine(Environment.CurrentDirectory, "checker-report.html"));

	try
	{
		using var stream = File.OpenRead(channelsPath);
		var checker = new ChannelChecker();
		var html = checker.BuildHtmlReport(stream);

		var directory = Path.GetDirectoryName(outputPath);
		if (!string.IsNullOrWhiteSpace(directory))
		{
			Directory.CreateDirectory(directory);
		}

		File.WriteAllText(outputPath, html);
		Console.WriteLine($"SUCCESS: Checker report written to '{outputPath}'.");
		return 0;
	}
	catch (Exception ex)
	{
		Console.Error.WriteLine($"ERROR: {ex.Message}");
		return 1;
	}
}

static int RunUndoCommand(string[] args)
{
	var options = ParseNamedArgs(args);
	if (!TryGetRequiredOption(options, "channels-csv", out var channelsPath)
		|| !TryGetRequiredOption(options, "zones-csv", out var zonesPath)
		|| !TryGetRequiredOption(options, "talkgroups-csv", out var talkgroupsPath)
		|| !TryGetRequiredOption(options, "output-directory", out var outputDirectory))
	{
		Console.Error.WriteLine("Missing one or more required undo options.");
		PrintUsage();
		return 1;
	}

	try
	{
		var undo = new UndoBuilder();
		undo.Run(channelsPath, zonesPath, talkgroupsPath, outputDirectory);
		Console.WriteLine($"SUCCESS: Undo output files written to '{outputDirectory}'.");
		return 0;
	}
	catch (Exception ex)
	{
		Console.Error.WriteLine($"ERROR: {ex.Message}");
		return 1;
	}
}

static Dictionary<string, string> ParseNamedArgs(string[] args)
{
	var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
	foreach (var arg in args)
	{
		if (!arg.StartsWith("--", StringComparison.Ordinal))
		{
			continue;
		}

		var splitIndex = arg.IndexOf('=');
		if (splitIndex <= 2)
		{
			continue;
		}

		var key = arg[2..splitIndex];
		var value = arg[(splitIndex + 1)..];
		result[key] = value;
	}

	return result;
}

static bool TryGetRequiredOption(Dictionary<string, string> options, string name, out string value)
{
	if (options.TryGetValue(name, out var parsedValue) && !string.IsNullOrWhiteSpace(parsedValue))
	{
		value = parsedValue;
		return true;
	}

	value = string.Empty;
	return false;
}

static string GetOptionOrDefault(Dictionary<string, string> options, string name, string defaultValue)
{
	if (options.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value))
	{
		return value;
	}

	return defaultValue;
}

static SortMode ParseSortMode(string value)
{
	return value.ToLowerInvariant() switch
	{
		"repeaters-first" => SortMode.RepeatersFirst,
		"analog-first" => SortMode.AnalogFirst,
		_ => SortMode.Alpha,
	};
}

static HotspotTxPermitMode ParseHotspotMode(string value)
{
	return value.Equals("always", StringComparison.OrdinalIgnoreCase)
		? HotspotTxPermitMode.Always
		: HotspotTxPermitMode.SameColorCode;
}

static NicknameMode ParseNicknameMode(string value)
{
	return value.ToLowerInvariant() switch
	{
		"prefix" => NicknameMode.Prefix,
		"suffix" => NicknameMode.Suffix,
		"prefix-forced" => NicknameMode.PrefixForced,
		"suffix-forced" => NicknameMode.SuffixForced,
		_ => NicknameMode.Off,
	};
}
