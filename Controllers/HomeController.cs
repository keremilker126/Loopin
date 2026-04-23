using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Loopin.Models;
using Loopin.Data;
using Microsoft.EntityFrameworkCore;
using Loopin.Services;

namespace Loopin.Controllers;

public class HomeController : Controller
{
    // HomeController içinde
    public static List<string> _blockedEmails = new List<string>();

    private readonly ILogger<HomeController> _logger;
    private readonly AppDbContext _context;
    private readonly EmailService _emailService;
    private readonly List<string> _adminEmails = new List<string>
    {
        "keremilker56@gmail.com",
        "keremilker126@gmail.com",
        "mehmedkaan46@gmail.com"
    };
    public HomeController(ILogger<HomeController> logger, AppDbContext context, EmailService emailService)
    {
        _logger = logger;
        _context = context;
        _emailService = emailService;
    }
    public async Task<IActionResult> Index()
    {
        var videolar = await _context.Videolar
            .Include(v => v.Kullanici)
            .OrderByDescending(v => v.IzlenmeSayisi) // izlenme sayısı yüksek olanlar önce
            .Take(150)                               // maksimum 150 video
            .ToListAsync();

        if (videolar.Count == 0)
        {
            ViewBag.Message = "Henüz video yüklenmemiş.";
            return View(new List<Video>());
        }

        return View(videolar);
    }

    [HttpGet]
    public IActionResult Giris()
    {
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> Giris(string Email, string Sifre)
    {
        var kullanici = await _context.Kullanicilar
            .FirstOrDefaultAsync(k => k.Email == Email && k.Sifre == Sifre);

        if (kullanici == null)
        {
            ViewBag.ErrorMessage = "Geçersiz e-posta veya şifre.";
            return View();
        }

        if (!kullanici.EmailOnayli)
        {
            ViewBag.ErrorMessage = "Hesabınız doğrulanmamış. Lütfen e-posta adresinizi kontrol edin.";
            return View();
        }

        // ✅ Giriş doğrulama token üret
        var token = Guid.NewGuid().ToString();
        kullanici.EmailOnayToken = token;
        kullanici.EmailOnayExpire = DateTime.Now.AddMinutes(1);
        await _context.SaveChangesAsync();

        // Mail gönder
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
                <a href='http://localhost:5144/Home/GirisOnay?token={token}'
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

        ViewBag.InfoMessage = "Giriş doğrulama maili gönderildi. Lütfen e-postanı kontrol et.";
        return View();
    }
    [HttpGet]
    public async Task<IActionResult> GirisOnay(string token)
    {
        var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.EmailOnayToken == token);
        if (user == null) return NotFound();

        if (user.EmailOnayExpire < DateTime.Now)
        {
            ViewBag.ErrorMessage = "Giriş doğrulama süresi doldu. Tekrar giriş yapın.";
            return View("Giris");
        }

        // Session bilgilerini ayarla
        HttpContext.Session.SetString("UserEmail", user.Email);
        HttpContext.Session.SetString("UserName", user.KullaniciAdi);
        HttpContext.Session.SetInt32("UserId", user.Id);

        if (_adminEmails.Contains(user.Email))
            HttpContext.Session.SetString("IsAdmin", "true");
        else
            HttpContext.Session.SetString("IsAdmin", "false");

        // Token temizle
        user.EmailOnayToken = null;
        user.EmailOnayExpire = null;
        await _context.SaveChangesAsync();

        return RedirectToAction("Index");
    }


    [HttpGet]
    public IActionResult Kayit()
    {
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> Kayit(string KullaniciAdi, string Email, string Sifre)
    {
        // 🚫 Engellenmiş e‑posta kontrolü
        if (_blockedEmails.Contains(Email))
        {
            ViewBag.ErrorMessage = "Bu e-posta adresi engellenmiştir. Kayıt yapılamaz.";
            return View();
        }

        var token = Guid.NewGuid().ToString();

        var yeniKullanici = new Kullanici
        {
            KullaniciAdi = KullaniciAdi,
            Email = Email,
            Sifre = Sifre,
            EmailOnayli = false,
            EmailOnayToken = token,
            EmailOnayExpire = DateTime.Now.AddMinutes(1)
        };

        await _context.Kullanicilar.AddAsync(yeniKullanici);
        await _context.SaveChangesAsync();

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
                <a href='http://localhost:5144/Home/Onayla?token={token}'
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
                http://localhost:5144/Home/Onayla?token={token}
            </p>

            <p style='color:#666; font-size:12px; text-align:center; margin-top:10px;'>
                Eğer bu işlemi sen yapmadıysan bu e-postayı yok sayabilirsin.
            </p>

        </div>
    </div>
    "
);

        return RedirectToAction("Index");
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
    public async Task<IActionResult> AdminPanel(string searchTerm)
    {
        var isAdmin = HttpContext.Session.GetString("IsAdmin");
        if (isAdmin != "true")
            return Unauthorized();

        var tumVideolar = await _context.Videolar.Include(v => v.Kullanici).ToListAsync();
        var tumKullanicilar = await _context.Kullanicilar.ToListAsync();

        if (!string.IsNullOrEmpty(searchTerm))
        {
            tumVideolar = tumVideolar
                .Where(v => v.Baslik.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                         || v.Kullanici.KullaniciAdi.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                .ToList();

            tumKullanicilar = tumKullanicilar
                .Where(u => u.KullaniciAdi.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                         || u.Email.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Modeli genişletelim: videolar, kullanıcılar ve engellenen e‑postalar
        var model = new Tuple<List<Video>, List<Kullanici>, List<string>>(tumVideolar, tumKullanicilar, _blockedEmails);
        return View(model);
    }


    [HttpPost]
    public async Task<IActionResult> KullaniciSil(int id)
    {
        var isAdmin = HttpContext.Session.GetString("IsAdmin");
        if (isAdmin != "true") return Unauthorized();

        var user = await _context.Kullanicilar.FindAsync(id);
        if (user != null)
        {
            _context.Kullanicilar.Remove(user);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction("AdminPanel");
    }
    [HttpPost]
    public IActionResult EmailEngelle(string email)
    {
        var isAdmin = HttpContext.Session.GetString("IsAdmin");
        if (isAdmin != "true") return Unauthorized();

        if (!_blockedEmails.Contains(email))
            _blockedEmails.Add(email);

        // İlgili kullanıcıyı da sistemden sil
        var user = _context.Kullanicilar.FirstOrDefault(u => u.Email == email);
        if (user != null)
        {
            _context.Kullanicilar.Remove(user);
            _context.SaveChanges();
        }

        return RedirectToAction("AdminPanel");
    }
    [HttpPost]
    public IActionResult EmailEngelKaldir(string email)
    {
        var isAdmin = HttpContext.Session.GetString("IsAdmin");
        if (isAdmin != "true") return Unauthorized();

        if (_blockedEmails.Contains(email))
            _blockedEmails.Remove(email);

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

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Yukle(Video model, IFormFile VideoDosya, IFormFile KapakResmi)
    {
        var email = HttpContext.Session.GetString("UserEmail");
        if (email == null)
            return RedirectToAction("Giris", "Home");

        var user = await _context.Kullanicilar.FirstOrDefaultAsync(x => x.Email == email);
        if (user == null)
            return RedirectToAction("Giris", "Home");

        // 📌 Kapak resmi yükleme
        if (KapakResmi != null && KapakResmi.Length > 0)
        {
            var coverFileName = Guid.NewGuid() + Path.GetExtension(KapakResmi.FileName);
            var coverFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images");

            if (!Directory.Exists(coverFolderPath))
                Directory.CreateDirectory(coverFolderPath);

            var coverPath = Path.Combine(coverFolderPath, coverFileName);

            using (var stream = new FileStream(
                coverPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true))
            {
                await KapakResmi.CopyToAsync(stream);
            }

            model.KapakResmiUrl = "/images/" + coverFileName;
        }

        // 📌 Video yükleme
        if (VideoDosya != null && VideoDosya.Length > 0)
        {
            var fileName = Guid.NewGuid() + Path.GetExtension(VideoDosya.FileName);
            var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/videos");

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            var path = Path.Combine(folderPath, fileName);

            using (var stream = new FileStream(
                path,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true))
            {
                await VideoDosya.CopyToAsync(stream);
            }

            model.VideoUrl = "/videos/" + fileName;
        }

        // 📌 Metadata
        model.KullaniciId = user.Id;
        model.YuklenmeTarihi = DateTime.Now;

        _context.Videolar.Add(model);
        await _context.SaveChangesAsync();

        return RedirectToAction("Index", "Home");
    }



    public async Task<IActionResult> Izle(int id)
    {
        // 1. Videoyu getir (ilişkili kullanıcı ve yorumlarla birlikte)
        var video = await _context.Videolar
            .Include(v => v.Kullanici)
            .Include(v => v.VideoYorumlar)
                .ThenInclude(y => y.Kullanici)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (video == null)
            return NotFound();

        // 2. Abone sayısını hesapla
        video.AboneSayisi = await _context.Abonelikler
            .CountAsync(a => a.AboneOlunanId == video.KullaniciId);

        // 3. İzlenme sayısını artır
        video.IzlenmeSayisi++;
        await _context.SaveChangesAsync();

        // 4. Kullanıcı giriş yaptıysa geçmiş ve abonelik/begenme kontrolü
        var email = HttpContext.Session.GetString("UserEmail");
        if (!string.IsNullOrEmpty(email))
        {
            var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.Email == email);
            if (user != null)
            {
                // Geçmişe ekle (yoksa ekle)
                var mevcutGecmis = await _context.GecmisListesi
                    .FirstOrDefaultAsync(g => g.KullaniciId == user.Id && g.VideoId == id);

                if (mevcutGecmis == null)
                {
                    _context.GecmisListesi.Add(new Gecmis
                    {
                        KullaniciId = user.Id,
                        VideoId = id,
                        IzlenmeTarihi = DateTime.Now
                    });
                    await _context.SaveChangesAsync();
                }

                // Abonelik durumu
                ViewBag.AbonelikVar = await _context.Abonelikler
                    .AnyAsync(a => a.AboneOlanId == user.Id && a.AboneOlunanId == video.KullaniciId);

                // Beğeni durumu
                ViewBag.Begenildi = await _context.Begenmeler
                    .AnyAsync(b => b.VideoId == id && b.KullaniciId == user.Id);
            }
        }

        // 5. Yan panel için aynı yapımcıya ait diğer videolar
        ViewBag.DigerVideolar = await _context.Videolar
            .Where(v => v.KullaniciId == video.KullaniciId && v.Id != id)
            .OrderByDescending(v => v.YuklenmeTarihi)
            .Take(5)
            .ToListAsync();

        // 6. View'e video nesnesini gönder
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
            _context.Begenmeler.Add(new Begenme { VideoId = id, KullaniciId = user.Id });
            video.LikeSayisi++;
        }
        else
        {
            // beğeniyi kaldır
            _context.Begenmeler.Remove(mevcut);
            video.LikeSayisi--;
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

        var video = await _context.Videolar.FirstOrDefaultAsync(v => v.Id == id);
        if (video == null) return NotFound();

        // sadece kendi videosunu silebilsin
        var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.Email == email);
        if (video.KullaniciId != user.Id) return Unauthorized();

        _context.Videolar.Remove(video);
        await _context.SaveChangesAsync();

        return RedirectToAction("BenimVideolarim");
    }
    [HttpPost]
    public async Task<IActionResult> YorumSil(int id)
    {
        var email = HttpContext.Session.GetString("UserEmail");
        if (email == null) return RedirectToAction("Giris");

        var yorum = await _context.Yorumlar.Include(y => y.Kullanici).FirstOrDefaultAsync(y => y.Id == id);
        if (yorum == null) return NotFound();

        var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.Email == email);
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
            return View(new Tuple<List<Video>, List<Kullanici>>(new List<Video>(), new List<Kullanici>()));

        var videoSonuc = await _context.Videolar
            .Include(v => v.Kullanici)
            .Where(v => v.Baslik.Contains(q) || v.Aciklama.Contains(q))
            .ToListAsync();

        var kullaniciSonuc = await _context.Kullanicilar
            .Where(u => u.KullaniciAdi.Contains(q) || u.Email.Contains(q))
            .ToListAsync();

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
        var toplamKullanici = await _context.Kullanicilar.CountAsync();
        if (toplamKullanici == 0)
            return View(new List<Video>());

        var esik = toplamKullanici / 4;

        var trendVideolar = await _context.Videolar
            .Include(v => v.Kullanici)
            .Where(v => v.LikeSayisi >= esik && v.IzlenmeSayisi > 0) // 👈 izlenme > 0 şartı
            .OrderByDescending(v => v.LikeSayisi)
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

        return RedirectToAction("Index");
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

        return RedirectToAction("Index");
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
        return RedirectToAction("Index");
    }
    public async Task<IActionResult> Kullanici(int id)
    {
        var kullanici = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.Id == id);
        if (kullanici == null) return NotFound();

        var videolar = await _context.Videolar
            .Where(v => v.KullaniciId == id)
            .OrderByDescending(v => v.YuklenmeTarihi)
            .ToListAsync();

        var aboneSayisi = await _context.Abonelikler.CountAsync(a => a.AboneOlunanId == id);
        ViewBag.AboneSayisi = aboneSayisi;

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
            _context.DahaSonraIzleListesi.Add(new DahaSonraIzle
            {
                KullaniciId = user.Id,
                VideoId = id,
                EklenmeTarihi = DateTime.Now
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

        return View(); // Şifre değiştirme formunu gösterecek
    }

    [HttpPost]
    public async Task<IActionResult> SifreDegistir(string EskiSifre, string YeniSifre)
    {
        var email = HttpContext.Session.GetString("UserEmail");
        if (email == null)
            return RedirectToAction("Giris", "Home");

        var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null) return NotFound();

        if (user.Sifre != EskiSifre)
        {
            ViewBag.ErrorMessage = "Eski şifre yanlış.";
            return View();
        }

        user.Sifre = YeniSifre;
        await _context.SaveChangesAsync();

        ViewBag.SuccessMessage = "Şifre başarıyla değiştirildi.";
        return View();
    }

    [HttpGet]
    public IActionResult SifremiUnuttum()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> SifremiUnuttum(string Email)
    {
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

        // ✅ Mail gönderme
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
);
        return RedirectToAction("KodDogrula", new { email = Email });
    }

    [HttpGet]
    public IActionResult KodDogrula(string email)
    {
        ViewBag.Email = email;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> KodDogrula(string email, string code)
    {
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
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> YeniSifreBelirle(string email, string YeniSifre)
    {
        var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null) return NotFound();

        user.Sifre = YeniSifre;
        user.ResetCode = null;
        user.ResetCodeExpire = null;
        await _context.SaveChangesAsync();

        ViewBag.SuccessMessage = "Şifreniz başarıyla güncellendi.";
        return RedirectToAction("Giris");
    }


}
