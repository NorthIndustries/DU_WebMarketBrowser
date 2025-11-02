using NQ;

#nullable enable

namespace Backend.Scenegraph
{
    public interface IScenegraph
    {
        /// <summary>
        /// returns the position of the center of a construct (depends of the size)
        /// </summary>
        /// <param name="constructId"></param>
        /// <param name="size"></param>
        /// <returns>Center position</returns>
        Task<Vec3> GetCenterWorldPosition(ConstructId constructId, long size);

        /// <summary>
        /// returns the position of the center of a construct when you don't know its size.
        /// </summary>
        /// <param name="constructId"></param>
        /// <returns></returns>
        Task<Vec3> GetConstructCenterWorldPosition(ConstructId constructId);


        /// <summary>
        /// Compute the world position from a relative location
        /// NOTE : throws if the construct doesn't exists
        /// </summary>
        Task<RelativeLocation> ResolveWorldLocation(RelativeLocation loc);
        /// <summary>
        /// Compute the position of a position relative to a construct
        /// NOTE : throws if a construct doesn't exists
        /// </summary>
        Task<RelativeLocation> ResolveRelativeLocation(RelativeLocation loc, ConstructId target);

        /// <summary>
        /// Batch Compute the world position from a relative location
        /// NOTE : can return a null entry if the construct doesn't exists
        /// </summary>
        Task<RelativeLocation?[]> BatchResolveWorldLocations(RelativeLocation[] locs);
        /// <summary>
        /// Batch Compute the position of a position relative to a construct
        /// NOTE : can return a null entry if a construct doesn't exists
        /// </summary>
        Task<RelativeLocation?[]> BatchResolveRelativeLocations(RelativeLocation[] locs, ConstructId target);

        /// <summary>
        /// Compute the distance² between p1 and p2
        /// NOTE : throws if a construct doesn't exists
        /// </summary>
        Task<double> Distance2(RelativeLocation p1, RelativeLocation p2);

        /// <summary>
        /// Compute the distance² between a player and a position
        /// NOTE : throws if a construct doesn't exists
        /// </summary>
        Task<double> PlayerDistance2(PlayerId player, RelativeLocation pos);


        /// <summary>
        /// Get a player local position (works even when the player is disconnected)
        /// </summary>
        Task<RelativeLocation> GetPlayerLocation(PlayerId playerId);
        /// <summary>
        /// Get a player local position (for a connected player)
        /// </summary>
        Task<RelativeLocation?> GetPlayerLocalPosition(PlayerId player);
        /// <summary>
        /// return the relative position of all players in list
        /// </summary>
        /// <param name="players">list of players</param>
        /// <returns>list of relative location</returns>
        Task<List<RelativeLocation?>> GetPlayerLocalPositionBatch(List<PlayerId> players);

        /// <summary>
        /// Get a player world position (works even when the player is disconnected)
        /// NOTE : throws if the player is on a non-existant construct
        /// </summary>
        Task<(RelativeLocation local, RelativeLocation world)> GetPlayerWorldPosition(PlayerId playerId);

        /// <summary>
        /// Get the position of a player, should always be valid
        /// </summary>
        Task<RelativeLocation> GetPlayerSafeLocation(PlayerId playerId);
        Task<RelativeLocation> GetDefaultSpawnPos(PlayerId playerId);
        Task<List<ConstructRelativeData>> GetTree(ConstructId constructId, bool allowLeafDeleted = true);

        /// <summary>
        /// returns some construct data around the given position.
        /// It can't return constructs that would be too far to be visible for a regular player.
        /// </summary>
        /// <param name="worldPosition"></param>
        /// <param name="range"></param>
        /// <returns></returns>
        Task<NQ.Visibility.ConstructPositions> GetConstructsInRange(Vec3 worldPosition, double range);
    }
}
