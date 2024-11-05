
using DocumentFormat.OpenXml.Bibliography;
using ResXManager.Infrastructure;
using ResXManager.Model;
using Spectre.Console;
using TomsToolbox.Essentials;

namespace ResXManager.Console.Services;

public class DiffExporterService
{
    private readonly CultureKey germanKey = new CultureKey("de");
    private readonly CultureKey frenchKey = new CultureKey("fr");

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

    private string GetUniqueKey(ResourceTableEntry resource)
    {
        return resource.Container.ProjectName + resource.Container.BaseName;
    }

    public int Export(Scripting.Host resxHost)
    {
        if (resxHost is null)
            return -1;

        //get grouped entries
        var entries = resxHost.ResourceManager.TableEntries;
        var containerMap = new Dictionary<string, List<ResourceTableEntry>>();
        var changedResources = new List<DiffItem>();

        HashSet<string> invariantStrings = new();

        foreach (var entry in entries)
        {
            var uniqueContainerKey = GetUniqueKey(entry);
            if (containerMap.ContainsKey(uniqueContainerKey))
                containerMap[uniqueContainerKey].Add(entry);
            else
                containerMap[uniqueContainerKey] = new List<ResourceTableEntry>() { entry };


            var neutralText = entry.Values.GetValue(null) ?? string.Empty;
            var deText = entry.Values.GetValue(germanKey) ?? string.Empty;
            var frText = entry.Values.GetValue(frenchKey) ?? string.Empty;

            //if entry is invariant , we can skip it
            if (entry.IsInvariant)
            {
                invariantStrings.Add(neutralText);
                continue;
            }


            if (entry.SnapshotValues == null)
            {
                changedResources.Add(new DiffItem(entry, DiffType.New)
                {
                    NewFrenchValue = frText,
                    NewGermanValue = deText,
                    NewNeutralValue = neutralText,
                });
                continue;
            }

            var neutralSnapshotValue = entry.SnapshotValues.GetValue(null) ?? string.Empty;
            var deSnapshotValue = entry.SnapshotValues.GetValue(germanKey) ?? string.Empty;
            var frSnapshotValue = entry.SnapshotValues.GetValue(frenchKey) ?? string.Empty;

            var diffType = DiffType.None;

            //english value is deleted, only present in diff
            if (string.IsNullOrEmpty(neutralText) && !string.IsNullOrEmpty(neutralSnapshotValue))
            {
                diffType = DiffType.NeutralDeleted;
            }

            // english is added , but was not in snapshot 
            else if (!string.IsNullOrEmpty(neutralText) && string.IsNullOrEmpty(neutralSnapshotValue))
            {
                diffType = DiffType.NeutralAdded;
            }

            //or value and snapshot text is not matching for english
            else if (!string.Equals(neutralText, neutralSnapshotValue, StringComparison.Ordinal))
            {
                diffType |= DiffType.NeutralChanged;
            }

            //french value is deleted, only present in diff
            if (string.IsNullOrEmpty(frText) && !string.IsNullOrEmpty(frSnapshotValue))
            {
                diffType |= DiffType.FrenchDeleted;
            }

            // french is added , but was not in snapshot
            else if (!string.IsNullOrEmpty(frText) && string.IsNullOrEmpty(frSnapshotValue))
            {
                diffType |= DiffType.FrenchAdded;
            }

            //or value and snapshot text is not matching for french
            else if (!string.Equals(frText, frSnapshotValue, StringComparison.Ordinal))
            {
                diffType |= DiffType.FrenchChanged;
            }


            //german value is deleted, only present in diff
            if (string.IsNullOrEmpty(deText) && !string.IsNullOrEmpty(deSnapshotValue))
            {
                diffType |= DiffType.GermanDeleted;
            }

            // german is added , but was not in snapshot, or value and snapshot text is not matching for german
            else if (!string.IsNullOrEmpty(deText) && string.IsNullOrEmpty(deSnapshotValue))
            {
                diffType |= DiffType.GermanAdded;
            }

            //or value and snapshot text is not matching for german
            else if (!string.Equals(deText, deSnapshotValue, StringComparison.Ordinal))
            {
                diffType |= DiffType.GermanChanged;
            }



            if (diffType != DiffType.None)
            {
                //if all entries are added, it is a new entry
                if (diffType.HasFlag(DiffType.NeutralAdded) && diffType.HasFlag(DiffType.FrenchAdded) || diffType.HasFlag(DiffType.GermanAdded))
                {
                    diffType = DiffType.New;
                }

                changedResources.Add(new DiffItem(entry, diffType)
                {
                    NewFrenchValue = frText,
                    NewGermanValue = deText,
                    NewNeutralValue = neutralText,
                });
                Logger.Instance.Info($"Found change in \"{entry.Container.ProjectName}/{entry.Container.UniqueName}\" -> {entry.Key} -> {GetEnumNames(diffType)}");
            }
        }

        FineGrainDiffData(changedResources, containerMap, invariantStrings);
        return 0;

    }


    private bool HasEnumFlag(DiffType diffType, DiffType target)
    {
        return diffType.HasFlag(target);
    }

    private bool HasEnumFlags(DiffType diffType, params DiffType[] target)
    {
        return target.Where(x => x != DiffType.None).All(x => diffType.HasFlag(x));
    }


    private string GetEnumNames(DiffType diffType)
    {
        return string.Join(",", Enum.GetValues(typeof(DiffType))
        .Cast<DiffType>()
        .Where(x => diffType.HasFlag(x))
        .Select(x => x.ToString()));
    }
    private void FineGrainDiffData(List<DiffItem> items, Dictionary<string, List<ResourceTableEntry>> containerMap, HashSet<string> invariants)
    {
        List<DiffItem> itemsToRemove = new List<DiffItem>();

        foreach (var item in items)
        {
            //remove invariant keys
            if (IsInvariant(item, invariants))
            {
                itemsToRemove.Add(item);
                Logger.Instance.Warn($"Removing invariant key \"{item.Resource.Container.ProjectName}/{item.Resource.Container.UniqueName}\" -> {item.Resource.Key}");
            }
            else if (IsNeutralDeleted(item))
            {
                itemsToRemove.Add(item);
                Logger.Instance.Warn($"Removing item due to deleted neutral key \"{item.Resource.Container.ProjectName}/{item.Resource.Container.UniqueName}\" -> {item.Resource.Key}");
            }
            else if (IsEnglishAndGermanChangedButNotFrench(item))
            {
                //clear french value if english or german value is changed
                item.NewFrenchValue = string.Empty;

                Logger.Instance.Warn($"Clearing french value due to changed english and German for \"{item.Resource.Container.ProjectName}/{item.Resource.Container.UniqueName}\" -> {item.Resource.Key}" );
            }
            //if only german changed no neutral then
            else if (IsGermanOnlyChangedButNotFrench(item))
            {
                itemsToRemove.Add(item);

                Logger.Instance.Warn($"Removing item due to only changed german text for \"{item.Resource.Container.ProjectName}/{item.Resource.Container.UniqueName}\" -> {item.Resource.Key}");
            }
            else if (GermanIsMissing(item))
            {
                Logger.Instance.Warn($"German is missing for {item.Resource.Container.ProjectName}/{item.Resource.Container.UniqueName}/{item.Resource.Key}");
            }
            else if (IsFrenchIsNonEmptyForNewKey(item))
            {
                itemsToRemove.Add(item);
                Logger.Instance.Warn($"Removing item due to non empty french value for new key \"{item.Resource.Container.ProjectName}/{item.Resource.Container.UniqueName}\" -> {item.Resource.Key}");
            }

            // if (IsResxKeyRenamed(item, containerMap[GetUniqueKey(item.Resource)]))
            // {
            //     itemsToRemove.Add(item);
            // }
        }

    }

    private bool GermanIsMissing(DiffItem item)
    {
        return string.IsNullOrEmpty(item.NewGermanValue);
    }

    private bool IsInvariant(DiffItem item, HashSet<string> invariantStrings)
    {
        return invariantStrings.Contains(item.NewNeutralValue);
    }


    private bool IsNeutralDeleted(DiffItem item)
    {
        return item.DiffType.HasFlag(DiffType.NeutralDeleted);
    }

    private bool IsFrenchIsNonEmptyForNewKey(DiffItem item)
    {
        return HasEnumFlag(item.DiffType, DiffType.New)
            && !string.IsNullOrEmpty(item.Resource.Values.GetValue(frenchKey));
    }
    private bool IsEnglishAndGermanChangedButNotFrench(DiffItem item)
    {
        //english and german value is changed but not french
        return (item.DiffType.HasFlag(DiffType.NeutralAdded) || item.DiffType.HasFlag(DiffType.NeutralChanged))
            && (item.DiffType.HasFlag(DiffType.GermanAdded) || item.DiffType.HasFlag(DiffType.GermanChanged))
         && (!item.DiffType.HasFlag(DiffType.FrenchChanged));
    }
    private bool IsGermanOnlyChangedButNotFrench(DiffItem item)
    {
        //only german value is changed but not french
        return !item.DiffType.HasFlag(DiffType.NeutralAdded) && !item.DiffType.HasFlag(DiffType.NeutralChanged)
            && (item.DiffType.HasFlag(DiffType.GermanAdded) || item.DiffType.HasFlag(DiffType.GermanChanged))
         && (!item.DiffType.HasFlag(DiffType.FrenchChanged));
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


public record DiffItem(ResourceTableEntry Resource, DiffType DiffType)
{
    public string NewNeutralValue { get; set; } = string.Empty;
    public string NewFrenchValue { get; set; } = string.Empty;
    public string NewGermanValue { get; set; } = string.Empty;
};

[Flags]
public enum DiffType
{
    None = 0,
    New = 1,
    NeutralAdded = 2,
    FrenchAdded = 4,
    GermanAdded = 8,
    GermanChanged = 16,
    FrenchChanged = 32,
    NeutralChanged = 64,
    NeutralDeleted = 128,
    FrenchDeleted = 256,
    GermanDeleted = 512,

}