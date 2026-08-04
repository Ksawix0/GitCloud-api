using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using static gitCloud_api.LakeCacheClasses;

namespace gitCloud_api;

public class LakeCache(int capacity)
{
    public LakeCacheItem CacheRoot = new LakeCacheItem
    {
        Sha = null,
        Entities = new Dictionary<string, LakeCacheItem>()
    };
    
    private readonly Channel<LakeCacheNewItem> _queue = Channel.CreateBounded<LakeCacheNewItem>(new BoundedChannelOptions(capacity) {
        FullMode = BoundedChannelFullMode.DropWrite
    });
    
    public async ValueTask EnqueueAsync(LakeCacheNewItem item)
    {
        await _queue.Writer.WriteAsync(item);
    }
    
    public async ValueTask<LakeCacheNewItem> DequeueAsync(CancellationToken cancellationToken)
    {
        return await _queue.Reader.ReadAsync(cancellationToken);
    }

    public ref readonly LakeCacheItem TryGetCachedItemByPath(string path)
    {
        path = path.Trim('/');
        ref LakeCacheItem cache = ref CacheRoot;
        foreach (string name in path.Split('/'))
        {
            if(cache.Entities == null || !cache.Entities.Any()){return ref Unsafe.NullRef<LakeCacheItem>();}
            cache = ref CollectionsMarshal.GetValueRefOrNullRef(cache.Entities, name);
            if (  Unsafe.IsNullRef(ref cache) )
            {
                return ref Unsafe.NullRef<LakeCacheItem>();
            }
        }
        return ref cache;
    }
    
    public void DeleteCachedItemByPath(string path)
    {
        path = path.Trim('/');
        ref LakeCacheItem cacheItem = ref CacheRoot;
        string[] pathParts = path.Split('/');
        foreach (string name in pathParts[..^1])
        {
            if(cacheItem.Entities == null || !cacheItem.Entities.Any()){return;}
            cacheItem = ref CollectionsMarshal.GetValueRefOrNullRef(cacheItem.Entities, name);
            if (  Unsafe.IsNullRef(ref cacheItem) )
            {
                return;
            }
        }

        cacheItem.Entities?.Remove(pathParts[^1]);
    }
    
}

public class LakeCacheBackgroundService(LakeCache lakeCache) : BackgroundService
{
    private static readonly ILogger Logger = LoggerFactory.Create(builder => builder.AddConsole() ).CreateLogger(typeof(LakeCacheBackgroundService));

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            LakeCacheNewItem request = await lakeCache.DequeueAsync(cancellationToken);
            if (request.Entities == null && request.Sha == null)
            {
                Logger.LogError("Lake cache item with path {path} has empty both sha and entities", request.Path);
                continue;
            }
            
            request.Path = request.Path.Trim('/');

            ref LakeCacheItem cacheItem  = ref lakeCache.CacheRoot;
            
            {
                foreach (string name in request.Path.Split('/'))
                {
                    cacheItem = ref CollectionsMarshal.GetValueRefOrAddDefault(cacheItem.Entities, name, out var exists);
                    if (!exists)
                    {
                        cacheItem = new LakeCacheItem()
                        {
                            Sha = null,
                            Entities = new Dictionary<string, LakeCacheItem>()
                        };
                    }
                }
            }
            
            cacheItem.Sha = request.Sha;
            if (request.Entities == null)
            {
                cacheItem.Entities = null;
            }
            else 
            {
                foreach (KeyValuePair<string, LakeCacheItem> item in request.Entities)
                {
                    if (!cacheItem.Entities.ContainsKey(item.Key))
                    {
                        cacheItem.Entities.Add(item.Key, item.Value);
                    }
                }
            }
            
            request.TaskCompletionSource?.SetResult();
        }
    }
}



