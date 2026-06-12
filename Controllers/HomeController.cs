using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Loopin.Models;
using Loopin.Data;
using Microsoft.EntityFrameworkCore;
using Loopin.Services;
using System.Net.Mail;

namespace Loopin.Controllers;

public class HomeController : Controller
{
    // HomeController içinde
    public static List<string> _blockedEmails = new List<string>();

    private readonly ILogger<HomeController> _logger;
    private readonly AppDbContext _context;
    private readonly EmailService _emailService;
    private readonly MobileNotificationService _mobileNotificationService;
    private readonly PasswordService _passwordService;
    private readonly List<string> _adminEmails = new List<string>
 {

""

 };
    public HomeController(
        ILogger<HomeController> logger,
        AppDbContext context,
        EmailService emailService,
        MobileNotificationService mobileNotificationService,
        PasswordService passwordService)
    {
        _logger = logger;
        _context = context;
        _emailService = emailService;
        _mobileNotificationService = mobileNotificationService;
        _passwordService = passwordService;
    }

    private bool IsAdminEmail(string? email)
    {
        var normalizedEmail = NormalizeEmail(email);

        return !string.IsNullOrWhiteSpace(normalizedEmail) &&
            _adminEmails.Select(NormalizeEmail).Contains(normalizedEmail, StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeEmail(string? email)
    {
        return (email ?? "").Trim();
    }

    private void PrepareSecurityCode(string key)
    {
        var random = new Random();
        var first = random.Next(2, 10);
        var second = random.Next(2, 10);

        HttpContext.Session.SetInt32($"SecurityCode:{key}", first + second);
        ViewBag.SecurityQuestion = $"{first} + {second} = ?";
    }

    private bool ValidateSecurityCode(string key, string? guvenlikKodu)
    {
        var expected = HttpContext.Session.GetInt32($"SecurityCode:{key}");

        return expected.HasValue &&
            int.TryParse(guvenlikKodu, out var actual) &&
            actual == expected.Value;
    }

    private bool IsBlockedEmail(string? email)
    {
        var normalizedEmail = NormalizeEmail(email);

        return _blockedEmails.Any(blocked =>
            string.Equals(NormalizeEmail(blocked), normalizedEmail, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsValidEmail(string? email)
    {
        var normalizedEmail = NormalizeEmail(email);

        try
        {
            return !string.IsNullOrWhiteSpace(normalizedEmail) &&
                new MailAddress(normalizedEmail).Address == normalizedEmail;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsStrongEnoughPassword(string? password)
    {
        return !string.IsNullOrWhiteSpace(password) && password.Length >= 6 && password.Length <= 64;
    }

    private void SignInUser(Kullanici user)
    {
        HttpContext.Session.SetString("UserEmail", user.Email);
        HttpContext.Session.SetString("UserName", user.KullaniciAdi ?? "");
        HttpContext.Session.SetInt32("UserId", user.Id);
        HttpContext.Session.SetString("IsAdmin", IsAdminEmail(user.Email) ? "true" : "false");
    }

    private void UpgradePasswordIfNeeded(Kullanici user, string suppliedPassword)
    {
        if (_passwordService.NeedsRehash(user.Sifre))
        {
            user.Sifre = _passwordService.HashPassword(suppliedPassword);
        }
    }

    private void TryDeletePublicFile(string? publicUrl)
    {
        if (string.IsNullOrWhiteSpace(publicUrl))
        {
            return;
        }

        var relativePath = publicUrl.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(relativePath))
        {
            _logger.LogWarning("Gecersiz medya dosyasi yolu reddedildi. PublicUrl: {PublicUrl}", publicUrl);
            return;
        }

        var webRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"));
        var filePath = Path.GetFullPath(Path.Combine(webRoot, relativePath));

        if (!filePath.StartsWith(webRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("wwwroot disindaki medya dosyasi silme istegi reddedildi. PublicUrl: {PublicUrl}", publicUrl);
            return;
        }

        try
        {
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Medya dosyasi silinemedi. Dosya: {FilePath}", filePath);
        }
    }

    private async Task DeleteVideoWithRelationsAsync(Video video, bool saveChanges = true)
    {
        _context.Yorumlar.RemoveRange(await _context.Yorumlar.Where(y => y.VideoId == video.Id).ToListAsync());
        _context.Begenmeler.RemoveRange(await _context.Begenmeler.Where(b => b.VideoId == video.Id).ToListAsync());
        _context.GecmisListesi.RemoveRange(await _context.GecmisListesi.Where(g => g.VideoId == video.Id).ToListAsync());
        _context.DahaSonraIzleListesi.RemoveRange(await _context.DahaSonraIzleListesi.Where(d => d.VideoId == video.Id).ToListAsync());
        _context.KanalEtkilesimleri.RemoveRange(await _context.KanalEtkilesimleri.Where(e => e.VideoId == video.Id).ToListAsync());

        TryDeletePublicFile(video.VideoUrl);
        TryDeletePublicFile(video.KapakResmiUrl);

        _context.Videolar.Remove(video);

        if (saveChanges)
        {
            await _context.SaveChangesAsync();
        }
    }

    private async Task DeleteUserWithRelationsAsync(Kullanici user)
    {
        if (IsAdminEmail(user.Email))
        {
            return;
        }

        var videos = await _context.Videolar.Where(v => v.KullaniciId == user.Id).ToListAsync();
        foreach (var video in videos)
        {
            await DeleteVideoWithRelationsAsync(video, saveChanges: false);
        }

        _context.Abonelikler.RemoveRange(await _context.Abonelikler
            .Where(a => a.AboneOlanId == user.Id || a.AboneOlunanId == user.Id)
            .ToListAsync());
        _context.Yorumlar.RemoveRange(await _context.Yorumlar.Where(y => y.KullaniciId == user.Id).ToListAsync());
        _context.Begenmeler.RemoveRange(await _context.Begenmeler.Where(b => b.KullaniciId == user.Id).ToListAsync());
        _context.GecmisListesi.RemoveRange(await _context.GecmisListesi.Where(g => g.KullaniciId == user.Id).ToListAsync());
        _context.DahaSonraIzleListesi.RemoveRange(await _context.DahaSonraIzleListesi.Where(d => d.KullaniciId == user.Id).ToListAsync());

        _context.Kullanicilar.Remove(user);
        await _context.SaveChangesAsync();
    }
    public async Task<IActionResult> Index()
    {
        var videolar = await _context.Videolar
        .Include(v => v.Kullanici)
        .OrderByDescending(v => v.IzlenmeSayisi) // izlenme sayısı yüksek olanlar önce
        .Take(75) // maksimum 75 video
        .ToListAsync();

        if (videolar.Count == 0)
        {
            ViewBag.Message = "Henüz video yüklenmemiş.";
            return View(new List<Video>());
        }

        return View(videolar);
    }

    [HttpGet]
    public IActionResult MobilUygulamayiIndir()
    {
        try
        {
            var apkPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                "mobil",
                "app-debug.apk");

            if (!System.IO.File.Exists(apkPath))
            {
                TempData["DownloadError"] = "Mobil uygulama dosyasi su anda bulunamadi.";
                return RedirectToAction("Index");
            }

            return PhysicalFile(
                apkPath,
                "application/vnd.android.package-archive",
                "Loopin.apk",
                enableRangeProcessing: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mobil uygulama APK dosyasi indirilirken hata olustu.");
            TempData["DownloadError"] = "Mobil uygulama indirilemedi. Lutfen daha sonra tekrar deneyin.";
            return RedirectToAction("Index");
        }
    }

    [HttpGet]
    public IActionResult Giris()
    {
        PrepareSecurityCode("Giris");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> Giris(string Email, string Sifre, string GuvenlikKodu)
    {
        if (!ValidateSecurityCode("Giris", GuvenlikKodu))
        {
            ViewBag.ErrorMessage = "Güvenlik kodu hatalı.";
            PrepareSecurityCode("Giris");
            return View();
        }

        PrepareSecurityCode("Giris");

        Email = NormalizeEmail(Email);

        if (!IsValidEmail(Email) || string.IsNullOrWhiteSpace(Sifre))
        {
            ViewBag.ErrorMessage = "Gecerli e-posta ve sifre giriniz.";
            return View();
        }

        if (IsBlockedEmail(Email))
        {
            ViewBag.ErrorMessage = "Bu e-posta adresi engellenmistir. Giris yapilamaz.";
            return View();
        }

        var kullanici = await _context.Kullanicilar
            .FirstOrDefaultAsync(k => k.Email == Email);

        if (kullanici == null || !_passwordService.VerifyPassword(kullanici.Sifre, Sifre))
        {
            ViewBag.ErrorMessage = "Gecersiz e-posta veya sifre.";
            return View();
        }

        UpgradePasswordIfNeeded(kullanici, Sifre);

        if (!kullanici.EmailOnayli)
        {
            ViewBag.ErrorMessage = "Hesabınız doğrulanmamış. Lütfen e-posta adresinizi kontrol edin.";
            return View();
        }

        // Eğer admin değilse → direkt giriş
        if (!IsAdminEmail(kullanici.Email))
        {
            SignInUser(kullanici);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index", "Home");
        }

        // Eğer adminse → giriş doğrulama maili gönder
        var token = Guid.NewGuid().ToString();
        kullanici.EmailOnayToken = token;
        kullanici.EmailOnayExpire = DateTime.Now.AddMinutes(1);
        await _context.SaveChangesAsync();

        try
        {
            await _emailService.SendEmailAsync(
             Email,
             "🔐 Loopin Giriş Doğrulama",
             $@"
 <div style='font-family:Segoe UI, Arial; background:#0f0f1a; padding:30px;'>

 <div style='max-width:520px; margin:auto; background:#1a1a2e; padding:25px; border-radius:16px; box-shadow:0 10px 30px rgba(0,0,0,0.5);'>

 <h2 style='color:#c084fc; text-align:center; margin-bottom:20px;'>
 🔐 Giriş Doğrulama
 </h2>

 <p style='color:#e5e5e5; font-size:14px;'>
 Merhaba <b>{kullanici.KullaniciAdi}</b>,
 </p>

 <p style='color:#aaa; font-size:14px; line-height:1.6;'>
 Hesabına yeni bir giriş yapılmak isteniyor.
 Eğer bu işlem sana aitse aşağıdaki butona tıklayarak girişe izin verebilirsin.
 </p>

 <div style='text-align:center; margin:30px 0;'>
 <a href='https://loopin.fun/Home/GirisOnay?token={token}'
 style='background:linear-gradient(45deg,#22c55e,#16a34a);
 color:white;
 padding:12px 26px;
 border-radius:999px;
 text-decoration:none;
 font-weight:600;
 display:inline-block;'>
 ✅ Girişi Onayla
 </a>
 </div>

 <p style='color:#fbbf24; font-size:13px; text-align:center;'>
 ⏱ Bu bağlantı sadece <b>1 dakika</b> geçerlidir.
 </p>

 <hr style='border:none; border-top:1px solid #333; margin:20px 0;'>

 <p style='color:#ef4444; font-size:13px; text-align:center;'>
 ⚠ Eğer bu giriş sana ait değilse bu maili görmezden gel ve şifreni değiştir.
 </p>

 <p style='color:#777; font-size:12px; text-align:center;'>
 Güvenliğin bizim için önemli 💜
 </p>

 </div>
 </div>
 "
            );

            ViewBag.SuccessMessage = "Admin giriş doğrulama maili gönderildi. Lütfen e-postanı kontrol et.";
            return View(); // Admin için mail gönderildikten sonra aynı sayfada kalır
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin giris dogrulama maili gonderilemedi. Email: {Email}", Email);
            ViewBag.ErrorMessage2 = "Mail gonderilemedi. Lutfen daha sonra tekrar deneyin.";
            return View();
        }
    }

    [HttpGet]
    public async Task<IActionResult> GirisOnay(string token)
    {
        var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.EmailOnayToken == token);
        if (user == null) return NotFound();

        if (user.EmailOnayExpire < DateTime.Now)
        {
            ViewBag.ErrorMessage1 = "Giriş doğrulama süresi doldu. Tekrar giriş yapın.";
            return View("Giris");
        }

        SignInUser(user);

        // Token temizle
        user.EmailOnayToken = null;
        user.EmailOnayExpire = null;
        await _context.SaveChangesAsync();

        return RedirectToAction("Index");
    }


    [HttpGet]
    public IActionResult Kayit()
    {
        PrepareSecurityCode("Kayit");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> Kayit(string KullaniciAdi, string Email, string Sifre, string GuvenlikKodu)
    {
        if (!ValidateSecurityCode("Kayit", GuvenlikKodu))
        {
            ViewBag.ErrorMessage = "Güvenlik kodu hatalı.";
            PrepareSecurityCode("Kayit");
            return View();
        }

        PrepareSecurityCode("Kayit");

        KullaniciAdi = (KullaniciAdi ?? "").Trim();
        Email = NormalizeEmail(Email);

        if (KullaniciAdi.Length < 3 || KullaniciAdi.Length > 40)
        {
            ViewBag.ErrorMessage = "Kullanici adi 3-40 karakter arasinda olmalidir.";
            return View();
        }

        if (!IsValidEmail(Email))
        {
            ViewBag.ErrorMessage = "Gecerli bir e-posta adresi giriniz.";
            return View();
        }

        if (!IsStrongEnoughPassword(Sifre))
        {
            ViewBag.ErrorMessage = "Sifre en az 6, en fazla 64 karakter olmalidir.";
            return View();
        }

        // 🚫 Engellenmiş e‑posta kontrolü
        if (IsBlockedEmail(Email))
        {
            ViewBag.ErrorMessage = "Bu e-posta adresi engellenmistir. Kayit yapilamaz.";
            return View();
        }


        // 🚫 Aynı e‑posta ile daha önce kayıt yapılmış mı?
        var mevcutKullanici = await _context.Kullanicilar
        .FirstOrDefaultAsync(k => k.Email == Email);

        if (mevcutKullanici != null)
        {
            ViewBag.ErrorMessage = "Bu e-posta adresiyle zaten bir hesap mevcut.";
            return View();
        }

        var token = Guid.NewGuid().ToString();

        var yeniKullanici = new Kullanici
        {
            KullaniciAdi = KullaniciAdi,
            Email = Email,
            Sifre = _passwordService.HashPassword(Sifre),
            EmailOnayli = false,
            EmailOnayToken = token,
            EmailOnayExpire = DateTime.Now.AddMinutes(1)
        };

        await _context.Kullanicilar.AddAsync(yeniKullanici);
        await _context.SaveChangesAsync();
        try
        {
            await _emailService.SendEmailAsync(
           Email,
           "🎬 Loopin Hesap Onayı",
           $@"
 <div style='font-family:Segoe UI, Arial; background:#0f0f1a; padding:30px;'>

 <div style='max-width:520px; margin:auto; background:#1a1a2e; padding:25px; border-radius:16px; box-shadow:0 10px 30px rgba(0,0,0,0.5);'>

 <h2 style='color:#c084fc; text-align:center; margin-bottom:20px;'>
 🎬 Loopin Hesap Aktivasyonu
 </h2>

 <p style='color:#e5e5e5; font-size:14px;'>
 Merhaba <b>{KullaniciAdi}</b>,
 </p>

 <p style='color:#aaa; font-size:14px; line-height:1.6;'>
 Loopin hesabını aktifleştirmek için aşağıdaki butona tıklaman yeterli.
 </p>

 <div style='text-align:center; margin:30px 0;'>
 <a href='https://loopin.fun/Home/Onayla?token={token}'
 style='background:linear-gradient(45deg,#9333ea,#6366f1);
 color:white;
 padding:12px 26px;
 border-radius:999px;
 text-decoration:none;
 font-weight:600;
 display:inline-block;'>
 ✅ Hesabımı Onayla
 </a>
 </div>

 <p style='color:#fbbf24; font-size:13px; text-align:center;'>
 ⏱ Bu bağlantı sadece <b>1 dakika</b> geçerlidir.
 </p>

 <hr style='border:none; border-top:1px solid #333; margin:20px 0;'>

 <p style='color:#888; font-size:12px; text-align:center;'>
 Buton çalışmazsa aşağıdaki linki kullan:
 </p>

 <p style='color:#777; font-size:11px; word-break:break-all; text-align:center;'>
 https://loopin.fun/Home/Onayla?token={token}
 </p>

 <p style='color:#666; font-size:12px; text-align:center; margin-top:10px;'>
 Eğer bu işlemi sen yapmadıysan bu e-postayı yok sayabilirsin.
 </p>

 </div>
 </div>
 "
           );
            ViewBag.SuccessMessage = "Onay maili gönderildi. Lütfen e-postanı kontrol et.";
            return View(); // ✅ Başarılıysa aynı sayfada kal
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hesap onay maili gonderilemedi. Email: {Email}", Email);
            ViewBag.ErrorMessage = "Mail gonderilemedi. Lutfen daha sonra tekrar deneyin.";
            return View(); // ❌ Hata varsa yönlendirme yok, aynı sayfada kal
        }






    }






    [HttpGet]
    public async Task<IActionResult> Onayla(string token)
    {
        var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.EmailOnayToken == token);
        if (user == null) return NotFound();

        if (user.EmailOnayExpire < DateTime.Now)
        {
            _context.Kullanicilar.Remove(user);
            await _context.SaveChangesAsync();
            ViewBag.ErrorMessage = "Onay süresi doldu. Lütfen tekrar kayıt olun.";
            return View("Onayla");
        }

        user.EmailOnayli = true;
        user.EmailOnayToken = null;
        user.EmailOnayExpire = null;
        await _context.SaveChangesAsync();

        ViewBag.SuccessMessage = "E-posta adresiniz başarıyla doğrulandı.";
        return View("Onayla");
    }

    [HttpGet]
    public async Task<IActionResult> AdminPanel(string searchTerm, DateTime? baslangicTarihi, DateTime? bitisTarihi)
    {
        var isAdmin = HttpContext.Session.GetString("IsAdmin");
        if (isAdmin != "true")
            return Unauthorized();

        try
        {
            var bitis = (bitisTarihi ?? DateTime.Today).Date;
            var baslangic = (baslangicTarihi ?? bitis.AddDays(-29)).Date;

            if (baslangic > bitis)
            {
                (baslangic, bitis) = (bitis, baslangic);
                ViewBag.AdminFilterMessage = "Tarih araligi duzeltilerek uygulandi.";
            }

            if ((bitis - baslangic).TotalDays > 364)
            {
                baslangic = bitis.AddDays(-364);
                ViewBag.AdminFilterMessage = "Grafik performansi icin en fazla 365 gun gosterilir.";
            }

            var bitisHaric = bitis.AddDays(1);
            var tumVideolar = await _context.Videolar.Include(v => v.Kullanici).ToListAsync();
            var tumKullanicilar = await _context.Kullanicilar.ToListAsync();
            var butunVideolar = tumVideolar.ToList();
            var butunKullanicilar = tumKullanicilar.ToList();
            searchTerm = (searchTerm ?? "").Trim();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                tumVideolar = tumVideolar
                    .Where(v => (v.Baslik ?? "").Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                        || (v.Kullanici?.KullaniciAdi ?? "").Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                tumKullanicilar = tumKullanicilar
                    .Where(u => (u.KullaniciAdi ?? "").Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                        || (u.Email ?? "").Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (!tumVideolar.Any() && !tumKullanicilar.Any())
                {
                    tumVideolar = butunVideolar;
                    tumKullanicilar = butunKullanicilar;
                    ViewBag.AdminSearchMessage = "Arama sonucu bulunamadi, tum kayitlar listeleniyor.";
                }
            }

            var kanalKullaniciIdleri = string.IsNullOrWhiteSpace(searchTerm)
                ? butunVideolar.Select(v => v.KullaniciId).ToHashSet()
                : tumVideolar.Select(v => v.KullaniciId)
                    .Concat(tumKullanicilar.Select(u => u.Id))
                    .ToHashSet();

            var kanalVideolari = butunVideolar
                .Where(v => v.Kullanici != null && kanalKullaniciIdleri.Contains(v.KullaniciId))
                .ToList();
            var kanalVideoIdleri = kanalVideolari.Select(v => v.Id).ToList();
            var etkilesimler = await _context.KanalEtkilesimleri
                .Where(e => kanalVideoIdleri.Contains(e.VideoId)
                    && e.Tarih >= baslangic
                    && e.Tarih < bitisHaric)
                .ToListAsync();
            var gunSayisi = (bitis - baslangic).Days + 1;

            var kanalIstatistikleri = kanalVideolari
                .GroupBy(v => v.Kullanici)
                .Select(g =>
                {
                    var videoIdleri = g.Select(v => v.Id).ToHashSet();
                    var kanalEtkilesimleri = etkilesimler
                        .Where(e => videoIdleri.Contains(e.VideoId))
                        .ToList();

                    return new ChannelStatsViewModel
                    {
                        KullaniciId = g.Key.Id,
                        KullaniciAdi = g.Key.KullaniciAdi ?? "Isimsiz Kanal",
                        Email = g.Key.Email,
                        VideoSayisi = g.Count(),
                        ToplamLike = kanalEtkilesimleri.Count(e => e.Tur == KanalEtkilesimi.Begeni),
                        ToplamGoruntulenme = kanalEtkilesimleri.Count(e => e.Tur == KanalEtkilesimi.Goruntulenme),
                        DahaSonraIzleSayisi = kanalEtkilesimleri.Count(e => e.Tur == KanalEtkilesimi.DahaSonraIzle),
                        GunlukIstatistikler = Enumerable.Range(0, gunSayisi)
                            .Select(i =>
                            {
                                var gun = baslangic.AddDays(i);
                                var gunEtkilesimleri = kanalEtkilesimleri
                                    .Where(e => e.Tarih.Date == gun)
                                    .ToList();

                                return new ChannelDailyStatsViewModel
                                {
                                    Tarih = gun,
                                    GoruntulenmeSayisi = gunEtkilesimleri.Count(e => e.Tur == KanalEtkilesimi.Goruntulenme),
                                    BegeniSayisi = gunEtkilesimleri.Count(e => e.Tur == KanalEtkilesimi.Begeni),
                                    DahaSonraIzleSayisi = gunEtkilesimleri.Count(e => e.Tur == KanalEtkilesimi.DahaSonraIzle)
                                };
                            })
                            .ToList()
                    };
                })
                .OrderByDescending(k => k.ToplamGoruntulenme)
                .ThenByDescending(k => k.VideoSayisi)
                .ToList();

            var model = new AdminPanelViewModel
            {
                Videolar = tumVideolar,
                Kullanicilar = tumKullanicilar,
                EngellenenEpostalar = _blockedEmails,
                KanalIstatistikleri = kanalIstatistikleri,
                BaslangicTarihi = baslangic,
                BitisTarihi = bitis
            };

            ViewBag.AdminEmails = _adminEmails.Select(NormalizeEmail).ToList();

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin panel yüklenirken hata oluştu.");
            ViewBag.AdminEmails = _adminEmails.Select(NormalizeEmail).ToList();
            ViewBag.AdminSearchMessage = "Admin panel yüklenirken bir sorun oluştu, boş liste gösteriliyor.";

            return View(new AdminPanelViewModel
            {
                EngellenenEpostalar = _blockedEmails,
                BaslangicTarihi = (baslangicTarihi ?? DateTime.Today.AddDays(-29)).Date,
                BitisTarihi = (bitisTarihi ?? DateTime.Today).Date
            });
        }
    }


    [HttpGet]
    public async Task<IActionResult> AdminRapor(string searchTerm, DateTime? baslangicTarihi, DateTime? bitisTarihi)
    {
        var isAdmin = HttpContext.Session.GetString("IsAdmin");
        if (isAdmin != "true")
            return Unauthorized();

        var bitis = (bitisTarihi ?? DateTime.Today).Date;
        var baslangic = (baslangicTarihi ?? bitis.AddDays(-29)).Date;

        if (baslangic > bitis)
        {
            (baslangic, bitis) = (bitis, baslangic);
        }

        if ((bitis - baslangic).TotalDays > 364)
        {
            baslangic = bitis.AddDays(-364);
        }

        var bitisHaric = bitis.AddDays(1);
        searchTerm = (searchTerm ?? "").Trim();

        var tumVideolar = await _context.Videolar
            .Include(v => v.Kullanici)
            .OrderByDescending(v => v.YuklenmeTarihi)
            .ToListAsync();

        var tumKullanicilar = await _context.Kullanicilar
            .OrderBy(u => u.KullaniciAdi)
            .ToListAsync();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            tumVideolar = tumVideolar
                .Where(v => (v.Baslik ?? "").Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                    || (v.Kullanici?.KullaniciAdi ?? "").Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                .ToList();

            tumKullanicilar = tumKullanicilar
                .Where(u => (u.KullaniciAdi ?? "").Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                    || (u.Email ?? "").Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var kanalKullaniciIdleri = tumVideolar.Select(v => v.KullaniciId)
            .Concat(tumKullanicilar.Select(u => u.Id))
            .ToHashSet();

        var kanalVideolari = await _context.Videolar
            .Include(v => v.Kullanici)
            .Where(v => kanalKullaniciIdleri.Contains(v.KullaniciId))
            .ToListAsync();

        var kanalVideoIdleri = kanalVideolari.Select(v => v.Id).ToList();
        var etkilesimler = await _context.KanalEtkilesimleri
            .Where(e => kanalVideoIdleri.Contains(e.VideoId)
                && e.Tarih >= baslangic
                && e.Tarih < bitisHaric)
            .ToListAsync();

        var gunSayisi = (bitis - baslangic).Days + 1;
        var kanalIstatistikleri = kanalVideolari
            .GroupBy(v => v.Kullanici)
            .Select(g =>
            {
                var videoIdleri = g.Select(v => v.Id).ToHashSet();
                var kanalEtkilesimleri = etkilesimler
                    .Where(e => videoIdleri.Contains(e.VideoId))
                    .ToList();

                return new ChannelStatsViewModel
                {
                    KullaniciId = g.Key.Id,
                    KullaniciAdi = g.Key.KullaniciAdi ?? "Isimsiz Kanal",
                    Email = g.Key.Email,
                    VideoSayisi = g.Count(),
                    ToplamLike = kanalEtkilesimleri.Count(e => e.Tur == KanalEtkilesimi.Begeni),
                    ToplamGoruntulenme = kanalEtkilesimleri.Count(e => e.Tur == KanalEtkilesimi.Goruntulenme),
                    DahaSonraIzleSayisi = kanalEtkilesimleri.Count(e => e.Tur == KanalEtkilesimi.DahaSonraIzle),
                    GunlukIstatistikler = Enumerable.Range(0, gunSayisi)
                        .Select(i =>
                        {
                            var gun = baslangic.AddDays(i);
                            var gunEtkilesimleri = kanalEtkilesimleri
                                .Where(e => e.Tarih.Date == gun)
                                .ToList();

                            return new ChannelDailyStatsViewModel
                            {
                                Tarih = gun,
                                GoruntulenmeSayisi = gunEtkilesimleri.Count(e => e.Tur == KanalEtkilesimi.Goruntulenme),
                                BegeniSayisi = gunEtkilesimleri.Count(e => e.Tur == KanalEtkilesimi.Begeni),
                                DahaSonraIzleSayisi = gunEtkilesimleri.Count(e => e.Tur == KanalEtkilesimi.DahaSonraIzle)
                            };
                        })
                        .ToList()
                };
            })
            .OrderByDescending(k => k.ToplamGoruntulenme)
            .ThenByDescending(k => k.ToplamLike)
            .ToList();

        return View(new AdminPanelViewModel
        {
            Videolar = tumVideolar,
            Kullanicilar = tumKullanicilar,
            EngellenenEpostalar = _blockedEmails,
            KanalIstatistikleri = kanalIstatistikleri,
            BaslangicTarihi = baslangic,
            BitisTarihi = bitis
        });
    }


    [HttpPost]
    public async Task<IActionResult> KullaniciSil(int id)
    {
        var isAdmin = HttpContext.Session.GetString("IsAdmin");
        if (isAdmin != "true") return Unauthorized();

        try
        {
            var user = await _context.Kullanicilar.FindAsync(id);
            if (user != null)
            {
                if (IsAdminEmail(user.Email))
                    return RedirectToAction("AdminPanel");

                await DeleteUserWithRelationsAsync(user);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kullanıcı silinirken hata oluştu. Kullanıcı id: {UserId}", id);
        }

        return RedirectToAction("AdminPanel");
    }
    [HttpPost]
    public async Task<IActionResult> EmailEngelle(string email)
    {
        var isAdmin = HttpContext.Session.GetString("IsAdmin");
        if (isAdmin != "true") return Unauthorized();

        if (IsAdminEmail(email))
            return RedirectToAction("AdminPanel");

        try
        {
        var normalizedEmail = NormalizeEmail(email);

        if (!_blockedEmails.Any(blocked => string.Equals(NormalizeEmail(blocked), normalizedEmail, StringComparison.OrdinalIgnoreCase)))
            _blockedEmails.Add(normalizedEmail);

        // İlgili kullanıcıyı da sistemden sil
        var user = await _context.Kullanicilar
            .FirstOrDefaultAsync(u => u.Email != null && u.Email.Trim().ToLower() == normalizedEmail.ToLower());
        if (user != null && !IsAdminEmail(user.Email))
        {
            await DeleteUserWithRelationsAsync(user);
        }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email engellenirken hata oluştu. Email: {Email}", email);
        }

        return RedirectToAction("AdminPanel");
    }
    [HttpPost]
    public IActionResult EmailEngelKaldir(string email)
    {
        var isAdmin = HttpContext.Session.GetString("IsAdmin");
        if (isAdmin != "true") return Unauthorized();

        try
        {
            var normalizedEmail = NormalizeEmail(email);
            var blockedEmail = _blockedEmails
                .FirstOrDefault(blocked => string.Equals(NormalizeEmail(blocked), normalizedEmail, StringComparison.OrdinalIgnoreCase));

            if (blockedEmail != null)
                _blockedEmails.Remove(blockedEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email engeli kaldırılırken hata oluştu. Email: {Email}", email);
        }

        return RedirectToAction("AdminPanel");
    }





    public IActionResult Cikis()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Index");
    }
    [HttpGet]
    public IActionResult Yukle()
    {
        var user = HttpContext.Session.GetString("UserEmail");

        if (user == null)
            return RedirectToAction("Giris", "Home");

        PrepareSecurityCode("Yukle");
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Yukle(Video model, IFormFile VideoDosya, IFormFile KapakResmi, string GuvenlikKodu)
    {
        var email = HttpContext.Session.GetString("UserEmail");
        if (email == null)
            return RedirectToAction("Giris", "Home");

        var user = await _context.Kullanicilar.FirstOrDefaultAsync(x => x.Email == email);
        if (user == null)
            return RedirectToAction("Giris", "Home");

        // 🎥 Video zorunlu
        if (!ValidateSecurityCode("Yukle", GuvenlikKodu))
        {
            ModelState.AddModelError("", "Güvenlik kodu hatalı.");
            PrepareSecurityCode("Yukle");
            return View(model);
        }

        PrepareSecurityCode("Yukle");

        model.Baslik = (model.Baslik ?? "").Trim();
        model.Aciklama = (model.Aciklama ?? "").Trim();

        if (model.Baslik.Length < 3 || model.Baslik.Length > 120)
        {
            ModelState.AddModelError("", "Video basligi 3-120 karakter arasinda olmalidir.");
            return View(model);
        }

        if (model.Aciklama.Length > 1000)
        {
            ModelState.AddModelError("", "Aciklama 1000 karakterden uzun olamaz.");
            return View(model);
        }

        if (VideoDosya == null || VideoDosya.Length == 0)
        {
            ModelState.AddModelError("", "Lütfen bir video seçin.");
            return View(model);
        }

        // 🎥 Video boyut kontrolü (1024MB)
        if (VideoDosya.Length > 1024 * 1024 * 1024)
        {
            ModelState.AddModelError("", "Video çok büyük. Lütfen videonun boyutunu düşürüp tekrar deneyin.");
            return View(model);
        }

        // 🎥 Video uzantı kontrolü
        var videoExt = Path.GetExtension(VideoDosya.FileName).ToLower();
        if (videoExt != ".mp4")
        {
            ModelState.AddModelError("", "Sadece MP4 formatında video yükleyebilirsiniz.");
            return View(model);
        }

        // 🖼️ Kapak kontrolü (opsiyonel)

        if (KapakResmi != null)
        {
            if (KapakResmi.Length > 5 * 1024 * 1024)
            {
                ModelState.AddModelError("", "Kapak resmi 5MB'den büyük olamaz.");
                return View(model);
            }

            var imageExt = Path.GetExtension(KapakResmi.FileName).ToLower();
            if (imageExt != ".jpg" && imageExt != ".jpeg" && imageExt != ".png")
            {
                ModelState.AddModelError("", "Kapak resmi JPG veya PNG olmalıdır.");
                return View(model);
            }
        }

        // 📌 Klasörler

        try{
        
        var videoFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/videos");
        var imageFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images");

        Directory.CreateDirectory(videoFolder);
        Directory.CreateDirectory(imageFolder);

        // 📌 Video kaydet
        var videoFileName = Guid.NewGuid() + videoExt;
        var videoPath = Path.Combine(videoFolder, videoFileName);

        using (var stream = new FileStream(videoPath, FileMode.Create))
        {
            await VideoDosya.CopyToAsync(stream);
        }

        model.VideoUrl = "/videos/" + videoFileName;

        // 📌 Kapak kaydet
        if (KapakResmi != null && KapakResmi.Length > 0)
        {
            var coverFileName = Guid.NewGuid() + Path.GetExtension(KapakResmi.FileName);
            var coverPath = Path.Combine(imageFolder, coverFileName);

            using (var stream = new FileStream(coverPath, FileMode.Create))
            {
                await KapakResmi.CopyToAsync(stream);
            }

            model.KapakResmiUrl = "/images/" + coverFileName;
        }

        // 📌 Metadata
        model.KullaniciId = user.Id;
        model.YuklenmeTarihi = DateTime.Now;

        _context.Videolar.Add(model);
        await _context.SaveChangesAsync();
        await _mobileNotificationService.CreateNewVideoNotificationsAsync(model);

        // ✅ başarı mesajı
        TempData["Success"] = "Video başarıyla yüklendi!";
        }
        catch(Exception ex)
        {
            TryDeletePublicFile(model.VideoUrl);
            TryDeletePublicFile(model.KapakResmiUrl);
            _logger.LogError(ex, "Web video yukleme sirasinda hata olustu. KullaniciId: {KullaniciId}", user.Id);
            TempData["Error"] = "Video yüklenirken bir hata oluştu. Lütfen tekrar deneyin.";
        }

        return RedirectToAction("Yukle");
    }




    public async Task<IActionResult> Izle(int id)
    {
        var video = await _context.Videolar
            .Include(v => v.Kullanici)
            .Include(v => v.VideoYorumlar)
                .ThenInclude(y => y.Kullanici)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (video == null)
            return NotFound();

        var email = HttpContext.Session.GetString("UserEmail");
        bool izlenmeArtir = false; // 👈 default olarak false

        if (!string.IsNullOrEmpty(email))
        {
            var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.Email == email);

            if (user != null)
            {
                var gecmisKaydi = await _context.GecmisListesi
                    .FirstOrDefaultAsync(g => g.KullaniciId == user.Id && g.VideoId == id);

                if (gecmisKaydi == null)
                {
                    // 📌 ilk kez izliyor → geçmişe ekle
                    _context.GecmisListesi.Add(new Gecmis
                    {
                        KullaniciId = user.Id,
                        VideoId = id,
                        IzlenmeTarihi = DateTime.Now
                    });

                    izlenmeArtir = true; // 👈 sadece giriş yapmış ve ilk kez izliyorsa artar
                }

                // abonelik + beğeni
                ViewBag.AbonelikVar = await _context.Abonelikler
                    .AnyAsync(a => a.AboneOlanId == user.Id && a.AboneOlunanId == video.KullaniciId);

                ViewBag.Begenildi = await _context.Begenmeler
                    .AnyAsync(b => b.VideoId == id && b.KullaniciId == user.Id);
            }
        }

        if (izlenmeArtir)
        {
            video.IzlenmeSayisi++;
            _context.KanalEtkilesimleri.Add(new KanalEtkilesimi
            {
                VideoId = id,
                Tur = KanalEtkilesimi.Goruntulenme,
                Tarih = DateTime.Now
            });
            await _context.SaveChangesAsync();
        }

        ViewBag.DigerVideolar = await _context.Videolar
            .Where(v => v.KullaniciId == video.KullaniciId && v.Id != id)
            .OrderByDescending(v => v.YuklenmeTarihi)
            .Take(5)
            .ToListAsync();

        return View(video);
    }



    [HttpPost]
    public async Task<IActionResult> Begen(int id)
    {
        var email = HttpContext.Session.GetString("UserEmail");
        if (email == null) return RedirectToAction("Giris");

        var user = await _context.Kullanicilar.FirstOrDefaultAsync(x => x.Email == email);
        var video = await _context.Videolar.FirstOrDefaultAsync(v => v.Id == id);

        if (video == null || user == null) return NotFound();

        var mevcut = await _context.Begenmeler
        .FirstOrDefaultAsync(b => b.VideoId == id && b.KullaniciId == user.Id);

        if (mevcut == null)
        {
            // beğeni ekle
            var tarih = DateTime.Now;
            _context.Begenmeler.Add(new Begenme { VideoId = id, KullaniciId = user.Id, Tarih = tarih });
            _context.KanalEtkilesimleri.Add(new KanalEtkilesimi
            {
                VideoId = id,
                Tur = KanalEtkilesimi.Begeni,
                Tarih = tarih
            });
            video.LikeSayisi++;
        }
        else
        {
            // beğeniyi kaldır
            _context.Begenmeler.Remove(mevcut);
            video.LikeSayisi = Math.Max(0, video.LikeSayisi - 1);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction("Izle", new { id });
    }
    [HttpPost]
    public async Task<IActionResult> BegenilerdenKaldir(int id)
    {
        var email = HttpContext.Session.GetString("UserEmail");
        if (email == null) return RedirectToAction("Giris");

        var user = await _context.Kullanicilar.FirstOrDefaultAsync(x => x.Email == email);
        if (user == null) return NotFound();

        var begeni = await _context.Begenmeler
        .FirstOrDefaultAsync(b => b.VideoId == id && b.KullaniciId == user.Id);

        if (begeni != null)
        {
            _context.Begenmeler.Remove(begeni);

            var video = await _context.Videolar.FirstOrDefaultAsync(v => v.Id == id);
            if (video != null && video.LikeSayisi > 0)
                video.LikeSayisi--;

            await _context.SaveChangesAsync();
        }

        return RedirectToAction("BegenilenVideolar");
    }

    [HttpPost]
    public async Task<IActionResult> YorumEkle(int id, string Icerik)
    {
        var email = HttpContext.Session.GetString("UserEmail");
        if (email == null) return RedirectToAction("Giris");

        var user = await _context.Kullanicilar.FirstOrDefaultAsync(x => x.Email == email);
        var video = await _context.Videolar.FirstOrDefaultAsync(v => v.Id == id);

        if (video == null || user == null) return NotFound();

        Icerik = (Icerik ?? "").Trim();
        if (Icerik.Length < 2 || Icerik.Length > 500)
        {
            TempData["Error"] = "Yorum 2-500 karakter arasinda olmalidir.";
            return RedirectToAction("Izle", new { id });
        }

        var yorum = new Yorum
        {
            Icerik = Icerik,
            KullaniciId = user.Id,
            VideoId = id
        };

        await _context.Yorumlar.AddAsync(yorum);
        await _context.SaveChangesAsync();

        return RedirectToAction("Izle", new { id });
    }
    public async Task<IActionResult> BenimVideolarim()
    {
        var email = HttpContext.Session.GetString("UserEmail");
        if (email == null) return RedirectToAction("Giris");

        var user = await _context.Kullanicilar
        .Include(u => u.Videolar)
        .FirstOrDefaultAsync(u => u.Email == email);

        if (user == null) return NotFound();

        return View(user.Videolar);
    }
    [HttpPost]
    public async Task<IActionResult> VideoSil(int id)
    {
        var email = HttpContext.Session.GetString("UserEmail");
        if (email == null) return RedirectToAction("Giris");

        var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null) return RedirectToAction("Giris");

        var video = await _context.Videolar.FirstOrDefaultAsync(v => v.Id == id);
        if (video == null) return NotFound();

        // 🔒 sadece kendi videosu
        if (video.KullaniciId != user.Id)
            return Unauthorized();

        await DeleteVideoWithRelationsAsync(video);

        return RedirectToAction("BenimVideolarim");
    }
    [HttpPost]
    public async Task<IActionResult> AdminVideoSil(int id)
    {
        var isAdmin = HttpContext.Session.GetString("IsAdmin");
        if (isAdmin != "true") return Unauthorized();

        var video = await _context.Videolar.FirstOrDefaultAsync(v => v.Id == id);
        if (video == null) return NotFound();

        await DeleteVideoWithRelationsAsync(video);

        return RedirectToAction("AdminPanel");
    }



    [HttpPost]
    public async Task<IActionResult> YorumSil(int id)
    {
        var email = HttpContext.Session.GetString("UserEmail");
        if (email == null) return RedirectToAction("Giris");

        var yorum = await _context.Yorumlar.Include(y => y.Kullanici).FirstOrDefaultAsync(y => y.Id == id);
        if (yorum == null) return NotFound();

        var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null) return RedirectToAction("Giris");

        if (yorum.KullaniciId != user.Id) return Unauthorized(); // sadece kendi yorumunu silebilir

        _context.Yorumlar.Remove(yorum);
        await _context.SaveChangesAsync();

        return RedirectToAction("Izle", new { id = yorum.VideoId });
    }
    public async Task<IActionResult> BegenilenVideolar()
    {
        var email = HttpContext.Session.GetString("UserEmail");
        if (email == null) return RedirectToAction("Giris");

        var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null) return NotFound();

        var videolar = await _context.Begenmeler
        .Where(b => b.KullaniciId == user.Id)
        .Include(b => b.Video)
        .ThenInclude(v => v.Kullanici)
        .Select(b => b.Video)
        .ToListAsync();

        return View(videolar);
    }
    [HttpGet]
    public async Task<IActionResult> Arama(string q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return View(new Tuple<List<Video>, List<Kullanici>>(
                new List<Video>(),
                new List<Kullanici>()
            ));

        q = q.Trim();

        // 🔥 1. ÖNCELİK: BAŞLAYANLAR
        var videoBaslayan = await _context.Videolar
            .Include(v => v.Kullanici)
            .Where(v =>
                v.Baslik.StartsWith(q) ||
                v.Aciklama.StartsWith(q))
            .ToListAsync();

        var kullaniciBaslayan = await _context.Kullanicilar
            .Where(u =>
                (u.KullaniciAdi ?? "").StartsWith(q) ||
                u.Email.StartsWith(q))
            .ToListAsync();

        // 🔥 2. İÇERENLER (fallback)
        var videoIceren = await _context.Videolar
            .Include(v => v.Kullanici)
            .Where(v =>
                v.Baslik.Contains(q) ||
                v.Aciklama.Contains(q))
            .ToListAsync();

        var kullaniciIceren = await _context.Kullanicilar
            .Where(u =>
                (u.KullaniciAdi ?? "").Contains(q) ||
                u.Email.Contains(q))
            .ToListAsync();

        // 🔥 3. birleştir + tekrarları kaldır
        var videoSonuc = videoBaslayan
            .Concat(videoIceren)
            .DistinctBy(v => v.Id)
            .ToList();

        var kullaniciSonuc = kullaniciBaslayan
            .Concat(kullaniciIceren)
            .DistinctBy(u => u.Id)
            .ToList();

        // abonelik kontrolü aynı
        var email = HttpContext.Session.GetString("UserEmail");
        if (email != null)
        {
            var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.Email == email);

            if (user != null)
            {
                var abonelikler = await _context.Abonelikler
                    .Where(a => a.AboneOlanId == user.Id)
                    .Select(a => a.AboneOlunanId)
                    .ToListAsync();

                ViewBag.Abonelikler = abonelikler;
            }
        }

        return View(new Tuple<List<Video>, List<Kullanici>>(videoSonuc, kullaniciSonuc));
    }
    public async Task<IActionResult> Trendler()
    {
        var trendVideolar = await _context.Videolar
            .Include(v => v.Kullanici)
            .Where(v => v.IzlenmeSayisi > 0) // en az izlenenler
            .OrderByDescending(v =>
                (v.LikeSayisi * 3) + v.IzlenmeSayisi // trend skoru
            )
            .Take(75)
            .ToListAsync();

        return View(trendVideolar);
    }
    [HttpPost]
    public async Task<IActionResult> AboneOl(int id) // id = abone olunacak kullanıcı
    {
        var email = HttpContext.Session.GetString("UserEmail");
        if (email == null) return RedirectToAction("Giris");

        var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null) return NotFound();

        var mevcut = await _context.Abonelikler
        .FirstOrDefaultAsync(a => a.AboneOlanId == user.Id && a.AboneOlunanId == id);

        if (mevcut == null)
        {
            _context.Abonelikler.Add(new Abonelik { AboneOlanId = user.Id, AboneOlunanId = id });
            await _context.SaveChangesAsync();
        }

        return RedirectToAction("AbonelikVideolari");// abone olunca direkt abonelik videolarına yönlendir
    }
    [HttpPost]
    public async Task<IActionResult> AboneliktenCik(int id)
    {
        var email = HttpContext.Session.GetString("UserEmail");
        if (email == null) return RedirectToAction("Giris");

        var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null) return NotFound();

        var abonelik = await _context.Abonelikler
        .FirstOrDefaultAsync(a => a.AboneOlanId == user.Id && a.AboneOlunanId == id);

        if (abonelik != null)
        {
            _context.Abonelikler.Remove(abonelik);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction("AbonelikVideolari");// abonelikten çıkınca direkt abonelik videolarına yönlendir
    }
    public async Task<IActionResult> AbonelikVideolari()
    {
        var email = HttpContext.Session.GetString("UserEmail");
        if (email == null) return RedirectToAction("Giris");

        var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null) return NotFound();

        // Abone olunan kullanıcılar
        var abonelikler = await _context.Abonelikler
        .Where(a => a.AboneOlanId == user.Id)
        .Include(a => a.AboneOlunan)
        .ToListAsync();

        // Abone olunan kullanıcıların videoları
        var videolar = await _context.Videolar
        .Include(v => v.Kullanici)
        .Where(v => abonelikler.Select(a => a.AboneOlunanId).Contains(v.KullaniciId))
        .ToListAsync();

        ViewBag.Abonelikler = abonelikler;

        return View(videolar);
    }



    [HttpPost]
    public async Task<IActionResult> AbonelikToggle(int id)
    {
        var email = HttpContext.Session.GetString("UserEmail");
        if (email == null) return RedirectToAction("Giris");

        var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null) return NotFound();

        var mevcut = await _context.Abonelikler
        .FirstOrDefaultAsync(a => a.AboneOlanId == user.Id && a.AboneOlunanId == id);

        if (mevcut == null)
        {
            // abone ol
            _context.Abonelikler.Add(new Abonelik { AboneOlanId = user.Id, AboneOlunanId = id });
        }
        else
        {
            // abonelikten çık
            _context.Abonelikler.Remove(mevcut);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction("AbonelikVideolari");// abonelikten çıkınca direkt abonelik videolarına yönlendir
    }
    public async Task<IActionResult> Kullanici(int id, string filter)
    {
        var kullanici = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.Id == id);
        if (kullanici == null) return NotFound();

        var videolarQuery = _context.Videolar
            .Where(v => v.KullaniciId == id);

        // 🔥 Filtreleme
        switch (filter)
        {
            case "popular":
                videolarQuery = videolarQuery.OrderByDescending(v => v.IzlenmeSayisi);
                break;
            case "oldest":
                videolarQuery = videolarQuery.OrderBy(v => v.YuklenmeTarihi);
                break;
            default: // newest
                videolarQuery = videolarQuery.OrderByDescending(v => v.YuklenmeTarihi);
                break;
        }

        var videolar = await videolarQuery.ToListAsync();

        var aboneSayisi = await _context.Abonelikler.CountAsync(a => a.AboneOlunanId == id);
        ViewBag.AboneSayisi = aboneSayisi;
        ViewBag.Filter = filter; // seçili filtreyi view’a göndermek için

        var email = HttpContext.Session.GetString("UserEmail");
        if (email != null)
        {
            var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.Email == email);
            if (user != null)
            {
                var abonelik = await _context.Abonelikler
                    .FirstOrDefaultAsync(a => a.AboneOlanId == user.Id && a.AboneOlunanId == id);

                ViewBag.AbonelikVar = abonelik != null;
            }
        }

        var model = new Tuple<Kullanici, List<Video>>(kullanici, videolar);
        return View(model);
    }

    public async Task<IActionResult> Gecmis()
    {
        var email = HttpContext.Session.GetString("UserEmail");
        if (email == null) return RedirectToAction("Giris");

        var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null) return NotFound();

        var gecmis = await _context.GecmisListesi
        .Where(g => g.KullaniciId == user.Id)
        .Include(g => g.Video)
        .ThenInclude(v => v.Kullanici)
        .OrderByDescending(g => g.IzlenmeTarihi)
        .Select(g => g.Video)
        .ToListAsync();

        return View(gecmis);
    }
    public async Task<IActionResult> DahaSonraIzle()
    {
        var email = HttpContext.Session.GetString("UserEmail");
        if (email == null) return RedirectToAction("Giris");

        var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null) return NotFound();

        var liste = await _context.DahaSonraIzleListesi
        .Where(d => d.KullaniciId == user.Id)
        .Include(d => d.Video)
        .ThenInclude(v => v.Kullanici)
        .OrderByDescending(d => d.EklenmeTarihi)
        .Select(d => d.Video)
        .ToListAsync();

        return View(liste);
    }

    [HttpPost]
    public async Task<IActionResult> DahaSonraEkle(int id)
    {
        var email = HttpContext.Session.GetString("UserEmail");
        if (email == null) return RedirectToAction("Giris");

        var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null) return NotFound();

        var mevcut = await _context.DahaSonraIzleListesi
        .FirstOrDefaultAsync(d => d.KullaniciId == user.Id && d.VideoId == id);

        if (mevcut == null)
        {
            var tarih = DateTime.Now;
            _context.DahaSonraIzleListesi.Add(new DahaSonraIzle
            {
                KullaniciId = user.Id,
                VideoId = id,
                EklenmeTarihi = tarih
            });
            _context.KanalEtkilesimleri.Add(new KanalEtkilesimi
            {
                VideoId = id,
                Tur = KanalEtkilesimi.DahaSonraIzle,
                Tarih = tarih
            });
            await _context.SaveChangesAsync();
        }

        return RedirectToAction("DahaSonraIzle");
    }

    [HttpPost]
    public async Task<IActionResult> DahaSonraSil(int id)
    {
        var email = HttpContext.Session.GetString("UserEmail");
        if (email == null) return RedirectToAction("Giris");

        var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null) return NotFound();

        var kayit = await _context.DahaSonraIzleListesi
        .FirstOrDefaultAsync(d => d.KullaniciId == user.Id && d.VideoId == id);

        if (kayit != null)
        {
            _context.DahaSonraIzleListesi.Remove(kayit);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction("DahaSonraIzle");
    }

    [HttpPost]
    public async Task<IActionResult> GecmisTemizle()
    {
        var email = HttpContext.Session.GetString("UserEmail");
        if (email == null) return RedirectToAction("Giris");

        var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null) return NotFound();

        var gecmisKayitlari = _context.GecmisListesi.Where(g => g.KullaniciId == user.Id);
        _context.GecmisListesi.RemoveRange(gecmisKayitlari);
        await _context.SaveChangesAsync();

        return RedirectToAction("Gecmis");
    }
    [HttpPost]
    public async Task<IActionResult> GecmisSil(int id)
    {
        var email = HttpContext.Session.GetString("UserEmail");
        if (email == null) return RedirectToAction("Giris");

        var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null) return NotFound();

        var kayit = await _context.GecmisListesi
        .FirstOrDefaultAsync(g => g.KullaniciId == user.Id && g.VideoId == id);

        if (kayit != null)
        {
            _context.GecmisListesi.Remove(kayit);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction("Gecmis");
    }
    [HttpGet]
    public IActionResult SifreDegistir()
    {
        var email = HttpContext.Session.GetString("UserEmail");
        if (email == null)
            return RedirectToAction("Giris", "Home");

        PrepareSecurityCode("SifreDegistir");
        return View(); // Şifre değiştirme formunu gösterecek
    }

    [HttpPost]
    public async Task<IActionResult> SifreDegistir(string EskiSifre, string YeniSifre, string GuvenlikKodu)
    {
        var email = HttpContext.Session.GetString("UserEmail");
        if (email == null)
            return RedirectToAction("Giris", "Home");

        var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null) return NotFound();

        if (!ValidateSecurityCode("SifreDegistir", GuvenlikKodu))
        {
            ViewBag.ErrorMessage = "Güvenlik kodu hatalı.";
            PrepareSecurityCode("SifreDegistir");
            return View();
        }

        PrepareSecurityCode("SifreDegistir");

        if (!_passwordService.VerifyPassword(user.Sifre, EskiSifre))
        {
            ViewBag.ErrorMessage = "Eski şifre yanlış.";
            return View();
        }

        if (!IsStrongEnoughPassword(YeniSifre))
        {
            ViewBag.ErrorMessage = "Yeni sifre en az 6, en fazla 64 karakter olmalidir.";
            return View();
        }

        user.Sifre = _passwordService.HashPassword(YeniSifre);
        await _context.SaveChangesAsync();

        ViewBag.SuccessMessage = "Şifre başarıyla değiştirildi.";
        return View();
    }

    [HttpGet]
    public IActionResult SifremiUnuttum()
    {
        PrepareSecurityCode("SifremiUnuttum");
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> SifremiUnuttum(string Email, string GuvenlikKodu)
    {
        if (!ValidateSecurityCode("SifremiUnuttum", GuvenlikKodu))
        {
            ViewBag.ErrorMessage = "Güvenlik kodu hatalı.";
            PrepareSecurityCode("SifremiUnuttum");
            return View();
        }

        PrepareSecurityCode("SifremiUnuttum");

        Email = NormalizeEmail(Email);

        if (!IsValidEmail(Email))
        {
            ViewBag.ErrorMessage = "Gecerli bir e-posta adresi giriniz.";
            return View();
        }

        var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.Email == Email);
        if (user == null)
        {
            ViewBag.ErrorMessage = "Bu e-posta sistemde kayıtlı değil.";
            return View();
        }

        // 6 haneli kod üret
        var code = new Random().Next(100000, 999999).ToString();

        // Kod ve geçerlilik süresi DB’ye kaydedilir
        user.ResetCode = code;
        user.ResetCodeExpire = DateTime.Now.AddMinutes(10);
        await _context.SaveChangesAsync();
        try
        {
            await _emailService.SendEmailAsync(
            Email,
            "🔐 Loopin Şifre Sıfırlama Kodu",
            $@"
 <div style='font-family:Segoe UI, sans-serif; background:#0f0f1a; padding:30px; color:#fff;'>

 <div style='max-width:500px; margin:auto; background:#1a1a2e; border-radius:20px; padding:30px; box-shadow:0 10px 40px rgba(0,0,0,0.5);'>

 <h2 style='text-align:center; color:#c084fc;'>🔐 Şifre Sıfırlama</h2>

 <p>Merhaba <b>{user.KullaniciAdi}</b>,</p>

 <p>Şifreni sıfırlamak için aşağıdaki kodu kullanabilirsin:</p>

 <div style='text-align:center; margin:25px 0;'>
 <span style='font-size:28px; letter-spacing:5px; background:#0f0f1a; padding:12px 25px; border-radius:12px; border:1px solid #6c63ff;'>
 {code}
 </span>
 </div>

 <p style='font-size:13px; color:#aaa; text-align:center;'>
 ⏳ Bu kod <b>10 dakika</b> boyunca geçerlidir.
 </p>

 <hr style='border:none; border-top:1px solid #333; margin:20px 0;'>

 <p style='font-size:12px; color:#888; text-align:center;'>
 Eğer bu isteği sen yapmadıysan bu maili dikkate alma.
 </p>

 <p style='text-align:center; margin-top:20px; color:#a855f7; font-size:13px;'>
 🎬 Loopin - Modern Video Platform
 </p>

 </div>

 </div>
 "
           ); return RedirectToAction("KodDogrula", new { email = Email });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sifre sifirlama maili gonderilemedi. Email: {Email}", Email);
            ViewBag.ErrorMessage = "Mail gonderilemedi. Lutfen daha sonra tekrar deneyin.";
            return View();
        }

        // ✅ Mail gönderme

    }

    [HttpGet]
    public IActionResult KodDogrula(string email)
    {
        ViewBag.Email = email;
        PrepareSecurityCode("KodDogrula");
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> KodDogrula(string email, string code, string GuvenlikKodu)
    {
        if (!ValidateSecurityCode("KodDogrula", GuvenlikKodu))
        {
            ViewBag.Email = email;
            ViewBag.ErrorMessage = "Güvenlik kodu hatalı.";
            PrepareSecurityCode("KodDogrula");
            return View();
        }

        PrepareSecurityCode("KodDogrula");

        var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null) return NotFound();

        if (user.ResetCode != code || user.ResetCodeExpire < DateTime.Now)
        {
            ViewBag.ErrorMessage = "Kod geçersiz veya süresi dolmuş.";
            return View();
        }

        // Kod doğru → yeni şifre formuna yönlendir
        return RedirectToAction("YeniSifreBelirle", new { email = email });
    }

    [HttpGet]
    public IActionResult YeniSifreBelirle(string email)
    {
        ViewBag.Email = email;
        PrepareSecurityCode("YeniSifreBelirle");
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> YeniSifreBelirle(string email, string YeniSifre, string GuvenlikKodu)
    {
        if (!ValidateSecurityCode("YeniSifreBelirle", GuvenlikKodu))
        {
            ViewBag.Email = email;
            ViewBag.ErrorMessage = "Güvenlik kodu hatalı.";
            PrepareSecurityCode("YeniSifreBelirle");
            return View();
        }

        PrepareSecurityCode("YeniSifreBelirle");

        if (!IsStrongEnoughPassword(YeniSifre))
        {
            ViewBag.Email = email;
            ViewBag.ErrorMessage = "Yeni sifre en az 6, en fazla 64 karakter olmalidir.";
            return View();
        }

        var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null) return NotFound();

        user.Sifre = _passwordService.HashPassword(YeniSifre);
        user.ResetCode = null;
        user.ResetCodeExpire = null;
        await _context.SaveChangesAsync();

        ViewBag.SuccessMessage = "Şifreniz başarıyla güncellendi.";
        return RedirectToAction("Giris");
    }


    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }
}

