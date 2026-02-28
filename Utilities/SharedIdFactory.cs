using System;
using System.Collections.Generic;
using System.Text;

namespace FarmSim.Domain.Utilities;
public static class SharedIdFactory
{
    public static Guid Create(FarmKey farm, string entityKind, params BasicList<string> parts)
    {
        var main = farm.IsMain ? farm : farm.AsMain;

        // Use only stable fields that define the pair identity
        // (Slot is forced to main already by the line above)
        string pairKey = $"{main.ProfileId}|{main.Theme}";

        string key = string.Join("|", new[] { pairKey, entityKind }.Concat(parts).Select(Norm));

        using var md5 = System.Security.Cryptography.MD5.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(key);
        var hash = md5.ComputeHash(bytes);
        return new Guid(hash);
    }
    private static string Norm(string? s) =>
        string.IsNullOrWhiteSpace(s) ? "" : s.Trim().ToLowerInvariant();
}
