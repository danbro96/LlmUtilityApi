using System.Buffers.Text;
using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace LlmUtilityApi.Mcp;

[McpServerToolType]
public static class CryptoTools
{
    [McpServerTool(Name = "hash")]
    [Description("Hash a UTF-8 string. algorithm = sha256 (default), sha1, sha512, or md5. Returns lowercase hex.")]
    public static string Hash(
        [Description("The text to hash.")] string input,
        [Description("Hash algorithm: sha256, sha1, sha512, or md5.")] string algorithm = "sha256")
    {
        var data = Encoding.UTF8.GetBytes(input);
        var hash = algorithm.ToLowerInvariant() switch
        {
            "sha256" => SHA256.HashData(data),
            "sha1" => SHA1.HashData(data),
            "sha512" => SHA512.HashData(data),
            "md5" => MD5.HashData(data),
            _ => throw new McpException("algorithm must be one of: sha256, sha1, sha512, md5"),
        };
        return Convert.ToHexStringLower(hash);
    }

    [McpServerTool(Name = "hmac")]
    [Description("HMAC of a UTF-8 message under a UTF-8 key. algorithm = sha256 (default), sha1, or sha512. Returns lowercase hex.")]
    public static string Hmac(
        [Description("The secret key (UTF-8).")] string key,
        [Description("The message (UTF-8).")] string input,
        [Description("HMAC hash: sha256, sha1, or sha512.")] string algorithm = "sha256")
    {
        var k = Encoding.UTF8.GetBytes(key);
        var data = Encoding.UTF8.GetBytes(input);
        var mac = algorithm.ToLowerInvariant() switch
        {
            "sha256" => HMACSHA256.HashData(k, data),
            "sha1" => HMACSHA1.HashData(k, data),
            "sha512" => HMACSHA512.HashData(k, data),
            _ => throw new McpException("algorithm must be one of: sha256, sha1, sha512"),
        };
        return Convert.ToHexStringLower(mac);
    }

    [McpServerTool(Name = "encode")]
    [Description("Encode a UTF-8 string. scheme = base64, base64url, hex, or url.")]
    public static string Encode(
        [Description("The text to encode.")] string input,
        [Description("Encoding scheme: base64, base64url, hex, or url.")] string scheme)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        return scheme.ToLowerInvariant() switch
        {
            "base64" => Convert.ToBase64String(bytes),
            "base64url" => Base64Url.EncodeToString(bytes),
            "hex" => Convert.ToHexStringLower(bytes),
            "url" => Uri.EscapeDataString(input),
            _ => throw new McpException("scheme must be one of: base64, base64url, hex, url"),
        };
    }

    [McpServerTool(Name = "decode")]
    [Description("Decode to a UTF-8 string. scheme = base64, base64url, hex, or url.")]
    public static string Decode(
        [Description("The encoded text.")] string input,
        [Description("Encoding scheme: base64, base64url, hex, or url.")] string scheme)
    {
        try
        {
            return scheme.ToLowerInvariant() switch
            {
                "base64" => Encoding.UTF8.GetString(Convert.FromBase64String(input)),
                "base64url" => Encoding.UTF8.GetString(Base64Url.DecodeFromChars(input)),
                "hex" => Encoding.UTF8.GetString(Convert.FromHexString(input)),
                "url" => Uri.UnescapeDataString(input),
                _ => throw new McpException("scheme must be one of: base64, base64url, hex, url"),
            };
        }
        catch (FormatException ex)
        {
            throw new McpException($"could not decode as {scheme}: {ex.Message}");
        }
    }

    [McpServerTool(Name = "jwt_decode")]
    [Description("Decode a JWT's header and payload (base64url). Does NOT verify the signature.")]
    public static JwtParts JwtDecode(
        [Description("The JWT (header.payload.signature).")] string token)
    {
        var parts = token.Split('.');
        if (parts.Length < 2)
            throw new McpException("not a JWT (expected at least header.payload)");
        try
        {
            return new JwtParts(DecodeSegment(parts[0]), DecodeSegment(parts[1]));
        }
        catch (Exception ex)
        {
            throw new McpException($"could not decode JWT: {ex.Message}");
        }
    }

    [McpServerTool(Name = "uuid")]
    [Description("Generate a UUID v7 (time-ordered).")]
    public static string Uuid() => Guid.CreateVersion7().ToString();

    [McpServerTool(Name = "ulid")]
    [Description("Generate a ULID (lexicographically sortable, Crockford base32).")]
    public static string NewUlid() => global::System.Ulid.NewUlid().ToString();

    private static JsonNode? DecodeSegment(string segment)
    {
        var bytes = Base64Url.DecodeFromChars(segment);
        return JsonNode.Parse(bytes);
    }
}

public sealed record JwtParts(JsonNode? Header, JsonNode? Payload);
