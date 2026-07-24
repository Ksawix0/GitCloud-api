using Microsoft.IdentityModel.Tokens;
using ScottBrady.IdentityModel.Tokens;


namespace gitCloud_api;

public static class GitCloud
{
    private static readonly ILogger Logger = LoggerFactory.Create(builder => builder.AddConsole() ).CreateLogger(typeof(GitCloud));


    public static void Init(IConfigurationSection gitCloudConfigurationSection,  IConfigurationSection jwtConfigurationSection, EdDsa keys, TokenValidationParameters validationParameters)
    {
        GitCloudConfigClass gitCloudConfig = new GitCloudConfigClass();
        JwtConfigClass jwtConfig = new JwtConfigClass();
        
        gitCloudConfig.Token = gitCloudConfigurationSection["GitHubToken"] ?? throw new Exception("GitHubToken not found");
        gitCloudConfig.RepositoryName = gitCloudConfigurationSection["RepositoryName"] ?? throw new Exception("RepositoryName not found");
        gitCloudConfig.UserName = gitCloudConfigurationSection["UserName"] ?? throw new Exception("UserName not found");

        jwtConfig.PublicKey = jwtConfigurationSection["PublicKeyBase64"] ?? throw new Exception("PublicKey not found");
        jwtConfig.PrivateKey = jwtConfigurationSection["PrivateKeyBase64"] ?? throw new Exception("PrivateKey not found");
        jwtConfig.Issuer = jwtConfigurationSection["Issuer"] ?? throw new Exception("Issuer not found");
        jwtConfig.Audience = jwtConfigurationSection["Audience"] ?? throw new Exception("Audience not found");
        jwtConfig.AccessTokenExpirationTimeInMinutes = int.Parse(jwtConfigurationSection["AccessTokenExpirationTimeInMinutes"] ?? throw new Exception("AccessTokenExpirationTimeInMinutes not found"));
        jwtConfig.RefreshTokenExpirationTimeInDays = int.Parse(jwtConfigurationSection["RefreshTokenExpirationTimeInDays"] ?? throw new Exception("RefreshTokenExpirationTimeInDays not found"));


        Lake.InitLake(gitCloudConfig.Token, gitCloudConfig.RepositoryName, gitCloudConfig.UserName);
        Auth.Init(keys, jwtConfig, validationParameters);
        Db.Init();
    }

    

}
