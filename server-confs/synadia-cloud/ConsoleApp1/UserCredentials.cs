using System.Text;
using Chaos.NaCl;

namespace NATS.Client.Core.Internal;

internal class UserCredentials
{
    public UserCredentials(
        string? nkeyFile = null,
        string? credsFile = null,
        string? seed = null,
        string? nkey = null,
        string? token = null,
        string? jwt = null)
    {
        Jwt = jwt;
        Seed = seed;
        NKey = nkey;
        Token = token;

        if (!string.IsNullOrEmpty(credsFile))
        {
            (Jwt, Seed) = LoadCredsFile(credsFile);
        }

        if (!string.IsNullOrEmpty(nkeyFile))
        {
            (Seed, NKey) = LoadNKeyFile(nkeyFile);
        }
    }

    public string? Jwt { get; }

    public string? Seed { get; }

    public string? NKey { get; }

    public string? Token { get; }

    public string? Sign(string? nonce)
    {
        if (Seed == null || nonce == null)
            return null;

        using var kp = NKeys.FromSeed(Seed);
        var bytes = kp.Sign(Encoding.ASCII.GetBytes(nonce));
        var sig = CryptoBytes.ToBase64String(bytes);

        return sig;
    }

    private (string, string) LoadCredsFile(string path)
    {
        string? jwt = null;
        string? seed = null;
        using var reader = new StreamReader(path);
        while (reader.ReadLine()?.Trim() is { } line)
        {
            if (line.StartsWith("-----BEGIN NATS USER JWT-----"))
            {
                jwt = reader.ReadLine();
                if (jwt == null)
                    break;
            }
            else if (line.StartsWith("-----BEGIN USER NKEY SEED-----"))
            {
                seed = reader.ReadLine();
                if (seed == null)
                    break;
            }
        }

        if (jwt == null)
            throw new Exception($"Can't find JWT while loading creds file ${path}");
        if (seed == null)
            throw new Exception($"Can't find NKEY seed while loading creds file ${path}");

        return (jwt, seed);
    }

    private (string, string) LoadNKeyFile(string path)
    {
        string? seed = null;
        string? nkey = null;

        using var reader = new StreamReader(path);
        while (reader.ReadLine()?.Trim() is { } line)
        {
            if (line.StartsWith("SU"))
            {
                seed = line;
            }
            else if (line.StartsWith("U"))
            {
                nkey = line;
            }
        }

        if (seed == null)
            throw new Exception($"Can't find seed while loading NKEY file ${path}");
        if (nkey == null)
            throw new Exception($"Can't find public key while loading NKEY file ${path}");

        return (seed, nkey);
    }
}
