using System.Text;
using static gitCloud_api.Lake;

namespace gitCloud_api;

public static partial class Db
{
    
    // public static GitCloudDbClass GitCloudDb = new GitCloudDbClass(){Users = []};
    
    private static readonly ILogger Logger = LoggerFactory.Create(builder => builder.AddConsole() ).CreateLogger(typeof(Db));

    public static void Init()
    {
        //? User file init
        GetContentClass getResponse = GetLakeRequest(GitCloudDbFilePaths.UserFile).Result;
        if (getResponse.ErrorCode != null)
        {
            if (getResponse.ErrorCode != 404)
            {
                throw new Exception($"\nFetch get error while initializing db\nError Code: {(short)getResponse.ErrorCode}\nMessage:\n{getResponse.ErrorMessage}");
            }
            
            Logger.LogInformation("Didn't found GitCloud .gcpasswd file. Initializing db..");

            GitCloudDb.Users.Add(new GitCloudUser(){  Guid = Guid.Parse("019f862b-4c5f-798e-8141-210243f2a36b"), Name = "admin", PasswdHash = "$argon2id$v=19$m=4096,t=3,p=1$c29tZXNhbHQ$EFS0W4ghMqXsLkLqtn5kweHhBGOnwYrd/3YX/i0x4Dc" , Role = "Admin"});
            PutContentClass putResponse = PutLakeRequest(GitCloudDbFilePaths.UserFile,Convert.ToBase64String(Encoding.UTF8.GetBytes(GitCloudDbSerializer.FileSerializer(GitCloudDb.Users)))).Result;
            if (putResponse.ErrorCode != null)
            {
                throw new Exception($"\nPut error while initializing .gcpasswd file\nError Code: {(short)getResponse.ErrorCode}\nMessage:\n{getResponse.ErrorMessage}");
            }
            Logger.LogInformation("Initializing .gcpasswd completed. Created default user 'admin' with password 'admin'");
        }else{
            GitCloudDb.Users = GitCloudDbSerializer.FileDeserializer<GitCloudUser>(Encoding.UTF8.GetString(Convert.FromBase64String(getResponse.Content)).AsSpan() );
        }
        
        Logger.LogInformation("Loaded {UsersCount} users", GitCloudDb.Users.Count);

        
        //? Token file init
        getResponse = GetLakeRequest(GitCloudDbFilePaths.RefreshTokenFile).Result;
        if (getResponse.ErrorCode != null)
        {
            if (getResponse.ErrorCode != 404)
            {
                throw new Exception($"\nFetch get error while initializing db\nError Code: {(short)getResponse.ErrorCode}\nMessage:\n{getResponse.ErrorMessage}");
            }
            
            Logger.LogInformation("Didn't found GitCloud .gcrefreshtokens file. Initializing db..");

            PutContentClass putResponse = PutLakeRequest(GitCloudDbFilePaths.RefreshTokenFile,Convert.ToBase64String(Encoding.UTF8.GetBytes(GitCloudDbSerializer.FileSerializer(GitCloudDb.RefreshTokens)))).Result;
            if (putResponse.ErrorCode != null)
            {
                throw new Exception($"\nPut error while initializing .gcrefreshtokens file\nError Code: {(short)getResponse.ErrorCode}\nMessage:\n{getResponse.ErrorMessage}");
            }
            Logger.LogInformation("Initializing .gcrefreshtokens completed.");
        }else{
            GitCloudDb.RefreshTokens = GitCloudDbSerializer.FileDeserializer<GitCloudRefreshToken>(Encoding.UTF8.GetString(Convert.FromBase64String(getResponse.Content)).AsSpan() );
        }
        
        Logger.LogInformation("Loaded {TokenCount} tokens", GitCloudDb.RefreshTokens.Count);
    }

    
}