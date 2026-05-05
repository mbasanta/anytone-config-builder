using AnytoneConfigBuilder.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AnytoneConfigBuilder.Web.Pages.Checker;

public class IndexModel : PageModel
{
    private const long MaxUploadSizeBytes = 1024 * 1024;

    private readonly ChannelChecker _checker = new();

    public string ErrorMessage { get; private set; } = string.Empty;

    public string InfoMessage { get; private set; } = "Upload a channels.csv file to generate a consistency report.";

    public string ReportHtml { get; private set; } = string.Empty;

    public void OnPost(IFormFile? channels)
    {
        if (channels is null)
        {
            ErrorMessage = "❌ channels.csv file is required. Please select the file to upload.";
            return;
        }

        if (channels.Length == 0)
        {
            ErrorMessage = "❌ channels.csv file is empty. The file contains no data.";
            return;
        }

        if (channels.Length > MaxUploadSizeBytes)
        {
            var sizeMb = channels.Length / (1024.0 * 1024.0);
            ErrorMessage = $"❌ channels.csv file is too large ({sizeMb:F2} MB). Maximum allowed size is 1 MB.";
            return;
        }

        try
        {
            using var stream = channels.OpenReadStream();
            ReportHtml = _checker.BuildHtmlReport(stream);
            InfoMessage = string.Empty;
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = $"❌ Validation Error: {ex.Message}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"❌ Unexpected Error: {ex.Message}";
        }
    }
}
