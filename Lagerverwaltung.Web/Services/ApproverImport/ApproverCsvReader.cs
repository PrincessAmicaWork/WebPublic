using System.Text;

namespace Lagerverwaltung.Web.Services;

public interface IApproverCsvReader
{
    Task<List<ApproverImportRow>> ReadAsync(
        Stream stream,
        CancellationToken token = default);

    Task<List<ApproverImportRow>> ReadFileAsync(
        string path,
        CancellationToken token = default);

    Task WriteFileAsync(
        string path,
        IEnumerable<ApproverImportRow> rows,
        CancellationToken token = default);
}

public class ApproverCsvReader : IApproverCsvReader
{
    public async Task<List<ApproverImportRow>> ReadFileAsync(
        string path,
        CancellationToken token = default)
    {
        await using var stream = File.OpenRead(path);
        return await ReadAsync(stream, token);
    }

    public async Task<List<ApproverImportRow>> ReadAsync(
        Stream stream,
        CancellationToken token = default)
    {
        var rows = new List<ApproverImportRow>();

        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 1024,
            leaveOpen: true);

        var headerLine = await reader.ReadLineAsync();

        if (string.IsNullOrWhiteSpace(headerLine))
            return rows;

        var commaHeaders = ParseCsvLine(headerLine, ',');
        var semicolonHeaders = ParseCsvLine(headerLine, ';');

        var delimiter = semicolonHeaders.Count > commaHeaders.Count ? ';' : ',';
        var headers = delimiter == ';' ? semicolonHeaders : commaHeaders;

        headers = headers
            .Select(h => h.Trim().TrimStart('\uFEFF'))
            .ToList();

        var displayNameIndexes = FindHeaderIndexes(
            headers,
            "DisplayName",
            "Display name",
            "displayName",
            "name");

        var emailIndexes = FindHeaderIndexes(
            headers,
            "Email",
            "mail",
            "userPrincipalName",
            "User principal name",
            "UPN");

        if (displayNameIndexes.Count == 0 || emailIndexes.Count == 0)
            return rows;

        string? line;

        while ((line = await reader.ReadLineAsync()) is not null)
        {
            token.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(line))
                continue;

            var fields = ParseCsvLine(line, delimiter);

            var displayName = FirstNonEmpty(fields, displayNameIndexes);
            var email = FirstNonEmpty(fields, emailIndexes);

            if (string.IsNullOrWhiteSpace(displayName) ||
                string.IsNullOrWhiteSpace(email))
            {
                continue;
            }

            rows.Add(new ApproverImportRow(
                displayName.Trim(),
                email.Trim()));
        }

        return rows;
    }

    public async Task WriteFileAsync(
        string path,
        IEnumerable<ApproverImportRow> rows,
        CancellationToken token = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);

        await using var writer = new StreamWriter(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        await writer.WriteLineAsync("DisplayName,Email");

        foreach (var row in rows)
        {
            token.ThrowIfCancellationRequested();

            await writer.WriteLineAsync(
                $"{Csv(row.DisplayName)},{Csv(row.Email)}");
        }
    }

    private static List<int> FindHeaderIndexes(
        List<string> headers,
        params string[] acceptedNames)
    {
        var indexes = new List<int>();

        for (var i = 0; i < headers.Count; i++)
        {
            foreach (var acceptedName in acceptedNames)
            {
                if (string.Equals(
                    headers[i],
                    acceptedName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    indexes.Add(i);
                    break;
                }
            }
        }

        return indexes;
    }

    private static string FirstNonEmpty(
        List<string> fields,
        List<int> indexes)
    {
        foreach (var index in indexes)
        {
            if (index < fields.Count &&
                !string.IsNullOrWhiteSpace(fields[index]))
            {
                return fields[index];
            }
        }

        return "";
    }

    private static List<string> ParseCsvLine(string line, char delimiter)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == delimiter && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        result.Add(current.ToString());

        return result;
    }

    private static string Csv(string value)
    {
        value ??= "";

        var mustQuote =
            value.Contains(',') ||
            value.Contains('"') ||
            value.Contains('\n') ||
            value.Contains('\r');

        if (!mustQuote)
            return value;

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}