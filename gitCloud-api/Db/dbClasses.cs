namespace gitCloud_api;

public partial class Db
{
    public static class GitCloudDb
    {
        public static List<GitCloudUser> Users = new List<GitCloudUser>(10);
        public static List<GitCloudRefreshToken> RefreshTokens = new List<GitCloudRefreshToken>(100); 
    }

    [method: SetsRequiredMembers]
    public class GitCloudUser()
    {
        public required Guid Guid = Guid.Empty;
        public required string Name = "";
        public required string PasswdHash = "";
        public required string Role = "";
    }
    
    public static class GitCloudDbFilePaths
    {
        public static string UserFile { get;} = "/.gcpasswd";
    }
}