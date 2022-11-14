using System.Collections.Concurrent;
using System.Text.RegularExpressions;

// Extract data from downloaded collection file (.html)
Console.Write("Your collection file: ");
var inputText = File.ReadAllText(Console.ReadLine() ?? "");

// Ignore collection in extracted links
Console.Write("Your collection ID: ");
var collectionID = Console.ReadLine() ?? "";

List<string>? workShopLinks = GetWorkshopLinks(inputText);
if (workShopLinks is null) return;

List<string>? workshopIds = GetWorkshopIds(workShopLinks, collectionID);

HttpClient client = new();
int maxQueue = 5;

Regex modIdRegex = new(@"\b(?i)mod id: [\s\S].+?((?=<)|(?=\n))");
ConcurrentBag<string> modIds = new();

for (int i = 0; i < workShopLinks.Count; i += maxQueue)
{
    Console.WriteLine($"Starting {maxQueue} parallel downloads | Index: {i}");

    Task[] downloadJobs = new Task[maxQueue];
    for (int q = 0; q < maxQueue; q++)
    {
        int localScope = q;
        downloadJobs[localScope] = ScrapModId(workShopLinks, modIds, localScope + i, modIdRegex);
    }

    await Task.WhenAll(downloadJobs);
    Console.WriteLine("Waiting for next queue batch");
    Console.WriteLine();
    await Task.Delay(500);
}

var modIdsResult = modIds.ToList().Distinct().OrderBy(q => q);

// Format mods IDs result
string modsResult = string.Join(";", modIdsResult);
// Format workshop items ids
string workshopFinal = string.Join(";", workshopIds);

Console.WriteLine("<========>");
Console.WriteLine($"Mods IDs: {modsResult}");
Console.WriteLine("<========>");
Console.WriteLine($"Mods Workshop IDs: {workshopFinal}");
Console.WriteLine("<========>");
Console.WriteLine($"Item count: {workshopIds.Count}");

List<string>? GetWorkshopLinks(string input)
{
    // as for now mods are contained within div with "collectionChildren" class
    input = input[input.IndexOf("collectionChildren")..];
    var workshopLinksRegex = new Regex(@"\bhttps:\/\/steamcommunity.com\/sharedfiles\/filedetails\/\?id=[0-9]{10}");
    var workShopLinkMatches = workshopLinksRegex.Matches(input);

    return workShopLinkMatches?
        .Select(item => item.Value)
        .Distinct()
        .ToList();
}

List<string>? GetWorkshopIds(List<string> workshopLinks, string collectionId)
{
    List<string> workshopIds = new();

    // extract workshop item ids
    foreach (var workShopLink in workShopLinks)
    {
        if (string.IsNullOrEmpty(workShopLink)) continue;

        var result = workShopLink[(workShopLink.IndexOf('=') + 1)..];

        workshopIds.Add(result);
    }

    workshopIds = workshopIds.Distinct().ToList();
    workshopIds.Sort();

    var sourceCollection = workshopIds.FirstOrDefault(e => e == collectionId);
    if (!string.IsNullOrEmpty(sourceCollection))
        workshopIds.Remove(sourceCollection);

    return workshopIds;
}

async Task ScrapModId(List<string> workshopLinks, ConcurrentBag<string> modIds, int index, Regex regex)
{
    if (workshopLinks is null || index > workShopLinks?.Count - 1) return;

    var content = await client.GetStringAsync(workShopLinks![index]);

    var matches = regex.Matches(content);

    foreach (var match in matches)
    {
        if (match is null) continue;

        var result = match.ToString();
        if (string.IsNullOrEmpty(result)) continue;
        result = result[(result.LastIndexOf(": ") + 2)..];

        modIds.Add(result);
    }

    Console.WriteLine($"Downloaded {workShopLinks[index]} | Matches: {matches.Count}");
    Console.WriteLine(string.Join(", ", matches.Select(e => e.Value)));
}