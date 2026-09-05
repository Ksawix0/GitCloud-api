namespace gitCloud_api;

public static class LakeCacheClasses
{
    public class LakeCacheItem
    {
        public string? Sha;
        public long? ByteSize;
        public Dictionary<string, LakeCacheItem>? Entities;
        public bool Known;
    }
    
    public class LakeCacheNewItem
    {
        public required string Path;
        public string? Sha;
        public long? ByteSize;
        public Dictionary<string, LakeCacheItem>? Entities;
        public TaskCompletionSource? TaskCompletionSource;
    }
}