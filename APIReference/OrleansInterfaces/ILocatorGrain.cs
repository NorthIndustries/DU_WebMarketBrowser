using Orleans;

namespace NQ
{
    public static class LocatorKeyHelper
    {
        public static string GrainKey(this LocationDescriptor descriptor)
        {
            return descriptor.propertyName + "@" + descriptor.propertyValue + "@" + descriptor.algorithm + "@" + descriptor.parentConstructId + "@" + descriptor.ownerPlayerId;
        }
        public static void FillFromKey(this LocationDescriptor descriptor, string key, out ulong parameter)
        {
            parameter = 0;
            var comps = key.Split("@");
            if (comps.Length != 5)
                throw new Exception("Invalid key: " + key);
            descriptor.propertyName = comps[0];
            descriptor.propertyValue = comps[1];
            var algo = comps[2];
            var acomps = algo.Split(":");
            if (acomps.Length == 1)
                descriptor.algorithm = algo;
            else
            {
                descriptor.algorithm = acomps[0];
                parameter = UInt64.Parse(acomps[1]);
            }
            descriptor.parentConstructId = UInt64.Parse(comps[3]);
            descriptor.ownerPlayerId = UInt64.Parse(comps[4]);
        }
    }
}

namespace NQ.Interfaces
{
    public interface ILocatorGrain : IGrainWithStringKey
    {
        Task<DetailedLocation> Get(ulong requester, RelativeLocation pos);
        Task<(DetailedLocation, ElementId)> GetWithId(ulong requester, RelativeLocation pos);
        Task<DetailedLocations> GetAll(ulong requester);
    }
}
