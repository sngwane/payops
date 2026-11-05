using System.Security.Cryptography;
using System.Text;

namespace PayOps.Client.Data.Services;

/// <summary>
/// Generates and hashes client API keys. Keys carry 256 random bits, so a single SHA-256 is enough to store them safely
/// and cheap enough to check on every request. Legacy access keys are migrated through <see cref="Hash"/> unchanged.
/// </summary>
public static class ApiKeyHasher
{
    public const string KeyMarker = "pok_";
    public const int PrefixLength = 12;

    public static string Generate()
        => KeyMarker + Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    public static byte[] Hash(string key) => SHA256.HashData(Encoding.UTF8.GetBytes(key));

    public static string Prefix(string key) => key.Length <= PrefixLength ? key : key[..PrefixLength];

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
