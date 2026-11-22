using System.Security.Cryptography;
namespace ChapGiti.Domain.Orders;

public static class TrackingCode
{
    private const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
    public static string Generate() => "CG-" + RandomNumberGenerator.GetString(Alphabet,5) + "-" + RandomNumberGenerator.GetString(Alphabet,5);
}
