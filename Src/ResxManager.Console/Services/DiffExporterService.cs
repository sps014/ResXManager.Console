
using ResXManager.Model;
using Spectre.Console;

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

        //get grouped entries
        var entries = resxHost.ResourceManager.TableEntries.GroupBy(x => x.Container);

        int count = 0;

        //access each group
        foreach (var gp in entries)
        {
            Logger.Instance.Info($"Finding Changes in {gp.First().Container.ProjectName}/{gp.First().Container.UniqueName}");

            //access each resource of group
            foreach (var resource in gp)
            {
                //check if invariant
                if (resource.IsInvariant)
                    continue;

                var changes = new List<DiffItem>();

                //read string of all supported cultures
                foreach (var lang in resource.Languages)
                {
                    var snap = resource.SnapshotValues.GetValue(lang.Culture); //get snap value
                    var value = resource.Values.GetValue(lang.Culture); // getcurrent value

                    //if is invariant or deleted , skip
                    if (resource.IsInvariant || string.IsNullOrWhiteSpace(resource.Values.GetValue(null)))
                    {
                        continue;
                    }

                    //if both are null skip
                    if (string.IsNullOrWhiteSpace(snap) && string.IsNullOrWhiteSpace(value))
                        continue;

                    //detect if snap value and current value are different
                    if ((string.IsNullOrWhiteSpace(snap) && !string.IsNullOrWhiteSpace(value)) ||
                        (!string.IsNullOrWhiteSpace(snap) && string.IsNullOrWhiteSpace(value)) ||
                        !snap.Equals(value, StringComparison.Ordinal))
                    {

                        var cultureText = lang.Culture == null ? string.Empty : lang.Culture.Name;
                        //add info to changes list
                        changes.Add(
                            new DiffItem(cultureText,
                            resource.Container.ProjectName,
                            resource.Container.UniqueName,
                            OldValue: snap,
                            value,
                            resource.Key,
                            resource
                        ));
                        count++;
                    }
                }
            }
        }
        return 0;

    }
}

// static void ExportExcelDiff(List<DiffItem> items, string excelFilePath)
//     {
//         if (items.Count == 0)
//         {
//             AnsiConsole.MarkupLine("[yellow]No Diff Found[/]");
//             return;
//         }

//         excelFilePath ??= "";
//         var file = Path.Combine(excelFilePath, $"diff{DateTime.UtcNow:yyyy-MM-dd_HH-mm-ss-fff}.xlsx");
//         if (File.Exists(file))
//             File.Delete(file);

//         using var writer = new ExcelWriter(file);
//         int row = 1;

//         //write 
//         writer.Write("Project", 1, row);
//         writer.Write("File", 2, row);
//         writer.Write("Key", 3, row);
//         writer.Write("", 4, row);
//         writer.Write("Comment", 5, row);

//         var set = items.Select(x => x.Resource).ToHashSet(); //get set of resource entries
//         int c = 6;
//         foreach (var lang in set.First().Languages)
//         {
//             if (lang.IsNeutral)
//                 continue;
//             var culture = lang.Culture == null ? string.Empty : lang.Culture.Name;
//             writer.Write(culture, c++, row);
//             writer.Write($"Comment.{culture}", c++, row);
//         }

//         row++;
//         //write body
//         foreach (var v in set)
//         {
//             c = 1;
//             writer.Write(v.Container.ProjectName, c++, row);
//             writer.Write(v.Container.UniqueName, c++, row);
//             writer.Write(v.Key, c++, row);

//             foreach (var lang in v.Languages)
//             {
//                 var value = v.Values.GetValue(lang.Culture);
//                 writer.Write(value, c++, row);
//                 writer.Write(v.Comments.GetValue(lang.Culture), c++, row);
//             }
//             row++;
//         }

//         AnsiConsole.MarkupLine($"[cyan]Written diff file at [/][yellow] {file.EscapeMarkup()} [/]");

//     }
//     public static void ImportDiff(string path, bool modify = false)
//     {
//         path ??= "diff.xlsx";

//         Program.ScriptHost.ImportExcel(path);

//         AnsiConsole.WriteLine();

//         //if want to save changes
//         if (modify)
//         {
//             Program.ScriptHost.Save();
//             AnsiConsole.MarkupLine("[lime]Excel Changes imported to Solution[/]");
//             return;
//         }
//         AnsiConsole.MarkupLine("[yellow]Excel Changes are not saved Solution, pass send flag[/]");

//     }

public record DiffItem(
    string CultureText,
    string ProjectName,
    string ResourceName,
    string? OldValue,
    string? NewValue,
    string Key,
    ResourceTableEntry Resource
);