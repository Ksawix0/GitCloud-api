using System.Runtime.CompilerServices;
using System.Threading.Channels;
using static gitCloud_api.LakeModifyServiceClasses;
using static gitCloud_api.Lake;
using static gitCloud_api.LakeCacheClasses;

namespace gitCloud_api;


public class LakeModifyQueue(int capacity)
{
    
    private readonly Channel<FullLakeModifyRequest> _queue = Channel.CreateBounded<FullLakeModifyRequest>(new BoundedChannelOptions(capacity)
    {
        FullMode = BoundedChannelFullMode.Wait
    });
    
    public async Task<IResponseRest?> TryEnqueueTaskAsync(LakeModifyRequest item)
    {
        TaskCompletionSource<IResponseRest> tcs = new TaskCompletionSource<IResponseRest>();
        if (!_queue.Writer.TryWrite(new FullLakeModifyRequest(tcs, item)))
        {
            return null;
        }
        return await tcs.Task;
    }
    
    public async ValueTask<FullLakeModifyRequest> DequeueAsync(CancellationToken cancellationToken)
    {
        return await _queue.Reader.ReadAsync(cancellationToken);
    }
}

public class LakeModifyBackgroundService(LakeModifyQueue modifyQueue, LakeCache cache) : BackgroundService
{
    private static TaskCompletionSource? _pendingCacheRequest;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            FullLakeModifyRequest fullModifyRequest = await modifyQueue.DequeueAsync(stoppingToken);

            switch (fullModifyRequest.ModifyRequest)
            {
                //? Put
                case LakePutRequest lakePutRequest:
                {
                    RestPutClass output = new RestPutClass();

                    _pendingCacheRequest?.Task.WaitAsync(stoppingToken);
                    ref readonly LakeCacheItem cachedItem = ref cache.TryGetCachedItemByPath(lakePutRequest.Path);
                    try
                    {
                        if (Unsafe.IsNullRef(in cachedItem))
                        {
                            GraphQlGetClass itemInfo =
                                await GetLakeInfo(lakePutRequest.Path, lakePutRequest.CancellationToken);

                            if (itemInfo.StatusCode != 200)
                            {
                                output.StatusCode = 500;
                                if (itemInfo.StatusCode == 499)
                                {
                                    output.StatusCode = itemInfo.StatusCode;
                                    output.ErrorMessage = "Client Closed Request";
                                    fullModifyRequest.TaskCompletionSource.SetResult(output);
                                    break;
                                }

                                if (itemInfo.StatusCode != 404)
                                {
                                    output.ErrorMessage = "Github GET Request Error\nError Code: " + itemInfo.StatusCode;
                                    fullModifyRequest.TaskCompletionSource.SetResult(output);
                                    break;
                                }

                                output = await PutLakeRequest(lakePutRequest.Path, lakePutRequest.ContentStream,
                                    lakePutRequest.ContentLength, lakePutRequest.CancellationToken);
                                output.StatusCode = 201;

                            }
                            else
                            {
                                if (itemInfo.Data.Repository?.Object?.TypeName != "Blob")
                                {
                                    output.StatusCode = 409;
                                    output.ErrorMessage = lakePutRequest.Path + " is a directory" ;
                                    fullModifyRequest.TaskCompletionSource.SetResult(output);
                                    break;
                                }
                                
                                output = await PutLakeRequest(lakePutRequest.Path, lakePutRequest.ContentStream,
                                    lakePutRequest.ContentLength, lakePutRequest.CancellationToken,
                                    itemInfo.Data.Repository.Object.Oid);
                                output.StatusCode = 200;
                            }
                        }
                        else
                        {
                            if (cachedItem is { Entities: not null, Sha: null })
                            {
                                output.StatusCode = 409;
                                output.ErrorMessage = lakePutRequest.Path + " is a directory" ;
                                fullModifyRequest.TaskCompletionSource.SetResult(output);
                                break;
                            }
                            
                            string? cachedSha = cachedItem.Sha;
                            output = await PutLakeRequest(lakePutRequest.Path, lakePutRequest.ContentStream,
                                lakePutRequest.ContentLength, lakePutRequest.CancellationToken, cachedSha);
                            output.StatusCode = 200;
                        }


                        if (output.StatusCode != null && output.StatusCode != 200 && output.StatusCode != 201)
                        {

                            if (output.StatusCode < 0)
                            {
                                output.StatusCode = 500;
                                fullModifyRequest.TaskCompletionSource.SetResult(output);
                                break;
                            }

                            if (output.StatusCode == 499)
                            {
                                fullModifyRequest.TaskCompletionSource.SetResult(output);
                                break;
                            }

                            output.StatusCode = 500;
                            output.ErrorMessage = "Github PUT Request Error\nError Code: " + output.StatusCode;
                            fullModifyRequest.TaskCompletionSource.SetResult(output);
                            break;
                        }

                        _pendingCacheRequest = new TaskCompletionSource();
                        await cache.EnqueueAsync(new LakeCacheNewItem
                        {
                            Path = lakePutRequest.Path,
                            Sha = output.RestContent.Sha,
                            ByteSize = output.RestContent.Size,
                            Entities = null,
                            TaskCompletionSource = _pendingCacheRequest
                        });

                        fullModifyRequest.TaskCompletionSource.SetResult(output);
                    }
                    catch(OperationCanceledException)
                    {
                        output.StatusCode = 499;
                        fullModifyRequest.TaskCompletionSource.SetResult(output);
                    }
                    break;
                }
                
                //? Del
                case LakeDelRequest lakeDelRequest:
                {
                    RestDeleteClass output = new RestDeleteClass();
                    
                    _pendingCacheRequest?.Task.WaitAsync(stoppingToken);
                    ref readonly LakeCacheItem cachedItem = ref cache.TryGetCachedItemByPath(lakeDelRequest.Path);

                    try
                    {
                        
                        if (Unsafe.IsNullRef(in cachedItem))
                        {
                            GraphQlGetClass itemInfo = await GetLakeInfo(lakeDelRequest.Path, lakeDelRequest.CancellationToken);

                            if (itemInfo.StatusCode != 200 || itemInfo.Data.Repository?.Object is null)
                            {
                                if (itemInfo.StatusCode == 404)
                                {
                                    output.StatusCode = itemInfo.StatusCode;
                                    output.ErrorMessage = "Not Found";
                                    fullModifyRequest.TaskCompletionSource.SetResult(output);
                                    break;
                                }
                                if (itemInfo.StatusCode == 499)
                                {
                                    output.StatusCode = itemInfo.StatusCode;
                                    output.ErrorMessage = "Client Closed Request";
                                    fullModifyRequest.TaskCompletionSource.SetResult(output);
                                    break;
                                }
                    
                                output.StatusCode = 500;
                                fullModifyRequest.TaskCompletionSource.SetResult(output);
                                break;
                                
                            }
                                
                            if (itemInfo.Data.Repository.Object.TypeName == "Tree")
                            {
                                output.StatusCode = 409;
                                output.ErrorMessage = "Cannot delete a folder";
                                fullModifyRequest.TaskCompletionSource.SetResult(output);
                                break;
                            }
                
                            output = await DelLakeRequest(lakeDelRequest.Path, itemInfo.Data.Repository.Object.Oid, fullModifyRequest.ModifyRequest.CancellationToken);
                        }
                        else
                        {
                            if (cachedItem.Sha == null)
                            {
                                output.StatusCode = 409;
                                output.ErrorMessage = "Cannot delete a folder";
                                fullModifyRequest.TaskCompletionSource.SetResult(output);
                                break;
                            }
                            output = await DelLakeRequest(lakeDelRequest.Path, cachedItem.Sha, fullModifyRequest.ModifyRequest.CancellationToken);
                        }
            
            
                        if (output.StatusCode != null)
                        {
                            output.StatusCode = 500;
                            if (output.StatusCode == 499)
                            {
                                fullModifyRequest.TaskCompletionSource.SetResult(output);
                                break;
                            }
                
                            output.ErrorMessage = "Github DEL Request Error\nError Code: " + output.StatusCode;
                        }
                        
                        cache.DeleteCachedItemByPath(lakeDelRequest.Path);
                        
                        fullModifyRequest.TaskCompletionSource.SetResult(output);
                    }
                    catch(OperationCanceledException)
                    {
                        output.StatusCode = 499;
                        fullModifyRequest.TaskCompletionSource.SetResult(output);
                    }
                    break;
                }
                
                default:
                    throw new NotSupportedException($"Type {fullModifyRequest.GetType()} not supported");
            }
        }
    }
}
