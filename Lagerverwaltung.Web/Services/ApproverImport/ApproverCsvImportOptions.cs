namespace Lagerverwaltung.Web.Services;

public class ApproverCsvImportOptions
{
    public string Mode { get; set; } = "ManualOnly";

    public string RootFolder { get; set; } =
        @"\\sn0vm00047.emea.bosch.com\lagerverwaltung$";

    public int ExpectedRawFiles { get; set; } = 2;

    public int PollMinutes { get; set; } = 720; //1 Minute for Dev.

    public int MinimumFileAgeSeconds { get; set; } = 60;

    public bool RunOnStartup { get; set; } = true;

    public bool KeepImportedFiles { get; set; } = true;

    public string RawFolder => Path.Combine(RootFolder, "raw");
    public string OutputFolder => Path.Combine(RootFolder, "output");
    public string ImportedFolder => Path.Combine(RootFolder, "imported");
    public string RawHistoryFolder => Path.Combine(RootFolder, "raw-history");
    public string ErrorFolder => Path.Combine(RootFolder, "error");
}