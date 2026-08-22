using System.Text.Json.Serialization;

namespace gitCloud_api;

public static partial class Lake
{
    //? REST api
    public interface IResponseRest
    {
        [JsonIgnore]
        public short? ErrorCode { get; set; }
        
        [JsonIgnore]
        public string? ErrorMessage { get; set; }
    }
    
    //? get
    public class RestGetClass: IResponseRest
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }
        
        [JsonPropertyName("encoding")]
        public string Encoding { get; set; }

        [JsonPropertyName("size")]
        public int? Size { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("path")]
        public string Path { get; set; }
        
        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("sha")]
        public string Sha { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("git_url")]
        public string GitUrl { get; set; }

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; }

        [JsonPropertyName("download_url")]
        public string? DownloadUrl { get; set; }

        [JsonPropertyName("entries")]
        public RestGetClass[]? Entries { get; set; }
        
        [JsonPropertyName("_links")]
        public RestLinksClass RestLinks { get; set; }
        
        [JsonIgnore]
        public short? ErrorCode { get; set; }
        
        [JsonIgnore]
        public string? ErrorMessage { get; set; }
    }
    
    //? put
    public class RestPutClass: IResponseRest
    {

        [JsonPropertyName("content")]
        [JsonRequired]
        public RestContentClass RestContent { get; set; }
        
        [JsonPropertyName("commit")]
        [JsonRequired]
        public RestCommitClass RestCommit { get; set; }
        
        [JsonIgnore]
        public short? ErrorCode { get; set; }
        
        [JsonIgnore]
        public string? ErrorMessage { get; set; }
    }
    
    //? del
    public class RestDeleteClass: IResponseRest
    {
        [JsonPropertyName("content")]
        public RestContentClass? Content { get; set; }
        
        [JsonPropertyName("commit")]
        public RestCommitClass RestCommit { get; set; } = null!;
        
        [JsonIgnore]
        public short? ErrorCode { get; set; }
        
        [JsonIgnore]
        public string? ErrorMessage { get; set; }
    }
    
    public class RestLinksClass
    {
        [JsonPropertyName("git")]
        public string Git { get; set; }

        [JsonPropertyName("html")]
        public string Html { get; set; }

        [JsonPropertyName("self")]
        public string Self { get; set; }
    }
    
    public class RestContentClass
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }
        
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("path")]
        public string Path { get; set; }

        [JsonPropertyName("sha")]
        public string Sha { get; set; }

        [JsonPropertyName("size")]
        public int Size { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; }

        [JsonPropertyName("git_url")]
        public string GitUrl { get; set; }

        [JsonPropertyName("download_url")]
        public string? DownloadUrl { get; set; }

        [JsonPropertyName("_links")]
        public RestLinksClass RestLinks { get; set; }
    }

    public class RestCommitClass
    {
        [JsonPropertyName("sha")]
        public string? Sha { get; set; }

        [JsonPropertyName("node_id")]
        public string? NodeId { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("author")]
        public RestCommitUserClass? Author { get; set; }

        [JsonPropertyName("committer")]
        public RestCommitUserClass? Committer { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("tree")]
        public RestCommitTreeClass? Tree { get; set; }

        [JsonPropertyName("parents")]
        public List<RestParentClass>? Parents { get; set; }

        [JsonPropertyName("verification")]
        public RestVerificationClass? Verification { get; set; }
    }

    public class RestCommitUserClass
    {
        [JsonPropertyName("date")]
        public string? Date { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("email")]
        public string Email { get; set; }
    }

    public class RestCommitTreeClass
    {
        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("sha")]
        public string Sha { get; set; }
    }

    public class RestParentClass
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("sha")]
        public string? Sha { get; set; }
    }

    public class RestVerificationClass
    {
        [JsonPropertyName("verified")]
        public bool Verified { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }

        [JsonPropertyName("signature")]
        public string? Signature { get; set; }

        [JsonPropertyName("payload")]
        public string? Payload { get; set; }

        [JsonPropertyName("verified_at")]
        public string? VerifiedAt { get; set; }
    }
    
    //? graphQl api
    public class GraphQlGetClass
    {
        
        
        [JsonPropertyName("data")]
        public required GraphQlData Data { get; set; }
    }
    
    public class GraphQlData
    {
        [JsonPropertyName("repository")]
        public GraphQlRepository? Repository { get; set; }
    }
    
    public new class GraphQlRepository
    {
        [JsonPropertyName("object")]
        public GraphQlObject? Object { get; set; }
    }
    
    public class GraphQlObject{
        [JsonPropertyName("__typename")]
        public required string TypeName { get; set;}
        //? Blob
        [JsonPropertyName("byteSize")]
        public long? ByteSize { get; set;}
        [JsonPropertyName("oid")]
        public string? Oid { get; set; }
        //? Tree
        [JsonPropertyName("entries")]
        public GraphQlEntry[]? Entries { get; set; }
    }

    public class GraphQlEntry
    {
        [JsonPropertyName("name")]
        public required string Name { get; set; }
        [JsonPropertyName("object")]
        public required GraphQlObject Object { get; set; }
    }

    public class GitCloudGetResponseClass
    {
        [JsonPropertyName("type")]
        public required string Type { get; set; }
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        [JsonPropertyName("byteSize")]
        public long? ByteSize { get; set; }
        [JsonPropertyName("entities")]
        public GitCloudGetResponseClass[]? Entities { get; set; }
    }
    
    public enum LakeErrorCodes
    {
        InternalErrorWhileParsingJson = -1
    }
}
