using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;

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
    
    [method: SetsRequiredMembers]
    public class GitCloudRefreshToken()
    {
        public required Guid TokenId = Guid.Empty;
        public required Guid? FamilyId = Guid.Empty;
        public required DateTime ExpiresAt = DateTime.MinValue;
    }
    
    private static class GitCloudDbFilePaths
    {
        public static string UserFile { get;} = "/.gcpasswd";
        public static string RefreshTokenFile { get;} = "/.gctokens";
    }
    
    private static class GitCloudDbSerializer 
    {
        
        public static string FileSerializer<T>(List<T> objList) where T : class
        {
            StringBuilder serializedUsers = new StringBuilder("§\n");
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

            if (serializedUsers.Length >= 2)
            {
                serializedUsers.Remove(serializedUsers.Length - 1, 1);
            }
            
            return serializedUsers.ToString();
        }

        public static List<T> FileDeserializer<T>(ReadOnlySpan<char> data) where T : class, new()
        {
            if (data.Length is <= 1 or <= 2) { return new List<T>();}
            data = data[1..];
            if (data[0] == '\n') { data = data[1..];}
            List<T> outputList = new List<T>();
            FieldInfo[] fields = typeof(T).GetFields();
            T outObj = new T();
            
            foreach (Range line in data.Split("\n"))
            {

                uint count = 0;
                foreach (Range tmp in data[line].Split(':'))
                {
                    if (count > fields.Length)
                    {
                        Console.WriteLine($"Entry for {typeof(T).Name} is corrupted skipping");
                        break;
                    }
                    
                    switch (fields[count].FieldType.Name)
                    {
                        case "Guid":
                            Guid guid = Guid.Empty;
                            if (!Guid.TryParse(data[line][tmp].ToString(), out guid))
                            {
                                Console.WriteLine($"Entry for {typeof(T).Name} is corrupted skipping");
                                break;
                            }
                            fields[count].SetValue(outObj, guid);
                            break;
                        default:
                            fields[count].SetValue(outObj, data[line][tmp].ToString());
                            break;
                    }
                    count++;
                }

                if (count != fields.Length)
                {
                    Console.WriteLine($"Entry for {typeof(T).Name} is corrupted skipping");
                    continue;
                }
                outputList.Add(outObj);
                    
            }
            return outputList;
        }
    }

}