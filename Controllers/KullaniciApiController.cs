using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Loopin.Data;
using Loopin.Models;
using Loopin.Services;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity.Data; // MailAddress kontrolü için

namespace Loopin.Controllers.Api;

[ApiController]
[Route("api/auth")]
public class AuthApiController : ControllerBase
{
    private readonly List<string> _adminEmails = new()
{
    "keremilker56@gmail.com",
    "keremilker126@gmail.com",
    "mehmedkaan46@gmail.com"
};
    private readonly AppDbContext _context;
    private readonly EmailService _emailService;

    public AuthApiController(AppDbContext context, EmailService emailService)
    {
        _context = context;
        _emailService = emailService;
    }
    [HttpGet("user/{id}")]
    public async Task<ActionResult<KullaniciDto>> GetUserById(int id)
    {
        var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.Id == id);
        var aboneSayisi = await _context.Abonelikler.CountAsync(a => a.AboneOlunanId == id);
        if (user == null) return NotFound();

        var dto = new KullaniciDto
        {
            Id = user.Id,
            KullaniciAdi = user.KullaniciAdi,
            Email = user.Email,
            EmailOnayli = user.EmailOnayli,
            AboneSayisi = aboneSayisi
        };

        return Ok(dto);
    }
    // 📌 Tüm kullanıcıları getir (Admin)
    [HttpGet("admin/users")]
    public async Task<IActionResult> GetAllUsers([FromQuery] string adminEmail)
    {
        if (!_adminEmails.Contains(adminEmail))
            return Unauthorized(new { message = "Yetkisiz işlem" });

        var users = await _context.Kullanicilar
            .Select(u => new
            {
                u.Id,
                u.KullaniciAdi,
                u.Email,
                u.EmailOnayli
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        // 📌 ADIM 1: E-posta engellenmiş mi kontrol et
        // HomeController içindeki _blockedEmails listesine bakar
        if (HomeController._blockedEmails.Contains(dto.Email))
        {
            return BadRequest(new { message = "Bu e-posta adresi yönetici tarafından yasaklanmıştır!" });
        }

        // 1. GERÇEK E-POSTA KONTROLÜ (Format kontrolü)
        try
        {
            var addr = new MailAddress(dto.Email);
            if (addr.Address != dto.Email) throw new Exception();
        }
        catch
        {
            return BadRequest(new { message = "Lütfen geçerli bir e-posta adresi giriniz!" });
        }

        var exists = await _context.Kullanicilar.AnyAsync(x => x.Email == dto.Email);
        if (exists) return BadRequest(new { message = "Bu e-posta zaten kayıtlı." });

        // Link için uzun bir GUID oluşturuyoruz (6 haneli kod yerine)
        var token = Guid.NewGuid().ToString();

        var user = new Kullanici
        {
            KullaniciAdi = dto.KullaniciAdi,
            Email = dto.Email,
            Sifre = dto.Sifre,
            EmailOnayli = false,
            EmailOnayToken = token,
            EmailOnayExpire = DateTime.Now.AddHours(24) // Aktivasyon linki için 24 saat idealdir
        };

        _context.Kullanicilar.Add(user);
        await _context.SaveChangesAsync();

        // 2. BUTONLU (LİNK) MAİL GÖNDERİMİ
        // NOT: Buradaki localhost:5000'i kendi API adresinle güncelle
        var confirmationLink = $"http://localhost:5144/api/auth/verify?token={token}";
        var emailBody = $@"
<div style='font-family:Arial,sans-serif; background:#0f0f1a; padding:30px; color:white;'>

    <div style='max-width:520px; margin:auto; background:#1a1a2e; border-radius:18px; padding:30px; box-shadow:0 15px 50px rgba(0,0,0,0.6); text-align:center;'>

        <h2 style='color:#c084fc; margin-bottom:15px;'>
            🎬 Loopin'e Hoş Geldin!
        </h2>

        <p style='color:#ddd; font-size:14px;'>
            Hesabını aktif etmek için aşağıdaki butona tıklaman yeterli.
        </p>

        <div style='margin:30px 0;'>

            <a href='{confirmationLink}' 
               style='
                    display:inline-block;
                    padding:14px 28px;
                    background:linear-gradient(45deg,#9333ea,#6366f1);
                    color:white;
                    text-decoration:none;
                    border-radius:999px;
                    font-weight:600;
                    font-size:14px;
                    box-shadow:0 5px 20px rgba(147,51,234,0.5);
               '>
               ✅ Hesabımı Onayla
            </a>

        </div>

        <p style='color:#888; font-size:13px;'>
            Bu bağlantı güvenlik nedeniyle kısa süre içinde geçerliliğini yitirir.
        </p>

        <hr style='border:none; border-top:1px solid rgba(255,255,255,0.1); margin:25px 0;' />

        <p style='color:#777; font-size:12px;'>
            Eğer buton çalışmazsa, aşağıdaki bağlantıyı tarayıcına yapıştır:
        </p>

        <p style='word-break:break-all; font-size:12px; color:#a855f7;'>
            {confirmationLink}
        </p>

        <p style='margin-top:20px; font-size:11px; color:#555;'>
            © 2026 Loopin • Tüm hakları saklıdır
        </p>

    </div>

</div>";

        try
        {
            await _emailService.SendEmailAsync(dto.Email, "🎬 Loopin Hesap Aktivasyonu", emailBody);
        }
        catch
        {
            // E-posta gönderilemezse (sahte/geçersiz e-posta durumu)
            return BadRequest(new { message = "E-posta gönderimi başarısız oldu. Lütfen geçerli bir adres yazın." });
        }

        return Ok(new { message = "Kayıt başarılı. Lütfen e-postanızdaki onay butonuna tıklayın." });
    }

    [HttpGet("verify")]
    public async Task<IActionResult> Verify(string token)
    {
        var user = await _context.Kullanicilar
            .FirstOrDefaultAsync(x => x.EmailOnayToken == token);

        if (user == null) return Content("<h1>Geçersiz veya kullanılmış onay linki.</h1>", "text/html");
        if (user.EmailOnayExpire < DateTime.Now) return Content("<h1>Onay linkinin süresi dolmuş.</h1>", "text/html");

        user.EmailOnayli = true;
        user.EmailOnayToken = null;
        user.EmailOnayExpire = null;

        await _context.SaveChangesAsync();

        // Tarayıcıda kullanıcıyı karşılayan şık bir mesaj
        return Content(@"
<div style='
    font-family:Arial,sans-serif;
    background:radial-gradient(circle at top,#141428,#0b0b14);
    height:100vh;
    display:flex;
    justify-content:center;
    align-items:center;
    color:white;
'>

    <div style='
        background:#1a1a2e;
        padding:40px;
        border-radius:20px;
        text-align:center;
        max-width:420px;
        width:100%;
        box-shadow:0 20px 60px rgba(0,0,0,0.6);
        border:1px solid rgba(255,255,255,0.05);
    '>

        <h1 style='
            color:#22c55e;
            font-size:22px;
            margin-bottom:15px;
        '>
            ✅ Hesabın Onaylandı!
        </h1>

        <p style='
            color:#aaa;
            font-size:14px;
            margin-bottom:25px;
        '>
            Artık Loopin'e dönüp giriş yapabilirsin 🚀
        </p>

        <button onclick='window.close()'
            style='
                background:linear-gradient(45deg,#9333ea,#6366f1);
                border:none;
                padding:12px 25px;
                border-radius:999px;
                color:white;
                font-weight:600;
                cursor:pointer;
                transition:0.3s;
            '
            onmouseover=""this.style.transform='scale(1.05)'"" 
            onmouseout=""this.style.transform='scale(1)'"" >
            🔒 Sekmeyi Kapat
        </button>

        <p style='
            margin-top:20px;
            font-size:11px;
            color:#555;
        '>
            © 2026 Loopin
        </p>

    </div>

</div>
", "text/html");
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var user = await _context.Kullanicilar
            .FirstOrDefaultAsync(k => k.Email == dto.Email && k.Sifre == dto.Sifre);

        if (user == null)
            return BadRequest(new { message = "Geçersiz e-posta veya şifre!" });

        // 📌 1. ENGEL: Mail onayı yoksa asla kod gönderme ve girişi reddet
        if (!user.EmailOnayli)
        {
            return BadRequest(new
            {
                message = "Hesabınız henüz aktive edilmemiş. Lütfen mailinizdeki 'Hesabımı Onayla' butonuna tıklayın.",
                requiresVerification = true
            });
        }

        // 📌 2. ENGEL: Şifre doğru ve mail onaylıysa 2FA kodu üret
        var loginToken = Guid.NewGuid().ToString().Substring(0, 6).ToUpper();
        user.EmailOnayToken = loginToken;
        user.EmailOnayExpire = DateTime.Now.AddMinutes(5);
        await _context.SaveChangesAsync();

        // Giriş kodunu gönder
        await _emailService.SendEmailAsync(
            user.Email,
            "🔐 Loopin Giriş Doğrulama Kodu",
            $@"
    <div style='font-family:Arial,sans-serif; background:#0f0f1a; padding:30px; color:white;'>

        <div style='max-width:500px; margin:auto; background:#1a1a2e; border-radius:16px; padding:25px; box-shadow:0 10px 40px rgba(0,0,0,0.6);'>

            <h2 style='text-align:center; color:#c084fc; margin-bottom:20px;'>
                🔐 Loopin Giriş Doğrulama
            </h2>

            <p style='font-size:14px; color:#ddd;'>
                Merhaba <b>{user.KullaniciAdi}</b>,
            </p>

            <p style='font-size:14px; color:#aaa;'>
                Hesabına giriş yapmak için aşağıdaki doğrulama kodunu kullan:
            </p>

            <div style='margin:25px 0; text-align:center;'>
                <span style='
                    display:inline-block;
                    background:linear-gradient(45deg,#9333ea,#6366f1);
                    padding:15px 30px;
                    border-radius:12px;
                    font-size:24px;
                    font-weight:bold;
                    letter-spacing:3px;
                    color:white;
                '>
                    {loginToken}
                </span>
            </div>

            <p style='font-size:13px; color:#888; text-align:center;'>
                ⏱ Bu kod <b>1 dakika</b> boyunca geçerlidir.
            </p>

            <hr style='border:none; border-top:1px solid rgba(255,255,255,0.1); margin:20px 0;' />

            <p style='font-size:12px; color:#777; text-align:center;'>
                Eğer bu isteği sen yapmadıysan, bu e-postayı dikkate alma.
            </p>

            <p style='font-size:11px; color:#555; text-align:center; margin-top:10px;'>
                © 2026 Loopin
            </p>

        </div>

    </div>"
        );
        return Ok(new { message = "Giriş kodu e-postanıza gönderildi." });
    }

    // 📌 3. ENGEL: VerifyLogin metodu veritabanındaki kodu kontrol etmeden asla user dönmez
    [HttpGet("verify-login")]
    public async Task<IActionResult> VerifyLogin(string token)
    {
        var user = await _context.Kullanicilar
            .FirstOrDefaultAsync(u => u.EmailOnayToken == token);

        if (user == null || user.EmailOnayExpire < DateTime.Now)
            return BadRequest(new { message = "Girdiğiniz kod hatalı veya süresi dolmuş!" });

        // Kod doğruysa temizle ve girişi tamamla
        user.EmailOnayToken = null;
        user.EmailOnayExpire = null;
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Giriş başarılı",
            user = new { user.Id, user.KullaniciAdi, user.Email }
        });
    }
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        // 1. Kullanıcıyı bul (Gerçek projede JWT Token'dan Id alınır, 
        // şimdilik basitlik adına email üzerinden gittiğimizi varsayalım)
        var user = await _context.Kullanicilar.FirstOrDefaultAsync(u => u.Email == dto.Email);

        if (user == null)
            return BadRequest(new { message = "Kullanıcı bulunamadı." });

        // 2. Mevcut şifre kontrolü
        if (user.Sifre != dto.CurrentPassword)
            return BadRequest(new { message = "Mevcut şifreniz hatalı." });

        // 3. Şifreyi güncelle
        user.Sifre = dto.NewPassword;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Şifreniz başarıyla değiştirildi." });
    }
    // --- ŞİFRE SIFIRLAMA AKIŞI ---

    // ADIM 1: E-posta kontrolü ve Kod Gönderme
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPasswordStep1([FromBody] ForgotPasswordRequest request)
    {
        var user = await _context.Kullanicilar.FirstOrDefaultAsync(x => x.Email == request.Email);

        if (user == null)
            return NotFound(new { message = "Bu e-posta adresi sistemde kayıtlı değil!" });

        // 6 Haneli rastgele kod oluştur (Örn: 482931)
        var resetCode = new Random().Next(100000, 999999).ToString();

        // Kodu geçici olarak veritabanına kaydet (EmailOnayToken alanını kullanabilirsin)
        user.EmailOnayToken = resetCode;
        user.EmailOnayExpire = DateTime.Now.AddMinutes(10); // Kod 10 dk geçerli
        await _context.SaveChangesAsync();

        // E-posta gönder
        var body = $@"
<div style='font-family:Arial,sans-serif; background:#0f0f1a; padding:30px; color:white;'>

    <div style='max-width:500px; margin:auto; background:#1a1a2e; border-radius:18px; padding:30px; box-shadow:0 15px 50px rgba(0,0,0,0.6); text-align:center;'>

        <h2 style='color:#c084fc; margin-bottom:15px;'>
            🔒 Şifre Sıfırlama
        </h2>

        <p style='color:#ddd; font-size:14px;'>
            Şifreni yenilemek için aşağıdaki doğrulama kodunu kullan:
        </p>

        <div style='margin:30px 0;'>

            <span style='
                display:inline-block;
                background:linear-gradient(45deg,#9333ea,#6366f1);
                padding:15px 35px;
                border-radius:14px;
                font-size:26px;
                font-weight:bold;
                letter-spacing:6px;
                color:white;
                box-shadow:0 5px 20px rgba(147,51,234,0.5);
            '>
                {resetCode}
            </span>

        </div>

        <p style='color:#888; font-size:13px;'>
            ⏱ Bu kod <b>10 dakika</b> boyunca geçerlidir.
        </p>

        <hr style='border:none; border-top:1px solid rgba(255,255,255,0.1); margin:25px 0;' />

        <p style='color:#777; font-size:12px;'>
            Eğer bu isteği sen yapmadıysan bu e-postayı dikkate alma.
        </p>

        <p style='margin-top:20px; font-size:11px; color:#555;'>
            © 2026 Loopin
        </p>

    </div>

</div>";

        await _emailService.SendEmailAsync(user.Email, "🔑 Loopin Şifre Sıfırlama Kodu", body);

        return Ok(new { success = true, message = "Sıfırlama kodu gönderildi." });
    }

    // ADIM 2: Kod Doğrulama ve Yeni Şifre Kaydı
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPasswordStep2([FromBody] ResetPasswordRequest request)
    {
        var user = await _context.Kullanicilar.FirstOrDefaultAsync(x => x.Email == request.Email);

        if (user == null)
            return BadRequest(new { message = "Geçersiz işlem." });

        // Kod ve Süre Kontrolü
        if (user.EmailOnayToken != request.Code || user.EmailOnayExpire < DateTime.Now)
            return BadRequest(new { message = "Doğrulama kodu hatalı veya süresi dolmuş!" });

        // Şifreyi Güncelle
        user.Sifre = request.NewPassword;
        user.EmailOnayToken = null; // Kodu temizle
        user.EmailOnayExpire = null;
        await _context.SaveChangesAsync();

        return Ok(new { success = true, message = "Şifreniz başarıyla güncellendi." });
    }

    // DTO Sınıfı (Aynı dosyanın altına veya Models klasörüne ekleyebilirsin)
    public class ChangePasswordDto
    {
        public string Email { get; set; }
        public string CurrentPassword { get; set; }
        public string NewPassword { get; set; }
    }
    public class ForgotPasswordRequest
    {
        public string Email { get; set; }
    }

    public class ResetPasswordRequest
    {
        public string Email { get; set; }
        public string Code { get; set; }
        public string NewPassword { get; set; }
    }
    [HttpDelete("admin/delete-user/{id}")]
    public async Task<IActionResult> DeleteUser(int id, [FromQuery] string adminEmail)
    {
        if (!_adminEmails.Contains(adminEmail))
            return Unauthorized(new { message = "Yetkisiz işlem" });

        var user = await _context.Kullanicilar.FindAsync(id);
        if (user == null)
            return NotFound(new { message = "Kullanıcı bulunamadı" });

        _context.Kullanicilar.Remove(user);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Kullanıcı silindi" });
    }

    // 📌 Video sil
    [HttpDelete("admin/delete-video/{id}")]
    public async Task<IActionResult> DeleteVideo(int id, [FromQuery] string adminEmail)
    {
        if (!_adminEmails.Contains(adminEmail))
            return Unauthorized();

        var video = await _context.Videolar.FindAsync(id);
        if (video == null)
            return NotFound();

        _context.Videolar.Remove(video);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Video silindi" });
    }

    // 📌 Email engelle
    // 📌 Engellenen e-postaları getir (Admin için)
    [HttpGet("admin/blocked-emails")]
    public IActionResult GetBlockedEmails([FromQuery] string adminEmail)
    {
        // Güvenlik kontrolü (Sadece admin görebilsin)
        if (!_adminEmails.Contains(adminEmail))
            return Unauthorized(new { message = "Yetkisiz işlem" });

        // HomeController'daki static listeye erişiyoruz
        // Not: Listenin HomeController içinde 'public static' olması gerekir
        return Ok(HomeController._blockedEmails);
    }
    // 📌 Yeni bir e-posta engelle (Admin)
    [HttpPost("admin/block-email")]
    public IActionResult BlockEmail([FromQuery] string email, [FromQuery] string adminEmail)
    {
        // 1. Yetki Kontrolü
        if (!_adminEmails.Contains(adminEmail))
            return Unauthorized(new { message = "Yetkisiz işlem" });

        // 2. Geçerli bir email mi?
        if (string.IsNullOrEmpty(email) || !email.Contains("@"))
            return BadRequest(new { message = "Geçersiz e-posta adresi." });

        // 3. Zaten engellenmiş mi?
        if (HomeController._blockedEmails.Contains(email))
            return BadRequest(new { message = "Bu e-posta zaten engellenmiş." });

        // 4. Listeye ekle
        HomeController._blockedEmails.Add(email);

        return Ok(new { message = $"{email} başarıyla engellendi." });
    }
    // 📌 E-posta engelini kaldır (Admin)
    [HttpPost("admin/unblock-email")]
    public IActionResult UnblockEmail([FromQuery] string email, [FromQuery] string adminEmail)
    {
        // 1. Yetki Kontrolü
        if (!_adminEmails.Contains(adminEmail))
            return Unauthorized(new { message = "Yetkisiz işlem" });

        // 2. Geçerli bir email mi?
        if (string.IsNullOrEmpty(email))
            return BadRequest(new { message = "E-posta adresi boş olamaz." });

        // 3. Liste kontrolü ve Engeli Kaldırma
        if (HomeController._blockedEmails.Contains(email))
        {
            HomeController._blockedEmails.Remove(email);
            return Ok(new { success = true, message = $"{email} adresinin engeli kaldırıldı." });
        }

        return BadRequest(new { success = false, message = "Bu e-posta zaten engellenenler listesinde değil." });
    }


}