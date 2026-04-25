using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TradingJournal.Shared.Extensions;

public static class ObjectExtensions
{
    private static readonly JsonSerializerOptions CloneOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static T DeepCopy<T>(this T obj)
        where T : class
    {
        string json = JsonSerializer.Serialize(obj, CloneOptions);
        T clone = JsonSerializer.Deserialize<T>(json, CloneOptions)!;
        return clone;
    }

    public static string ToHashString(this object obj)
    {
        // Convert the object to a string representation
        string? objectString = SerializeObject(obj);

        if (objectString == null) return string.Empty;

        // Convert the string to a byte array
        byte[] data = Encoding.UTF8.GetBytes(objectString);

#pragma warning disable CA5351
        // Compute the hash of the byte array
        byte[] hashBytes = MD5.HashData(data);
#pragma warning restore CA5351

        // Convert the hash byte array to a string
        StringBuilder builder = new();
        foreach (byte t in hashBytes)
        {
            builder.Append(t.ToString("x2")); // "x2" formats byte as hexadecimal string
        }

        return builder.ToString();
    }

    private static string SerializeObject(object obj)
    {
        return JsonSerializer.Serialize(obj);
    }
}