namespace gitCloud_api;

public class GitCloudConfigClass
{
    public string Token = "";
    public string RepositoryName = "";
    public string UserName = "";
}

public  class JwtConfigClass
{
    public string PublicKey = "";
    public string PrivateKey = "";
    public string Issuer = "";
    public string Audience = "";
    public int RefreshTokenExpirationTimeInDays = 0;
    public int AccessTokenExpirationTimeInMinutes = 0;
}
