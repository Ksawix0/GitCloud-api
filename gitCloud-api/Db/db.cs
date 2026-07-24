using System.Text;
using static gitCloud_api.Lake;

namespace gitCloud_api;

public static partial class Db
{
    
    // public static GitCloudDbClass GitCloudDb = new GitCloudDbClass(){Users = []};
    
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
            PutContentClass putResponse = PutLakeRequest(GitCloudDbFilePaths.UserFile,Convert.ToBase64String(Encoding.UTF8.GetBytes(GitCloudDbSerializer.FileSerializer(GitCloudDb.Users)))).Result;
            if (putResponse.ErrorCode != null)
            {
                throw new Exception($"\nPut error while initializing .gcpasswd file\nError Code: {(short)getResponse.ErrorCode}\nMessage:\n{getResponse.ErrorMessage}");
            }
            Logger.LogInformation("Initializing .gcpasswd completed. Created default user 'admin' with password 'admin'");
            return;
        }

        GitCloudDb.Users = GitCloudDbSerializer.FileDeserializer<GitCloudUser>(Encoding.UTF8.GetString(Convert.FromBase64String(getResponse.Content)));
        
        Logger.LogInformation("Loaded {UsersCount} users", GitCloudDb.Users.Count);
    }

    private static class GitCloudDbSerializer 
    {
        
        public static string FileSerializer<T>(List<T> objList) where T : class
        {
            StringBuilder serializedUsers = new StringBuilder();
            foreach(object obj in objList)
            {
                FieldInfo[] fields = obj.GetType().GetFields(BindingFlags.Public |  BindingFlags.Instance);
                List<string> values = new List<string>();
                foreach (FieldInfo field in fields)
                {
                    if (field.Name.Contains("<")) { continue; }

                    string? value = field.GetValue(obj)?.ToString();
                    if(value == null){ continue; }
                    values.Add(value);
                }
                serializedUsers.Append(string.Join(":",values));
                serializedUsers.Append('\n');
            }
            
            serializedUsers.Remove(serializedUsers.Length - 1, 1);
            return serializedUsers.ToString();
        }

        public static List<T> FileDeserializer<T>(string data) where T : class, new()
        {
            List<T> outputList = new List<T>();
            
            foreach (string line in data.Split("\n"))
            {
                FieldInfo[] fields = typeof(T).GetFields();
                
                string[] tmp = line.Split(':');
                if (tmp.Length != fields.Length)
                {
                    Console.WriteLine($"Entry for {typeof(T).Name} is corrupted skipping");
                    continue;
                }

                T outObj = new T();
                for (int i = 0; i < fields.Length; i++)
                {
                    switch (fields[i].FieldType.Name)
                    {
                        case "Guid":
                            Guid guid = Guid.Empty;
                            if (!Guid.TryParse(tmp[i], out guid))
                            {
                                Console.WriteLine($"Entry for {typeof(T).Name} is corrupted skipping");
                                continue;
                            }
                            fields[i].SetValue(outObj, guid);
                            break;
                        default:
                            fields[i].SetValue(outObj, tmp[i]);
                            break;
                    }
                }
                outputList.Add(outObj);
            }
            return outputList;
        }
    }
    
    
}