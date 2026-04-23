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

    public DahaSonraIzleApiController(AppDbContext context)
    {
        _context = context;
    }

    // 📌 KULLANICININ LİSTESİNİ GETİR (Kitaplık sayfası için)
    [HttpGet("{userId}")]
    public async Task<IActionResult> GetList(int userId)
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
                KullaniciAdi = x.Video.Kullanici.KullaniciAdi
            }).ToListAsync();

        return Ok(liste);
    }

    // 📌 LİSTEYE EKLE / VARSA ÇIKAR (Toggle Mantığı)
    // Flutter'da tek butonla hem ekleyip hem çıkarmak için bu yapı daha pratiktir
    [HttpPost("toggle")]
    public async Task<IActionResult> Toggle([FromBody] DahaSonraIzleDto dto)
    {
        if (dto == null) return BadRequest("Geçersiz veri");

        var mevcut = await _context.DahaSonraIzleListesi
            .FirstOrDefaultAsync(x => x.KullaniciId == dto.KullaniciId && x.VideoId == dto.VideoId);

        if (mevcut == null)
        {
            _context.DahaSonraIzleListesi.Add(new DahaSonraIzle
            {
                KullaniciId = dto.KullaniciId,
                VideoId = dto.VideoId,
                EklenmeTarihi = DateTime.Now
            });
            await _context.SaveChangesAsync();
            return Ok(new { status = "added", message = "Daha sonra izle listesine eklendi." });
        }
        else
        {
            _context.DahaSonraIzleListesi.Remove(mevcut);
            await _context.SaveChangesAsync();
            return Ok(new { status = "removed", message = "Listeden çıkarıldı." });
        }
    }

    // 📌 LİSTEDEN SİL (Alternatif Delete Metodu)
    [HttpDelete]
    public async Task<IActionResult> Remove(DahaSonraIzleDto dto)
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

    // 📌 TÜM LİSTEYİ TEMİZLE
    [HttpDelete("clear/{userId}")]
    public async Task<IActionResult> ClearAll(int userId)
    {
        var liste = _context.DahaSonraIzleListesi.Where(x => x.KullaniciId == userId);
        _context.DahaSonraIzleListesi.RemoveRange(liste);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Liste tamamen temizlendi." });
    }
}