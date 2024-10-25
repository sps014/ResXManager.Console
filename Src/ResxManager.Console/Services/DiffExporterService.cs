
namespace ResXManager.Console.Services;

public class DiffExporterService
{
    private static DiffExporterService? diffExporterService;
    public static DiffExporterService Instance
    {
        get
        {
            if (diffExporterService == null)
                diffExporterService = new DiffExporterService();
            return diffExporterService;
        }
    }
    private DiffExporterService()
    {

    }

    public int Export(Scripting.Host resxHost)
    {
        if (resxHost is null)
            return -1;

        return 0;
    }
}