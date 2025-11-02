namespace NQ.Interfaces
{
    public interface IRecipeGrain : Orleans.IGrainWithIntegerKey
    {
        Task<RecipeStatus> Get();
        Task<RecipeQueue> GetQueue();
        Task<RecipeStatus> Enqueue(RecipeRequest rr);
        Task<RecipeStatus> Resume();
        Task Abort(RecipeStatusId rsi);
        Task Collect();
        Task Move(RecipeMoveAfter rma);
        Task WipeDeath();

        // Surrogates and sandbox
        Task<List<RecipeStatus>> Stash();
        Task Unstash(List<RecipeStatus> stashed);

        // Backoffice

        Task ClearAbort();
        Task ClearAll();
        Task InstantCraft();
        Task<List<RecipeStatus>> BoGetQueue();
    }
}
