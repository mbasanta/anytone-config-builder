using AnytoneConfigBuilder.Core;
using AnytoneConfigBuilder.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.IO.Compression;

namespace AnytoneConfigBuilder.Web.Pages;

public class IndexModel : PageModel
{
    private const long MaxUploadSizeBytes = 2 * 1024 * 1024;

    private readonly ConfigBuilder _configBuilder = new();

    public List<string> Errors { get; } = [];

    public List<string> Warnings { get; } = [];

    public string InfoMessage { get; private set; } = string.Empty;

    public bool ShowDownloadOptions { get; set; } = false;

    public void OnGet()
    {
        InfoMessage = "Upload your source CSV files to generate channels.csv, zones.csv, scanlists.csv, and talkgroups.csv.";
    }

    public async Task<IActionResult> OnPostAsync(
        IFormFile? analog,
        IFormFile? digitalothers,
        IFormFile? digitalrepeaters,
        IFormFile? talkgroups,
        string? sort,
        string? hotspot,
        string? nicknames)
    {
        // Validate all input files
        ValidateFile("Analog", analog);
        ValidateFile("Digital-Others", digitalothers);
        ValidateFile("Digital-Repeaters", digitalrepeaters);
        ValidateFile("TalkGroups", talkgroups);

        if (Errors.Count > 0)
        {
            return Page();
        }

        var rootTempDir = Path.Combine(Path.GetTempPath(), "acb-build", Guid.NewGuid().ToString("N"));
        var inputDir = Path.Combine(rootTempDir, "input");
        var outputDir = Path.Combine(rootTempDir, "output");
        Directory.CreateDirectory(inputDir);
        Directory.CreateDirectory(outputDir);

        try
        {
            var analogPath = Path.Combine(inputDir, "Analog.csv");
            var digitalOthersPath = Path.Combine(inputDir, "Digital-Others.csv");
            var digitalRepeatersPath = Path.Combine(inputDir, "Digital-Repeaters.csv");
            var talkgroupsPath = Path.Combine(inputDir, "TalkGroups.csv");

            await SaveUploadAsync(analog!, analogPath);
            await SaveUploadAsync(digitalothers!, digitalOthersPath);
            await SaveUploadAsync(digitalrepeaters!, digitalRepeatersPath);
            await SaveUploadAsync(talkgroups!, talkgroupsPath);

            using var analogStream = System.IO.File.OpenRead(analogPath);
            using var digitalOthersStream = System.IO.File.OpenRead(digitalOthersPath);
            using var digitalRepeatersStream = System.IO.File.OpenRead(digitalRepeatersPath);
            using var talkgroupsStream = System.IO.File.OpenRead(talkgroupsPath);

            var options = new BuildOptions(
                ParseSortMode(sort),
                ParseHotspotMode(hotspot),
                ParseNicknameMode(nicknames));

            var result = _configBuilder.Build(
                options,
                analogStream,
                digitalOthersStream,
                digitalRepeatersStream,
                talkgroupsStream,
                outputDir);

            Warnings.AddRange(result.Warnings);
            if (!result.Success)
            {
                Errors.AddRange(result.Errors.Select(e => $"Build Error: {e}"));
                return Page();
            }

            ShowDownloadOptions = true;
            HttpContext.Session.SetString("BuildOutputPath", outputDir);
            HttpContext.Session.SetString("BuildRootPath", rootTempDir);
            InfoMessage = "Build completed successfully! Download your files below.";
            
            return Page();
        }
        catch (InvalidOperationException ex)
        {
            Errors.Add($"Validation Error: {ex.Message}");
            return Page();
        }
        catch (Exception ex)
        {
            Errors.Add($"Unexpected Error: {ex.Message}");
            return Page();
        }
        finally
        {
            // Don't clean up immediately if successful - user may want to download files
        }
    }

    private static async Task SaveUploadAsync(IFormFile file, string destinationPath)
    {
        await using var stream = System.IO.File.Create(destinationPath);
        await file.CopyToAsync(stream);
    }

    private static SortMode ParseSortMode(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "repeaters-first" => SortMode.RepeatersFirst,
            "analog-first" => SortMode.AnalogFirst,
            _ => SortMode.Alpha,
        };
    }

    private static HotspotTxPermitMode ParseHotspotMode(string? value)
    {
        return value?.Equals("always", StringComparison.OrdinalIgnoreCase) == true
            ? HotspotTxPermitMode.Always
            : HotspotTxPermitMode.SameColorCode;
    }

    private static NicknameMode ParseNicknameMode(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "prefix" => NicknameMode.Prefix,
            "suffix" => NicknameMode.Suffix,
            "prefix-forced" => NicknameMode.PrefixForced,
            "suffix-forced" => NicknameMode.SuffixForced,
            _ => NicknameMode.Off,
        };
    }

    public async Task<IActionResult> OnGetDownloadZipAsync()
    {
        var outputDir = HttpContext.Session.GetString("BuildOutputPath");
        var rootDir = HttpContext.Session.GetString("BuildRootPath");

        if (string.IsNullOrEmpty(outputDir) || string.IsNullOrEmpty(rootDir))
        {
            return NotFound();
        }

        try
        {
            var zipPath = Path.Combine(rootDir, "builder-output.zip");
            if (System.IO.File.Exists(zipPath))
            {
                var bytes = await System.IO.File.ReadAllBytesAsync(zipPath);
                return File(bytes, "application/zip", "acb-builder-output.zip");
            }

            // Create zip on-the-fly if it doesn't exist
            ZipFile.CreateFromDirectory(outputDir, zipPath);
            var newBytes = await System.IO.File.ReadAllBytesAsync(zipPath);
            return File(newBytes, "application/zip", "acb-builder-output.zip");
        }
        catch
        {
            return NotFound();
        }
    }

    public async Task<IActionResult> OnGetDownloadFileAsync(string filename)
    {
        var outputDir = HttpContext.Session.GetString("BuildOutputPath");

        if (string.IsNullOrEmpty(outputDir))
        {
            return NotFound();
        }

        // Prevent directory traversal attacks
        if (filename.Contains("..") || filename.Contains("/") || filename.Contains("\\"))
        {
            return BadRequest();
        }

        var filePath = Path.Combine(outputDir, filename);

        // Ensure the file is within the output directory
        var fullOutputPath = Path.GetFullPath(outputDir);
        var fullFilePath = Path.GetFullPath(filePath);

        if (!fullFilePath.StartsWith(fullOutputPath))
        {
            return BadRequest();
        }

        if (!System.IO.File.Exists(filePath))
        {
            return NotFound();
        }

        var bytes = await System.IO.File.ReadAllBytesAsync(filePath);
        return File(bytes, "text/csv", filename);
    }

    private void ValidateFile(string label, IFormFile? file)
    {
        if (file is null)
        {
            Errors.Add($"❌ {label} CSV file is required. Please select a file to upload.");
            return;
        }

        if (file.Length == 0)
        {
            Errors.Add($"❌ {label} CSV file is empty. The file contains no data.");
            return;
        }

        if (file.Length > MaxUploadSizeBytes)
        {
            var sizeMb = file.Length / (1024.0 * 1024.0);
            Errors.Add($"❌ {label} CSV file is too large ({sizeMb:F2} MB). Maximum allowed size is 2 MB.");
            return;
        }

        if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            Errors.Add($"❌ {label} file '{file.FileName}' does not have a .csv extension. Please upload a CSV file.");
        }
    }
}
