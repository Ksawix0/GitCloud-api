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
            FullLakeModifyRequest modifyRequest = await modifyQueue.DequeueAsync(stoppingToken);

            switch (modifyRequest.ModifyRequest)
            {
                //? Put
                case LakePutRequest lakePutRequest:
                {
                    RestPutClass output = new RestPutClass();

                    _pendingCacheRequest?.Task.WaitAsync(stoppingToken);
                    ref readonly LakeCacheItem cachedItem = ref cache.TryGetCachedItemByPath(lakePutRequest.Path);
                    
                    if (Unsafe.IsNullRef(in cachedItem))
                    {
                        var lakeContent = await GetLakeRequest(lakePutRequest.Path, modifyRequest.ModifyRequest.CancellationToken);
        
                        if (lakeContent.ErrorCode != null)
                        {
                            output.ErrorCode = 500;
                            if (lakeContent.ErrorCode < 0)
                            {
                                output.ErrorMessage = lakeContent.ErrorMessage!;
                                modifyRequest.TaskCompletionSource.SetResult(output);
                                break;
                            } 
                            if (lakeContent.ErrorCode == 499)
                            {
                                output.ErrorCode = lakeContent.ErrorCode;
                                output.ErrorMessage = lakeContent.ErrorMessage ?? "";
                                modifyRequest.TaskCompletionSource.SetResult(output);
                                break;
                            }
                            if (lakeContent.ErrorCode != 404)
                            {
                                output.ErrorMessage = string.Join("", "Github GET Request Error\nError Code: ", lakeContent.ErrorCode, "\nMessage: \n", lakeContent.ErrorMessage);
                                modifyRequest.TaskCompletionSource.SetResult(output);
                                break;
                            }
                            
                            output = await PutLakeRequest(lakePutRequest.Path, await lakePutRequest.BodyContentTask, cancellationToken: modifyRequest.ModifyRequest.CancellationToken);
                            output.ErrorCode = 201;
                            
                        }
                        else
                        {
                            output = await PutLakeRequest(lakePutRequest.Path, await lakePutRequest.BodyContentTask, lakeContent.Sha, modifyRequest.ModifyRequest.CancellationToken);
                            output.ErrorCode = 200;
                        }
                    }
                    else
                    {
                        string? cachedSha = cachedItem.Sha;
                        output = await PutLakeRequest(lakePutRequest.Path, await lakePutRequest.BodyContentTask, cachedSha, modifyRequest.ModifyRequest.CancellationToken);
                        output.ErrorCode = 200;
                    }

                    
                    if (output.ErrorCode != null && output.ErrorCode != 200 && output.ErrorCode != 201)
                    {
            
                        if (output.ErrorCode < 0)
                        {
                            output.ErrorCode = 500;
                            modifyRequest.TaskCompletionSource.SetResult(output);
                            break;
                        }
                        if (output.ErrorCode == 499)
                        {
                            modifyRequest.TaskCompletionSource.SetResult(output);
                            break;
                        }
                        
                        output.ErrorCode = 500;
                        output.ErrorMessage = string.Join("", "Github PUT Request Error\nError Code: ", output.ErrorCode, "\nMessage: \n", output.ErrorMessage);
                        modifyRequest.TaskCompletionSource.SetResult(output);
                        break;
                    }

                    _pendingCacheRequest = new TaskCompletionSource();
                    await cache.EnqueueAsync(new LakeCacheNewItem
                    {
                        Path = lakePutRequest.Path,
                        Sha = output.RestContent.Sha,
                        Entities = null,
                        TaskCompletionSource = _pendingCacheRequest
                    });
                    
                    modifyRequest.TaskCompletionSource.SetResult(output);
                    break;
                }
                
                //? Del
                case LakeDelRequest lakeDelRequest:
                {
                    RestDeleteClass output = new RestDeleteClass();
                    
                    _pendingCacheRequest?.Task.WaitAsync(stoppingToken);
                    ref readonly LakeCacheItem cachedItem = ref cache.TryGetCachedItemByPath(lakeDelRequest.Path);

                    if (Unsafe.IsNullRef(in cachedItem))
                    {
                        RestGetClass lake = await GetLakeRequest(lakeDelRequest.Path, modifyRequest.ModifyRequest.CancellationToken);
                        if (lake.ErrorCode != null)
                        {
                            if (lake.ErrorCode == 404)
                            {
                                output.ErrorCode = lake.ErrorCode;
                                output.ErrorMessage = "Not Found";
                                modifyRequest.TaskCompletionSource.SetResult(output);
                                break;
                            }
                            if (lake.ErrorCode == 499)
                            {
                                output.ErrorCode = lake.ErrorCode;
                                output.ErrorMessage = lake.ErrorMessage ?? "";
                                modifyRequest.TaskCompletionSource.SetResult(output);
                                break;
                            }
                
                            output.ErrorCode = 500;
                            if (lake.ErrorCode < 0)
                            {
                                output.ErrorMessage = lake.ErrorMessage!;
                                modifyRequest.TaskCompletionSource.SetResult(output);
                                break;
                            }
                
                            output.ErrorMessage = string.Join("", "Github GET Request Error\nError Code: ", lake.ErrorCode, "\nMessage: \n", lake.ErrorMessage);
                            modifyRequest.TaskCompletionSource.SetResult(output);
                            break;
                        }

                        if (lake.Type == "dir")
                        {
                            output.ErrorCode = 409;
                            output.ErrorMessage = "Cannot delete a folder";
                            modifyRequest.TaskCompletionSource.SetResult(output);
                            break;
                        }
            
                        output = await DelLakeRequest(lakeDelRequest.Path, lake.Sha, modifyRequest.ModifyRequest.CancellationToken);
                    }
                    else
                    {
                        if (cachedItem.Sha == null)
                        {
                            output.ErrorCode = 409;
                            output.ErrorMessage = "Cannot delete a folder";
                            modifyRequest.TaskCompletionSource.SetResult(output);
                            break;
                        }
                        output = await DelLakeRequest(lakeDelRequest.Path, cachedItem.Sha, modifyRequest.ModifyRequest.CancellationToken);
                    }
        
        
                    if (output.ErrorCode != null)
                    {
                        output.ErrorCode = 500;
                        if (output.ErrorCode < 0)
                        {
                            output.ErrorMessage = output.ErrorMessage!;           
                            modifyRequest.TaskCompletionSource.SetResult(output);
                            break;
                        }
                        if (output.ErrorCode == 499)
                        {
                            modifyRequest.TaskCompletionSource.SetResult(output);
                            break;
                        }
            
                        output.ErrorMessage = string.Join("", "Github DEL Request Error\nError Code: ", output.ErrorCode, "\nMessage: \n", output.ErrorMessage);
                    }
                    
                    cache.DeleteCachedItemByPath(lakeDelRequest.Path);
                    
                    modifyRequest.TaskCompletionSource.SetResult(output);
                    break;
                }
                
                default:
                    throw new NotSupportedException($"Type {modifyRequest.GetType()} not supported");
            }
        }
    }
}
