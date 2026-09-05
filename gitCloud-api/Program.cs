using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using ScottBrady.IdentityModel.Crypto;
using ScottBrady.IdentityModel.Tokens;
using static gitCloud_api.Db;

namespace gitCloud_api;

public static class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        //? jwt
        IConfigurationSection jwtConfig = builder.Configuration.GetSection("JwtConfiguration");
        string privateKey = jwtConfig["PrivateKeyBase64"] ?? throw new Exception("JWT public key not found");
        string publicKey = jwtConfig["PublicKeyBase64"] ?? throw new Exception("JWT public key not found");
        EdDsa keys = EdDsa.Create(new EdDsaParameters(ExtendedSecurityAlgorithms.Curves.Ed25519)
        {
            D = Convert.FromBase64String(privateKey),
            X = Convert.FromBase64String(publicKey)
        });
        
        //? validation params
        TokenValidationParameters validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtConfig["Issuer"],
            ValidAudience = jwtConfig["Audience"],
            IssuerSigningKey = new EdDsaSecurityKey(keys),
            ClockSkew = TimeSpan.Zero
        };
        
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(options => options.TokenValidationParameters = validationParameters);

        builder.Services.AddAuthorization(options =>
        {
            options.DefaultPolicy = new AuthorizationPolicyBuilder().RequireClaim("type", ["access"]).Build();
        });
        //?

        builder.Services.AddHostedService<Db.DbSync>();
        builder.Services.AddSingleton<LakeCache>(_ => new LakeCache(builder.Configuration.GetValue<int>("GitCloudConfiguration:CacheQueueCapacity")));
        builder.Services.AddSingleton<LakeModifyQueue>(_ => new LakeModifyQueue(builder.Configuration.GetValue<int>("GitCloudConfiguration:ModifyQueueCapacity")));
        builder.Services.AddHostedService<LakeCacheBackgroundService>();
        builder.Services.AddHostedService<LakeModifyBackgroundService>();
        
        builder.Services.AddCors(options =>
            options.AddPolicy(name: "gitCloudSite",
                policy => { policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()?? [])
                    .AllowAnyMethod().AllowAnyHeader().Build(); }));
        
        var app = builder.Build();

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseHttpsRedirection();

        app.UseCors("gitCloudSite");
        
        GitCloud.Init(builder.Configuration.GetSection("GitCloudConfiguration"), jwtConfig, keys, validationParameters);
        app.MapGroup("/lake/").MapGitCloudLakeEndpoints();
        app.MapGroup("/").MapGitCloudAuthEndPoints();

        app.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopped.Register(() =>
        {
            ILogger logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger(typeof(Db));
            logger.LogInformation("Started shutdown db upload");
            DbSync.UploadUsersUpstream(CancellationToken.None).Wait();
            DbSync.UploadTokensUpstream(CancellationToken.None).Wait();
            logger.LogInformation("Db uploaded upstream");
        });

        app.Run();
    }
}