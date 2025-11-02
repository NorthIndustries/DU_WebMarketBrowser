namespace NQ.Interfaces;

/// <summary>
/// This interface is to encapsulate everything that is common to player and organization
/// </summary>
public interface IEntityGrain
{
    Task<NamedEntity> GetName();
}
