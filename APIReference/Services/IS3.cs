using NQutils.Config;

namespace Backend.AWS
{
    public interface IS3
    {
        Task<string> Get(AWSS3Settings settings, string path, bool noCache = false);
        Task<(bool modified, string? content, string newEtag)> GetLazy(AWSS3Settings settings, string path, string expectedEtag);
        Task<bool> KeyExists(AWSS3Settings settings, string key);
        Task MoveTo(AWSS3Settings settings, string srcKey, string destKey, Dictionary<string, string> metadata);
        Task<byte[]> GetBytes(AWSS3Settings settings, string path);
        Task<string?> GetRandomKey(AWSS3Settings settings, string folder);
        Task Put(AWSS3Settings settings, string path, byte[] content, bool compress = true);
        IAsyncEnumerable<(string index, List<NQ.PlayerLog> logs)> ReadPlayerLogs(AWSS3Settings settings, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
        Task<List<string>> List(AWSS3Settings settings, string path);
        Task ListAll(AWSS3Settings settings, Func<string, DateTime, Task> onEntry);
        Task Delete(AWSS3Settings settings, string fullPath);
        Task DeleteMultiple(AWSS3Settings settings, IEnumerable<string> pathes);
        Task RestoreDeleted(AWSS3Settings settings, string key);
    }

    public static class S3Extensions
    {
        public static Task Put(this IS3 s3, AWSS3Settings settings, string path, string content)
        {
            return s3.Put(settings, path, System.Text.Encoding.UTF8.GetBytes(content));
        }
    }
}
