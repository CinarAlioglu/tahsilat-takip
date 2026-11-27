using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using TahsilatTakip.Data.Servisler;

namespace TahsilatTakip.App.Yardimcilar;

/// <summary>Birden çok ekranda kullanılan işlemler: hatırlatma mesajı, PDF ekstre, Excel kaydetme.</summary>
public static class OrtakIslemler
{
    private static DateOnly Bugun => DateOnly.FromDateTime(DateTime.Today);

    /// <summary>Hatırlatma mesajını panoya kopyalar; WhatsApp'a yapıştırılabilir.</summary>
    public static async Task MesajiKopyala(int mukellefId)
    {
        var metin = App.Servisler.GetRequiredService<TakipServisi>().HatirlatmaMesaji(mukellefId, Bugun);
        Clipboard.SetText(metin);
        await Mesaj.Bilgi("Mesaj kopyalandı", "Hatırlatma mesajı panoya kopyalandı. WhatsApp'ta Ctrl+V ile yapıştırabilirsiniz:\n\n" + metin);
    }

    public static void EkstrePdf(int mukellefId, string unvan)
    {
        var dosya = KayitYeriSor($"{DosyaAdi(unvan)}_ekstre_{DateTime.Today:yyyyMMdd}.pdf", "PDF dosyası (*.pdf)|*.pdf");
        if (dosya is null) return;
        App.Servisler.GetRequiredService<RaporServisi>().EkstrePdf(mukellefId, Bugun, dosya);
        Ac(dosya);
    }

    public static string? KayitYeriSor(string varsayilanAd, string filtre)
    {
        var dlg = new SaveFileDialog { FileName = varsayilanAd, Filter = filtre, AddExtension = true };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public static void Ac(string dosya) => Process.Start(new ProcessStartInfo(dosya) { UseShellExecute = true });

    public static string DosyaAdi(string s)
    {
        var gecersiz = Path.GetInvalidFileNameChars();
        var temiz = new string(s.Select(c => gecersiz.Contains(c) ? '_' : c).ToArray()).Trim();
        return temiz.Length > 50 ? temiz[..50] : temiz;
    }
}
