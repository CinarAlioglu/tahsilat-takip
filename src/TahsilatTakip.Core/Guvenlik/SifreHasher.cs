using System.Security.Cryptography;

namespace TahsilatTakip.Core.Guvenlik;

/// <summary>Şifreler PBKDF2 (SHA-256) ile tuzlanarak saklanır; düz metin hiçbir yerde tutulmaz.</summary>
public static class SifreHasher
{
    private const int Iterasyon = 210_000;
    private const int TuzUzunlugu = 16;
    private const int HashUzunlugu = 32;

    public static string Hashle(string sifre)
    {
        var tuz = RandomNumberGenerator.GetBytes(TuzUzunlugu);
        var hash = Rfc2898DeriveBytes.Pbkdf2(sifre, tuz, Iterasyon, HashAlgorithmName.SHA256, HashUzunlugu);
        return $"v1${Iterasyon}${Convert.ToBase64String(tuz)}${Convert.ToBase64String(hash)}";
    }

    public static bool Dogrula(string sifre, string kayitliHash)
    {
        var parcalar = kayitliHash.Split('$');
        if (parcalar.Length != 4 || parcalar[0] != "v1" || !int.TryParse(parcalar[1], out var iterasyon))
            return false;
        try
        {
            var tuz = Convert.FromBase64String(parcalar[2]);
            var beklenen = Convert.FromBase64String(parcalar[3]);
            var hash = Rfc2898DeriveBytes.Pbkdf2(sifre, tuz, iterasyon, HashAlgorithmName.SHA256, beklenen.Length);
            return CryptographicOperations.FixedTimeEquals(hash, beklenen);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>En az 6 karakter. Ofis içi kullanım için makul bir alt sınır.</summary>
    public static string? SifreKontrol(string? sifre) =>
        string.IsNullOrEmpty(sifre) || sifre.Length < 6 ? "Şifre en az 6 karakter olmalıdır." : null;
}
