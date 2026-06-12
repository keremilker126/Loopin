using Loopin.Data;
using Loopin.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Loopin.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class YorumApiController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<YorumApiController> _logger;

    public YorumApiController(AppDbContext context, ILogger<YorumApiController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("video/{videoId}")]
    public async Task<IActionResult> GetByVideo(int videoId)
    {
        if (videoId <= 0)
            return BadRequest(new { message = "Gecerli bir videoId gereklidir." });

        try
        {
            var yorumlar = await _context.Yorumlar
                .Include(y => y.Kullanici)
                .Where(y => y.VideoId == videoId)
                .OrderByDescending(y => y.Tarih)
                .Select(y => new YorumDto
                {
                    Id = y.Id,
                    Icerik = y.Icerik,
                    Tarih = y.Tarih,
                    KullaniciId = y.KullaniciId,
                    KullaniciAdi = y.Kullanici.KullaniciAdi ?? "",
                    VideoId = y.VideoId
                })
                .ToListAsync();

            return Ok(yorumlar);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Yorumlar getirilemedi. VideoId: {VideoId}", videoId);
            return StatusCode(500, new { message = "Yorumlar getirilemedi. Lutfen tekrar deneyin." });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] YorumDto dto)
    {
        if (dto == null || dto.KullaniciId <= 0 || dto.VideoId <= 0)
            return BadRequest(new { message = "Gecersiz veri." });

        dto.Icerik = (dto.Icerik ?? "").Trim();
        if (string.IsNullOrWhiteSpace(dto.Icerik))
            return BadRequest(new { message = "Yorum icerigi bos olamaz." });

        if (dto.Icerik.Length > 1000)
            return BadRequest(new { message = "Yorum 1000 karakterden uzun olamaz." });

        try
        {
            if (!await _context.Videolar.AnyAsync(v => v.Id == dto.VideoId))
                return NotFound(new { message = "Video bulunamadi." });

            if (!await _context.Kullanicilar.AnyAsync(k => k.Id == dto.KullaniciId))
                return NotFound(new { message = "Kullanici bulunamadi." });

            var yorum = new Yorum
            {
                Icerik = dto.Icerik,
                KullaniciId = dto.KullaniciId,
                VideoId = dto.VideoId,
                Tarih = DateTime.Now
            };

            _context.Yorumlar.Add(yorum);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Yorum eklendi.", id = yorum.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Yorum eklenemedi. VideoId: {VideoId}, KullaniciId: {KullaniciId}", dto.VideoId, dto.KullaniciId);
            return StatusCode(500, new { message = "Yorum eklenemedi. Lutfen tekrar deneyin." });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, [FromQuery] int userId)
    {
        if (id <= 0 || userId <= 0)
            return BadRequest(new { message = "Gecerli id ve userId gereklidir." });

        try
        {
            var yorum = await _context.Yorumlar.FindAsync(id);
            if (yorum == null)
                return NotFound(new { message = "Yorum bulunamadi." });

            if (yorum.KullaniciId != userId)
                return Unauthorized(new { message = "Bu yorumu silme yetkiniz yok." });

            _context.Yorumlar.Remove(yorum);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Yorum silindi." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Yorum silinemedi. Id: {Id}, KullaniciId: {KullaniciId}", id, userId);
            return StatusCode(500, new { message = "Yorum silinemedi. Lutfen tekrar deneyin." });
        }
    }
}
