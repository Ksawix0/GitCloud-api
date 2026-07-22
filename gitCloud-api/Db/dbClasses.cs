namespace gitCloud_api;

public partial class Db
{
    public class GitCloudDbClass
    {
        public required List<GitCloudUser> Users { get; set;}
    }

    public class GitCloudUser
    {
        public required Guid Guid { get; set; }
        public required string Name { get; set; }
        public required string PasswdHash { get; set; }
        public required bool Admin {get; set;}
    }
}