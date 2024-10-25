using System.Diagnostics;
using ResXManager.Console.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ResXManager.Console.Commands;

public class ExportDiffCommand : Command
{
    public override int Execute(CommandContext context)
    {

        AnsiConsole.Clear();

        LoadFolderCommand loadFolderCommand = new();

        if(loadFolderCommand.Execute(context)<0)
            return -1;

            var snapshotFilePath = AnsiConsole
            .Prompt(new TextPrompt<string>("[yellow]Please write the snapshot file[/].:"))
            .Trim();

        if (string.IsNullOrWhiteSpace(snapshotFilePath) || !File.Exists(snapshotFilePath) || Path.GetExtension(snapshotFilePath)!=".snapshot")
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

                loadFolderCommand.Host!.LoadSnapshot(snapshotFilePath);

                Logger.Instance.Info($"Loaded snapshot data \"{snapshotFilePath}\" in resx host");
            });
        }
        catch (Exception e)
        {
            Logger.Instance.Exception(e);
            return -1;
        }

        if(DiffExporterService.Instance.Export(loadFolderCommand.Host!)<0)
            return -1;

        return 0;
    }
}