using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;

namespace TradingJournal.Shared.Extensions;

public static class ObjectExtensions
{
    public static T DeepCopy<T>(this T obj)
        where T : class
    {
        string json = JsonConvert.SerializeObject(obj);
        T clone = JsonConvert.DeserializeObject<T>(json)!;
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
        return JsonConvert.SerializeObject(obj);
    }
}