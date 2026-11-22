using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace ChapGiti.Api.Contracts;

public static class RequestBody
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    public static JsonObject ToBody<T>(this T request) => JsonSerializer.SerializeToNode(request, Options)!.AsObject();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CustomerStartRequest(string? Phone);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CustomerLoginRequest(string? Phone, string? Password);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AdminLoginRequest(string? Username, string? Password);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AdminEmailRequest(string? Email);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AdminPasswordRequest(string? CurrentPassword, string? Password, string? Confirmation);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateAdministratorRequest(string? Email, string? Password, string? Confirmation);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OtpRequest(string? Phone, string? Purpose);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CompleteRegistrationRequest(string? RegistrationToken, string? FirstName, string? LastName, string? Password, string? Confirmation);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record VerifyOtpRequest(string? ChallengeId, string? Code);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ResetPasswordRequest(string? ResetToken, string? Password, string? Confirmation);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProfileRequest(string? FirstName, string? LastName, string? Phone);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateUserRequest(string? FirstName, string? LastName, string? Phone, string? Password, string? Confirmation);

// Counts accept JSON numbers or decimal digit strings; Domain normalizes and validates them.
public sealed record QuoteRequest(JsonNode? Pages, JsonNode? Quantity, string? Size, string? Paper,
    string? Color, string? Binding, JsonObject? Details, JsonNode? CatalogIds = null);
