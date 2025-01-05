using System.CommandLine;
using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Marauder.Mini.Commands.Build;

public class BuildEnumsCommand : Command, IDisposable
{
    public const string Namespace = "Marauder.Mini.Enums";
    private readonly HttpClient _http = new();
    private Dictionary<string, string> _locale = [];

    public BuildEnumsCommand() : base("enums", "Build source enum files")
    {
        this.SetHandler(async () =>
        {
            _locale = await _http.GetFromJsonAsync<Dictionary<string, string>>(
                "https://raw.githubusercontent.com/blizzhackers/d2data/refs/heads/master/json/localestrings-eng.json") ?? [];

            await BuildDifficultyEnums();
            await BuildGemEnums();
            await BuildLevelEnums();
            await BuildNpcEnums();
        });
    }

    /// <summary>
    /// Build Difficulty Enums
    /// </summary>
    /// <returns></returns>
    public async Task BuildDifficultyEnums()
    {
        var dictionary = await _http.GetFromJsonAsync<Dictionary<string, DifficultyLevel>>(
            "https://raw.githubusercontent.com/blizzhackers/d2data/refs/heads/master/json/difficultylevels.json");

        List<DifficultyLevel> levels = [];
        var builder = new StringBuilder();
        builder.AppendLine($"namespace {Namespace};");
        builder.AppendLine();
        builder.AppendLine("public enum Difficulty");
        builder.AppendLine("{");

        foreach(var kvp in dictionary ?? [])
        {
            levels.Add(kvp.Value);
        }

        levels.Sort((l1, l2) => l1.LineNumber.CompareTo(l2.LineNumber));

        foreach(var level in levels)
        {
            builder.AppendLine($"    {level.Name},");   
        }

        builder.AppendLine("}");

        var path = "Enums/Difficulty.cs";
        using var writer = new StreamWriter(path);
        writer.Write(builder);
    }

    public class DifficultyLevel
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
        public int MercenaryDamagePercentVSBoss {get; set; }
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

    /// <summary>
    /// Build Gem Enums
    /// </summary>
    /// <returns></returns>
    public async Task BuildGemEnums()
    {
        var dictionary = await _http.GetFromJsonAsync<Dictionary<string, GemInfo>>(
            "https://raw.githubusercontent.com/blizzhackers/d2data/refs/heads/master/json/gems.json");

        List<GemInfo> gems = [];
        var builder = new StringBuilder();
        var gemBuilder = new StringBuilder();
        var runeBuilder = new StringBuilder();
        builder.AppendLine($"namespace {Namespace};");
        builder.AppendLine();
        gemBuilder.AppendLine("public enum Gem");
        gemBuilder.AppendLine("{");
        runeBuilder.AppendLine("public enum Rune");
        runeBuilder.AppendLine("{");

        foreach(var kvp in dictionary ?? [])
        {
            gems.Add(kvp.Value);
        }

        gems.Sort((l1, l2) => l1.LineNumber.CompareTo(l2.LineNumber));

        foreach(var gem in gems)
        {
            if (gem.IsExpansion)
            {
                runeBuilder.AppendLine($"    {ConvertToEnumName(gem.Name)},");
                continue;
            }
            gemBuilder.AppendLine($"    {ConvertToEnumName(gem.Name)},");
        }

        gemBuilder.AppendLine("}");
        runeBuilder.AppendLine("}");
        builder.AppendLine(gemBuilder.ToString());
        builder.AppendLine();
        builder.AppendLine(runeBuilder.ToString());

        var path = "Enums/Gem.cs";
        using var writer = new StreamWriter(path);
        writer.Write(builder);
    }

    public class GemInfo
    {
        public required string Name { get; set; }
        public int? Expansion { get; set; }
        public bool IsExpansion => Expansion == 1;
        public int LineNumber { get; set; }
    }

    /// <summary>
    /// Build Level Enums
    /// </summary>
    /// <returns></returns>
    public async Task BuildLevelEnums()
    {
        var dictionary = await _http.GetFromJsonAsync<Dictionary<string, LevelInfo>>(
            "https://raw.githubusercontent.com/blizzhackers/d2data/refs/heads/master/json/levels.json");

        List<LevelInfo> levels = [];
        var builder = new StringBuilder();
        builder.AppendLine($"namespace {Namespace};");
        builder.AppendLine();
        builder.AppendLine("public enum Level");
        builder.AppendLine("{");

        foreach(var kvp in dictionary ?? [])
        {
            levels.Add(kvp.Value);
        }

        int talRashasTombCount = 0;
        levels.Sort((l1, l2) => l1.LineNumber.CompareTo(l2.LineNumber));

        foreach(var level in levels)
        {
            if (string.IsNullOrEmpty(level.StringName)) continue;

            var name = _locale.ContainsKey(level.StringName) ? _locale[level.StringName] : level.StringName;
            var key = ConvertToEnumName(name);
            if (key == "TalRashasTomb")
            {
                talRashasTombCount++;
                key = $"TalRashasTomb{talRashasTombCount}";
            }
            if (key == "SewersLevel1" && level.Act == 2)
            {
                key = $"KurastSewersLevel1";
            }
            if (key == "SewersLevel2" && level.Act == 2)
            {
                key = $"KurastSewersLevel2";
            }
            if (key == "Tristram" && level.Act == 4)
            {
                key = $"UberTristram";
            }
            builder.AppendLine($"    {key},");
        }

        builder.AppendLine("}");

        var path = "Enums/Level.cs";
        using var writer = new StreamWriter(path);
        writer.Write(builder);
    }

    public class LevelInfo
    {
        public int LineNumber { get; set; }
        public int Id { get; set; }
        public int Act { get; set; }
        public string LevelName { get; set; } = string.Empty;

        [JsonPropertyName("*StringName")]
        public string StringName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Build Npc Enums
    /// </summary>
    /// <returns></returns>
    public async Task BuildNpcEnums()
    {
        var dictionary = await _http.GetFromJsonAsync<Dictionary<string, NpcInfo>>(
            "https://raw.githubusercontent.com/blizzhackers/d2data/refs/heads/master/json/npc.json");

        List<NpcInfo> npcs = [];
        var builder = new StringBuilder();
        builder.AppendLine($"namespace {Namespace};");
        builder.AppendLine();
        builder.AppendLine("public enum Npc");
        builder.AppendLine("{");

        foreach(var kvp in dictionary ?? [])
        {
            npcs.Add(kvp.Value);
        }

        npcs.Sort((l1, l2) => l1.LineNumber.CompareTo(l2.LineNumber));

        foreach(var npc in npcs)
        {
            builder.AppendLine($"    {ConvertToEnumName(npc.Name)},");
        }

        builder.AppendLine("}");

        var path = "Enums/Npc.cs";
        using var writer = new StreamWriter(path);
        writer.Write(builder);
    }

    public class NpcInfo
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

    public static string ConvertToEnumName(string input)
    {
        string cleaned = Regex.Replace(input, @"[^a-zA-Z0-9\s]", "");
        TextInfo textInfo = CultureInfo.InvariantCulture.TextInfo;
        string camelCase = textInfo.ToTitleCase(cleaned.ToLower()).Replace(" ", "");
        return camelCase;
    }

    public void Dispose()
    {
        _http.Dispose();
        GC.SuppressFinalize(this);
    }
}