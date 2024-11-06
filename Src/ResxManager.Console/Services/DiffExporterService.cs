
using ClosedXML.Excel;
using ResXManager.Infrastructure;
using ResXManager.Model;
using Spectre.Console;

namespace ResXManager.Console.Services;

public class DiffExporterService
{
    internal readonly CultureKey GermanKey = new CultureKey("de");
    internal readonly CultureKey FrenchKey = new CultureKey("fr");

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
            var deText = entry.Values.GetValue(GermanKey) ?? string.Empty;
            var frText = entry.Values.GetValue(FrenchKey) ?? string.Empty;

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
            var deSnapshotValue = entry.SnapshotValues.GetValue(GermanKey) ?? string.Empty;
            var frSnapshotValue = entry.SnapshotValues.GetValue(FrenchKey) ?? string.Empty;

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
        return WriteToExcel(changedResources, AppDomain.CurrentDomain.BaseDirectory);

    }


    private bool HasEnumFlag(DiffType diffType, DiffType target)
    {
        return diffType.HasFlag(target);
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
                Logger.Instance.Warn(item.Resource, $"Removing invariant key \"{item.Resource.Container.ProjectName}/{item.Resource.Container.UniqueName}\" -> {item.Resource.Key}");
            }
            else if (IsNeutralDeleted(item))
            {
                itemsToRemove.Add(item);
                Logger.Instance.Warn(item.Resource, $"Removing item due to deleted neutral key \"{item.Resource.Container.ProjectName}/{item.Resource.Container.UniqueName}\" -> {item.Resource.Key}");
            }
            //if english is not null and french is null we need to add that string for sure
            else if(!string.IsNullOrWhiteSpace(item.NewNeutralValue) && string.IsNullOrWhiteSpace(item.NewFrenchValue))
            {
                continue;
            }

            else if (IsEnglishAndGermanChangedButNotFrench(item) || IsOnlyNeutralChanged(item))
            {
                //clear french value if english or german value is changed
                item.NewFrenchValue = string.Empty;

                Logger.Instance.Warn(item.Resource, $"Clearing french value due to changed english and German for \"{item.Resource.Container.ProjectName}/{item.Resource.Container.UniqueName}\" -> {item.Resource.Key}");
            }
            //if only french or German is changed we can skip the change
            else if (IsFrenchGermanChangedButNotEnglish(item))
            {
                itemsToRemove.Add(item);

                Logger.Instance.Warn(item.Resource, $"Removing item due to only changed French or German text only no neutral changes for \"{item.Resource.Container.ProjectName}/{item.Resource.Container.UniqueName}\" -> {item.Resource.Key}");
            }
            //if only german changed no neutral then
            else if (IsGermanOnlyChangedButNotFrench(item))
            {
                itemsToRemove.Add(item);

                Logger.Instance.Warn(item.Resource, $"Removing item due to only changed german text for \"{item.Resource.Container.ProjectName}/{item.Resource.Container.UniqueName}\" -> {item.Resource.Key}");
            }
            else if (GermanIsMissing(item))
            {
                Logger.Instance.Error(item.Resource, $"German is missing form (developer side) {item.Resource.Container.ProjectName}/{item.Resource.Container.UniqueName}/{item.Resource.Key}");
            }
            else if (IsFrenchIsNonEmptyForNewKey(item))
            {
                itemsToRemove.Add(item);
                Logger.Instance.Warn(item.Resource, $"Removing item due to non empty french value for new key \"{item.Resource.Container.ProjectName}/{item.Resource.Container.UniqueName}\" -> {item.Resource.Key}");
            }
        }

        foreach (var toRemove in itemsToRemove)
        {
            items.Remove(toRemove);
        }
    }

    private bool GermanIsMissing(DiffItem item)
    {
        return string.IsNullOrEmpty(item.NewGermanValue);
    }

    private bool IsInvariant(DiffItem item, HashSet<string> invariantStrings)
    {
        return invariantStrings.Contains(item.NewNeutralValue)
            || (item.NewGermanValue == item.NewNeutralValue && item.NewGermanValue == item.NewFrenchValue);
    }


    private bool IsNeutralDeleted(DiffItem item)
    {
        return item.DiffType.HasFlag(DiffType.NeutralDeleted);
    }

    private bool IsFrenchIsNonEmptyForNewKey(DiffItem item)
    {
        return HasEnumFlag(item.DiffType, DiffType.New)
            && !string.IsNullOrEmpty(item.Resource.Values.GetValue(FrenchKey));
    }
    private bool IsEnglishAndGermanChangedButNotFrench(DiffItem item)
    {
        //english and german value is changed but not french
        return (item.DiffType.HasFlag(DiffType.NeutralAdded) || item.DiffType.HasFlag(DiffType.NeutralChanged))
            && (item.DiffType.HasFlag(DiffType.GermanAdded) || item.DiffType.HasFlag(DiffType.GermanChanged))
         && (!item.DiffType.HasFlag(DiffType.FrenchChanged));
    }

    private bool IsFrenchGermanChangedButNotEnglish(DiffItem item)
    {
        //french or german value is changed but not english
        return (!item.DiffType.HasFlag(DiffType.NeutralAdded) && !item.DiffType.HasFlag(DiffType.NeutralChanged))
            && (item.DiffType.HasFlag(DiffType.GermanAdded) || item.DiffType.HasFlag(DiffType.GermanChanged)
         || item.DiffType.HasFlag(DiffType.FrenchChanged) || item.DiffType.HasFlag(DiffType.FrenchAdded));
    }
    private bool IsGermanOnlyChangedButNotFrench(DiffItem item)
    {
        //only german value is changed but not french
        return !item.DiffType.HasFlag(DiffType.NeutralAdded) && !item.DiffType.HasFlag(DiffType.NeutralChanged)
            && (item.DiffType.HasFlag(DiffType.GermanAdded) || item.DiffType.HasFlag(DiffType.GermanChanged))
         && (!item.DiffType.HasFlag(DiffType.FrenchChanged));
    }

    private bool IsOnlyNeutralChanged(DiffItem item)
    {
        return item.DiffType == DiffType.NeutralChanged || item.DiffType == DiffType.NeutralAdded;
    }

    private int WriteToExcel(List<DiffItem> items, string? outputDir)
    {
        outputDir ??= "";
        var file = Path.Combine(outputDir, $"diff{DateTime.UtcNow:yyyy-MM-dd_HH-mm-ss-fff}.xlsx");
        if (File.Exists(file))
            File.Delete(file);

        Logger.Instance.Info($"Exporting {items.Count} items to excel in path {outputDir}");

        try
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Sheet1");

            // write headers
            worksheet.Cell("A1").Value = "Project";
            worksheet.Cell("B1").Value = "File";
            worksheet.Cell("C1").Value = "Key";
            worksheet.Cell("D1").Value = "";
            worksheet.Cell("E1").Value = "Comment";

            worksheet.Cell("F1").Value = "de";
            worksheet.Cell("G1").Value = "Comment.de";
            worksheet.Cell("H1").Value = "fr";
            worksheet.Cell("I1").Value = "Comment.fr";

            var row = 2;
            var uniqueDiffs = items.ToHashSet();
            //write body
            foreach (var diffItem in uniqueDiffs)
            {
                var resource = diffItem.Resource;

                worksheet.Cell($"A{row}").Value = resource.Container.ProjectName;
                worksheet.Cell($"B{row}").Value = resource.Container.UniqueName;
                worksheet.Cell($"C{row}").Value = resource.Key;

                worksheet.Cell($"D{row}").Value = diffItem.NewNeutralValue;
                worksheet.Cell($"E{row}").Value = resource.Comments.GetValue(null);

                worksheet.Cell($"F{row}").Value = diffItem.NewGermanValue;
                worksheet.Cell($"G{row}").Value = resource.Comments.GetValue(GermanKey);

                worksheet.Cell($"H{row}").Value = diffItem.NewFrenchValue;
                worksheet.Cell($"I{row}").Value = resource.Comments.GetValue(FrenchKey);

                row++;
            }

            workbook.SaveAs(file);
            Logger.Instance.Info($"Exported {items.Count} items to excel in path \"{file}\"");
        }
        catch (Exception ex)
        {
            Logger.Instance.Exception(ex);
            return -1;
        }
        return 0;
    }

}


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