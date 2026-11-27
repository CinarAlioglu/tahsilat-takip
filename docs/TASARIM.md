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
3. ✅ Görüşmeler ve ödeme sözleri, ödeme grupları, gecikenler (yaşlandırma), aylık durum tablosu,
   hatırlatma mesajı, PDF ekstre, Excel raporları, eleman tahsilatı teslim takibi, ortadan mükellef arama
4. İşlem günlüğü ekranı, çok kullanıcılı sunucu sürümü

## Ödeme sözleri
- Görüşmede söz tarihi (ve isteğe bağlı tutar) girilir. Görüşmeden sonraki ödemeler söz tutarına (tutar yoksa herhangi
  bir ödemeye) ulaşınca söz "Tutuldu"; söz tarihi geçip ulaşmazsa "Tutulmadı" olur.
- Yeni söz alınınca aynı mükellefin bekleyen eski sözü "Tutulmadı" sayılır. Tutulmayan söz, mükellefle yeniden
  görüşülünce takipten düşer.

## Ödeme grupları
- Gruba gelen ödeme üyelerin açık borçlarına en eskiden başlayarak dağıtılır; artan kısım ilk üyeye (alfabetik) avans.
- Her üye için ayrı tahsilat kaydı oluşur, aynı işlem numarasıyla bağlıdır; birinin iptali hepsini iptal eder.

## Teslim takibi
- "Tahsil eden" boş veya "Ofis" değilse tahsilat "teslim bekliyor" olarak başlar; Tahsilatlar ekranından toplu
  "ofise teslim alındı" işaretlenir.
