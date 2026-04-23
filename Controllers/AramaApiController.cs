using Loopin.Data;
using Loopin.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Loopin.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class AramaApiController : ControllerBase
{
    private readonly AppDbContext _context;

    public AramaApiController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { message = "Arama terimi boş olamaz." });

        // Küçük harf/büyük harf duyarlılığını yönetmek için ToLower() kullanabilirsin 
        // (Ancak SQL Server konfigürasyonuna göre Contains zaten duyarsız çalışabilir)
        var searchTerm = q.ToLower();
        
        // 1. VİDEOLARDA ARA (Başlık veya Açıklama)
        var videolar = await _context.Videolar
            .Include(v => v.Kullanici)
            .Where(v => v.Baslik.ToLower().Contains(searchTerm) || 
                        (v.Aciklama != null && v.Aciklama.ToLower().Contains(searchTerm)))
            .OrderByDescending(v => v.IzlenmeSayisi) // En çok izlenenleri öne çıkar
            .Select(v => new VideoDto
            {
                Id = v.Id,
                Baslik = v.Baslik,
                VideoUrl = v.VideoUrl,
                KapakResmiUrl = v.KapakResmiUrl,
                Aciklama = v.Aciklama,
                YuklenmeTarihi = v.YuklenmeTarihi,
                IzlenmeSayisi = v.IzlenmeSayisi,
                LikeSayisi = v.LikeSayisi,
                KullaniciId = v.KullaniciId,
                KullaniciAdi = v.Kullanici.KullaniciAdi,
                AboneSayisi = v.AboneSayisi,
                
            }).ToListAsync();

        // 2. KULLANICILARDA ARA (Kanal Arama)
        var kullanicilar = await _context.Kullanicilar
            .Where(u => u.KullaniciAdi.ToLower().Contains(searchTerm))
            .Select(u => new KullaniciDto
            {
                Id = u.Id,
                KullaniciAdi = u.KullaniciAdi,
                Email = u.Email,
                EmailOnayli = u.EmailOnayli
            }).ToListAsync();

        // 3. SONUÇLARI BİRLEŞTİR
        return Ok(new
        {
            query = q,
            videoCount = videolar.Count,
            userCount = kullanicilar.Count,
            videolar = videolar,
            kullanicilar = kullanicilar
        });
    }
}