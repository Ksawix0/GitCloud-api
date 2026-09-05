using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using static gitCloud_api.LakeModifyServiceClasses;
using static gitCloud_api.LakeCacheClasses;

namespace gitCloud_api;

public static partial class Lake
{
    private static readonly ILogger Logger = LoggerFactory.Create(builder => builder.AddConsole() ).CreateLogger(typeof(Lake));
    private static readonly HttpClient HttpClient = new HttpClient();

    private static readonly JsonSerializerOptions LakeDefaultSerializerOptions = new JsonSerializerOptions()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    
    private static string _lakeUrl = "";
    private static Func<string, string> _getInfoGraphQlQuery = _ =>"";

    private static byte[][] _modifyLakePrefix =
    [
        [.. "{\"message\":\"ci: "u8],
        [.. "\",\"committer\":{\"name\":\"gitCloud-api\",\"email\":\"gitcloud@example.com\"},\"content\":\""u8]
    ]; 

    public static void InitLake(string token, string repositoryName, string userName)
    {
        
        HttpClient.DefaultRequestHeaders.Accept.Clear();
        HttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.object", 1));
        HttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json", 0.9));
        HttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        HttpClient.DefaultRequestHeaders.UserAgent.Clear();
        HttpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("gitCloud-api", "1.0.0"));
        HttpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        HttpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2026-03-10");

        _lakeUrl = $"https://api.github.com/repos/{userName}/{repositoryName}/contents";

        string query = """
                       query CheckType($owner: String!, $name: String!, $expression: String!) {
                         repository(owner: $owner, name: $name) {
                           object(expression: $expression) {
                             __typename
                       
                             ... on Blob {
                               byteSize
                               oid
                             }
                             
                             ... on Tree {
                               entries{
                                 name
                                 
                                 object{
                                   __typename
                                 
                                   ... on Blob{
                                     byteSize
                                     oid
                                   }
                                 }
                               }
                             }
                           }
                         }
                       }
                       """.Replace("\r\n", "\\n").Replace("\n", "\\n");

        string partialRequest = $$"""
                                {"query":"{{query}}","variables":{"owner":"{{userName}}","name":"{{repositoryName}}","expression":"main:
                                """.Replace("\r\n","").Replace("\n","");
        
        _getInfoGraphQlQuery = path => partialRequest+path.Trim('/')+@"""}}";
    }
    
    public static void MapGitCloudLakeEndpoints(this RouteGroupBuilder builder)
    {
        builder.MapGet("/{*path}", LakeGet);
        builder.MapPut("/{*path}", LakePut).RequireAuthorization();
        builder.MapDelete("/{*path}", LakeDelete).RequireAuthorization();
    } 
    
    // public static async Task<RestGetClass> GetLakeRequest(string path, CancellationToken? cancellationToken = null)
    // {
    //     try
    //     {
    //         HttpResponseMessage resp =  await HttpClient.GetAsync(_lakeUrl+path, cancellationToken??CancellationToken.None);
    //         RestGetClass? output;
    //         if (resp.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotModified or HttpStatusCode.Found)
    //         {
    //             try
    //             {
    //                 output = JsonSerializer.Deserialize<RestGetClass>(await resp.Content.ReadAsStringAsync()) ?? new RestGetClass { ErrorMessage = "Json deserialization returned null" };
    //             }
    //             catch (JsonException e)
    //             {
    //                 Logger.LogError(e.Message);
    //                 output = new RestGetClass
    //                 {
    //                     StatusCode = (short)LakeErrorCodes.InternalErrorWhileParsingJson,
    //                     ErrorMessage = "Json internal deserialization failed"
    //                 };
    //             }
    //         }
    //         else
    //         {
    //             output = new RestGetClass
    //             {
    //                 StatusCode = (short)resp.StatusCode,
    //                 ErrorMessage = await resp.Content.ReadAsStringAsync()
    //             };
    //         }
    //         
    //         return output;
    //     }
    //     catch (OperationCanceledException)
    //     {
    //         return new RestGetClass()
    //         {
    //             StatusCode = 499,
    //             ErrorMessage = "Client Closed Request"
    //         };
    //     }
    // }

    public static async Task<RestGetRawDataInfo> GetLakeRawDataBlob(string path, Stream outputStream,CancellationToken cancellationToken)
    {
        RestGetRawDataInfo outputInfo = new RestGetRawDataInfo();
        HttpResponseMessage restResponse;
        using (HttpRequestMessage restRequest = new HttpRequestMessage(HttpMethod.Get, _lakeUrl + path))
        {
            restRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.raw+json", 1.0));
            restResponse = await HttpClient.SendAsync(restRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }

        switch (restResponse.StatusCode)
        {
            case HttpStatusCode.OK:
            {
                break;
            }
            case HttpStatusCode.NotFound:
            {
                outputInfo.StatusCode = 404;
                return outputInfo;
            }
            default:
            {
                outputInfo.StatusCode = 500;
                return outputInfo;
            }
        }
        outputInfo.Length = restResponse.Content.Headers.ContentLength;
        outputInfo.StatusCode = 200;
        await (await restResponse.Content.ReadAsStreamAsync(cancellationToken)).CopyToAsync(outputStream,81920, cancellationToken);
        return outputInfo;
    }

    public static async Task<GraphQlGetClass> GetLakeInfo(string path, CancellationToken cancellationToken)
    {
        GraphQlGetClass? graphQlResult = new GraphQlGetClass(){Data = new GraphQlData()};
        try
        {
            using (HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.github.com/graphql"))
            {
                httpRequest.Content = new StringContent(_getInfoGraphQlQuery(path));
                HttpResponseMessage graphQlResponse = await HttpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                
                if (graphQlResponse.StatusCode != HttpStatusCode.OK)
                {
                    graphQlResult.StatusCode = 500;
                    return graphQlResult;
                }
                graphQlResult = JsonSerializer.Deserialize<GraphQlGetClass>(await graphQlResponse.Content.ReadAsStringAsync(cancellationToken));
                if (graphQlResult?.Data.Repository?.Object is not null)
                {
                    graphQlResult.StatusCode = (short)graphQlResponse.StatusCode;
                    return graphQlResult;
                }
            }

            
            graphQlResult ??= new GraphQlGetClass() { Data = new GraphQlData() };
            graphQlResult.StatusCode = 404;
            return graphQlResult;

        }
        catch(OperationCanceledException)
        {
            graphQlResult ??= new GraphQlGetClass() { Data = new GraphQlData() };
            graphQlResult.StatusCode = 499;
            return graphQlResult;
        }
        
    }
    
    public static async Task<RestPutClass> PutLakeRequest(string path, Stream contentStream, long? contentLenght, CancellationToken cancellationToken, string? sha = null)
    {
        try
        {
            HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Put, _lakeUrl + path);
            if (sha is null)
            {
                httpRequest.Content = new StreamCircumfixContent(
                    [.. _modifyLakePrefix[0], .. Encoding.UTF8.GetBytes("add " + path), .. _modifyLakePrefix[1]],
                    contentStream,
                    [.. "\"}"u8], contentLenght);
            }
            else
            {
                httpRequest.Content = new StreamCircumfixContent(
                    [.. _modifyLakePrefix[0], .. Encoding.UTF8.GetBytes("modify " + path), .. _modifyLakePrefix[1]],
                    contentStream,
                    [.. "\",\"sha\":\""u8, .. Encoding.UTF8.GetBytes(sha), .. "\"}"u8], contentLenght);
            }
            
            HttpResponseMessage response = await HttpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            
            RestPutClass? output;
            if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created)
            {
                try
                {
                    output = JsonSerializer.Deserialize<RestPutClass>(await response.Content.ReadAsStringAsync(cancellationToken)) ?? new RestPutClass { ErrorMessage = "Json deserialization returned null" };
                    output.StatusCode = (short)response.StatusCode;
                }
                catch (JsonException e)
                {
                    Logger.LogError(e.Message);
                    output = new RestPutClass
                    {
                        StatusCode = 500,
                        ErrorMessage = "Json internal deserialization failed"
                    };
                }
            }
            else
            {
                output = new RestPutClass
                {
                    StatusCode = (short)response.StatusCode,
                    ErrorMessage = await response.Content.ReadAsStringAsync(cancellationToken)
                };
            }
            
            return output;
            
        }
        catch (OperationCanceledException)
        {
            return new RestPutClass()
            {
                StatusCode = 499,
                ErrorMessage = "Client Closed Request"
            };
        }
    }

    public static async Task<RestDeleteClass> DelLakeRequest(string path, string sha, CancellationToken cancellationToken)
    {
        try
        {

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Delete, _lakeUrl+path);
            request.Content = new ByteArrayContent([
                .. _modifyLakePrefix[0], .. Encoding.UTF8.GetBytes("delete " + path), .._modifyLakePrefix[1], .."\",\"sha\":\""u8, ..Encoding.UTF8.GetBytes(sha), .."\"}"u8
            ]);
            HttpResponseMessage resp = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,cancellationToken);
            
            RestDeleteClass? output;
            if (resp.StatusCode is HttpStatusCode.OK)
            {
                output = new RestDeleteClass();

                // try
                // {
                //     output = JsonSerializer.Deserialize<RestDeleteClass>(await resp.Content.ReadAsStringAsync()) ?? new RestDeleteClass { ErrorMessage = "Json deserialization returned null" };
                // }
                // catch (JsonException e)
                // {
                //     Logger.LogError(e.Message);
                //     output = new RestDeleteClass
                //     {
                //         ErrorCode = (short)LakeErrorCodes.InternalErrorWhileParsingJson,
                //         ErrorMessage = "Json internal deserialization failed"
                //     };
                // }
            }
            else
            {
                output = new RestDeleteClass
                {
                    StatusCode = (short)resp.StatusCode,
                    ErrorMessage = await resp.Content.ReadAsStringAsync(cancellationToken)
                };
            }
            
            return output;
            
        }
        catch (OperationCanceledException)
        {
            return new RestDeleteClass()
            {
                StatusCode = 499,
                ErrorMessage = "Client Closed Request"
            };
        }
    }


    private static async Task LakeGet(HttpContext context, ClaimsPrincipal user, LakeCache cache, CancellationToken cancellationToken, string path = "")
    {
        if (context.Request.Query.ContainsKey("root") && user.IsInRole("Admin"))
        {
            path = (Uri.UnescapeDataString(context.Request.Query["root"].ToString()) + path);
        }
        else
        {
            path = "/Lake/" + path;
        }
        
        
        try
        {
            
            ref readonly LakeCacheItem cachedItem = ref cache.TryGetCachedItemByPath(path);

            if (!Unsafe.IsNullRef(in cachedItem) && cachedItem.Known)
            {
                //? not blob
                if (cachedItem.Sha is null && cachedItem.Entities != null)
                {
                    GitCloudGetResponseClass[] entitiesArray = new GitCloudGetResponseClass[cachedItem.Entities?.Count ?? 0];
                    int i = 0;
                    foreach (string name in cachedItem.Entities?.Keys?? new Dictionary<string, LakeCacheItem>.KeyCollection(new Dictionary<string, LakeCacheItem>()))
                    {
                        entitiesArray[i] = new GitCloudGetResponseClass
                        {
                            Type = cachedItem.Entities![name].Sha is null ? "dir" : "file",
                            Name = name,
                            ByteSize = cachedItem.Entities[name].ByteSize ,
                            Entities = cachedItem.Entities[name].Sha is null ? [] : null
                        };
                        i++;
                    }

                    context.Response.Headers.ContentType = "application/json";
                    ReadOnlyMemory<byte> output = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new GitCloudGetResponseClass
                    {
                        Type = "dir",
                        Entities = entitiesArray
                    }, LakeDefaultSerializerOptions));
                    context.Response.ContentLength = output.Length;
                    await context.Response.Body.WriteAsync(output, cancellationToken);
                }
                else //? Blob
                {
                    context.Response.Headers.ContentLength = cachedItem.ByteSize;
                    context.Response.StatusCode = 200;
                    RestGetRawDataInfo info = await GetLakeRawDataBlob(path, context.Response.Body, cancellationToken);
                    
                    if (info.StatusCode != 200)
                    {
                        context.Response.Headers.ContentLength = 0;
                        context.Response.StatusCode = (int)info.StatusCode!;
                        if(info.StatusCode == 404)
                        {
                            cache.DeleteCachedItemByPath(path);
                        }
                    }
                }

                return;
            }
            
            GraphQlGetClass graphQlResult = await GetLakeInfo(path, cancellationToken);
            if(graphQlResult.StatusCode != 200 )
            {
                context.Response.StatusCode = graphQlResult.StatusCode;
                return;
            }
            
            switch (graphQlResult.Data.Repository!.Object!.TypeName)
            {
                case "Blob":
                    context.Response.Headers.ContentLength = graphQlResult.Data.Repository.Object.ByteSize;
                    context.Response.StatusCode = 200;
                    Task<RestGetRawDataInfo> sendDataTask = GetLakeRawDataBlob(path, context.Response.Body, cancellationToken);
                    
                    await cache.EnqueueAsync(new LakeCacheNewItem
                    {
                        Path = path,
                        Sha = graphQlResult.Data.Repository.Object.Oid,
                        ByteSize = graphQlResult.Data.Repository.Object.ByteSize
                    });

                    RestGetRawDataInfo info = await sendDataTask;
                    if (info.StatusCode != 200)
                    {
                        context.Response.Headers.ContentLength = 0;
                        context.Response.StatusCode = (int)info.StatusCode!;
                    }
                    return;
                
                case "Tree":
                {
                    Dictionary<string, LakeCacheItem> entities = new Dictionary<string, LakeCacheItem>();
                
                    foreach (GraphQlEntry entry in graphQlResult.Data.Repository.Object.Entries?? [])
                    {
                        if (entry.Object.TypeName == "Tree")
                        {
                            entities.Add(entry.Name, new LakeCacheItem
                            {
                                Entities = new Dictionary<string, LakeCacheItem>()
                            });
                        }
                        else
                        {
                            entities.Add(entry.Name, new LakeCacheItem
                            {
                                ByteSize = entry.Object.ByteSize,
                                Sha = entry.Object.Oid,
                                Known = true
                            });
                        }
                    }
                
                    await cache.EnqueueAsync(new LakeCacheNewItem
                    {
                        Path = path,
                        Sha = null,
                        Entities = entities
                    });
                    
                    context.Response.Headers.ContentType = "application/json";
                    ReadOnlyMemory<byte> output = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new GitCloudGetResponseClass()
                   {
                       Type = "dir",
                       Entities =
                       [
                           .. entities.Select(pair => new GitCloudGetResponseClass()
                           {
                               Type = pair.Value.Sha is null ? "dir" : "file",
                               Name = pair.Key,
                               ByteSize = pair.Value.ByteSize,
                               Entities = pair.Value.Entities is null ? null : []
                           })
                       ]
                    }, LakeDefaultSerializerOptions));
                    context.Response.ContentLength = output.Length;
                    await context.Response.Body.WriteAsync(output, cancellationToken);
                    return;
                }
            }
            
            context.Response.StatusCode = 500;
            
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = 499;
            
        }
    }

    private static async Task<string> LakePut(HttpContext context, ClaimsPrincipal user, LakeCache cache, LakeModifyQueue modifyQueue, CancellationToken cancellationToken, string path = "")
    {
        if (context.Request.Query.ContainsKey("root") && user.IsInRole("Admin"))
        {
            path = (context.Request.Query["root"].ToString().Replace("%2F", "/") + path);
        }
        else
        {
            path = "/Lake" + path;
        }

        try
        {
            RestPutClass? output = (RestPutClass?)await modifyQueue.TryEnqueueTaskAsync(new LakePutRequest
            {
                Path = path,
                ContentStream = context.Request.Body,
                ContentLength = context.Request.ContentLength, 
                CancellationToken = cancellationToken
            });

            if (output is null)
            {
                context.Response.StatusCode = 429;
                return "Too Many Requests";
            }
            context.Response.StatusCode = (output.StatusCode ?? 200);
            if(output.ErrorMessage is not null)
            {
                return output.ErrorMessage!;
            }
            
            return "";

        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = 499;
            return "Client Closed Request";
        }
    }

    private static async Task<string> LakeDelete(HttpContext context, ClaimsPrincipal user, LakeCache cache, LakeModifyQueue modifyQueue, CancellationToken cancellationToken, string path = "")
    {
        if (context.Request.Query.ContainsKey("root") && user.IsInRole("Admin"))
        {
            path = (context.Request.Query["root"].ToString().Replace("%2F", "/") + path);
        }
        else
        {
            path = "/Lake" + path;
        }
        
        RestDeleteClass? output = (RestDeleteClass?)await modifyQueue.TryEnqueueTaskAsync(new LakeDelRequest
        {
            Path = path,
            CancellationToken = cancellationToken
        });
        
        if (output is null)
        {
            context.Response.StatusCode = 429;
            return "Too Many Requests";
        }
        
        context.Response.StatusCode = (output.StatusCode ?? 200);
        if(output.ErrorMessage is not null)
        {
            return output.ErrorMessage!;
        }
        
        return "";
    }
}