using System.Diagnostics;
using ResXManager.Console.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ResXManager.Console.Commands;

public class ExportDiffCommand : Command<ExportDiffCommand.ExportDiffSettings>
{
    public class ExportDiffSettings : CommandSettings
    {
        [CommandArgument(0, "[FolderPath]")]
        public string? FolderPath { get; set; }

        [CommandArgument(1, "[SnapshotPath]")]
        public string? SnapshotPath { get; set; }
    }
    public override int Execute(CommandContext context, ExportDiffSettings settings)
    {

        AnsiConsole.Clear();

        LoadFolderCommand loadFolderCommand = new();
        var folderSettings = new LoadFolderCommand.LoadFolderSettings()
        {
            FolderPath = settings.FolderPath
        };

        if (loadFolderCommand.Execute(context, folderSettings) < 0)
            return -1;

        var snapshotFilePath = settings.SnapshotPath;

        if (snapshotFilePath == null)
        {
            AnsiConsole
            .Prompt(new TextPrompt<string>("[yellow]Please write the snapshot file path:[/].:"))
            .Trim();
        }

        if (string.IsNullOrWhiteSpace(snapshotFilePath) || !File.Exists(snapshotFilePath) || Path.GetExtension(snapshotFilePath) != ".snapshot")
        {
            Logger.Instance.Error($"folder path \"{snapshotFilePath}\" does not exists or extension is invalid");
            return -1;
        }

        try
        {
            AnsiConsole.Status()
            .Start($"Loading Snapshot...", ctx =>
            {
                Logger.Instance.Info($"Loading snapshot data \"{snapshotFilePath}\" in resx host");

                loadFolderCommand.Host!.LoadSnapshot(File.ReadAllText(snapshotFilePath));

                Logger.Instance.Info($"Loaded snapshot data \"{snapshotFilePath}\" in resx host");
            });
        }
        catch (Exception e)
        {
            Logger.Instance.Exception(e);
            return -1;
        }

        if (DiffExporterService.Instance.Export(loadFolderCommand.Host!) < 0)
            return -1;

        return 0;
    }
}