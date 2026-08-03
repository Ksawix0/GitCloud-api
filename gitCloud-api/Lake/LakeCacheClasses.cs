namespace gitCloud_api;

public static class LakeCacheClasses
{
    public class LakeCacheItem
    {
        public string? Sha;
        public Dictionary<string, LakeCacheItem>? Entities;
    }
    
    public class LakeCacheNewItem
    {
        public required string Path;
        public string? Sha;
        public Dictionary<string, LakeCacheItem>? Entities;
    }
}