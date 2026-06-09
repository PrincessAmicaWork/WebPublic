using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Lagerverwaltung.Web.Services;

public record ApproverFolderImportResult(
    int RawFilesProcessed,
    int OutputFilesImported,
    int ImportedRows,
    int Added,
    int Updated,
    int Deactivated,
    List<string> Messages);

public interface IApproverFolderImportRunner
{
    Task<ApproverFolderImportResult> RunOnceAsync(
        CancellationToken token = default);
}

public class ApproverCsvImportRunner : IApproverFolderImportRunner
{
    private readonly ApproverCsvImportOptions _options;
    private readonly IApproverCsvReader _csv;
    private readonly IApproverService _approvers;
    private readonly ILogger<ApproverCsvImportRunner> _log;

    public ApproverCsvImportRunner(
        IOptions<ApproverCsvImportOptions> options,
        IApproverCsvReader csv,
        IApproverService approvers,
        ILogger<ApproverCsvImportRunner> log)
    {
        _options = options.Value;
        _csv = csv;
        _approvers = approvers;
        _log = log;
    }

    public async Task<ApproverFolderImportResult> RunOnceAsync(
        CancellationToken token = default)
    {
        EnsureFolders();

        var messages = new List<string>();
        var rawFilesProcessed = 0;
        var outputFilesImported = 0;
        var importedRows = 0;
        var added = 0;
        var updated = 0;
        var deactivated = 0;

        var runId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string? createdOutputPath = null;

        try
        {
            var rawFiles = Directory
                .GetFiles(_options.RawFolder, "*.csv")
                .Where(IsFileReadyForImport)
                .OrderBy(x => x)
                .ToList();

            if (rawFiles.Count > 0)
            {
                if (rawFiles.Count < _options.ExpectedRawFiles)
                {
                    messages.Add(
                        $"Raw CSVs found, but only {rawFiles.Count}/{_options.ExpectedRawFiles}. Waiting for all files.");

                    return new ApproverFolderImportResult(
                        rawFilesProcessed,
                        outputFilesImported,
                        importedRows,
                        added,
                        updated,
                        deactivated,
                        messages);
                }

                var mergedRows = new List<ApproverImportRow>();

                foreach (var rawFile in rawFiles)
                {
                    token.ThrowIfCancellationRequested();

                    var rows = await _csv.ReadFileAsync(rawFile, token);
                    mergedRows.AddRange(rows);
                }

                var normalizedRows = mergedRows
                    .Where(r =>
                        !string.IsNullOrWhiteSpace(r.DisplayName) &&
                        !string.IsNullOrWhiteSpace(r.Email))
                    .GroupBy(r => NormalizeEmail(r.Email))
                    .Where(g => !string.IsNullOrWhiteSpace(g.Key))
                    .Select(g => g.First())
                    .OrderBy(r => r.DisplayName)
                    .ToList();

                if (normalizedRows.Count == 0)
                    throw new InvalidOperationException(
                        "Raw CSV files contained no valid approver rows.");

                var outputPath = Path.Combine(
                    _options.OutputFolder,
                    $"approvers_{runId}.csv");

                await _csv.WriteFileAsync(outputPath, normalizedRows, token);
                createdOutputPath = outputPath;

                var rawHistoryRunFolder = Path.Combine(
                    _options.RawHistoryFolder,
                    runId);

                Directory.CreateDirectory(rawHistoryRunFolder);

                foreach (var rawFile in rawFiles)
                {
                    token.ThrowIfCancellationRequested();

                    MoveToFolder(rawFile, rawHistoryRunFolder);
                    rawFilesProcessed++;
                }

                messages.Add(
                    $"Created normalized output CSV with {normalizedRows.Count} rows.");
            }

            var outputFiles = Directory
                .GetFiles(_options.OutputFolder, "*.csv")
                .Where(IsFileReadyForImport)
                .OrderBy(x => x)
                .ToList();

            // Important:
            // IsFileReadyForImport respects MinimumFileAgeSeconds. That is correct for
            // externally copied files, but a CSV created by this same runner has just
            // been fully written and closed by WriteFileAsync. Queue it explicitly so
            // raw -> output -> database happens in the same run instead of waiting for
            // the next PollMinutes interval.
            if (!string.IsNullOrWhiteSpace(createdOutputPath) &&
                File.Exists(createdOutputPath))
            {
                outputFiles.RemoveAll(x =>
                    string.Equals(
                        x,
                        createdOutputPath,
                        StringComparison.OrdinalIgnoreCase));

                outputFiles.Add(createdOutputPath);

                messages.Add(
                    $"Queued freshly created output CSV for immediate import: {Path.GetFileName(createdOutputPath)}.");
            }

            foreach (var outputFile in outputFiles)
            {
                try
                {
                    token.ThrowIfCancellationRequested();

                    var rows = await _csv.ReadFileAsync(outputFile, token);

                    if (rows.Count == 0)
                        throw new InvalidOperationException(
                            "Output CSV contained no valid approver rows.");

                    var importResult =
                        await _approvers.ReplaceFromImportAsync(rows);

                    importedRows += rows.Count;
                    added += importResult.Added;
                    updated += importResult.Updated;
                    deactivated += importResult.Deactivated;
                    outputFilesImported++;

                    var resultInfo = new
                    {
                        ImportedAt = DateTime.Now,
                        SourceFile = Path.GetFileName(outputFile),
                        Rows = rows.Count,
                        importResult.Added,
                        importResult.Updated,
                        importResult.Deactivated
                    };

                    if (_options.KeepImportedFiles)
                    {
                        var importedPath = MoveToFolder(
                            outputFile,
                            _options.ImportedFolder);

                        var resultPath =
                            Path.ChangeExtension(importedPath, ".result.json");

                        await File.WriteAllTextAsync(
                            resultPath,
                            JsonSerializer.Serialize(
                                resultInfo,
                                new JsonSerializerOptions
                                {
                                    WriteIndented = true
                                }),
                            token);
                    }
                    else
                    {
                        File.Delete(outputFile);
                    }

                    messages.Add(
                        $"Imported {Path.GetFileName(outputFile)}: Added={importResult.Added}, Updated={importResult.Updated}, Deactivated={importResult.Deactivated}");
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _log.LogError(
                        ex,
                        "Failed to import output CSV {File}",
                        outputFile);

                    try
                    {
                        MoveToFolder(outputFile, _options.ErrorFolder);
                    }
                    catch (Exception moveEx)
                    {
                        _log.LogError(
                            moveEx,
                            "Failed to move failed output CSV {File} to error folder {ErrorFolder}",
                            outputFile,
                            _options.ErrorFolder);
                    }

                    messages.Add(
                        $"Failed to import {Path.GetFileName(outputFile)}: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Approver folder import failed.");
            messages.Add($"Folder import failed: {ex.Message}");
        }

        return new ApproverFolderImportResult(
            rawFilesProcessed,
            outputFilesImported,
            importedRows,
            added,
            updated,
            deactivated,
            messages);
    }

    private void EnsureFolders()
    {
        Directory.CreateDirectory(_options.RawFolder);
        Directory.CreateDirectory(_options.OutputFolder);
        Directory.CreateDirectory(_options.ImportedFolder);
        Directory.CreateDirectory(_options.RawHistoryFolder);
        Directory.CreateDirectory(_options.ErrorFolder);
    }

    private static string NormalizeEmail(string? email)
    {
        return email?.Trim().ToLowerInvariant() ?? "";
    }

    private bool IsFileReadyForImport(string path)
    {
        try
        {
            var minimumAge = TimeSpan.FromSeconds(
                Math.Max(0, _options.MinimumFileAgeSeconds));

            var lastWriteAge = DateTime.UtcNow - File.GetLastWriteTimeUtc(path);

            if (lastWriteAge < minimumAge)
                return false;

            using var stream = File.Open(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.None);

            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string MoveToFolder(string sourcePath, string targetFolder)
    {
        Directory.CreateDirectory(targetFolder);

        var fileName = Path.GetFileNameWithoutExtension(sourcePath);
        var ext = Path.GetExtension(sourcePath);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        var targetPath = Path.Combine(
            targetFolder,
            $"{fileName}_{stamp}{ext}");

        targetPath = GetUniquePath(targetPath);

        File.Move(sourcePath, targetPath);

        return targetPath;
    }

    private static string GetUniquePath(string path)
    {
        if (!File.Exists(path))
            return path;

        var folder = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);

        for (var i = 1; ; i++)
        {
            var candidate = Path.Combine(folder, $"{name}_{i}{ext}");

            if (!File.Exists(candidate))
                return candidate;
        }
    }
}
