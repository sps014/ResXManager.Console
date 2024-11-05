namespace ResXManager.Console.Services;

public class ReportGenerator
{
    private StringWriter stringWriter;
    public ReportGenerator()
    {
        stringWriter = new StringWriter();
    }

}