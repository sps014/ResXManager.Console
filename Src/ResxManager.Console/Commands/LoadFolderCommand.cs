using ResXManager.Console.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ResXManager.Console.Commands;

public class LoadFolderCommand : Command
{
    public Scripting.Host? Host { get; private set; }
    public override int Execute(CommandContext context)
    {
        var folderPath = AnsiConsole
            .Prompt(new TextPrompt<string>("[yellow]Please write the folder path for all resource files[/].:"))
            .Trim();


        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            Logger.Instance.Error($"folder path \"{folderPath}\" does not exists");
            return -1;
        }


        Host = new();

        try
        {
            AnsiConsole.Status()
            .Start($"Loading Resx Files...", ctx =>
            {
                Logger.Instance.Info($"Loading folder data \"{folderPath}\" in resx host");

                Host.Load(folderPath);

                Logger.Instance.Info($"Loading completed for folder data \"{folderPath}\" in resx host");
            });
        }
        catch (Exception e)
        {
            Logger.Instance.Exception(e);
            return -1;
        }

        return 0;
    }
}