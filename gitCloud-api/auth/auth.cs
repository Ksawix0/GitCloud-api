using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using ScottBrady.IdentityModel.Tokens;
using static gitCloud_api.Db;

namespace gitCloud_api;

public static partial class Auth
{
    private static readonly ILogger Logger = LoggerFactory.Create(builder => builder.AddConsole() ).CreateLogger(typeof(Auth));

    private static EdDsa _keys = null!;
    private static JwtConfigClass _jwtConfig = new JwtConfigClass();
    private static TokenValidationParameters _validationParameters = new();
    

    public static void Init(EdDsa keys, JwtConfigClass jwtConfig, TokenValidationParameters validationParameters)
    {
          _keys = keys;
          _jwtConfig = jwtConfig;
          _validationParameters = validationParameters;
    }
    
    public static void MapGitCloudAuthEndPoints(this RouteGroupBuilder builder)
    {
        builder.MapPost("/login", GitCloudLogin);
        builder.MapGet("/refreshtokens", GitCloudRefreshTokens);
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
        Guid refreshTokenGuid = Guid.CreateVersion7();
        JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
        GitCloudRefreshToken gitCloudTokenHandler = new GitCloudRefreshToken()
        {
            TokenId = refreshTokenGuid, 
            FamilyId = refreshTokenGuid, 
            UserId = user.Guid, 
            GivenName = user.Name,
            Role = user.Role
        };
        
        SecurityTokenDescriptor tokenDescriptor = gitCloudTokenHandler.ExportRefreshTokenDescriptor();
        SecurityToken token = tokenHandler.CreateToken(tokenDescriptor);
        
        context.Response.Cookies.Append("RefreshToken", tokenHandler.WriteToken(token), new CookieOptions
        {
            SameSite = SameSiteMode.Strict,
            HttpOnly = true,
            MaxAge = TimeSpan.FromDays(_jwtConfig.RefreshTokenExpirationTimeInDays).Subtract(TimeSpan.FromMinutes(1)),
        });
        //?
        
        //? gen refresh Token
        tokenDescriptor = gitCloudTokenHandler.GenerateAccessTokenDescriptor();
        token = tokenHandler.CreateToken(tokenDescriptor);
        //?
        
        GitCloudDb.RefreshTokens.Add(new Db.GitCloudRefreshToken(){TokenId = gitCloudTokenHandler.TokenId, FamilyId = gitCloudTokenHandler.FamilyId, ExpiresAt = DateTime.UtcNow.AddDays(_jwtConfig.RefreshTokenExpirationTimeInDays)});
        
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(string.Join("", "{\"accessToken\":\"", tokenHandler.WriteToken(token), "\"}"));
    }

    public static async Task GitCloudRefreshTokens(HttpContext context)
    {
        if(!context.Request.Cookies.TryGetValue("RefreshToken", out string? refreshTokenRaw)){context.Response.StatusCode = 401; return; }

        SecurityToken refreshToken = new JwtSecurityToken();
        JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
        try
        {
            ClaimsPrincipal tokenClaimsPrincipal = tokenHandler.ValidateToken(refreshTokenRaw, _validationParameters, out refreshToken );
            if (GitCloudDb.RefreshTokens.All(token => token.TokenId != Guid.Parse(tokenClaimsPrincipal.Claims.First(claim => claim.Type == "jti").Value)))
            {
                GitCloudDb.RefreshTokens.RemoveAll(token => token.FamilyId == Guid.Parse(tokenClaimsPrincipal.Claims.First(claim => claim.Type == "family_jti").Value) );
                context.Response.StatusCode = 401;
                return;
            }
            
        }
        catch (SecurityTokenValidationException e)
        {
            if (e is SecurityTokenExpiredException)
            {
                JwtSecurityToken secToken = new JwtSecurityTokenHandler().ReadJwtToken(refreshTokenRaw);
                GitCloudDb.RefreshTokens.Remove( GitCloudDb.RefreshTokens.First(token => token.TokenId == Guid.Parse(secToken.Claims.First(claim => claim.Type == "jit").Value)));
            }
            context.Response.StatusCode = 401; 
            Logger.LogError(e,  e.Message,  e.StackTrace);
            throw;
            return;
        }
            
        //? gen refresh Token
        GitCloudRefreshToken originalGitCloudRefreshToken = new GitCloudRefreshToken().ImportRefreshToken((JwtSecurityToken)refreshToken);
        SecurityTokenDescriptor tokenDescriptor = originalGitCloudRefreshToken.GenerateNewRefreshTokenDescriptor();
        SecurityToken token = tokenHandler.CreateToken(tokenDescriptor);
        
        GitCloudDb.RefreshTokens.Remove(new Db.GitCloudRefreshToken(){TokenId = originalGitCloudRefreshToken.TokenId, FamilyId = originalGitCloudRefreshToken.FamilyId, ExpiresAt = originalGitCloudRefreshToken.ExpiresAt});
        GitCloudDb.RefreshTokens.Add(new Db.GitCloudRefreshToken(){TokenId = (Guid)tokenDescriptor.Claims["jti"], FamilyId = (Guid)tokenDescriptor.Claims["family_jti"], ExpiresAt = (DateTime)tokenDescriptor.Expires! });
    
        context.Response.Cookies.Append("RefreshToken", tokenHandler.WriteToken(token), new CookieOptions
        {
            SameSite = SameSiteMode.Strict,
            HttpOnly = true,
            MaxAge = TimeSpan.FromDays(_jwtConfig.RefreshTokenExpirationTimeInDays).Subtract(TimeSpan.FromMinutes(1)),
        });
        //?
    
        //? gen access Token
        tokenDescriptor = originalGitCloudRefreshToken.GenerateAccessTokenDescriptor();
        token = tokenHandler.CreateToken(tokenDescriptor);
        //?
        
        
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(string.Join("", "{\"accessToken\":\"", tokenHandler.WriteToken(token), "\"}"));
        
        return;
    }

    public static async Task AuthTest(HttpContext context)
    {
        await context.Response.WriteAsync("Nice");
    }
}