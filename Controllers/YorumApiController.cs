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

    public YorumApiController(AppDbContext context)
    {
        _context = context;
    }

    // 📌 VİDEOYA AİT YORUMLARI GETİR (En yeni yorum en üstte)
    [HttpGet("video/{videoId}")]
    public async Task<IActionResult> GetByVideo(int videoId)
    {
        var yorumlar = await _context.Yorumlar
            .Include(y => y.Kullanici)
            .Where(y => y.VideoId == videoId)
            .OrderByDescending(y => y.Tarih) // HomeController mantığı: Yeni yorumlar önce
            .Select(y => new YorumDto
            {
                Id = y.Id,
                Icerik = y.Icerik,
                Tarih = y.Tarih,
                KullaniciId = y.KullaniciId,
                KullaniciAdi = y.Kullanici.KullaniciAdi,
                VideoId = y.VideoId
            }).ToListAsync();

        return Ok(yorumlar);
    }

    // 📌 YORUM EKLE
    [HttpPost]
    public async Task<IActionResult> Add(YorumDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Icerik))
            return BadRequest(new { message = "Yorum içeriği boş olamaz." });

        var yorum = new Yorum
        {
            Icerik = dto.Icerik,
            KullaniciId = dto.KullaniciId,
            VideoId = dto.VideoId,
            Tarih = DateTime.Now
        };

        _context.Yorumlar.Add(yorum);
        await _context.SaveChangesAsync();

        // Flutter tarafına eklenen yorumun ID'sini dönmek bazen faydalı olur
        return Ok(new { message = "Yorum eklendi.", id = yorum.Id });
    }

    // 📌 YORUM SİL (Yetki Kontrollü)
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, [FromQuery] int userId)
    {
        var yorum = await _context.Yorumlar.FindAsync(id);
        if (yorum == null) return NotFound(new { message = "Yorum bulunamadı." });

        // HomeController mantığı: Sadece yorumu yazan silebilir. 
        // Not: Admin kontrolü eklemek istersen buraya ekleyebilirsin.
        if (yorum.KullaniciId != userId)
        {
            return Unauthorized(new { message = "Bu yorumu silme yetkiniz yok." });
        }

        _context.Yorumlar.Remove(yorum);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Yorum silindi." });
    }
}