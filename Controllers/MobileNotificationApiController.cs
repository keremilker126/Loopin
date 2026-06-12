using Loopin.Data;
using Loopin.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Loopin.Controllers.Api;

[ApiController]
[Route("api/mobile-notifications")]
public class MobileNotificationApiController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<MobileNotificationApiController> _logger;

    public MobileNotificationApiController(AppDbContext context, ILogger<MobileNotificationApiController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpPost("device-token")]
    public async Task<IActionResult> RegisterDeviceToken([FromBody] MobilCihazTokenDto dto)
    {
        try
        {
            if (dto == null || dto.KullaniciId <= 0 || string.IsNullOrWhiteSpace(dto.Token))
            {
                return BadRequest(new { message = "KullaniciId ve token gereklidir." });
            }

            var platform = (dto.Platform ?? "").Trim().ToLowerInvariant();
            if (platform != "android" && platform != "ios")
            {
                return BadRequest(new { message = "Platform android veya ios olmalidir." });
            }

            if (!await _context.Kullanicilar.AnyAsync(k => k.Id == dto.KullaniciId))
            {
                return NotFound(new { message = "Kullanici bulunamadi." });
            }

            var token = dto.Token.Trim();
            var kayit = await _context.MobilCihazTokenlari
                .FirstOrDefaultAsync(t => t.Token == token);

            if (kayit == null)
            {
                kayit = new MobilCihazToken
                {
                    KullaniciId = dto.KullaniciId,
                    Token = token,
                    Platform = platform,
                    CihazId = string.IsNullOrWhiteSpace(dto.CihazId) ? null : dto.CihazId.Trim(),
                    Aktif = true,
                    KayitTarihi = DateTime.Now
                };

                _context.MobilCihazTokenlari.Add(kayit);
            }
            else
            {
                kayit.KullaniciId = dto.KullaniciId;
                kayit.Platform = platform;
                kayit.CihazId = string.IsNullOrWhiteSpace(dto.CihazId) ? kayit.CihazId : dto.CihazId.Trim();
                kayit.Aktif = true;
                kayit.GuncellenmeTarihi = DateTime.Now;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Mobil cihaz tokeni kaydedildi.",
                deviceTokenId = kayit.Id
            });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Mobil cihaz tokeni kaydedilirken veritabani hatasi olustu.");
            return StatusCode(500, new { message = "Mobil cihaz tokeni kaydedilemedi. Lutfen tekrar deneyin." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mobil cihaz tokeni kaydedilirken beklenmeyen hata olustu.");
            return StatusCode(500, new { message = "Mobil cihaz tokeni kaydedilemedi. Lutfen tekrar deneyin." });
        }
    }

    [HttpDelete("device-token")]
    public async Task<IActionResult> RemoveDeviceToken([FromBody] MobilCihazTokenDto dto)
    {
        try
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Token))
            {
                return BadRequest(new { message = "Token gereklidir." });
            }

            var token = dto.Token.Trim();
            var kayit = await _context.MobilCihazTokenlari
                .FirstOrDefaultAsync(t => t.Token == token);

            if (kayit == null)
            {
                return Ok(new { message = "Token zaten pasif." });
            }

            kayit.Aktif = false;
            kayit.GuncellenmeTarihi = DateTime.Now;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Mobil cihaz tokeni pasife alindi." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mobil cihaz tokeni pasife alinirken hata olustu.");
            return StatusCode(500, new { message = "Mobil cihaz tokeni pasife alinamadi. Lutfen tekrar deneyin." });
        }
    }

    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetUserNotifications(int userId, [FromQuery] bool onlyUnread = false)
    {
        try
        {
            if (userId <= 0)
            {
                return BadRequest(new { message = "Gecerli bir kullaniciId gereklidir." });
            }

            var query = _context.MobilBildirimleri
                .AsNoTracking()
                .Include(b => b.Video)
                .Where(b => b.KullaniciId == userId);

            if (onlyUnread)
            {
                query = query.Where(b => !b.Okundu);
            }

            var bildirimler = await query
                .OrderByDescending(b => b.OlusturulmaTarihi)
                .Take(100)
                .Select(b => new
                {
                    b.Id,
                    b.KullaniciId,
                    b.VideoId,
                    b.KanalKullaniciId,
                    b.Tip,
                    b.Baslik,
                    b.Mesaj,
                    b.Okundu,
                    b.OlusturulmaTarihi,
                    b.OkunmaTarihi,
                    videoBaslik = b.Video != null ? b.Video.Baslik : null,
                    kapakResmiUrl = b.Video != null ? b.Video.KapakResmiUrl : null
                })
                .ToListAsync();

            return Ok(bildirimler);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mobil bildirimler listelenirken hata olustu. KullaniciId: {UserId}", userId);
            return StatusCode(500, new { message = "Bildirimler getirilemedi. Lutfen tekrar deneyin." });
        }
    }

    [HttpGet("unread-count/{userId}")]
    public async Task<IActionResult> GetUnreadCount(int userId)
    {
        try
        {
            if (userId <= 0)
            {
                return BadRequest(new { message = "Gecerli bir kullaniciId gereklidir." });
            }

            var count = await _context.MobilBildirimleri
                .CountAsync(b => b.KullaniciId == userId && !b.Okundu);

            return Ok(new { unreadCount = count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Okunmamis mobil bildirim sayisi alinirken hata olustu. KullaniciId: {UserId}", userId);
            return StatusCode(500, new { message = "Okunmamis bildirim sayisi getirilemedi." });
        }
    }

    [HttpPost("{id}/read")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        try
        {
            if (id <= 0)
            {
                return BadRequest(new { message = "Gecerli bir bildirim id gereklidir." });
            }

            var bildirim = await _context.MobilBildirimleri.FindAsync(id);
            if (bildirim == null)
            {
                return NotFound(new { message = "Bildirim bulunamadi." });
            }

            if (!bildirim.Okundu)
            {
                bildirim.Okundu = true;
                bildirim.OkunmaTarihi = DateTime.Now;
                await _context.SaveChangesAsync();
            }

            return Ok(new { message = "Bildirim okundu olarak isaretlendi." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mobil bildirim okundu olarak isaretlenirken hata olustu. BildirimId: {NotificationId}", id);
            return StatusCode(500, new { message = "Bildirim okundu olarak isaretlenemedi." });
        }
    }

    [HttpPost("read-all/{userId}")]
    public async Task<IActionResult> MarkAllAsRead(int userId)
    {
        try
        {
            if (userId <= 0)
            {
                return BadRequest(new { message = "Gecerli bir kullaniciId gereklidir." });
            }

            var bildirimler = await _context.MobilBildirimleri
                .Where(b => b.KullaniciId == userId && !b.Okundu)
                .ToListAsync();

            var now = DateTime.Now;
            foreach (var bildirim in bildirimler)
            {
                bildirim.Okundu = true;
                bildirim.OkunmaTarihi = now;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Tum bildirimler okundu olarak isaretlendi.",
                updatedCount = bildirimler.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tum mobil bildirimler okundu olarak isaretlenirken hata olustu. KullaniciId: {UserId}", userId);
            return StatusCode(500, new { message = "Bildirimler okundu olarak isaretlenemedi." });
        }
    }
}
