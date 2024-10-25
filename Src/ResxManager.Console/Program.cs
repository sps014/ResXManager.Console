using ResXManager.Console.Commands;
using Spectre.Console;
using Spectre.Console.Cli;

AnsiConsole.Clear();

var app = new CommandApp();

app.Configure(config =>
{
    config.AddCommand<ExportDiffCommand>("export-diff")
        .WithDescription("Create a excel from the snapshot diff");

    config.AddCommand<LoadFolderCommand>("load-folder")
        .IsHidden()
        .WithDescription("Load resx files in memory for performing operations");
});

return app.Run(args);