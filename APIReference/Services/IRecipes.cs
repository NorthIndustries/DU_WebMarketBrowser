using NQ;

namespace Backend
{
    public class SRecipe
    {
        //[YamlMember(Alias = "id")]
        public long Id { get; set; }
        //[YamlMember(Alias = "time")]
        public int Time { get; set; }
        public bool Nanocraftable { get; set; }
        //[YamlMember(Alias = "in")]
        public List<Dictionary<string, double>> In { get; set; } = new List<Dictionary<string, double>>();
        //[YamlMember(Alias = "out")]
        public List<Dictionary<string, double>> Out { get; set; } = new List<Dictionary<string, double>>();
        //[YamlMember(Alias = "industries")]
        public List<string> Industries { get; set; } = new List<string>();
    }
    public interface IRecipes
    {
        Task<Recipe> GetRecipe(ulong recipeId);
        Task<string> Export();
        Task Import(string doc, TrackProgress trackProgress = null,
            HandleException handleException = null, bool wipeOld = true);
        Task<string> ExportOne(ulong recipeId);
        Task ImportOne(string doc);
        Task<List<SRecipe>> GetAllPretty();
        Task<string> MakeRecipeItems();
        Task DeleteOne(ulong recipeId);
        Task NotifyChangedAsync();
        Task<List<Recipe>> GetProducing(ulong itemTypeId);
        Task<List<Recipe>> GetAllRecipes();
        // Returns null for 'not needed' or a list of usable schematic ids
        Task<List<ulong>> GetNeededSchematics(ulong recipeId);
        Task<Dictionary<ulong, List<ulong>>> GetSchematicsCache();
        Task<List<ItemAndQuantity>> GetSalvageCandidates(ulong itemTypeId);
    }

    public delegate void TrackProgress(int current, int total);
    public delegate void HandleException(System.Exception innerException);
}
