using System.Security.Cryptography;
using System.Text;

namespace ERP.Shared.Security;

/// <summary>
/// One-way hash for opaque bearer secrets (refresh tokens) stored at rest, so a database
/// read cannot be replayed as a session. SHA-256 is appropriate here — unlike a password,
/// a refresh token is 64 bytes of CSPRNG output, so there is nothing to brute-force and a
/// slow KDF would only add latency to every refresh.
/// </summary>
public static class TokenHasher
{
    public static string Hash(string token)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
