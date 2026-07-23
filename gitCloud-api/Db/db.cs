using System.Text;
using static gitCloud_api.Lake;

namespace gitCloud_api;

public static partial class Db
{
    
    public static GitCloudDbClass GitCloudDb = new GitCloudDbClass(){Users = []};
    
    private static readonly ILogger Logger = LoggerFactory.Create(builder => builder.AddConsole() ).CreateLogger(typeof(Db));
    

    public static void Init()
    {
        
        GetContentClass getResponse = GetLakeRequest(GitCloudDbFilePaths.UserFile).Result;
        if (getResponse.ErrorCode != null)
        {
            if (getResponse.ErrorCode != 404)
            {
                throw new Exception($"\nFetch get error while initializing db\nError Code: {(short)getResponse.ErrorCode}\nMessage:\n{getResponse.ErrorMessage}");
            }
            
            Logger.LogInformation("Didn't found GitCloud .gcpasswd file. Initializing db..");

            GitCloudDb.Users.Add(new GitCloudUser(){  Guid = Guid.Parse("019f862b-4c5f-798e-8141-210243f2a36b"), Name = "admin", PasswdHash = "$argon2id$v=19$m=4096,t=3,p=1$c29tZXNhbHQ$EFS0W4ghMqXsLkLqtn5kweHhBGOnwYrd/3YX/i0x4Dc" , Role = "Admin"});
            PutContentClass putResponse = PutLakeRequest(GitCloudDbFilePaths.UserFile,Convert.ToBase64String(Encoding.UTF8.GetBytes(LakeDbSerializer.UserFileSerializer(GitCloudDb)))).Result;
            if (putResponse.ErrorCode != null)
            {
                throw new Exception($"\nPut error while initializing .gcpasswd file\nError Code: {(short)getResponse.ErrorCode}\nMessage:\n{getResponse.ErrorMessage}");
            }
            Logger.LogInformation("Initializing .gcpasswd completed. Created default user 'admin' with password 'admin'");
            return;
        }

        GitCloudDb.Users = LakeDbSerializer.UserFileDeserializer(Encoding.UTF8.GetString(Convert.FromBase64String(getResponse.Content)));
        
        Logger.LogInformation("Loaded {UsersCount} users", GitCloudDb.Users.Count);
    }

    private static class LakeDbSerializer
    {
        
        public static string UserFileSerializer(GitCloudDbClass db)
        {
            StringBuilder serializedUsers = new StringBuilder();
            
            foreach (GitCloudUser user in db.Users)
            {
                serializedUsers.Append(string.Join(":",user.Guid, user.Name, user.PasswdHash, user.Role));
                serializedUsers.Append("\n");
            }
            
            serializedUsers.Remove(serializedUsers.Length - 1, 1);
            return serializedUsers.ToString();
        }

        public static List<GitCloudUser> UserFileDeserializer(string data)
        {
            List<GitCloudUser> users = [];
            foreach (string line in data.Split("\n"))
            {
                string[] tmp = line.Split(':');
                if (tmp.Length != 4)
                {
                    Console.WriteLine("User entry is corrupted skipping");
                    continue;
                }

                Guid userGuid;
                if (!Guid.TryParse(tmp[0], out userGuid))
                {
                    Console.WriteLine("User entry is corrupted skipping");
                    continue;
                }
                users.Add(new GitCloudUser(){Guid = userGuid, Name = tmp[1], PasswdHash = tmp[2], Role = tmp[3] });
            }

            return users;
        }
    }
    
    
}