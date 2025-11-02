using NQ;
using Orleans;

public class IndustryUnitStats
{
    public ulong parentConstruct;
    public ulong elementId;
    public ulong itemId;
    public ulong started;
    public ulong stopped;
    public ulong produced;
    public ulong recipeChanged;
    public ulong lostMaterials;
    public Dictionary<ulong, ulong> proudcedByRecipe = new Dictionary<ulong, ulong>();
    public DateTime creationTime;
    public DateTime destructionTime;
    public TimeSpan runTime;
    public Dictionary<ulong, ulong> consumedSchematics = new Dictionary<ulong, ulong>();
}

public interface IIndustryUnitGrain : IGrainWithStringKey, IRemindable
{
    Task SetRecipe(ulong recipeId, ulong playerId);
    Task<(Recipe recipe, ulong batchSize)> GetRecipe(ulong recipeId, ulong playerId);
    Task Start(ulong playerId, IndustryStart parameters);
    Task StopHard(bool allowIngredientLoss);
    Task StopSoft();
    Task<IndustryStatus> Status();
    Task<IndustryUnitStats> Statistics();
    Task<Recipe> Recipe();
    Task SetClaimProducts(bool claim);
    Task SetNotifications(bool enabled);

    Task SetOutputContainer(ulong containerId, bool fromContainer = false);
    Task AddInputContainer(ulong containerId);
    Task RemoveInputContainer(ulong containerId, bool fromContainer = false);

    Task ContainerInputChanged();
    Task ContainerOutputChanged();
    Task SchematicsChanged();

    Task DeactivateGrain();
    Task Destroy();
    Task Break();
    Task ReloadProperties();

    Task DebugSetRunEnd(DateTimeOffset endTime);
}
