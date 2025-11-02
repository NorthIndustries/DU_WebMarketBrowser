using NQ;

namespace Backend
{
    public interface IPlanetList
    {
        bool IsPlanet(ConstructId constructId);

        /// <summary>
        /// Returns the list of "partial" ConstructInfo of planets.
        /// </summary>
        /// <returns></returns>
        IEnumerable<ConstructInfo> GetPlanetList();
        Task<ConstructCharacteristics?> GetPlanet(ConstructId id, bool allowDeleted=false);
        Task<PlanetProperties?> GetPlanetProperties(ConstructId id);
        Task<ConstructGeometry?> GetPlanetGeometry(ConstructId id);

        /// <summary>
        /// Returns a "partial" ContructInfo
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task<ConstructInfo> Fetch(ConstructId id, bool allowDeleted=false);
        Task<uint> GetTileIndex(ConstructId planet, Vec3 pos, bool allowDeleted=false);

        // safe zones
        bool IsInSafeZone(Vec3 point);

        bool IsInPlanetarySafeZone(Vec3 point);

        List<Sphere> GetPlanetSafeZones();
    }
}
