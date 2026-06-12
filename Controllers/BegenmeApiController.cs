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
    private readonly ILogger<BegenmeApiController> _logger;

    public BegenmeApiController(AppDbContext context, ILogger<BegenmeApiController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpPost("toggle")]
    public async Task<IActionResult> Toggle([FromBody] BegenmeDto dto)
    {
        if (dto == null || dto.VideoId <= 0 || dto.KullaniciId <= 0)
        {
            return BadRequest(new { message = "Gecersiz veri gonderildi. VideoId ve KullaniciId gereklidir." });
        }

        try
        {
            var video = await _context.Videolar.FindAsync(dto.VideoId);
            if (video == null)
                return NotFound(new { message = "Video bulunamadi." });

            var mevcut = await _context.Begenmeler
                .FirstOrDefaultAsync(x => x.VideoId == dto.VideoId && x.KullaniciId == dto.KullaniciId);

            string durum;
            if (mevcut == null)
            {
                _context.Begenmeler.Add(new Begenme
                {
                    VideoId = dto.VideoId,
                    KullaniciId = dto.KullaniciId,
                    Tarih = DateTime.Now
                });

                _context.KanalEtkilesimleri.Add(new KanalEtkilesimi
                {
                    VideoId = dto.VideoId,
                    Tur = KanalEtkilesimi.Begeni,
                    Tarih = DateTime.Now
                });

                video.LikeSayisi++;
                durum = "liked";
            }
            else
            {
                _context.Begenmeler.Remove(mevcut);
                video.LikeSayisi = Math.Max(0, video.LikeSayisi - 1);
                durum = "unliked";
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                status = durum,
                currentLikes = video.LikeSayisi,
                message = durum == "liked" ? "Video begenildi." : "Begeni geri alindi."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Begeni toggle sirasinda hata olustu. VideoId: {VideoId}, KullaniciId: {KullaniciId}", dto.VideoId, dto.KullaniciId);
            return StatusCode(500, new { message = "Begeni islemi tamamlanamadi. Lutfen tekrar deneyin." });
        }
    }

    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetLikedVideos(int userId)
    {
        if (userId <= 0)
            return BadRequest(new { message = "Gecerli bir kullaniciId gereklidir." });

        try
        {
            var likedVideos = await _context.Begenmeler
                .Include(b => b.Video)
                .ThenInclude(v => v.Kullanici)
                .Where(b => b.KullaniciId == userId)
                .OrderByDescending(b => b.Id)
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
                    KullaniciAdi = b.Video.Kullanici.KullaniciAdi ?? ""
                })
                .ToListAsync();

            return Ok(likedVideos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Begenilen videolar listelenirken hata olustu. KullaniciId: {KullaniciId}", userId);
            return StatusCode(500, new { message = "Begenilen videolar getirilemedi. Lutfen tekrar deneyin." });
        }
    }

    [HttpDelete("remove")]
    public async Task<IActionResult> Remove([FromBody] BegenmeDto dto)
    {
        if (dto == null || dto.VideoId <= 0 || dto.KullaniciId <= 0)
            return BadRequest(new { message = "Gecersiz veri." });

        try
        {
            var video = await _context.Videolar.FindAsync(dto.VideoId);
            var begeni = await _context.Begenmeler
                .FirstOrDefaultAsync(x => x.VideoId == dto.VideoId && x.KullaniciId == dto.KullaniciId);

            if (begeni == null)
                return NotFound(new { message = "Begeni kaydi bulunamadi." });

            _context.Begenmeler.Remove(begeni);

            if (video != null)
                video.LikeSayisi = Math.Max(0, video.LikeSayisi - 1);

            await _context.SaveChangesAsync();
            return Ok(new { status = "removed", message = "Begeni kaldirildi." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Begeni kaldirilirken hata olustu. VideoId: {VideoId}, KullaniciId: {KullaniciId}", dto.VideoId, dto.KullaniciId);
            return StatusCode(500, new { message = "Begeni kaldirilamadi. Lutfen tekrar deneyin." });
        }
    }
}
