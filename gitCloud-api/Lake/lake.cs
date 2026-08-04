using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Org.BouncyCastle.Crypto;
using static gitCloud_api.LakeCacheClasses;
using static gitCloud_api.LakeModifyServiceClasses;

namespace gitCloud_api;

public static partial class Lake
{
    private static readonly ILogger Logger = LoggerFactory.Create(builder => builder.AddConsole() ).CreateLogger(typeof(Lake));
    private static readonly HttpClient HttpClient = new HttpClient();
    private static string _lakeUrl = "";

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

    }
    
    public static void MapGitCloudLakeEndpoints(this RouteGroupBuilder builder)
    {
        builder.MapGet("/{*path}", LakeGet);
        builder.MapPut("/{*path}", LakePut).RequireAuthorization();
        builder.MapDelete("/{*path}", LakeDelete).RequireAuthorization();
    } 
    
    public static async Task<GetContentClass> GetLakeRequest(string path, CancellationToken? cancellationToken = null)
    {
        try
        {
            HttpResponseMessage resp =  await HttpClient.GetAsync(_lakeUrl+path, cancellationToken??CancellationToken.None);
            GetContentClass? output;
            if (resp.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotModified or HttpStatusCode.Found)
            {
                try
                {
                    output = JsonSerializer.Deserialize<GetContentClass>(await resp.Content.ReadAsStringAsync()) ?? new GetContentClass { ErrorMessage = "Json deserialization returned null" };
                }
                catch (JsonException e)
                {
                    Logger.LogError(e.Message);
                    output = new GetContentClass
                    {
                        ErrorCode = (short)LakeErrorCodes.InternalErrorWhileParsingJson,
                        ErrorMessage = "Json internal deserialization failed"
                    };
                }
            }
            else
            {
                output = new GetContentClass
                {
                    ErrorCode = (short)resp.StatusCode,
                    ErrorMessage = await resp.Content.ReadAsStringAsync()
                };
            }
            
            return output;
        }
        catch (OperationCanceledException e)
        {
            return new GetContentClass()
            {
                ErrorCode = 499,
                ErrorMessage = "Client Closed Request"
            };
        }
    }
    
    public static async Task<PutContentClass> PutLakeRequest(string path, string content, string? sha = null, CancellationToken? cancellationToken = null)
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
            
            PutContentClass? output;
            if (resp.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created)
            {
                try
                {
                    output = JsonSerializer.Deserialize<PutContentClass>(await resp.Content.ReadAsStringAsync()) ?? new PutContentClass { ErrorMessage = "Json deserialization returned null" };
                }
                catch (JsonException e)
                {
                    Logger.LogError(e.Message);
                    output = new PutContentClass
                    {
                        ErrorCode = (short)LakeErrorCodes.InternalErrorWhileParsingJson,
                        ErrorMessage = "Json internal deserialization failed"
                    };
                }
            }
            else
            {
                output = new PutContentClass
                {
                    ErrorCode = (short)resp.StatusCode,
                    ErrorMessage = await resp.Content.ReadAsStringAsync()
                };
            }
            
            return output;
            
        }
        catch (OperationCanceledException e)
        {
            return new PutContentClass()
            {
                ErrorCode = 499,
                ErrorMessage = "Client Closed Request"
            };
        }
    }

    public static async Task<DeleteContentClass> DelLakeRequest(string path, string sha, CancellationToken? cancellationToken = null)
    {
        try
        {

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Delete, _lakeUrl+path);
            request.Content = new StringContent($"{{\"message\":\"ci: Delete {path}\",\"committer\":{{\"name\":\"gitCloud-api\",\"email\":\"gitcloud@example.com\"}},\"sha\":\"{sha}\"}}");
            HttpResponseMessage resp = await HttpClient.SendAsync(request,cancellationToken?? CancellationToken.None);
            
            DeleteContentClass? output;
            if (resp.StatusCode is HttpStatusCode.OK)
            {
                try
                {
                    output = JsonSerializer.Deserialize<DeleteContentClass>(await resp.Content.ReadAsStringAsync()) ?? new DeleteContentClass { ErrorMessage = "Json deserialization returned null" };
                }
                catch (JsonException e)
                {
                    Logger.LogError(e.Message);
                    output = new DeleteContentClass
                    {
                        ErrorCode = (short)LakeErrorCodes.InternalErrorWhileParsingJson,
                        ErrorMessage = "Json internal deserialization failed"
                    };
                }
            }
            else
            {
                output = new DeleteContentClass
                {
                    ErrorCode = (short)resp.StatusCode,
                    ErrorMessage = await resp.Content.ReadAsStringAsync()
                };
            }
            
            return output;
            
        }
        catch (OperationCanceledException e)
        {
            return new DeleteContentClass()
            {
                ErrorCode = 499,
                ErrorMessage = "Client Closed Request"
            };
        }
    }


    private static async Task<string> LakeGet(string path ,HttpContext context, ClaimsPrincipal user, LakeCache cache, CancellationToken cancellationToken)
    {
        if (context.Request.Query.ContainsKey("root") && user.IsInRole("Admin"))
        {
            path = (context.Request.Query["root"].ToString().Replace("%2F", "/") + path);
        }
        else
        {
            path = "/Lake" + path;
        }
        
        GetContentClass output = await GetLakeRequest(path, cancellationToken);
        if (output.ErrorCode != null)
        {
            switch (output.ErrorCode)
            {
                case 404:
                    context.Response.StatusCode = 404;
                    return "Not Found";
                
                case 499:
                    context.Response.StatusCode = (int)output.ErrorCode;
                    return output.ErrorMessage??"";
            }
                
            context.Response.StatusCode = 500;
            if (output.ErrorCode < 0)
            {
                return output.ErrorMessage!;                
            }

            return string.Join("",
                "Github Get Request Error\nError Code: ", output.ErrorCode, "\nMessage: \n", output.ErrorMessage);
        }
        
        switch (output.Type)
        {
            case "file":
                await cache.EnqueueAsync(new LakeCacheNewItem
                {
                    Path = path,
                    Sha = output.Sha,
                    Entities = null
                });
                break;
            case "dir":
            {
                Dictionary<string, LakeCacheItem> entities = new Dictionary<string, LakeCacheItem>();
            
                foreach (GetContentClass getContent in output.Entries?? [])
                {
                    entities.Add(getContent.Name, new LakeCacheItem
                    {
                        Sha = getContent.Type == "dir" ? null : getContent.Sha,
                        Entities = getContent.Type == "dir" ? new Dictionary<string, LakeCacheItem>() : null
                    });
                }
            
                await cache.EnqueueAsync(new LakeCacheNewItem
                {
                    Path = path,
                    Sha = null,
                    Entities = entities
                });
                break;
            }
        }
        
        
        LakeGetResponseClass[]? entitiesArray = output.Type == "dir" ? new LakeGetResponseClass[output.Entries?.Length??0] : null;
        
        if (entitiesArray is not null)
        {
            for (int i = 0; i < (output.Entries?.Length??0); i++)
            {
                entitiesArray[i] = new LakeGetResponseClass
                {
                    Type = output.Entries![i].Type,
                    Name = output.Entries[i].Name,
                    Path = output.Entries[i].Path,
                    Content = null,
                    Entities = output.Entries[i].Type == "dir" ? [] : null
                };
            }
        }
        
        context.Response.StatusCode = 200;
        context.Response.ContentType = "application/json";
        return JsonSerializer.Serialize(new LakeGetResponseClass
        {
            Type = output.Type,
            Name = output.Name,
            Path = output.Path,
            Content = output.Content,
            Entities = entitiesArray
        });
    }

    private static async Task<String> LakePut(string path, HttpContext context, ClaimsPrincipal user, LakeCache cache, LakeModifyQueue modifyQueue, CancellationToken cancellationToken)
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

            PutContentClass? output = (PutContentClass?)await modifyQueue.TryEnqueueTaskAsync(new LakePutRequest
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

    private static async Task<String> LakeDelete(string path, HttpContext context, ClaimsPrincipal user, LakeCache cache, LakeModifyQueue modifyQueue, CancellationToken cancellationToken)
    {
        if (context.Request.Query.ContainsKey("root") && user.IsInRole("Admin"))
        {
            path = (context.Request.Query["root"].ToString().Replace("%2F", "/") + path);
        }
        else
        {
            path = "/Lake" + path;
        }
        
        DeleteContentClass? output = (DeleteContentClass?)await modifyQueue.TryEnqueueTaskAsync(new LakeDelRequest
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