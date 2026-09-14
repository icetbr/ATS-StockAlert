$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$sdk = (& dotnet --list-sdks | Select-Object -Last 1)
$sdkVersion = $sdk.Split(' ')[0]
$sdkRoot = $sdk.Substring($sdk.IndexOf('[') + 1).TrimEnd(']')
$roslyn = Join-Path $sdkRoot "$sdkVersion/Roslyn/bincore"
Add-Type -Path (Join-Path $roslyn 'Microsoft.CodeAnalysis.dll')
Add-Type -Path (Join-Path $roslyn 'Microsoft.CodeAnalysis.CSharp.dll')

function Get-SourceMembers($path, $names) {
    $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText((Get-Content -Raw $path))
    $members = @($tree.GetRoot().DescendantNodes() | Where-Object {
        $_.GetType().Name -in @('MethodDeclarationSyntax', 'ClassDeclarationSyntax') -and
        $_.Identifier.ValueText -in $names
    })
    if ($members.Count -ne $names.Count) { throw "Missing or ambiguous test members in $path" }
    return ($members | ForEach-Object { $_.ToString() }) -join "`n"
}

# Compile the actual calculator methods with small in-memory game substitutes.
$costMethods = Get-SourceMembers (Join-Path $repo 'Infrastructure/Hooks/TradeRouteProfitPatch.cs') @(
    'ResolveCost', 'ResolveRecipeCost', 'ResolveCheapestIngredient',
    'GetRawRequirementsForRecipeBatch', 'GetRawRequirementsForIngredient',
    'AddRawRequirements', 'HasEnoughRawInputs', 'CostResult', 'CostIngredient'
)
$outputMethods = Get-SourceMembers (Join-Path $repo 'Game/GameAPI.cs') @('GetEffectiveWorkshopRecipeOutput')
$source = @'
using System;
using System.Collections.Generic;
using System.Linq;

public class GoodModel { }
public class GoodRef
{
    public string Name;
    public int amount;
    public GoodModel good = new GoodModel();
}
public class GoodsSet { public GoodRef[] goods; }
public class ModelTag { public string Name; }
public class RecipeModel { public ModelTag[] tags = Array.Empty<ModelTag>(); }
public class WorkshopRecipeModel : RecipeModel
{
    public string Name;
    public GoodRef producedGood;
    public GoodsSet[] requiredGoods;
}
public class Building { public string ModelName; }
public class ProductionBuilding : Building { }
public class BuildingModel { public string Name; }
public struct Good
{
    public string name;
    public int amount;
    public Good(string name, int amount) { this.name = name; this.amount = amount; }
}
public static class Mathf
{
    public static int CeilToInt(float value) { return (int)Math.Ceiling(value); }
}
public class EffectsState
{
    public Dictionary<string, int> rawGoodsProductionBonus = new Dictionary<string, int>();
    public Dictionary<string, float> buildingPercentageProductionBonus = new Dictionary<string, float>();
    public Dictionary<string, float> recipesTagsPercentageProductionBonus = new Dictionary<string, float>();
}
public class GameAPI
{
    public class AvailableWorkshopRecipe
    {
        public WorkshopRecipeModel Recipe;
        public ProductionBuilding SourceBuilding;
        public BuildingModel SourceModel;
    }
    public static Dictionary<string, float> Stock = new Dictionary<string, float>();
    public static EffectsState Effects = new EffectsState();
    public static object LiveEffects;
    public static float GetStoredAmount(object model, string name) { return Stock.TryGetValue(name, out var value) ? value : 0f; }
    private static object GetEffectsService() { return LiveEffects; }
    private static EffectsState GetEffectsState() { return Effects; }
    private static int GetSafe(Dictionary<string, int> values, string name) { return values != null && values.TryGetValue(name, out var value) ? value : 0; }
    private static float GetSafe(Dictionary<string, float> values, string name) { return values != null && values.TryGetValue(name, out var value) ? value : 0f; }
    // OUTPUT_METHODS
}
public class LiveEffectsFixture
{
    public bool Called;
    public Good GetProduction(Building building, Good input, RecipeModel recipe, bool extra)
    {
        if (building.ModelName != "Makeshift Post" || extra) throw new Exception("Incorrect production context");
        Called = true;
        return new Good(input.name, 3);
    }
}
public static class ProfitRegressionTests
{
    private const int MaxCostDepth = 24;
    private static readonly Dictionary<string, float> Prices = new Dictionary<string, float>
    {
        ["Grain"] = 0.1f, ["Vegetables"] = 0.2f, ["Oil"] = 0.3f,
        ["Trade Packs"] = 0.75f, ["Crops"] = 0.3f
    };
    private static float GetTraderSellValueInAmber(string name, int amount) { return Prices[name] * amount; }
    private static string GetGoodDisplayName(string name) { return name; }
    // COST_METHODS

    private static GameAPI.AvailableWorkshopRecipe Recipe(string building, string product, int output, params GoodRef[] inputs)
    {
        return new GameAPI.AvailableWorkshopRecipe
        {
            SourceModel = new BuildingModel { Name = building },
            Recipe = new WorkshopRecipeModel
            {
                Name = product + " recipe",
                producedGood = new GoodRef { Name = product, amount = output },
                requiredGoods = new[] { new GoodsSet { goods = inputs } }
            }
        };
    }
    private static GoodRef Input(string name, int amount) { return new GoodRef { Name = name, amount = amount }; }
    private static CostResult Cost(string name, List<GameAPI.AvailableWorkshopRecipe> recipes, bool available)
    {
        return ResolveCost(name, new HashSet<string>(StringComparer.OrdinalIgnoreCase), recipes, 0, available);
    }
    private static void Expect(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        Console.WriteLine("PASS: " + name);
    }
    private static bool Near(float actual, float expected) { return Math.Abs(actual - expected) < 0.00001f; }
    public static void Run()
    {
        var packs = Recipe("Makeshift Post", "Trade Packs", 2, Input("Oil", 6));
        var oil = Recipe("Butcher", "Oil", 5, Input("Grain", 3), Input("Vegetables", 3));
        var recipes = new List<GameAPI.AvailableWorkshopRecipe> { packs, oil };
        GameAPI.Stock = new Dictionary<string, float> { ["Grain"] = 4, ["Vegetables"] = 14, ["Oil"] = 11 };
        GameAPI.Effects.buildingPercentageProductionBonus["Makeshift Post"] = 0.5f;

        Expect(GameAPI.GetEffectiveWorkshopRecipeOutput(packs) == 3, "Worker Mobilization applies to Better Packaging for an unbuilt Makeshift Post");
        var live = new LiveEffectsFixture();
        GameAPI.LiveEffects = live;
        packs.SourceBuilding = new ProductionBuilding { ModelName = "Makeshift Post" };
        Expect(GameAPI.GetEffectiveWorkshopRecipeOutput(packs) == 3 && live.Called, "Built Makeshift Post uses the game's live production calculation");
        GameAPI.LiveEffects = null;
        packs.SourceBuilding = null;

        var constrained = Cost("Trade Packs", recipes, true);
        Expect(constrained.Recipe != null && constrained.OutputAmount == 3 && Near(constrained.UnitCost, 0.24f),
            "Better Packaging stays eligible by using enough Vegetables for two Oil batches");
        Expect(Near(constrained.BatchRawRequirements["Vegetables"], 6f), "Availability uses whole upstream batches");
        Expect(Near(Cost("Trade Packs", recipes, false).UnitCost, 0.12f), "Disabled availability filter preserves cheapest unconstrained chain");

        GameAPI.Stock["Grain"] = 6;
        Expect(Near(Cost("Trade Packs", recipes, true).UnitCost, 0.12f), "Exactly enough Grain selects the cheaper chain");
        GameAPI.Effects.buildingPercentageProductionBonus.Clear();
        var baseline = Cost("Trade Packs", recipes, true);
        Expect(baseline.OutputAmount == 2 && Near(baseline.UnitCost, 0.18f), "Without Worker Mobilization Better Packaging keeps its base output");

        GameAPI.Effects.buildingPercentageProductionBonus["Makeshift Post"] = 0.5f;
        var crops = Recipe("Makeshift Post", "Crops", 2, Input("Grain", 6));
        recipes.Add(crops);
        Expect(Near(Cost("Crops", recipes, true).UnitCost, 0.2f), "Worker Mobilization applies equally to original Makeshift Post recipes");
        Expect(GameAPI.GetEffectiveWorkshopRecipeOutput(oil) == 5, "Makeshift bonus does not leak into other buildings");

        GameAPI.Stock["Grain"] = 0;
        GameAPI.Stock["Vegetables"] = 0;
        var storedOil = Cost("Trade Packs", recipes, true);
        Expect(storedOil.Recipe != null && Near(storedOil.UnitCost, 0.6f), "Existing Oil remains usable when no upstream recipe is available");
        GameAPI.Stock["Oil"] = 0;
        Expect(Cost("Trade Packs", recipes, true).Recipe == null, "Missing all inputs still rejects the recipe");
    }
}
'@
$source = $source.Replace('// OUTPUT_METHODS', $outputMethods).Replace('// COST_METHODS', $costMethods)
Add-Type -TypeDefinition $source
[ProfitRegressionTests]::Run()
