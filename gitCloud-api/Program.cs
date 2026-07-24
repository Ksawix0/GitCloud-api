using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using ScottBrady.IdentityModel.Crypto;
using ScottBrady.IdentityModel.Tokens;

namespace gitCloud_api;

public static class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

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
        
        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseHttpsRedirection();
        
        GitCloud.Init(builder.Configuration.GetSection("GitCloudConfiguration"), jwtConfig, keys, validationParameters);
        app.MapGroup("/lake/").MapGitCloudLakeEndpoints();
        app.MapGroup("/").MapGitCloudAuthEndPoints();
        
        

        app.Run();
    }
}