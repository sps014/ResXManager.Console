using ResXManager.Model;
using Spectre.Console;

namespace ResXManager.Console.Services;

public class Logger
{

    private static Logger? logger;
    public static Logger Instance
    {
        get
        {
            if (logger == null)
                logger = new Logger();
            return logger;
        }
    }
    private Logger()
    {

    }

    public void Log(LogLevel level, string message)
    {
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss").EscapeMarkup();
        string color = level switch
        {
            LogLevel.Info => "blue",
            LogLevel.Warn => "yellow",
            LogLevel.Error => "red",
            _ => "white"
        };
        AnsiConsole.MarkupLine($"[{color}]{level}:[/] {timestamp} {message.EscapeMarkup()}");
    }

    public void Log(LogLevel level, string message, ResourceTableEntry resource)
    {
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss").EscapeMarkup();
        string color = level switch
        {
            LogLevel.Info => "blue",
            LogLevel.Warn => "yellow",
            LogLevel.Error => "red",
            _ => "white"
        };

        AnsiConsole.MarkupLine($"[{color}]{level}:[/] {timestamp} {message.EscapeMarkup()}");

        var table = new Table();
        table.AddColumn("Type");
        table.AddColumn("Neutral");
        table.AddColumn("German");
        table.AddColumn("French");


        var neutralSnapshotValue = resource.SnapshotValues.GetValue(null) ?? string.Empty;
        var deSnapshotValue = resource.SnapshotValues.GetValue(DiffExporterService.Instance.GermanKey) ?? string.Empty;
        var frSnapshotValue = resource.SnapshotValues.GetValue(DiffExporterService.Instance.FrenchKey) ?? string.Empty;


        var neutralText = resource.Values.GetValue(null) ?? string.Empty;
        var deText = resource.Values.GetValue(DiffExporterService.Instance.GermanKey) ?? string.Empty;
        var frText = resource.Values.GetValue(DiffExporterService.Instance.FrenchKey) ?? string.Empty;

        table.AddRow("Resx", neutralText.EscapeMarkup(), deText.EscapeMarkup(), frText.EscapeMarkup());
        table.AddRow("Snapshot", neutralSnapshotValue.EscapeMarkup(), deSnapshotValue.EscapeMarkup(), frSnapshotValue.EscapeMarkup());

        table.Border(TableBorder.Square);


        AnsiConsole.Write(table);

        AnsiConsole.MarkupLine("\n");
    }

    public void Info(string message) => Log(LogLevel.Info, message);
    public void Warn(string message) => Log(LogLevel.Warn, message);
    public void Error(string message) => Log(LogLevel.Error, message);

    public void Info(ResourceTableEntry resource, string message) => Log(LogLevel.Info, message, resource);
    public void Warn(ResourceTableEntry resource, string message) => Log(LogLevel.Warn, message, resource);
    public void Error(ResourceTableEntry resource, string message) => Log(LogLevel.Error, message, resource);

    public void Exception(Exception ex) => AnsiConsole.WriteException(ex);

}

public enum LogLevel
{
    Info,
    Warn,
    Error
}