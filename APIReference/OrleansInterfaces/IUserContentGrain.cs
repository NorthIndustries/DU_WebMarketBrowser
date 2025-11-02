using Orleans;


namespace NQ.Interfaces
{
    public interface IUserContentGrain : IGrainWithGuidKey
    {
        Task<string> Store(byte[] body);
        Task<byte[]> Get(string hash);
    }
}
