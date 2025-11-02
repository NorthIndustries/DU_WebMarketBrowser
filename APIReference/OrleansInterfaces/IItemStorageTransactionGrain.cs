#nullable enable

using Orleans;
using Orleans.Concurrency;

namespace NQ.Interfaces;

/// <summary>
/// This grain is a transaction coordinator, it should never be interacted with in gameplay code
/// and its existance is hidden behind the storage API
/// </summary>
public interface IItemStorageTransactionGrain : IGrainWithGuidKey
{
    /// <summary>
    /// Tells the grain that we are begining a new transaction
    /// If this is not called, interacting with the transaction throws an error
    /// </summary>
    Task BeginTransaction(StorageOpTag tag);

    /// <summary>
    /// Commit the transaction
    /// </summary>
    Task Commit();

    /// <summary>
    /// Rollback the transaction
    /// </summary>
    Task Rollback();

    /// <summary>
    /// Rollback the transaction if was not already commited,
    /// Also deactivates the grain after the call
    /// </summary>
    Task EndTransaction();

    /// <summary>
    /// Add a transaction item to the transaction
    /// </summary>
    Task AddItem(ITransactionItem_Erased item);

    /// <summary>
    /// Internal
    /// </summary>
    /// <returns></returns>
    [OneWay]
    Task PerformPublish();

    public interface ITransactionItem_Erased
    {
        // this is empty because this interface is used to perform type erasure (we can't define the real interface because it depends on Backend objects)
        // the real type is `ITransactionItem`

        // DO NOT IMPLEMENT THIS INTERFACE OTHER THAN WITH `ITransactionItem`
    }

}
