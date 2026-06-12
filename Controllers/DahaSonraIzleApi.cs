using Loopin.Data;
using Loopin.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Loopin.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class DahaSonraIzleApiController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<DahaSonraIzleApiController> _logger;

    public DahaSonraIzleApiController(AppDbContext context, ILogger<DahaSonraIzleApiController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("{userId}")]
    public async Task<IActionResult> GetList(int userId)
    {
        if (userId <= 0)
            return BadRequest(new { message = "Gecerli bir kullaniciId gereklidir." });

        try
        {
            var liste = await _context.DahaSonraIzleListesi
                .Include(x => x.Video)
                .ThenInclude(v => v.Kullanici)
                .Where(x => x.KullaniciId == userId)
                .OrderByDescending(x => x.EklenmeTarihi)
                .Select(x => new VideoDto
                {
                    Id = x.Video.Id,
                    Baslik = x.Video.Baslik,
                    VideoUrl = x.Video.VideoUrl,
                    KapakResmiUrl = x.Video.KapakResmiUrl,
                    Aciklama = x.Video.Aciklama,
                    YuklenmeTarihi = x.Video.YuklenmeTarihi,
                    IzlenmeSayisi = x.Video.IzlenmeSayisi,
                    LikeSayisi = x.Video.LikeSayisi,
                    KullaniciId = x.Video.KullaniciId,
                    KullaniciAdi = x.Video.Kullanici.KullaniciAdi ?? ""
                })
                .ToListAsync();

            return Ok(liste);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Daha sonra izle listesi getirilemedi. KullaniciId: {KullaniciId}", userId);
            return StatusCode(500, new { message = "Daha sonra izle listesi getirilemedi. Lutfen tekrar deneyin." });
        }
    }

    [HttpPost("toggle")]
    public async Task<IActionResult> Toggle([FromBody] DahaSonraIzleDto dto)
    {
        if (dto == null || dto.KullaniciId <= 0 || dto.VideoId <= 0)
            return BadRequest(new { message = "Gecersiz veri." });

        try
        {
            if (!await _context.Videolar.AnyAsync(v => v.Id == dto.VideoId))
                return NotFound(new { message = "Video bulunamadi." });

            var mevcut = await _context.DahaSonraIzleListesi
                .FirstOrDefaultAsync(x => x.KullaniciId == dto.KullaniciId && x.VideoId == dto.VideoId);

            if (mevcut == null)
            {
                var tarih = DateTime.Now;
                _context.DahaSonraIzleListesi.Add(new DahaSonraIzle
                {
                    KullaniciId = dto.KullaniciId,
                    VideoId = dto.VideoId,
                    EklenmeTarihi = tarih
                });
                _context.KanalEtkilesimleri.Add(new KanalEtkilesimi
                {
                    VideoId = dto.VideoId,
                    Tur = KanalEtkilesimi.DahaSonraIzle,
                    Tarih = tarih
                });
                await _context.SaveChangesAsync();
                return Ok(new { status = "added", message = "Daha sonra izle listesine eklendi." });
            }

            _context.DahaSonraIzleListesi.Remove(mevcut);
            await _context.SaveChangesAsync();
            return Ok(new { status = "removed", message = "Listeden cikarildi." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Daha sonra izle toggle tamamlanamadi. VideoId: {VideoId}, KullaniciId: {KullaniciId}", dto.VideoId, dto.KullaniciId);
            return StatusCode(500, new { message = "Daha sonra izle islemi tamamlanamadi. Lutfen tekrar deneyin." });
        }
    }

    [HttpDelete]
    public async Task<IActionResult> Remove([FromBody] DahaSonraIzleDto dto)
    {
        if (dto == null || dto.KullaniciId <= 0 || dto.VideoId <= 0)
            return BadRequest(new { message = "Gecersiz veri." });

        try
        {
            var kayit = await _context.DahaSonraIzleListesi
                .FirstOrDefaultAsync(x => x.KullaniciId == dto.KullaniciId && x.VideoId == dto.VideoId);

            if (kayit != null)
            {
                _context.DahaSonraIzleListesi.Remove(kayit);
                await _context.SaveChangesAsync();
            }

            return Ok(new { message = "Silindi." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Daha sonra izle kaydi silinemedi. VideoId: {VideoId}, KullaniciId: {KullaniciId}", dto.VideoId, dto.KullaniciId);
            return StatusCode(500, new { message = "Daha sonra izle kaydi silinemedi. Lutfen tekrar deneyin." });
        }
    }

    [HttpDelete("clear/{userId}")]
    public async Task<IActionResult> ClearAll(int userId)
    {
        if (userId <= 0)
            return BadRequest(new { message = "Gecerli bir kullaniciId gereklidir." });

        try
        {
            var liste = _context.DahaSonraIzleListesi.Where(x => x.KullaniciId == userId);
            _context.DahaSonraIzleListesi.RemoveRange(liste);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Liste tamamen temizlendi." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Daha sonra izle listesi temizlenemedi. KullaniciId: {KullaniciId}", userId);
            return StatusCode(500, new { message = "Daha sonra izle listesi temizlenemedi. Lutfen tekrar deneyin." });
        }
    }
}
