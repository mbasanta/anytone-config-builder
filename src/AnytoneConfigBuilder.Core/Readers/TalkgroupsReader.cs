using CsvHelper;
using System.Globalization;

namespace AnytoneConfigBuilder.Core.Readers;

public sealed class TalkgroupsReader
{
    public (Dictionary<string, string> Mapping, Dictionary<string, int> Order) Read(Stream talkgroupsCsv)
    {
        if (talkgroupsCsv.CanSeek)
        {
            talkgroupsCsv.Seek(0, SeekOrigin.Begin);
        }

        using var reader = new StreamReader(talkgroupsCsv, leaveOpen: true);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var order = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var index = 1;
        while (csv.Read())
        {
            var name = (csv.GetField(0) ?? string.Empty).Trim();
            var id = (csv.GetField(1) ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            mapping[name] = id;
            order[name] = index++;
        }

        return (mapping, order);
    }
}
