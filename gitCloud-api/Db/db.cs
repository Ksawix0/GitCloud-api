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
        GraphQlGetClass getResponse = GetLakeInfo(GitCloudDbFilePaths.UserFile, CancellationToken.None).Result;
        if (getResponse.StatusCode != 200)
        {
            if (getResponse.StatusCode != 404)
            {
                throw new Exception($"\nFetch get error while initializing db\nError Code: {(short)getResponse.StatusCode}");
            }
            
            Logger.LogInformation("Didn't found GitCloud .gcpasswd file. Initializing db..");

            GitCloudDb.Users.Add(new GitCloudUser(){  Guid = Guid.Parse("019f862b-4c5f-798e-8141-210243f2a36b"), Name = "admin", PasswdHash = "$argon2id$v=19$m=4096,t=3,p=1$c29tZXNhbHQ$EFS0W4ghMqXsLkLqtn5kweHhBGOnwYrd/3YX/i0x4Dc" , Role = "Admin"});
            
            MemoryStream usersStream = new MemoryStream(Encoding.UTF8.GetBytes(Convert.ToBase64String(Encoding.UTF8.GetBytes(GitCloudDbSerializer.FileSerializer(GitCloudDb.Users)))));
            RestPutClass restPutResponse = PutLakeRequest(GitCloudDbFilePaths.UserFile,usersStream, usersStream.Length, CancellationToken.None).Result;
            usersStream.Dispose();
            if (restPutResponse.StatusCode != null)
            {
                throw new Exception($"\nPut error while initializing .gcpasswd file\nError Code: {(short)getResponse.StatusCode}");
            }
            Logger.LogInformation("Initializing .gcpasswd completed. Created default user 'admin' with password 'admin'");
        }else
        {
            MemoryStream usersStream = new MemoryStream();
            RestGetRawDataInfo dataInfo = GetLakeRawDataBlob(GitCloudDbFilePaths.UserFile, usersStream, CancellationToken.None).Result;
            if (dataInfo.StatusCode != 200)
            {
                throw new Exception($"\nPut error while initializing .gcpasswd file\nError Code: {(short)getResponse.StatusCode}");
            }

            usersStream.Position = 0;
            GitCloudDb.Users = GitCloudDbSerializer.FileDeserializer<GitCloudUser>(new StreamReader(usersStream).ReadToEnd());
            usersStream.Dispose();
        }
        
        Logger.LogInformation("Loaded {UsersCount} users", GitCloudDb.Users.Count);

        
        //? Token file init
        getResponse = GetLakeInfo(GitCloudDbFilePaths.UserFile, CancellationToken.None).Result;
        if (getResponse.StatusCode != 200)
        {
            if (getResponse.StatusCode != 404)
            {
                throw new Exception($"\nFetch get error while initializing db\nError Code: {(short)getResponse.StatusCode}");
            }
            
            Logger.LogInformation("Didn't found GitCloud .gcrefreshtokens file. Initializing db..");
            
            MemoryStream tokenStream = new MemoryStream(Encoding.UTF8.GetBytes(Convert.ToBase64String(Encoding.UTF8.GetBytes(GitCloudDbSerializer.FileSerializer(GitCloudDb.RefreshTokens)))));
            RestPutClass restPutResponse = PutLakeRequest(GitCloudDbFilePaths.RefreshTokenFile, tokenStream, tokenStream.Length, CancellationToken.None).Result;
            tokenStream.Dispose();
            if (restPutResponse.StatusCode != null)
            {
                throw new Exception($"\nPut error while initializing .gcrefreshtokens file\nError Code: {(short)getResponse.StatusCode}");
            }
            Logger.LogInformation("Initializing .gcrefreshtokens completed.");
        }else{
            MemoryStream tokenStream = new MemoryStream();
            RestGetRawDataInfo dataInfo = GetLakeRawDataBlob(GitCloudDbFilePaths.RefreshTokenFile, tokenStream, CancellationToken.None).Result;
            if (dataInfo.StatusCode != 200)
            {
                throw new Exception($"\nPut error while initializing .gcrefreshtokens file\nError Code: {(short)getResponse.StatusCode}");
            }
            tokenStream.Position = 0;
            GitCloudDb.RefreshTokens = GitCloudDbSerializer.FileDeserializer<GitCloudRefreshToken>(new StreamReader(tokenStream).ReadToEnd());
            tokenStream.Dispose();
        }
        
        Logger.LogInformation("Loaded {TokenCount} tokens", GitCloudDb.RefreshTokens.Count);
    }

    
}