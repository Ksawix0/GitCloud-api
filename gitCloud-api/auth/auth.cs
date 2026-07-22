using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using ScottBrady.IdentityModel.Crypto;
using ScottBrady.IdentityModel.Tokens;

using static gitCloud_api.Db;

namespace gitCloud_api;

public static partial class Auth
{

    private static readonly ILogger Logger = LoggerFactory.Create(builder => builder.AddConsole() ).CreateLogger(typeof(Auth));

    private static EdDsa _keys = null!;

    private static JwtConfigClass _jwtConfig = new JwtConfigClass();
    

    public static void Init(EdDsa keys, JwtConfigClass jwtConfig)
    {
          _keys = keys;
          _jwtConfig = jwtConfig;
    }
    
    public static void MapGitCloudAuthEndPoints(this RouteGroupBuilder builder)
    {
        builder.MapPost("/login", GitCloudLogin);
        builder.MapGet("/authTest", AuthTest).RequireAuthorization();
    }

    public static async Task GitCloudLogin(HttpContext context)
    {
        // Logger.LogInformation(await (new StreamReader(context.Request.Body, encoding: Encoding.UTF8)).ReadToEndAsync());
        string bodyContent = await (new StreamReader(context.Request.Body, encoding: Encoding.UTF8)).ReadToEndAsync();

        AuthLoginRequest loginRequest = JsonSerializer.Deserialize<AuthLoginRequest>(bodyContent) ?? new AuthLoginRequest();

        if (loginRequest.PasswdHash == String.Empty || loginRequest.Username == String.Empty)
        {
            byte[] message = Encoding.UTF8.GetBytes("Missing Credentials");
            context.Response.StatusCode = 401;
            await context.Response.Body.WriteAsync(new ReadOnlyMemory<byte>(message));
            return;
        }
        
        IEnumerable<GitCloudUser> match = from User in GitCloudDb.Users
            where User.Name == loginRequest.Username
            select User;

        match = match.ToArray();
        
        if (!match.Any() || loginRequest.PasswdHash != match.First().PasswdHash)
        {
            byte[] message = Encoding.UTF8.GetBytes("Invalid Credentials");
            context.Response.StatusCode = 401;
            await context.Response.Body.WriteAsync(new ReadOnlyMemory<byte>(message));
            return;
        }
        
        GitCloudUser  user = match.First();
        
        //? gen refresh Token
        JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
        byte[] privateKeyBytes = Encoding.UTF8.GetBytes(_jwtConfig.PrivateKey);
        SecurityTokenDescriptor tokenDescriptor = new SecurityTokenDescriptor
        {
            Audience = _jwtConfig.Audience,
            Expires = DateTime.UtcNow.AddDays(_jwtConfig.RefreshTokenExpirationTimeInDays),
            Issuer = _jwtConfig.Issuer,
            TokenType = "refresh",
            Subject = new ClaimsIdentity([
                new Claim(ClaimTypes.Name,user.Guid.ToString()),
                new Claim(ClaimTypes.GivenName, user.Name)
            ]),
            Claims = new Dictionary<string, object>()
            {
                { "Admin", user.Admin }
            },
            SigningCredentials = new SigningCredentials(new EdDsaSecurityKey(_keys), ExtendedSecurityAlgorithms.EdDsa)
        };
        SecurityToken token = tokenHandler.CreateToken(tokenDescriptor);
        //?
        
        context.Response.Cookies.Append("RefreshToken", tokenHandler.WriteToken(token), new CookieOptions
        {
            SameSite = SameSiteMode.Strict,
            HttpOnly = true,
            MaxAge = TimeSpan.FromDays(_jwtConfig.RefreshTokenExpirationTimeInDays),
        });
        
        //? gen access Token
        tokenDescriptor = new SecurityTokenDescriptor
        {
            Audience = _jwtConfig.Audience,
            Expires = DateTime.UtcNow.AddMinutes(_jwtConfig.AccessTokenExpirationTimeInMinutes),
            Issuer = _jwtConfig.Issuer,
            TokenType = "access",
            Subject = new ClaimsIdentity([
                new Claim(ClaimTypes.Name,user.Guid.ToString()),
                new Claim(ClaimTypes.GivenName, user.Name)
            ]),
            Claims = new Dictionary<string, object>()
            {
                { "Admin", user.Admin }
            },
            SigningCredentials = new SigningCredentials(new EdDsaSecurityKey(_keys), ExtendedSecurityAlgorithms.EdDsa),
        };
        token = tokenHandler.CreateToken(tokenDescriptor);
        //?
        
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(string.Join("", "{\"accessToken\":\"", tokenHandler.WriteToken(token), "\"}"));
    }

    public static async Task AuthTest(HttpContext context)
    {
        await context.Response.WriteAsync("Nice");
    }
}