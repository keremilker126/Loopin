using Loopin.Data;
using Loopin.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Loopin.Controllers.Api;

[Route("api/[controller]")]
[ApiController]
public class GecmisApiController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<GecmisApiController> _logger;

    public GecmisApiController(AppDbContext context, ILogger<GecmisApiController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("kullanici/{kullaniciId}")]
    public async Task<ActionResult<IEnumerable<GecmisDto>>> GetKullaniciGecmisi(int kullaniciId)
    {
        if (kullaniciId <= 0)
            return BadRequest(new { message = "Gecerli bir kullaniciId gereklidir." });

        try
        {
            var gecmis = await _context.GecmisListesi
                .Where(g => g.KullaniciId == kullaniciId)
                .Include(g => g.Video)
                .OrderByDescending(g => g.IzlenmeTarihi)
                .Select(g => new GecmisDto
                {
                    Id = g.Id,
                    KullaniciId = g.KullaniciId,
                    VideoId = g.VideoId,
                    VideoBaslik = g.Video.Baslik,
                    KapakResmiUrl = g.Video.KapakResmiUrl
                })
                .ToListAsync();

            return Ok(gecmis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gecmis listesi getirilemedi. KullaniciId: {KullaniciId}", kullaniciId);
            return StatusCode(500, new { message = "Izleme gecmisi getirilemedi. Lutfen tekrar deneyin." });
        }
    }

    [HttpPost]
    public async Task<ActionResult> GecmiseEkle([FromBody] GecmisDto dto)
    {
        if (dto == null || dto.KullaniciId <= 0 || dto.VideoId <= 0)
            return BadRequest(new { message = "Gecersiz veri." });

        try
        {
            if (!await _context.Videolar.AnyAsync(v => v.Id == dto.VideoId))
                return NotFound(new { message = "Video bulunamadi." });

            if (!await _context.Kullanicilar.AnyAsync(k => k.Id == dto.KullaniciId))
                return NotFound(new { message = "Kullanici bulunamadi." });

            var mevcutKayit = await _context.GecmisListesi
                .FirstOrDefaultAsync(x => x.KullaniciId == dto.KullaniciId && x.VideoId == dto.VideoId);

            if (mevcutKayit != null)
            {
                mevcutKayit.IzlenmeTarihi = DateTime.Now;
                _context.GecmisListesi.Update(mevcutKayit);
            }
            else
            {
                _context.GecmisListesi.Add(new Gecmis
                {
                    KullaniciId = dto.KullaniciId,
                    VideoId = dto.VideoId,
                    IzlenmeTarihi = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Gecmise kaydedildi." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gecmise ekleme tamamlanamadi. VideoId: {VideoId}, KullaniciId: {KullaniciId}", dto.VideoId, dto.KullaniciId);
            return StatusCode(500, new { message = "Gecmise kaydedilemedi. Lutfen tekrar deneyin." });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> KayitSil(int id)
    {
        if (id <= 0)
            return BadRequest(new { message = "Gecerli bir id gereklidir." });

        try
        {
            var kayit = await _context.GecmisListesi.FindAsync(id);
            if (kayit == null)
                return NotFound(new { message = "Kayit bulunamadi." });

            _context.GecmisListesi.Remove(kayit);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Kayit silindi." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gecmis kaydi silinemedi. Id: {Id}", id);
            return StatusCode(500, new { message = "Gecmis kaydi silinemedi. Lutfen tekrar deneyin." });
        }
    }

    [HttpDelete("temizle/{kullaniciId}")]
    public async Task<IActionResult> TumGecmisiSil(int kullaniciId)
    {
        if (kullaniciId <= 0)
            return BadRequest(new { message = "Gecerli bir kullaniciId gereklidir." });

        try
        {
            var liste = await _context.GecmisListesi
                .Where(x => x.KullaniciId == kullaniciId)
                .ToListAsync();

            if (!liste.Any())
                return NotFound(new { message = "Gecmis zaten bos." });

            _context.GecmisListesi.RemoveRange(liste);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Tum izleme gecmisi temizlendi." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tum gecmis silinemedi. KullaniciId: {KullaniciId}", kullaniciId);
            return StatusCode(500, new { message = "Izleme gecmisi temizlenemedi. Lutfen tekrar deneyin." });
        }
    }

    [HttpGet("video/{videoId}")]
    public async Task<ActionResult<VideoDto>> GetVideoById(int videoId)
    {
        if (videoId <= 0)
            return BadRequest(new { message = "Gecerli bir videoId gereklidir." });

        try
        {
            var video = await _context.Videolar
                .Include(v => v.Kullanici)
                .FirstOrDefaultAsync(v => v.Id == videoId);

            if (video == null)
                return NotFound(new { message = "Video bulunamadi." });

            var dto = new VideoDto
            {
                Id = video.Id,
                Baslik = video.Baslik,
                Aciklama = video.Aciklama,
                KapakResmiUrl = $"https://loopin.fun/{video.KapakResmiUrl}",
                VideoUrl = $"https://loopin.fun/{video.VideoUrl}",
                KullaniciId = video.KullaniciId,
                KullaniciAdi = video.Kullanici.KullaniciAdi ?? "",
                IzlenmeSayisi = video.IzlenmeSayisi,
                LikeSayisi = video.LikeSayisi
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gecmis video detayi getirilemedi. VideoId: {VideoId}", videoId);
            return StatusCode(500, new { message = "Video detayi getirilemedi. Lutfen tekrar deneyin." });
        }
    }
}
