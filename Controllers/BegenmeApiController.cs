using Loopin.Data;
using Loopin.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Loopin.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class BegenmeApiController : ControllerBase
{
    private readonly AppDbContext _context;

    public BegenmeApiController(AppDbContext context)
    {
        _context = context;
    }

    // 📌 BEĞENME / BEĞENİDEN VAZGEÇ (Toggle Like)
    [HttpPost("toggle")]
    public async Task<IActionResult> Toggle([FromBody] BegenmeDto dto) // 🔥 [FromBody] eklendi
    {
        // 1. Veri kontrolü (Flutter'dan gelen JSON boş mu?)
        if (dto == null || dto.VideoId == 0 || dto.KullaniciId == 0)
        {
            return BadRequest(new { message = "Geçersiz veri gönderildi. VideoId ve KullaniciId gereklidir." });
        }

        try
        {
            // 2. Video var mı kontrolü
            var video = await _context.Videolar.FindAsync(dto.VideoId);
            if (video == null)
                return NotFound(new { message = "Video bulunamadı." });

            // 3. Kullanıcı daha önce beğenmiş mi? (İlişki tablosunu kontrol et)
            var mevcut = await _context.Begenmeler
                .FirstOrDefaultAsync(x =>
                    x.VideoId == dto.VideoId &&
                    x.KullaniciId == dto.KullaniciId);

            string durum;
            if (mevcut == null)
            {
                // Beğeni yoksa ekle
                _context.Begenmeler.Add(new Begenme
                {
                    VideoId = dto.VideoId,
                    KullaniciId = dto.KullaniciId,
                    Tarih = DateTime.Now // Tarih bilgisini sunucu tarafında set etmek daha sağlıklıdır
                });

                video.LikeSayisi++;
                durum = "liked";
            }
            else
            {
                // Beğeni varsa kaldır
                _context.Begenmeler.Remove(mevcut);
                video.LikeSayisi = Math.Max(0, video.LikeSayisi - 1); // Beğeni sayısının eksiye düşmesini engeller
                durum = "unliked";
            }

            // 4. Veritabanına kaydet
            await _context.SaveChangesAsync();

            // 5. Başarılı yanıt ve güncel veriler
            return Ok(new
            {
                status = durum,
                currentLikes = video.LikeSayisi,
                message = durum == "liked" ? "Video beğenildi." : "Beğeni geri alındı."
            });
        }
        catch (Exception ex)
        {
            // Bir hata oluşursa Flutter'a hata mesajını gönder
            return StatusCode(500, new { message = "İşlem sırasında sunucu hatası oluştu: " + ex.Message });
        }
    }

    // 📌 KULLANICININ BEĞENDİĞİ VİDEOLARI LİSTELE (Beğenilenler Sayfası)
    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetLikedVideos(int userId)
    {
        var likedVideos = await _context.Begenmeler
            .Include(b => b.Video)
            .ThenInclude(v => v.Kullanici)
            .Where(b => b.KullaniciId == userId)
            .OrderByDescending(b => b.Id) // Son beğenilen en üstte
            .Select(b => new VideoDto
            {
                Id = b.Video.Id,
                Baslik = b.Video.Baslik,
                VideoUrl = b.Video.VideoUrl,
                KapakResmiUrl = b.Video.KapakResmiUrl,
                Aciklama = b.Video.Aciklama,
                YuklenmeTarihi = b.Video.YuklenmeTarihi,
                IzlenmeSayisi = b.Video.IzlenmeSayisi,
                LikeSayisi = b.Video.LikeSayisi,
                KullaniciId = b.Video.KullaniciId,
                KullaniciAdi = b.Video.Kullanici.KullaniciAdi
            }).ToListAsync();

        return Ok(likedVideos);
    }
    // 📌 BEĞENİYİ DİREKT KALDIR (Beğenilenler sayfasından silmek için)
    [HttpDelete("remove")]
    public async Task<IActionResult> Remove([FromBody] BegenmeDto dto)
    {
        if (dto == null || dto.VideoId == 0 || dto.KullaniciId == 0)
            return BadRequest(new { message = "Geçersiz veri." });

        var video = await _context.Videolar.FindAsync(dto.VideoId);
        var beğeni = await _context.Begenmeler
            .FirstOrDefaultAsync(x => x.VideoId == dto.VideoId && x.KullaniciId == dto.KullaniciId);

        if (beğeni != null)
        {
            _context.Begenmeler.Remove(beğeni);

            if (video != null)
                video.LikeSayisi = Math.Max(0, video.LikeSayisi - 1);

            await _context.SaveChangesAsync();
            return Ok(new { status = "removed", message = "Beğeni kaldırıldı." });
        }

        return NotFound(new { message = "Beğeni kaydı bulunamadı." });
    }
}