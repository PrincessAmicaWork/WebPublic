using Microsoft.Extensions.Options;

namespace Lagerverwaltung.Web.Services;

public class ApproverCsvImportWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ApproverCsvImportOptions _options;
    private readonly ILogger<ApproverCsvImportWorker> _log;

    public ApproverCsvImportWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<ApproverCsvImportOptions> options,
        ILogger<ApproverCsvImportWorker> log)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation(
            "Approver CSV worker starting. Mode={Mode}, RunOnStartup={RunOnStartup}, PollMinutes={PollMinutes}, RootFolder={RootFolder}",
            _options.Mode,
            _options.RunOnStartup,
            _options.PollMinutes,
            _options.RootFolder);

        if (IsDisabledForBackground())
        {
            _log.LogInformation(
                "Approver CSV background import disabled. Mode={Mode}",
                _options.Mode);

            return;
        }

        if (_options.RunOnStartup && !IsMode("IntervalOnly"))
        {
            await RunImportAsync(stoppingToken);
        }

        if (IsMode("StartupOnly"))
        {
            _log.LogInformation(
                "Approver CSV startup import finished. Periodic import disabled because Mode={Mode}.",
                _options.Mode);

            return;
        }

        if (!IsMode("StartupAndInterval") && !IsMode("IntervalOnly"))
        {
            _log.LogWarning(
                "Unknown ApproverCsvImport:Mode value {Mode}. Periodic import disabled.",
                _options.Mode);

            return;
        }

        if (_options.PollMinutes <= 0)
        {
            _log.LogWarning(
                "Invalid ApproverCsvImport:PollMinutes value {PollMinutes}. Periodic import disabled.",
                _options.PollMinutes);

            return;
        }

        var period = TimeSpan.FromMinutes(_options.PollMinutes);

        _log.LogInformation(
            "Approver CSV periodic import enabled. PollMinutes={PollMinutes}, Period={Period}",
            _options.PollMinutes,
            period);

        using var timer = new PeriodicTimer(period);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunImportAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    private bool IsDisabledForBackground()
    {
        return IsMode("Disabled") || IsMode("ManualOnly");
    }

    private bool IsMode(string mode)
    {
        return string.Equals(
            _options.Mode?.Trim(),
            mode,
            StringComparison.OrdinalIgnoreCase);
    }

    private async Task RunImportAsync(CancellationToken token)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();

            var runner = scope.ServiceProvider
                .GetRequiredService<IApproverFolderImportRunner>();

            var result = await runner.RunOnceAsync(token);

            _log.LogInformation(
                "Approver folder import finished. RawFiles={RawFiles}, OutputFiles={OutputFiles}, Rows={Rows}, Added={Added}, Updated={Updated}, Deactivated={Deactivated}",
                result.RawFilesProcessed,
                result.OutputFilesImported,
                result.ImportedRows,
                result.Added,
                result.Updated,
                result.Deactivated);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Normal during shutdown.
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Approver CSV background import failed.");
        }
    }
}
