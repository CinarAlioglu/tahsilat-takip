# Tahsilat Takip

SMMM ofisinin mükelleflerden aldığı muhasebe ücretlerini takip eden Windows masaüstü uygulaması.

- **Borç (tahakkuk):** Her mükellefin ücret planına göre dönem sonunda otomatik yazılır. Ekim ücreti 1 Kasım'da borçlanır; 3 aylık ücretler çeyrek, yıllık ücretler yıl sonunda.
- **Ödeme (tahsilat):** Ödemeler en eski borçtan başlayarak borçları kapatır; bakiye her zaman borç ve ödemelerden hesaplanır.
- **Kayıtlar silinmez:** Borç ve ödemeler gerekçesiyle iptal edilir; tüm değişiklikler işlem günlüğüne yazılır.

## Teknoloji

| Katman | Seçim |
|---|---|
| Platform | .NET 10 (LTS) |
| Arayüz | WPF + [WPF-UI](https://github.com/lepoco/wpfui) (Fluent), MVVM (CommunityToolkit.Mvvm) |
| Veri | EF Core + SQLite |
| Excel | ClosedXML |

```
src/TahsilatTakip.Core   İş kuralları (dönem, borç planlama, cari hesap, şifre) — arayüzden bağımsız
src/TahsilatTakip.Data   Veritabanı, servisler, Excel aktarımı, yedekleme
src/TahsilatTakip.App    WPF arayüzü
tests/TahsilatTakip.Tests
```

İş kuralları ve veri katmanı arayüzden ayrıdır; ileride sunucuya geçildiğinde yalnızca veri katmanı değişir.

## Veri klasörü

`D:\Tahsilat Takip` (D: sürücüsü yoksa `Belgeler\Tahsilat Takip`):

- `tahsilat.db` — veritabanı
- `Yedekler\` — her kapanışta otomatik yedek (son 30 adet)
- `Dekontlar\` — ödeme dekontları

Ayarlar'dan ek bir yedek klasörü (Google Drive, USB vb.) tanımlanabilir.

## Çalıştırma

**Hazır paket:** GitHub'da *Actions → Derle ve test et → son çalışma → Artifacts* bölümünden `TahsilatTakip-win-x64` indirilir. İçindeki `TahsilatTakip.exe` .NET kurulumu gerektirmeden çalışır.

**Kaynak koddan:** [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) kurulu olmalıdır.

```powershell
dotnet run --project src/TahsilatTakip.App
dotnet test
```

İlk açılışta yönetici hesabı oluşturulur. Ardından *Ayarlar → Excel'den mükellef aktar* ile mükellef listesi içe aktarılır ve her mükellefin kartından geçmiş borcu *Devir* olarak girilir.

## Veritabanı şeması değişiklikleri

```powershell
dotnet tool restore
dotnet ef migrations add DegisiklikAdi -p src/TahsilatTakip.Data -s src/TahsilatTakip.Data -o Migrations
```

Uygulama açılışta bekleyen değişiklikleri otomatik uygular.
