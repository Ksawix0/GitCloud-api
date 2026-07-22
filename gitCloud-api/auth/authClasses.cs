using System.Text.Json.Serialization;

namespace gitCloud_api;

public class AuthLoginRequest
{
    [JsonPropertyName("username")] 
    public string Username { get; init; } = "";

    [JsonPropertyName("passwdHash")] 
    public string PasswdHash { get; init; } = "";
}