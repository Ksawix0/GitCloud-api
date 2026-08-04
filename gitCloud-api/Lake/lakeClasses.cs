using System.Text.Json.Serialization;

namespace gitCloud_api;

public static partial class Lake
{
    public interface IResponseContent
    {
        [JsonIgnore]
        public short? ErrorCode { get; set; }
        
        [JsonIgnore]
        public string? ErrorMessage { get; set; }
    }
    
    //? get
    public class GetContentClass: IResponseContent
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
        public GetContentClass[]? Entries { get; set; }
        
        [JsonPropertyName("_links")]
        public LinksClass Links { get; set; }
        
        [JsonIgnore]
        public short? ErrorCode { get; set; }
        
        [JsonIgnore]
        public string? ErrorMessage { get; set; }
    }
    
    //? put
    public class PutContentClass: IResponseContent
    {

        [JsonPropertyName("content")]
        [JsonRequired]
        public ContentContentClass Content { get; set; }
        
        [JsonPropertyName("commit")]
        [JsonRequired]
        public CommitClass Commit { get; set; }
        
        [JsonIgnore]
        public short? ErrorCode { get; set; }
        
        [JsonIgnore]
        public string? ErrorMessage { get; set; }
    }
    
    //? del
    public class DeleteContentClass: IResponseContent
    {
        [JsonPropertyName("content")]
        public ContentContentClass? Content { get; set; }
        
        [JsonPropertyName("commit")]
        public CommitClass Commit { get; set; } = null!;
        
        [JsonIgnore]
        public short? ErrorCode { get; set; }
        
        [JsonIgnore]
        public string? ErrorMessage { get; set; }
    }
    
    public class LinksClass
    {
        [JsonPropertyName("git")]
        public string Git { get; set; }

        [JsonPropertyName("html")]
        public string Html { get; set; }

        [JsonPropertyName("self")]
        public string Self { get; set; }
    }
    
    public class ContentContentClass
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
        public LinksClass Links { get; set; }
    }

    public class CommitClass
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
        public CommitUserClass? Author { get; set; }

        [JsonPropertyName("committer")]
        public CommitUserClass? Committer { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("tree")]
        public CommitTreeClass? Tree { get; set; }

        [JsonPropertyName("parents")]
        public List<ParentClass>? Parents { get; set; }

        [JsonPropertyName("verification")]
        public VerificationClass? Verification { get; set; }
    }

    public class CommitUserClass
    {
        [JsonPropertyName("date")]
        public string? Date { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("email")]
        public string Email { get; set; }
    }

    public class CommitTreeClass
    {
        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("sha")]
        public string Sha { get; set; }
    }

    public class ParentClass
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("sha")]
        public string? Sha { get; set; }
    }

    public class VerificationClass
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

    public class LakeGetResponseClass
    {
        [JsonPropertyName("type")]
        public required string Type { get; set; }
        [JsonPropertyName("name")]
        public required string Name { get; set; }
        [JsonPropertyName("Path")] 
        public required string Path { get; set; }
        [JsonPropertyName("content")]
        public string? Content { get; set; }
        [JsonPropertyName("entities")]
        public required LakeGetResponseClass[]? Entities { get; set; }
    }
    
    public enum LakeErrorCodes
    {
        InternalErrorWhileParsingJson = -1
    }
}
