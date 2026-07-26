using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.IdentityModel.Tokens;
using ScottBrady.IdentityModel.Crypto;
using ScottBrady.IdentityModel.Tokens;

namespace gitCloud_api;

public static partial class Auth
{
    public class AuthLoginRequest
    {
        [JsonPropertyName("username")] 
        public string Username { get; init; } = "";

        [JsonPropertyName("passwdHash")] 
        public string PasswdHash { get; init; } = "";
    }

    private class GitCloudRefreshTokenHandler
    {
        public Guid TokenId = Guid.Empty;
        public Guid FamilyId = Guid.Empty;
        public Guid UserId = Guid.Empty;
        public string GivenName = "";
        public string Role = "";
        public DateTime ExpiresAt = DateTime.MinValue;
        private List<FieldInfo> _fields = typeof(GitCloudRefreshTokenHandler).GetFields(BindingFlags.Public |  BindingFlags.Instance).ToList();

        public GitCloudRefreshTokenHandler ImportRefreshToken( JwtSecurityToken jwtRefreshToken )
        {
            Claim[] claims = jwtRefreshToken.Claims.ToArray();
            for (int i = 0; i < _fields.Count-1; i++)
            {
                if (_fields[i].Name.Contains("<")) { continue; }

                if (_fields[i].FieldType.Name == "Guid")
                { 
                    _fields[i].SetValue(this, Guid.Parse(claims[i].Value)); 
                    continue;
                }
                
                _fields[i].SetValue(this, claims[i].Value); 
            }

            ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(long.Parse(claims.First(claim => claim.Type == "exp").Value)).DateTime;
            
            return this;
        }

        public SecurityTokenDescriptor ExportRefreshTokenDescriptor()
        {
            return new SecurityTokenDescriptor
            {
                Audience = _jwtConfig.Audience,
                Expires = DateTime.UtcNow.AddDays(_jwtConfig.RefreshTokenExpirationTimeInDays),
                Issuer = _jwtConfig.Issuer,
                Claims = new Dictionary<string, object>()
                {
                    { "jti" , TokenId },
                    { "family_jti" , FamilyId },
                    { "sub" , UserId },
                    { "given_name", GivenName },
                    { "role" , Role },
                    { "type", "refresh" }
                },
                SigningCredentials = new SigningCredentials(new EdDsaSecurityKey(_keys), ExtendedSecurityAlgorithms.EdDsa)
            };
        }
        
        public SecurityTokenDescriptor GenerateNewRefreshTokenDescriptor()
        {
            
            return new SecurityTokenDescriptor
            {
                Audience = _jwtConfig.Audience,
                Expires = DateTime.UtcNow.AddDays(_jwtConfig.RefreshTokenExpirationTimeInDays),
                Issuer = _jwtConfig.Issuer,
                Claims = new Dictionary<string, object>()
                {
                    { "jti" , Guid.CreateVersion7() },
                    { "family_jti" , FamilyId },
                    { "sub" , UserId },
                    { "given_name", GivenName },
                    { "role" , Role },
                    { "type", "refresh" }
                },
                SigningCredentials = new SigningCredentials(new EdDsaSecurityKey(_keys), ExtendedSecurityAlgorithms.EdDsa)
            };
        }
        
        public SecurityTokenDescriptor GenerateAccessTokenDescriptor()
        {
            return new SecurityTokenDescriptor
            {
                Audience = _jwtConfig.Audience,
                Expires = DateTime.UtcNow.AddDays(_jwtConfig.RefreshTokenExpirationTimeInDays),
                Issuer = _jwtConfig.Issuer,
                Subject = new ClaimsIdentity([
                    new Claim("sub" ,UserId.ToString()),
                    new Claim("given_name", GivenName),
                    new Claim("role", Role)
                ]),
                Claims = new Dictionary<string, object>()
                {
                    { "type", "access" }
                },
                SigningCredentials = new SigningCredentials(new EdDsaSecurityKey(_keys), ExtendedSecurityAlgorithms.EdDsa)
            };
        }
        
    }
}