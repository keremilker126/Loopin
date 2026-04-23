using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Loopin.Data;
using Loopin.Models;

[Route("api/[controller]")]
[ApiController]
public class GecmisApiController : ControllerBase
{
    private readonly AppDbContext _context;

    public GecmisApiController(AppDbContext context)
    {
        _context = context;
    }

    // 1. Kullanıcının İzleme Geçmişini Getir
    [HttpGet("kullanici/{kullaniciId}")]
    public async Task<ActionResult<IEnumerable<GecmisDto>>> GetKullaniciGecmisi(int kullaniciId)
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

    // 2. Geçmişe Yeni İzleme Kaydı Ekle
    [HttpPost]
    public async Task<ActionResult> GecmiseEkle(GecmisDto dto)
    {
        var mevcutKayit = await _context.GecmisListesi
            .FirstOrDefaultAsync(x => x.KullaniciId == dto.KullaniciId && x.VideoId == dto.VideoId);

        if (mevcutKayit != null)
        {
            mevcutKayit.IzlenmeTarihi = DateTime.Now;
            _context.GecmisListesi.Update(mevcutKayit);
        }
        else
        {
            var yeniGecmis = new Gecmis
            {
                KullaniciId = dto.KullaniciId,
                VideoId = dto.VideoId,
                IzlenmeTarihi = DateTime.Now
            };
            _context.GecmisListesi.Add(yeniGecmis);
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Geçmişe kaydedildi." });
    }

    // 3. Geçmişten Tek Bir Kayıt Sil
    [HttpDelete("{id}")]
    public async Task<IActionResult> KayitSil(int id)
    {
        var kayit = await _context.GecmisListesi.FindAsync(id);
        if (kayit == null) return NotFound();

        _context.GecmisListesi.Remove(kayit);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Kayıt silindi." });
    }

    // 4. Bir Kullanıcının Tüm Geçmişini Temizle
    [HttpDelete("temizle/{kullaniciId}")]
    public async Task<IActionResult> TumGecmisiSil(int kullaniciId)
    {
        var liste = await _context.GecmisListesi
            .Where(x => x.KullaniciId == kullaniciId)
            .ToListAsync();

        if (!liste.Any()) return NotFound("Geçmiş zaten boş.");

        _context.GecmisListesi.RemoveRange(liste);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Tüm izleme geçmişi temizlendi." });
    }
    // 5. VideoId'ye göre video detayını getir
    // GET: api/Gecmis/video/10
    [HttpGet("video/{videoId}")]
    public async Task<ActionResult<VideoDto>> GetVideoById(int videoId)
    {
        var video = await _context.Videolar
            .Include(v => v.Kullanici)
            .FirstOrDefaultAsync(v => v.Id == videoId);

        if (video == null) return NotFound();

        var dto = new VideoDto
        {
            Id = video.Id,
            Baslik = video.Baslik,
            Aciklama = video.Aciklama,
            KapakResmiUrl = $"http://localhost:5144/{video.KapakResmiUrl}",
            VideoUrl = $"http://localhost:5144/{video.VideoUrl}",
            KullaniciId = video.KullaniciId,
            KullaniciAdi = video.Kullanici.KullaniciAdi,
            IzlenmeSayisi = video.IzlenmeSayisi,
            LikeSayisi = video.LikeSayisi
        };

        return Ok(dto);
    }



}
