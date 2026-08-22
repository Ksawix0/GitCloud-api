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
    private static Func<string, string> _getGraphQlQuery = s =>"";

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
        
        _getGraphQlQuery = path => partialRequest+path.Trim('/')+@"""}}";
    }
    
    public static void MapGitCloudLakeEndpoints(this RouteGroupBuilder builder)
    {
        builder.MapGet("/{*path}", LakeGet);
        builder.MapPut("/{*path}", LakePut).RequireAuthorization();
        builder.MapDelete("/{*path}", LakeDelete).RequireAuthorization();
    } 
    
    public static async Task<RestGetClass> GetLakeRequest(string path, CancellationToken? cancellationToken = null)
    {
        try
        {
            HttpResponseMessage resp =  await HttpClient.GetAsync(_lakeUrl+path, cancellationToken??CancellationToken.None);
            RestGetClass? output;
            if (resp.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotModified or HttpStatusCode.Found)
            {
                try
                {
                    output = JsonSerializer.Deserialize<RestGetClass>(await resp.Content.ReadAsStringAsync()) ?? new RestGetClass { ErrorMessage = "Json deserialization returned null" };
                }
                catch (JsonException e)
                {
                    Logger.LogError(e.Message);
                    output = new RestGetClass
                    {
                        ErrorCode = (short)LakeErrorCodes.InternalErrorWhileParsingJson,
                        ErrorMessage = "Json internal deserialization failed"
                    };
                }
            }
            else
            {
                output = new RestGetClass
                {
                    ErrorCode = (short)resp.StatusCode,
                    ErrorMessage = await resp.Content.ReadAsStringAsync()
                };
            }
            
            return output;
        }
        catch (OperationCanceledException e)
        {
            return new RestGetClass()
            {
                ErrorCode = 499,
                ErrorMessage = "Client Closed Request"
            };
        }
    }

    public static async Task GetLakeSendRawDataBlob(string path, HttpContext outputContext, long? size,LakeCache cache ,CancellationToken cancellationToken)
    {
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
                cache.DeleteCachedItemByPath(path);
                outputContext.Response.StatusCode = 404;
                break;
            }
            default:
            {
                outputContext.Response.StatusCode = 500;
                return;
                break;
            }
        }

        outputContext.Response.StatusCode = 200;
        outputContext.Response.Headers.ContentLength = size;
        await restResponse.Content.ReadAsStreamAsync(cancellationToken).Result.CopyToAsync(outputContext.Response.Body,81920, cancellationToken);
    }
    
    public static async Task<RestPutClass> PutLakeRequest(string path, string content, string? sha = null, CancellationToken? cancellationToken = null)
    {
        try
        {

            HttpResponseMessage resp;
            if (sha is null)
            {
                resp = await HttpClient.PutAsync(_lakeUrl+path, new StringContent( $"{{\"message\":\"ci: add {path}\",\"committer\":{{\"name\":\"gitCloud-api\",\"email\":\"gitcloud@example.com\"}},\"content\":\"{content}\"}}"), cancellationToken??CancellationToken.None);
            }
            else
            {
                resp = await HttpClient.PutAsync(_lakeUrl+path, new StringContent( $"{{\"message\":\"ci: add {path}\",\"committer\":{{\"name\":\"gitCloud-api\",\"email\":\"gitcloud@example.com\"}},\"content\":\"{content}\",\"sha\":\"{sha}\"}}"), cancellationToken??CancellationToken.None);
            }
            
            RestPutClass? output;
            if (resp.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created)
            {
                try
                {
                    output = JsonSerializer.Deserialize<RestPutClass>(await resp.Content.ReadAsStringAsync()) ?? new RestPutClass { ErrorMessage = "Json deserialization returned null" };
                }
                catch (JsonException e)
                {
                    Logger.LogError(e.Message);
                    output = new RestPutClass
                    {
                        ErrorCode = (short)LakeErrorCodes.InternalErrorWhileParsingJson,
                        ErrorMessage = "Json internal deserialization failed"
                    };
                }
            }
            else
            {
                output = new RestPutClass
                {
                    ErrorCode = (short)resp.StatusCode,
                    ErrorMessage = await resp.Content.ReadAsStringAsync()
                };
            }
            
            return output;
            
        }
        catch (OperationCanceledException e)
        {
            return new RestPutClass()
            {
                ErrorCode = 499,
                ErrorMessage = "Client Closed Request"
            };
        }
    }

    public static async Task<RestDeleteClass> DelLakeRequest(string path, string sha, CancellationToken? cancellationToken = null)
    {
        try
        {

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Delete, _lakeUrl+path);
            request.Content = new StringContent($"{{\"message\":\"ci: Delete {path}\",\"committer\":{{\"name\":\"gitCloud-api\",\"email\":\"gitcloud@example.com\"}},\"sha\":\"{sha}\"}}");
            HttpResponseMessage resp = await HttpClient.SendAsync(request,cancellationToken?? CancellationToken.None);
            
            RestDeleteClass? output;
            if (resp.StatusCode is HttpStatusCode.OK)
            {
                try
                {
                    output = JsonSerializer.Deserialize<RestDeleteClass>(await resp.Content.ReadAsStringAsync()) ?? new RestDeleteClass { ErrorMessage = "Json deserialization returned null" };
                }
                catch (JsonException e)
                {
                    Logger.LogError(e.Message);
                    output = new RestDeleteClass
                    {
                        ErrorCode = (short)LakeErrorCodes.InternalErrorWhileParsingJson,
                        ErrorMessage = "Json internal deserialization failed"
                    };
                }
            }
            else
            {
                output = new RestDeleteClass
                {
                    ErrorCode = (short)resp.StatusCode,
                    ErrorMessage = await resp.Content.ReadAsStringAsync()
                };
            }
            
            return output;
            
        }
        catch (OperationCanceledException e)
        {
            return new RestDeleteClass()
            {
                ErrorCode = 499,
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

            if (!Unsafe.IsNullRef(in cachedItem))
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
                            Type = cachedItem.Entities[name].Sha is null ? "dir" : "file",
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
                    await GetLakeSendRawDataBlob(path, context, cachedItem.ByteSize, cache, cancellationToken);
                }

                return;
            }
            
            GraphQlGetClass? graphQlResult;
            using (HttpResponseMessage graphQlResponse = await HttpClient.PostAsync("https://api.github.com/graphql", new StringContent(_getGraphQlQuery(path)), cancellationToken))
            {
                if (graphQlResponse.StatusCode != HttpStatusCode.OK)
                {
                    context.Response.StatusCode = 500;
                    return;
                }
                graphQlResult = JsonSerializer.Deserialize<GraphQlGetClass>(await graphQlResponse.Content.ReadAsStringAsync(cancellationToken));
            }
            
            if(graphQlResult?.Data.Repository?.Object is null)
            {
                context.Response.StatusCode = 404;
                return;
            }

            
            
            switch (graphQlResult.Data.Repository.Object.TypeName)
            {
                case "Blob":

                    Task sendDataTask = GetLakeSendRawDataBlob(path, context, graphQlResult.Data.Repository.Object.ByteSize, cache, cancellationToken);
                    
                    await cache.EnqueueAsync(new LakeCacheNewItem
                    {
                        Path = path,
                        Sha = graphQlResult.Data.Repository.Object.Oid,
                        ByteSize = graphQlResult.Data.Repository.Object.ByteSize
                    });

                    await sendDataTask;
                    return;
                    break;
                
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
                                Sha = entry.Object.Oid,
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
                    break;
                }
            }

            // RestGetClass output = await GetLakeRequest(path, cancellationToken);
            // if (output.ErrorCode != null)
            // {
            //     switch (output.ErrorCode)
            //     {
            //         case 404:
            //             context.Response.StatusCode = 404;
            //             return "Not Found";
            //         
            //         case 499:
            //             context.Response.StatusCode = (int)output.ErrorCode;
            //             return output.ErrorMessage??"";
            //     }
            //         
            //     context.Response.StatusCode = 500;
            //     if (output.ErrorCode < 0)
            //     {
            //         return output.ErrorMessage!;                
            //     }
            //
            //     return string.Join("",
            //         "Github Get Request Error\nError Code: ", output.ErrorCode, "\nMessage: \n", output.ErrorMessage);
            // }
            //
            // switch (output.Type)
            // {
            //     case "file":
            //         await cache.EnqueueAsync(new LakeCacheNewItem
            //         {
            //             Path = path,
            //             Sha = output.Sha,
            //             Entities = null
            //         });
            //         break;
            //     case "dir":
            //     {
            //         Dictionary<string, LakeCacheItem> entities = new Dictionary<string, LakeCacheItem>();
            //     
            //         foreach (RestGetClass getContent in output.Entries?? [])
            //         {
            //             entities.Add(getContent.Name, new LakeCacheItem
            //             {
            //                 Sha = getContent.Type == "dir" ? null : getContent.Sha,
            //                 Entities = getContent.Type == "dir" ? new Dictionary<string, LakeCacheItem>() : null
            //             });
            //         }
            //     
            //         await cache.EnqueueAsync(new LakeCacheNewItem
            //         {
            //             Path = path,
            //             Sha = null,
            //             Entities = entities
            //         });
            //         break;
            //     }
            // }
            //
            //
            // GitCloudGetResponseClass[]? entitiesArray = output.Type == "dir" ? new GitCloudGetResponseClass[output.Entries?.Length??0] : null;
            //
            // if (entitiesArray is not null)
            // {
            //     for (int i = 0; i < (output.Entries?.Length??0); i++)
            //     {
            //         entitiesArray[i] = new GitCloudGetResponseClass
            //         {
            //             Type = output.Entries![i].Type,
            //             Name = output.Entries[i].Name,
            //             Path = output.Entries[i].Path,
            //             Content = null,
            //             Entities = output.Entries[i].Type == "dir" ? [] : null
            //         };
            //     }
            // }
            //
            // context.Response.StatusCode = 200;
            // context.Response.ContentType = "application/json";
            // return JsonSerializer.Serialize(new GitCloudGetResponseClass
            // {
            //     Type = output.Type,
            //     Name = output.Name,
            //     Path = output.Path,
            //     Content = output.Content,
            //     Entities = entitiesArray
            // });
            
            context.Response.StatusCode = 500;
            return;
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = 499;
            return;
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
            Task<string> bodyContentTask = (new StreamReader(context.Request.Body, encoding: Encoding.UTF8)).ReadToEndAsync(cancellationToken);

            RestPutClass? output = (RestPutClass?)await modifyQueue.TryEnqueueTaskAsync(new LakePutRequest
            {
                BodyContentTask = bodyContentTask,
                Path = path,
                CancellationToken = cancellationToken
            });

            if (output is null)
            {
                context.Response.StatusCode = 429;
                return "Too Many Requests";
            }
            context.Response.StatusCode = (output.ErrorCode ?? 200);
            if(output.ErrorMessage is not null)
            {
                return output.ErrorMessage!;
            }
            
            return "";

        }
        catch (OperationCanceledException e)
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
        
        context.Response.StatusCode = (output.ErrorCode ?? 200);
        if(output.ErrorMessage is not null)
        {
            return output.ErrorMessage!;
        }
        
        return "";
    }
}