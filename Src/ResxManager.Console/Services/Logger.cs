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

    public void Info(string message) => Log(LogLevel.Info, message);
    public void Warn(string message) => Log(LogLevel.Warn, message);
    public void Error(string message) => Log(LogLevel.Error, message);
    public void Exception(Exception ex) => AnsiConsole.WriteException(ex);

}

    public enum LogLevel
    {
        Info,
        Warn,
        Error
    }