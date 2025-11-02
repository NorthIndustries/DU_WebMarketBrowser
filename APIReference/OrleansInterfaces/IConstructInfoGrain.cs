using Orleans;
using Orleans.Concurrency;

namespace NQ.Interfaces
{
    public interface IConstructInfoGrain : IGrainWithIntegerKey
    {
        Task UnLoad();
        [AlwaysInterleave]
        Task Update(ConstructInfoUpdate update);
        [AlwaysInterleave]
        Task<ConstructInfo> Get();

        /// <summary>
        /// bump the version of the elements for the given lod (and all the greater LOD)
        /// </summary>
        /// <param name="lod"></param>
        /// <returns></returns>
        [AlwaysInterleave]
        Task BumpElementsLOD(ElementLOD lod);
        Task BumpElementsAllLODs();
    }
}
