using Microsoft.EntityFrameworkCore;
using TahsilatTakip.Core.Guvenlik;
using TahsilatTakip.Core.Varliklar;

namespace TahsilatTakip.Data.Servisler;

public sealed class IslemHatasi(string mesaj) : Exception(mesaj);

public sealed class KullaniciServisi(VeritabaniFabrikasi fabrika, Oturum oturum)
{
    public bool KullaniciVarMi()
    {
        using var db = fabrika.Olustur();
        return db.Kullanicilar.Any();
    }

    /// <summary>Kullanıcı adı ve şifre doğruysa oturumu açar.</summary>
    public bool GirisYap(string kullaniciAdi, string sifre)
    {
        using var db = fabrika.Olustur();
        var k = db.Kullanicilar.FirstOrDefault(x => x.KullaniciAdi == kullaniciAdi.Trim() && x.Aktif);
        if (k is null || !SifreHasher.Dogrula(sifre, k.SifreHash)) return false;
        k.SonGirisTarihi = DateTime.Now;
        oturum.Ac(k);
        db.SaveChanges();
        return true;
    }

    public List<Kullanici> Listele()
    {
        using var db = fabrika.Olustur();
        return db.Kullanicilar.AsNoTracking().OrderBy(k => k.AdSoyad).ToList();
    }

    /// <summary>İlk kurulumda yönetici oluşturur; sonrasında yalnızca yönetici kullanıcı ekleyebilir.</summary>
    public Kullanici Olustur(string kullaniciAdi, string adSoyad, string sifre, KullaniciRolu rol)
    {
        using var db = fabrika.Olustur();
        var ilkKullanici = !db.Kullanicilar.Any();
        if (!ilkKullanici && !oturum.YoneticiMi) throw new IslemHatasi("Kullanıcı eklemek için yönetici yetkisi gerekir.");
        if (ilkKullanici) rol = KullaniciRolu.Yonetici;

        kullaniciAdi = kullaniciAdi.Trim();
        if (kullaniciAdi.Length < 3) throw new IslemHatasi("Kullanıcı adı en az 3 karakter olmalıdır.");
        if (string.IsNullOrWhiteSpace(adSoyad)) throw new IslemHatasi("Ad soyad boş olamaz.");
        if (SifreHasher.SifreKontrol(sifre) is { } hata) throw new IslemHatasi(hata);
        if (db.Kullanicilar.Any(k => k.KullaniciAdi == kullaniciAdi))
            throw new IslemHatasi("Bu kullanıcı adı zaten kullanılıyor.");

        var yeni = new Kullanici
        {
            KullaniciAdi = kullaniciAdi,
            AdSoyad = adSoyad.Trim(),
            SifreHash = SifreHasher.Hashle(sifre),
            Rol = rol,
            OlusturmaTarihi = DateTime.Now,
        };
        db.Kullanicilar.Add(yeni);
        db.SaveChanges();
        return yeni;
    }

    public void SifreDegistir(int kullaniciId, string? eskiSifre, string yeniSifre)
    {
        using var db = fabrika.Olustur();
        var k = db.Kullanicilar.Find(kullaniciId) ?? throw new IslemHatasi("Kullanıcı bulunamadı.");
        var kendisi = oturum.Kullanici?.Id == kullaniciId;
        if (!kendisi && !oturum.YoneticiMi) throw new IslemHatasi("Bu işlem için yetkiniz yok.");
        if (kendisi && !SifreHasher.Dogrula(eskiSifre ?? "", k.SifreHash)) throw new IslemHatasi("Mevcut şifre hatalı.");
        if (SifreHasher.SifreKontrol(yeniSifre) is { } hata) throw new IslemHatasi(hata);
        k.SifreHash = SifreHasher.Hashle(yeniSifre);
        db.SaveChanges();
    }

    public void AktiflikDegistir(int kullaniciId, bool aktif)
    {
        if (!oturum.YoneticiMi) throw new IslemHatasi("Bu işlem için yönetici yetkisi gerekir.");
        if (oturum.Kullanici?.Id == kullaniciId && !aktif) throw new IslemHatasi("Kendi hesabınızı pasif yapamazsınız.");
        using var db = fabrika.Olustur();
        var k = db.Kullanicilar.Find(kullaniciId) ?? throw new IslemHatasi("Kullanıcı bulunamadı.");
        k.Aktif = aktif;
        db.SaveChanges();
    }
}
