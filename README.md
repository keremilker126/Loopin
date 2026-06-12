# Loopin

Loopin, .NET 10 tabanlı bir video paylaşım ve sosyal platform uygulamasıdır. Uygulama kullanıcı kaydı, e-posta doğrulama, video yükleme, beğeni, yorum, abonelik, “daha sonra izle” ve izleme geçmişi gibi özellikleri destekler.

## Özellikler

- Kullanıcı kaydı ve e-posta doğrulama
- Admin için ek e-posta tabanlı giriş onayı
- Video yükleme ve yönetme
- Videolar için beğeni/favori kontrolü
- Yorum ekleme ve silme
- Kanal aboneliği (abone ol / abonelikten çık)
- “Daha sonra izle” listesi
- İzleme geçmişi takibi
- Trend video ve popüler içerik listesi
- SQLite veritabanı
- CORS ve büyük dosya yüklemeleri için yapılandırma

## Teknolojiler

- .NET 10
- ASP.NET Core MVC
- Entity Framework Core
- SQLite
- Razor Views + API controller yapısı

## Kurulum

1. Proje klasörüne girin:
   ```powershell
   cd c:\Users\kerem\Desktop\Loopin
   ```

2. Gerekli paketleri yükleyin (dotnet restore otomatik olarak çalışmazsa):
   ```powershell
   dotnet restore
   ```

3. Veritabanını güncelleyin:
   ```powershell
   dotnet ef database update
   ```

4. Uygulamayı başlatın:
   ```powershell
   dotnet run
   ```

Varsayılan çalıştırma adresi `https://localhost:5144` veya `http://localhost:5144` olacaktır.

## Yapılandırma

`appsettings.json` veya `appsettings.Development.json` içinde `EmailSettings` bölümü ile SMTP ayarlarını yapılandırın:

- `SmtpServer`
- `Port`
- `SenderName`
- `SenderEmail`
- `Username`
- `Password`

E-posta doğrulama ve admin giriş onayı için bu ayarlar gereklidir.

## API Endpoints

### Kimlik Doğrulama
- `POST /api/auth/register` — yeni kullanıcı kaydı ve e-posta doğrulama linki gönderimi
- `GET /api/auth/verify?token={token}` — e-posta doğrulama
- `GET /api/auth/user/{id}` — kullanıcı bilgilerini getir
- `GET /api/auth/admin/users?adminEmail={email}` — admin için tüm kullanıcıları listeleme

### Video
- `GET /api/video` — tüm videoları getir
- `GET /api/video/trends` — trend videolar
- `GET /api/video/{id}?userId={userId}` — video detayı ve izlenme sayısı artışı
- `POST /api/video/upload` — video yükleme
- `DELETE /api/video/{id}` — video silme

### Beğeni
- `POST /api/begenme/toggle` — videoyu beğen veya beğeniyi kaldır
- `GET /api/begenme/user/{userId}` — kullanıcının beğendiği videolar
- `DELETE /api/begenme/remove` — beğeniyi kaldır

### Abonelik
- `POST /api/abonelik/toggle` — abone ol / abonelikten çık
- `GET /api/abonelik/check?followerId={id}&followingId={id}` — abonelik durumu kontrolü
- `GET /api/abonelik/following/{userId}` — takip edilen kanalları getir

### Yorum
- `GET /api/yorum/video/{videoId}` — video yorumlarını getir
- `POST /api/yorum` — yorum ekle
- `DELETE /api/yorum/{id}?userId={userId}` — yorumu sil

### Daha Sonra İzle
- `GET /api/dahasonraizle/{userId}` — kullanıcının liste öğelerini getir
- `POST /api/dahasonraizle/toggle` — ekle / çıkar
- `DELETE /api/dahasonraizle` — listeden çıkar
- `DELETE /api/dahasonraizle/clear/{userId}` — tüm listeyi temizle

## Notlar

- Video yükleme limiti `1 GB`
- Kapak resmi limiti `5 MB`
- `wwwroot/videos` ve `wwwroot/images` klasörleri uygulama içinde otomatik olarak oluşturulur
- Veritabanı dosyası `loopin.db` olarak kaydedilir
- Admin e-posta adresleri `HomeController` içinde sabit olarak tanımlanmıştır

## Geliştirme İpuçları

- Şifreler şu anda düz metin olarak saklanıyor; üretimde mutlaka hashleme ekleyin
- Admin ve yetkilendirme kontrollerini güçlendirmek için `HttpContext.Session` ve rol bazlı izinleri genişletebilirsiniz
- Kullanıcı upload boyutu ve güvenlik ayarları uygulama ihtiyaçlarına göre uyarlanabilir

---

Loopin, temel bir sosyal video platformu için hızlıca başlatılabilir ve kolayca genişletilebilir bir altyapı sunar.
