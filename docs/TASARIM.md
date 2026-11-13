# Tasarım kararları

## Kapsam
Ofis bilgisayarında çalışan, tek kullanıcıyla başlayan ama giriş/rol/işlem günlüğü altyapısı hazır bir uygulama.
Ofis bilgisayarı sunucu yapılmayacak; çok kullanıcıya geçiş, ileride kiralık sunucu + API + PostgreSQL ile yapılacak.

## Kurallar
- **Borç yazma:** Dönem bittikten sonraki gün. Aylık: Ekim → 1 Kasım. 3 aylık: takvim çeyrekleri (Oca-Mar → 1 Nisan). Yıllık: takvim yılı (→ 1 Ocak).
- 3 aylık planlar çeyrek başında, yıllık planlar Ocak'ta başlamalıdır.
- Zam: yeni plan eklenir; eski plan bir sonraki planın başlangıcına kadar geçerlidir. Borç yazılmış döneme geriye dönük plan eklenemez.
- Pasif mükellefe otomatik borç yazılmaz. Ayrılan mükellefe, dönem bitişi ayrılış tarihinden sonra olan dönemler yazılmaz.
- Aynı mükellef + hizmet + dönem için ikinci otomatik borç yazılmaz (iptal edilmiş olsa bile). Veritabanında benzersiz indeksle de korunur.
- Ödemeler borçları en eskiden başlayarak kapatır (FIFO); mahsup ayrıca saklanmaz, her seferinde hesaplanır.
- Vade: mükellefin ödeme günü tanımlıysa borç ayının o günü, değilse borç tarihi.
- Kayıtlar silinmez, iptal edilir. Tüm ekleme/değişiklikler `IslemGunlugu` tablosuna yazılır.

## Hizmet türleri
Muhasebe ücreti, Defter tasdiki, Devir (açılış bakiyesi), Diğer hizmet.

## Tahsilat alanları
Tahsil eden (Ofis / Eleman / ...), hesap (paranın geldiği yer), ödeyen kişi, ödeme yöntemi, dekont. Makbuz (e-SMM) takibi yapılmaz.

## Aşamalar
1. ✅ İskelet, veritabanı, giriş, mükellefler, ücret planları, devir, cari ekstre, otomatik borç yazma, Excel aktarımı, yedekleme
2. ✅ Tahsilat girişi ve listesi, elle borç (ek hizmet), borç/ödeme iptali
3. Görüşmeler ve ödeme sözleri, ödeme grupları
4. Gecikenler, raporlar (Excel/PDF), işlem günlüğü ekranı
