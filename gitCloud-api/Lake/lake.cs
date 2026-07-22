using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

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

        _lakeUrl = $"https://api.github.com/repos/{userName}/{repositoryName}/contents/";

    }
    
    public static void MapGitCloudLakeEndpoints(this RouteGroupBuilder builder)
    {
        builder.MapGet("/{*path}", LakeGet);
        builder.MapPut("/{*path}", LakePut);
        builder.MapDelete("/{*path}", LakeDelete);
    } 
    

    public static async Task<GetContentClass> GetLakeRequest(string path)
    {
        HttpResponseMessage resp =  await HttpClient.GetAsync(_lakeUrl+path);
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
    
    public static async Task<PutContentClass> PutLakeRequest(string path, string content, string? sha = null)
    {
        HttpResponseMessage resp;
        if (sha is null)
        {
            resp = await HttpClient.PutAsync(_lakeUrl+path, new StringContent( $"{{\"message\":\"ci: add {path}\",\"committer\":{{\"name\":\"gitCloud-api\",\"email\":\"gitcloud@example.com\"}},\"content\":\"{content}\"}}"));
        }
        else
        {
            resp = await HttpClient.PutAsync(_lakeUrl+path, new StringContent( $"{{\"message\":\"ci: add {path}\",\"committer\":{{\"name\":\"gitCloud-api\",\"email\":\"gitcloud@example.com\"}},\"content\":\"{content}\",\"sha\":\"{sha}\"}}"));
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

    public static async Task<DeleteContentClass> DelLakeContent(string path, string sha)
    {
        
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Delete, _lakeUrl+path);
        request.Content = new StringContent($"{{\"message\":\"ci: Delete {path}\",\"committer\":{{\"name\":\"gitCloud-api\",\"email\":\"gitcloud@example.com\"}},\"sha\":\"{sha}\"}}");
        HttpResponseMessage resp = await HttpClient.SendAsync(request);
        
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
    
    
    public static async Task<string> LakeGet(string path ,HttpContext ctx)
    {
        GetContentClass output = await GetLakeRequest(path);
        if (output.ErrorCode != null)
        {
            if (output.ErrorCode == 404)
            {
                ctx.Response.StatusCode = 404;
                return "Not Found";
            }
                
            ctx.Response.StatusCode = 500;
            if (output.ErrorCode < 0)
            {
                return output.ErrorMessage!;                
            }

            return string.Join("",
                "Github Get Request Error\nError Code: ", output.ErrorCode, "\nMessage: \n", output.ErrorMessage);
        }

        ctx.Response.StatusCode = 200;
        ctx.Response.ContentType = "application/json";
        return JsonSerializer.Serialize(output);
    }

    public static async Task<String> LakePut(string path, HttpContext ctx)
    {
        Task<GetContentClass> getLakeContentTask = GetLakeRequest(path);

        string bodyContent = await (new StreamReader(ctx.Request.Body, encoding: Encoding.UTF8)).ReadToEndAsync();

        GetContentClass lakeContent = await getLakeContentTask;

        PutContentClass output;
        if (lakeContent.ErrorCode != null)
        {
            ctx.Response.StatusCode = 500;
            if (lakeContent.ErrorCode < 0)
            {
                return lakeContent.ErrorMessage!;                
            }

            if (lakeContent.ErrorCode != 404)
            {
                return string.Join("",
                    "Github GET Request Error\nError Code: ", lakeContent.ErrorCode, "\nMessage: \n", lakeContent.ErrorMessage);
            }
            
            output = await PutLakeRequest(path, bodyContent);
            ctx.Response.StatusCode = 201;
        }
        else
        {
            output = await PutLakeRequest(path, bodyContent, lakeContent.Sha);
            ctx.Response.StatusCode = 200;
        }
        

        if (output.ErrorCode != null)
        {
            
            ctx.Response.StatusCode = 500;
            if (output.ErrorCode < 0)
            {
                return output.ErrorMessage!;                
            }

            return string.Join("",
                "Github PUT Request Error\nError Code: ", output.ErrorCode, "\nMessage: \n", output.ErrorMessage);
        }
        
        
        ctx.Response.ContentType = "application/json";
        return JsonSerializer.Serialize(output);
    }

    public static async Task<String> LakeDelete(string path, HttpContext ctx)
    {
        GetContentClass lakeContent = await GetLakeRequest(path);
        if (lakeContent.ErrorCode != null)
        {
            if (lakeContent.ErrorCode == 404)
            {
                ctx.Response.StatusCode = 404;
                return "Not Found";
            }
            
            ctx.Response.StatusCode = 500;
            if (lakeContent.ErrorCode < 0)
            {
                return lakeContent.ErrorMessage!;                
            }
            
            return string.Join("",
                "Github GET Request Error\nError Code: ", lakeContent.ErrorCode, "\nMessage: \n", lakeContent.ErrorMessage);
        }
        
        DeleteContentClass output = await DelLakeContent(path, lakeContent.Sha);
        
        if (output.ErrorCode != null)
        {
            ctx.Response.StatusCode = 500;
            if (lakeContent.ErrorCode < 0)
            {
                return lakeContent.ErrorMessage!;                
            }
            
            return string.Join("",
                "Github DEL Request Error\nError Code: ", lakeContent.ErrorCode, "\nMessage: \n", lakeContent.ErrorMessage);
        }
        
        ctx.Response.StatusCode = 200;
        ctx.Response.ContentType = "application/json";
        return JsonSerializer.Serialize(output);
    }
        
}