using System.CommandLine;
using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Marauder.Mini.Commands.Build;

/// <summary>
/// Command that generates enum source files from Diablo II game data.
/// Fetches data from the blizzhackers/d2data repository and generates
/// strongly-typed enums for game entities.
/// </summary>
public class BuildEnumsCommand : Command, IDisposable
{
    private const string Namespace = "Marauder.Mini.Enums";
    private const string BaseUrl = "https://raw.githubusercontent.com/blizzhackers/d2data/refs/heads/master/json";
    private readonly HttpClient _http = new();
    private Dictionary<string, string> _locale = [];

    public BuildEnumsCommand() : base("enums", "Build source enum files")
    {
        this.SetHandler(async () =>
        {
            _locale = await FetchJsonDataAsync<Dictionary<string, string>>("localestrings-eng.json") ?? [];

            await BuildDifficultyEnumsAsync();
            await BuildGemEnumsAsync();
            await BuildLevelEnumsAsync();
            await BuildMonsterEnumsAsync();
            await BuildNpcEnumsAsync();
        });
    }

    /// <summary>
    /// Fetches JSON data from the d2data repository and deserializes it to the specified type.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the JSON data to</typeparam>
    /// <param name="filename">The JSON file name in the d2data repository</param>
    /// <returns>The deserialized data, or null if the request fails</returns>
    private async Task<T?> FetchJsonDataAsync<T>(string filename)
    {
        var url = $"{BaseUrl}/{filename}";
        return await _http.GetFromJsonAsync<T>(url);
    }

    /// <summary>
    /// Fetches JSON data from the d2data repository and converts dictionary values to an enumerable.
    /// </summary>
    /// <typeparam name="T">The type of values in the dictionary</typeparam>
    /// <param name="filename">The JSON file name in the d2data repository</param>
    /// <returns>An enumerable of the dictionary values, ordered by LineNumber</returns>
    private async Task<IEnumerable<T>> FetchOrderedJsonDataAsync<T>(string filename) where T : ILineNumbered
    {
        var dictionary = await FetchJsonDataAsync<Dictionary<string, T>>(filename) ?? [];
        return dictionary.Values.OrderBy(x => x.LineNumber);
    }

    /// <summary>
    /// Generates the Difficulty.cs enum file containing game difficulty levels.
    /// </summary>
    public async Task BuildDifficultyEnumsAsync()
    {
        var levels = await FetchOrderedJsonDataAsync<DifficultyLevel>("difficultylevels.json");
        
        var builder = new StringBuilder();
        builder.AppendLine($"namespace {Namespace};");
        builder.AppendLine();
        builder.AppendLine("public enum Difficulty");
        builder.AppendLine("{");

        foreach(var level in levels)
        {
            builder.AppendLine($"    {level.Name},");   
        }

        builder.AppendLine("}");

        await WriteEnumFileAsync("Enums/Difficulty.cs", builder.ToString());
    }

    /// <summary>
    /// Generates the Gem.cs enum file containing both gems and runes.
    /// </summary>
    public async Task BuildGemEnumsAsync()
    {
        var gems = await FetchOrderedJsonDataAsync<GemInfo>("gems.json");
        
        var builder = new StringBuilder();
        var gemBuilder = new StringBuilder();
        var runeBuilder = new StringBuilder();
        
        builder.AppendLine($"namespace {Namespace};");
        builder.AppendLine();
        
        // Build separate enums for gems and runes
        gemBuilder.AppendLine("public enum Gem");
        gemBuilder.AppendLine("{");
        runeBuilder.AppendLine("public enum Rune");
        runeBuilder.AppendLine("{");

        foreach(var gem in gems)
        {
            var enumBuilder = gem.IsExpansion ? runeBuilder : gemBuilder;
            enumBuilder.AppendLine($"    {ConvertToEnumName(gem.Name)},");
        }

        gemBuilder.AppendLine("}");
        runeBuilder.AppendLine("}");
        
        builder.AppendLine(gemBuilder.ToString());
        builder.AppendLine(runeBuilder.ToString());

        await WriteEnumFileAsync("Enums/Gem.cs", builder.ToString());
    }

    /// <summary>
    /// Generates the Level.cs enum file containing game areas and dungeons.
    /// </summary>
    public async Task BuildLevelEnumsAsync()
    {
        var levels = await FetchOrderedJsonDataAsync<LevelInfo>("levels.json");
        
        var builder = new StringBuilder();
        builder.AppendLine($"namespace {Namespace};");
        builder.AppendLine();
        builder.AppendLine("public enum Level");
        builder.AppendLine("{");

        int talRashasTombCount = 0;
        
        foreach(var level in levels.Where(l => !string.IsNullOrEmpty(l.StringName)))
        {
            var name = _locale.GetValueOrDefault(level.StringName, level.StringName);
            var key = ConvertToEnumName(name);
            
            // Handle special cases for duplicate or ambiguous level names
            key = key switch
            {
                "TalRashasTomb" => $"TalRashasTomb{++talRashasTombCount}",
                "SewersLevel1" when level.Act == 2 => "KurastSewersLevel1",
                "SewersLevel2" when level.Act == 2 => "KurastSewersLevel2",
                "Tristram" when level.Act == 4 => "UberTristram",
                _ => key
            };
            
            builder.AppendLine($"    {key},");
        }

        builder.AppendLine("}");

        await WriteEnumFileAsync("Enums/Level.cs", builder.ToString());
    }

    /// <summary>
    /// Generates the Monster.cs enum file containing super unique monsters.
    /// </summary>
    public async Task BuildMonsterEnumsAsync()
    {
        var superUniques = await FetchOrderedJsonDataAsync<SuperUniqueInfo>("superuniques.json");
        
        var builder = new StringBuilder();
        builder.AppendLine($"namespace {Namespace};");
        builder.AppendLine();
        builder.AppendLine("public enum SuperUnique");
        builder.AppendLine("{");

        foreach(var superUnique in superUniques)
        {
            builder.AppendLine($"    {ConvertToEnumName(superUnique.Name)},");
        }

        builder.AppendLine("}");

        await WriteEnumFileAsync("Enums/Monster.cs", builder.ToString());
    }

    /// <summary>
    /// Generates the Npc.cs enum file containing game NPCs.
    /// </summary>
    public async Task BuildNpcEnumsAsync()
    {
        var npcs = await FetchOrderedJsonDataAsync<NpcInfo>("npc.json");
        
        var builder = new StringBuilder();
        builder.AppendLine($"namespace {Namespace};");
        builder.AppendLine();
        builder.AppendLine("public enum Npc");
        builder.AppendLine("{");

        foreach(var npc in npcs)
        {
            builder.AppendLine($"    {ConvertToEnumName(npc.Name)},");
        }

        builder.AppendLine("}");

        await WriteEnumFileAsync("Enums/Npc.cs", builder.ToString());
    }

    /// <summary>
    /// Writes the generated enum content to a file.
    /// </summary>
    private static async Task WriteEnumFileAsync(string path, string content)
    {
        await File.WriteAllTextAsync(path, content);
    }

    /// <summary>
    /// Converts a string to a valid C# enum name by removing special characters
    /// and converting to PascalCase.
    /// </summary>
    private string ConvertToEnumName(string input)
    {
        input = _locale.GetValueOrDefault(input, input);
        var alphanum = new GeneratedRegexAttribute(@"[^a-zA-Z0-9\s]");
        string cleaned = Regex.Replace(input, alphanum.Pattern, "");
        return CultureInfo.InvariantCulture.TextInfo
            .ToTitleCase(cleaned.ToLower())
            .Replace(" ", "");
    }

    /// <summary>
    /// IDisposable implementation
    /// </summary>
    public void Dispose()
    {
        _http.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Interface for JSON models that have a line number.
/// </summary>
public interface ILineNumbered
{
    int LineNumber { get; set; }
}

#region JSON Models

public class DifficultyLevel : ILineNumbered
{
    public required string Name { get; set; }
    public int ResistPenalty { get; set; }
    public int ResistPenaltyNonExpansion { get; set; }
    public int DeathExpPenalty { get; set; }
    public int MonsterSkillBonus { get; set; }
    public int MonsterFreezeDivisor { get; set; }
    public int MonsterColdDivisor { get; set; }
    public int AiCurseDivisor { get; set; }
    public int LifeStealDivisor { get; set; }
    public int ManaStealDivisor { get; set; }
    public int UniqueDamageBonus { get; set; }
    public int ChampionDamageBonus { get; set; }
    public int PlayerDamagePercentVSPlayer { get; set; }
    public int PlayerDamagePercentVSMercenary { get; set; }
    public int PlayerDamagePercentVSPrimeEvil { get; set; }
    public int PlayerHitReactBufferVSPlayer { get; set; }
    public int PlayerHitReactBufferVSMonster { get; set; }
    public int MercenaryDamagePercentVSPlayer { get; set; }
    public int MercenaryDamagePercentVSMercenary { get; set; }
    public int MercenaryDamagePercentVSBoss { get; set; }
    public int MercenaryMaxStunLength { get; set; }
    public int PrimeEvilDamagePercentVSPlayer { get; set; }
    public int PrimeEvilDamagePercentVSMercenary { get; set; }
    public int PrimeEvilDamagePercentVSPet { get; set; }
    public int PetDamagePercentVSPlayer { get; set; }
    public int MonsterCEDamagePercent { get; set; }
    public int MonsterFireEnchantExplosionDamagePercent { get; set; }
    public int StaticFieldMin { get; set; }
    public int GambleRare { get; set; }
    public int GambleSet { get; set; }
    public int GambleUnique { get; set; }
    public int GambleUber { get; set; }
    public int GambleUltra { get; set; }
    public int LineNumber { get; set; }
}

public class GemInfo : ILineNumbered
{
    public required string Name { get; set; }
    public int? Expansion { get; set; }
    public bool IsExpansion => Expansion == 1;
    public int LineNumber { get; set; }
}

public class LevelInfo : ILineNumbered
{
    public int LineNumber { get; set; }
    public int Id { get; set; }
    public int Act { get; set; }
    public string LevelName { get; set; } = string.Empty;

    [JsonPropertyName("*StringName")]
    public string StringName { get; set; } = string.Empty;
}

public class SuperUniqueInfo : ILineNumbered
{
    public required string Name { get; set; }
    public int LineNumber { get; set; }
}

public class NpcInfo : ILineNumbered
{
    [JsonPropertyName("npc")]
    public required string Name { get; set; }
    public int BuyMultiplier { get; set; }
    public int SellMultiplier { get; set; }
    public int RepairMultiplier { get; set; }
    public int QuestFlag { get; set; }
    public int QuestBuyMultiplier { get; set; }
    public int QuestSellMultiplier { get; set; }
    public int QuestRepairMultiplier { get; set; }
    public int MaxBuy { get; set; }
    public int MaxBuyNightmare { get; set; }
    public int MaxBuyHell { get; set; }
    public int LineNumber { get; set; }
}

#endregion