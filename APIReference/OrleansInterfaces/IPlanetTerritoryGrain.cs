namespace NQ.Interfaces
{
    public interface IPlanetTerritoryGrain : Orleans.IGrainWithIntegerKey
    {
        //Special owner (like aphelia) can internally (in the bo) claim territory but we don't want to check their inventory
        Task<TerritoryDetails> ClaimTerritoryBySpecialOwner(ulong playerId, TerritoryClaim claim);
        Task<TerritoryDetails> ClaimTerritory(ulong playerId, TerritoryClaim claim);
        Task<TerritoryDetails> ReserveTerritory(ulong playerId, TerritoryClaim claim);
        Task SetTerritoryUnitPosition(ulong playerId, Vec3 position);
        Task ReleaseTerritory(ulong playerId, TerritoryRelease release);
        Task UpdateTerritoryFixtures(List<TerritoryDetails> territories);
        Task<TerritoryMap> GetTerritoryMap();
        Task<TerritoryDetails> GetTerritoryTileByPos(Vec3 position);
        Task<TerritoryDetails> GetTerritoryTileByPosSafe(Vec3 position);
        Task<TerritoryDetails> GetTerritoryTile(uint tileIndex);
        Task<OwnedTerritoriesInfo> UpdateTerritory(ulong playerId, TerritoryUpdate update);
        Task UpdateTerritoryPosition(uint tileIndex, Vec3 position);
        Task Reset();
        Task RemoveTerritory(uint tileINdex);
        Task DoGC();
        Task<bool> UpdateBalance(uint tileIndex, long balance);
        Task<bool> IsOnline(uint tileIndex);
        Task<bool> IsKeyed(uint tileIndex);
        Task Ping();
        Task<TerritoryDetails> TokenizeTerritory(uint tileIndex);
        Task ConsumeKey(ulong playerId, uint tileIndex, EntityId newOwner, EntityId wallet);
        Task<ulong> GetAdjacentTilesCountWithSameOwner(uint tileIndex);
    }
}
