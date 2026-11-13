using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using TahsilatTakip.Core.Kurallar;
using TahsilatTakip.Core.Varliklar;
using TahsilatTakip.Data.Servisler;

namespace TahsilatTakip.App.Yardimcilar;

/// <summary>Kullanıcının yazdığı tutarı okur: "6250", "6.250", "6.250,50", "6250.5" hepsi kabul edilir.</summary>
public static class Sayi
{
    public static decimal? Oku(string? metin)
    {
        if (string.IsNullOrWhiteSpace(metin)) return null;
        var s = metin.Replace("₺", "").Replace("TL", "", StringComparison.OrdinalIgnoreCase).Replace(" ", "").Trim();
        // Tek nokta ve ardından 1-2 hane varsa ondalık ayırıcı kabul et (ör. 6250.5).
        var nokta = s.LastIndexOf('.');
        if (!s.Contains(',') && nokta >= 0 && s.Count(c => c == '.') == 1 && s.Length - nokta - 1 is 1 or 2)
            s = s.Replace('.', ',');
        return decimal.TryParse(s, NumberStyles.Number, Turkce.Kultur, out var d) ? d : null;
    }

    public static string Yaz(decimal d) => d.ToString("N2", Turkce.Kultur);
}

/// <summary>Ay seçim listelerinde bir öğe ("Kasım 2026").</summary>
public sealed record AySecenegi(DateOnly Ay)
{
    public string Ad => $"{Donem.AyAdlari[Ay.Month - 1]} {Ay.Year}";
    public override string ToString() => Ad;

    /// <summary>Bugünden <paramref name="geri"/> ay öncesinden <paramref name="ileri"/> ay sonrasına kadar.</summary>
    public static List<AySecenegi> Liste(int geri = 36, int ileri = 24)
    {
        var bu = Donem.AyBasi(DateOnly.FromDateTime(DateTime.Today));
        return Enumerable.Range(-geri, geri + ileri + 1).Select(i => new AySecenegi(bu.AddMonths(i))).ToList();
    }
}

public sealed record Secenek<T>(T Deger, string Ad)
{
    public override string ToString() => Ad;
}

public static class Secenekler
{
    public static readonly List<Secenek<UcretPeriyodu>> Periyotlar =
    [
        new(UcretPeriyodu.Aylik, "Aylık"),
        new(UcretPeriyodu.UcAylik, "3 Aylık"),
        new(UcretPeriyodu.Yillik, "Yıllık"),
    ];

    public static readonly List<Secenek<MukellefDurumu>> Durumlar =
    [
        new(MukellefDurumu.Aktif, "Aktif"),
        new(MukellefDurumu.Pasif, "Pasif (borç yazılmaz)"),
        new(MukellefDurumu.Ayrildi, "Ayrıldı"),
    ];

    public static readonly List<Secenek<OdemeTercihi>> OdemeTercihleri =
    [
        new(OdemeTercihi.Belirtilmedi, "Belirtilmedi"),
        new(OdemeTercihi.Elden, "Elden"),
        new(OdemeTercihi.Iban, "IBAN"),
    ];

    public static readonly List<Secenek<OdemeYontemi>> OdemeYontemleri =
    [
        new(OdemeYontemi.Nakit, "Nakit"),
        new(OdemeYontemi.HavaleEft, "Havale / EFT"),
        new(OdemeYontemi.KrediKarti, "Kredi kartı"),
        new(OdemeYontemi.CekSenet, "Çek / Senet"),
    ];

    public static readonly List<Secenek<KullaniciRolu>> Roller =
    [
        new(KullaniciRolu.Kullanici, "Kullanıcı"),
        new(KullaniciRolu.Yonetici, "Yönetici"),
    ];
}

/// <summary>Enum değerlerini Türkçe metne çevirir.</summary>
public sealed class EnumAciklamaDonusturucu : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        UcretPeriyodu p => Donem.PeriyotAdi(p),
        MukellefDurumu.Aktif => "Aktif",
        MukellefDurumu.Pasif => "Pasif",
        MukellefDurumu.Ayrildi => "Ayrıldı",
        OdemeTercihi.Iban => "IBAN",
        OdemeTercihi.Elden => "Elden",
        OdemeTercihi.Belirtilmedi => "",
        KullaniciRolu.Yonetici => "Yönetici",
        KullaniciRolu.Kullanici => "Kullanıcı",
        OdemeYontemi.Nakit => "Nakit",
        OdemeYontemi.HavaleEft => "Havale/EFT",
        OdemeYontemi.KrediKarti => "Kredi kartı",
        OdemeYontemi.CekSenet => "Çek/Senet",
        TahakkukKaynagi.Otomatik => "Otomatik",
        TahakkukKaynagi.Manuel => "Elle",
        TahakkukKaynagi.Devir => "Devir",
        EkstreSatirTuru.Borc => "Borç",
        EkstreSatirTuru.Odeme => "Ödeme",
        _ => value?.ToString(),
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Borçlu bakiye kırmızı, alacaklı (avans) yeşil.</summary>
public sealed class BakiyeRengiDonusturucu : IValueConverter
{
    private static readonly Brush Kirmizi = new SolidColorBrush(Color.FromRgb(0xC4, 0x2B, 0x1C));
    private static readonly Brush Yesil = new SolidColorBrush(Color.FromRgb(0x0F, 0x7B, 0x0F));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        decimal d when d > 0 => Kirmizi,
        decimal d when d < 0 => Yesil,
        int i when i > 0 => Kirmizi,
        _ => DependencyProperty.UnsetValue,
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class BoolGorunurlukDonusturucu : IValueConverter
{
    public bool Ters { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        (value is true) ^ Ters ? Visibility.Visible : Visibility.Collapsed;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class BosDegilGorunurDonusturucu : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null || value is string { Length: 0 } ? Visibility.Collapsed : Visibility.Visible;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Fluent görünümlü onay ve bilgi kutuları.</summary>
public static class Mesaj
{
    public static async Task<bool> Onayla(string baslik, string metin, string evet = "Evet", string hayir = "Vazgeç")
    {
        var kutu = new Wpf.Ui.Controls.MessageBox
        {
            Title = baslik,
            Content = metin,
            PrimaryButtonText = evet,
            CloseButtonText = hayir,
        };
        return await kutu.ShowDialogAsync() == Wpf.Ui.Controls.MessageBoxResult.Primary;
    }

    public static async Task Bilgi(string baslik, string metin)
    {
        var kutu = new Wpf.Ui.Controls.MessageBox { Title = baslik, Content = metin, CloseButtonText = "Tamam" };
        await kutu.ShowDialogAsync();
    }
}

public sealed class BoolTersDonusturucu : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
}

public static class PencereYardimcisi
{
    /// <summary>Açılacak iletişim penceresinin sahibi: o an etkin pencere (yoksa ana pencere).</summary>
    public static Window? Sahip() =>
        Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;
}
