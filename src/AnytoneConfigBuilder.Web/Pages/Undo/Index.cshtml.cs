using AnytoneConfigBuilder.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.IO.Compression;

namespace AnytoneConfigBuilder.Web.Pages.Undo;

public class IndexModel : PageModel
{
    private const long MaxUploadSizeBytes = 2 * 1024 * 1024;

    private readonly UndoBuilder _undoBuilder = new();

    public string ErrorMessage { get; private set; } = string.Empty;

    public string InfoMessage { get; private set; } = "Upload CPS export CSVs to reconstruct analog.csv, digital-others.csv, digital-repeaters.csv, and talkgroups.csv.";

    public async Task<IActionResult> OnPostAsync(IFormFile? channels, IFormFile? zones, IFormFile? talkgroups)
    {
        var validationError = ValidateFile("channels.csv", channels)
            ?? ValidateFile("zones.csv", zones)
            ?? ValidateFile("talkgroups.csv", talkgroups);

        if (validationError is not null)
        {
            ErrorMessage = validationError;
            return Page();
        }

        var rootTempDir = Path.Combine(Path.GetTempPath(), "acb-undo", Guid.NewGuid().ToString("N"));
        var inputDir = Path.Combine(rootTempDir, "input");
        var outputDir = Path.Combine(rootTempDir, "output");

        Directory.CreateDirectory(inputDir);
        Directory.CreateDirectory(outputDir);

        try
        {
            var channelsPath = Path.Combine(inputDir, "channels.csv");
            var zonesPath = Path.Combine(inputDir, "zones.csv");
            var talkgroupsPath = Path.Combine(inputDir, "talkgroups.csv");

            await using (var stream = System.IO.File.Create(channelsPath))
            {
                await channels!.CopyToAsync(stream);
            }

            await using (var stream = System.IO.File.Create(zonesPath))
            {
                await zones!.CopyToAsync(stream);
            }

            await using (var stream = System.IO.File.Create(talkgroupsPath))
            {
                await talkgroups!.CopyToAsync(stream);
            }

            _undoBuilder.Run(channelsPath, zonesPath, talkgroupsPath, outputDir);

            var zipPath = Path.Combine(rootTempDir, "undo-output.zip");
            ZipFile.CreateFromDirectory(outputDir, zipPath);
            var zipBytes = await System.IO.File.ReadAllBytesAsync(zipPath);

            return File(zipBytes, "application/zip", "acb-undo-output.zip");
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = $"❌ Validation Error: {ex.Message}";
            return Page();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"❌ Unexpected Error: {ex.Message}";
            return Page();
        }
        finally
        {
            try
            {
                if (Directory.Exists(rootTempDir))
                {
                    Directory.Delete(rootTempDir, recursive: true);
                }
            }
            catch
            {
                // Best effort cleanup.
            }
        }
    }

    private static string? ValidateFile(string label, IFormFile? file)
    {
        if (file is null)
        {
            return $"❌ {label} is required. Please select the file to upload.";
        }

        if (file.Length == 0)
        {
            return $"❌ {label} is empty. The file contains no data.";
        }

        if (file.Length > MaxUploadSizeBytes)
        {
            var sizeMb = file.Length / (1024.0 * 1024.0);
            return $"❌ {label} is too large ({sizeMb:F2} MB). Maximum allowed size is 2 MB.";
        }

        return null;
    }
}
