using Loopin.Data;
using Loopin.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Loopin.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class AbonelikApiController : ControllerBase
{
    private readonly AppDbContext _context;

    public AbonelikApiController(AppDbContext context)
    {
        _context = context;
    }

    // 📌 ABONE OL / ABONELİKTEN ÇIK (Toggle)
    // 📌 ABONE OL / ABONELİKTEN ÇIK (Toggle)
    [HttpPost("toggle")]
    public async Task<IActionResult> Toggle([FromBody] AbonelikDto dto)
    {
        // 1. Validasyon: Veri null mı?
        if (dto == null)
            return BadRequest(new { message = "Geçersiz veri formatı. JSON gövdesi okunamadı." });

        // 2. Validasyon: Kendine abone olma kontrolü
        if (dto.AboneOlanId == dto.AboneOlunanId)
            return BadRequest(new { message = "Kendi kanalınıza abone olamazsınız." });

        try
        {
            // 3. Mevcut abonelik kontrolü
            var mevcut = await _context.Abonelikler
                .FirstOrDefaultAsync(x =>
                    x.AboneOlanId == dto.AboneOlanId &&
                    x.AboneOlunanId == dto.AboneOlunanId);

            string durum;
            if (mevcut == null)
            {
                // Abone değilse ekle
                _context.Abonelikler.Add(new Abonelik
                {
                    AboneOlanId = dto.AboneOlanId,
                    AboneOlunanId = dto.AboneOlunanId
                });
                durum = "subscribed";
            }
            else
            {
                // Aboneyse çıkar
                _context.Abonelikler.Remove(mevcut);
                durum = "unsubscribed";
            }

            await _context.SaveChangesAsync();

            // 4. Güncel abone sayısını hesapla
            var guncelAboneSayisi = await _context.Abonelikler
                .CountAsync(a => a.AboneOlunanId == dto.AboneOlunanId);

            // 5. Yanıtı dön
            return Ok(new
            {
                status = durum,
                message = durum == "subscribed" ? "Abone olundu." : "Abonelikten çıkıldı.",
                currentSubscriberCount = guncelAboneSayisi
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "İşlem sırasında sunucu hatası oluştu: " + ex.Message });
        }
    }
    // 📌 ABONELİK DURUMUNU KONTROL ET (Sayfa ilk açıldığında Flutter için)
    [HttpGet("check")]
    public async Task<IActionResult> CheckSubscription(int followerId, int followingId)
    {
        var durum = await _context.Abonelikler
            .AnyAsync(a => a.AboneOlanId == followerId && a.AboneOlunanId == followingId);

        var sayi = await _context.Abonelikler
            .CountAsync(a => a.AboneOlunanId == followingId);

        return Ok(new
        {
            isSubscribed = durum,
            subscriberCount = sayi
        });
    }

    // 📌 KULLANICININ TAKİP ETTİKLERİ (Following)
    [HttpGet("following/{userId}")]
    public async Task<IActionResult> GetFollowing(int userId)
    {
        var abonelikler = await _context.Abonelikler
            .Include(a => a.AboneOlunan)
            .Where(a => a.AboneOlanId == userId)
            .Select(a => new
            {
                Id = a.AboneOlunan.Id,
                KullaniciAdi = a.AboneOlunan.KullaniciAdi,
                Email = a.AboneOlunan.Email
            }).ToListAsync();

        return Ok(abonelikler);
    }
}