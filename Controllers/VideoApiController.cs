using Loopin.Data;
using Loopin.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace Loopin.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class VideoApiController : ControllerBase
{
    private readonly AppDbContext _context;

    public VideoApiController(AppDbContext context)
    {
        _context = context;
    }

    // 📌 TÜM VİDEOLAR (Abone sayısı eklendi)
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var videolar = await _context.Videolar
            .Include(v => v.Kullanici)
            .OrderByDescending(v => v.YuklenmeTarihi)
            .ToListAsync();

        // Her video için abone sayısını hesaplayarak DTO'ya çeviriyoruz
        var dtoler = new List<VideoDto>();
        foreach (var v in videolar)
        {
            dtoler.Add(await MapToDtoWithCounts(v));
        }

        return Ok(dtoler);
    }

    // 📌 TREND VİDEOLAR (Abone sayısı eklendi)
    [HttpGet("trends")]
    public async Task<IActionResult> GetTrends()
    {
        var trendler = await _context.Videolar
            .Include(v => v.Kullanici)
            .OrderByDescending(v => v.LikeSayisi)
            .ThenByDescending(v => v.IzlenmeSayisi)
            .Take(70) // Burayı 70 yaptık
            .ToListAsync();

        var dtoler = new List<VideoDto>();
        foreach (var v in trendler)
        {
            dtoler.Add(await MapToDtoWithCounts(v));
        }

        return Ok(dtoler);
    }

    // 📌 TEK VİDEO VE İZLENME ARTIRMA
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id, [FromQuery] int? userId)
    {
        var v = await _context.Videolar
            .Include(x => x.Kullanici)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (v == null) return NotFound(new { message = "Video bulunamadı." });

        v.IzlenmeSayisi++;

        if (userId.HasValue)
        {
            var mevcutGecmis = await _context.GecmisListesi
                .FirstOrDefaultAsync(g => g.KullaniciId == userId && g.VideoId == id);

            if (mevcutGecmis == null)
            {
                _context.GecmisListesi.Add(new Gecmis
                {
                    KullaniciId = userId.Value,
                    VideoId = id,
                    IzlenmeTarihi = DateTime.Now
                });
            }
            else
            {
                mevcutGecmis.IzlenmeTarihi = DateTime.Now;
            }
        }

        await _context.SaveChangesAsync();

        return Ok(await MapToDtoWithCounts(v));
    }

    // 📌 VİDEO YÜKLEME
    [HttpPost("upload")]
    [DisableRequestSizeLimit] // Program.cs limitlerini bu metod için açar
    public async Task<IActionResult> Upload([FromForm] IFormFile videoDosyası, [FromForm] IFormFile kapakResmi, [FromForm] string baslik, [FromForm] string aciklama, [FromForm] int kullaniciId)
    {
        if (videoDosyası == null || videoDosyası.Length == 0)
            return BadRequest(new { message = "Video dosyası gereklidir." });

        try
        {
            string videoFileName = Guid.NewGuid().ToString() + Path.GetExtension(videoDosyası.FileName);
            string videoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/videos", videoFileName);

            using (var stream = new FileStream(videoPath, FileMode.Create))
            {
                await videoDosyası.CopyToAsync(stream);
            }

            string imgFileName = "default_thumb.jpg";
            if (kapakResmi != null)
            {
                imgFileName = Guid.NewGuid().ToString() + Path.GetExtension(kapakResmi.FileName);
                string imgPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images", imgFileName);
                using (var stream = new FileStream(imgPath, FileMode.Create))
                {
                    await kapakResmi.CopyToAsync(stream);
                }
            }

            var yeniVideo = new Video
            {
                Baslik = baslik,
                Aciklama = aciklama,
                KullaniciId = kullaniciId,
                VideoUrl = "/videos/" + videoFileName,
                KapakResmiUrl = "/images/" + imgFileName,
                YuklenmeTarihi = DateTime.Now,
                IzlenmeSayisi = 0,
                LikeSayisi = 0
            };

            _context.Videolar.Add(yeniVideo);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Video başarıyla yüklendi.", videoId = yeniVideo.Id });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Yükleme sırasında hata oluştu: " + ex.Message });
        }
    }

    // 📌 SİL
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var video = await _context.Videolar.FindAsync(id);
        if (video == null) return NotFound();

        _context.Videolar.Remove(video);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Video silindi." });
    }

    // 🔥 DÜZELTİLEN YARDIMCI METOD: Abone sayısını anlık hesaplar
    private async Task<VideoDto> MapToDtoWithCounts(Video v)
    {
        // Videoyu yükleyen kanala ait toplam aboneleri sayıyoruz
        var aboneSayisi = await _context.Abonelikler
            .CountAsync(a => a.AboneOlunanId == v.KullaniciId);

        return new VideoDto
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
            KullaniciAdi = v.Kullanici?.KullaniciAdi ?? "Bilinmeyen Kullanıcı",
            AboneSayisi = aboneSayisi // 🔥 DTO'daki yeni alana veriyi basıyoruz
        };
    }
    // [HttpPost("{id}/izlenme")]
    // public async Task<IActionResult> IncreaseViewCount(int id)
    // {
    //     var video = await _context.Videolar.FindAsync(id);
    //     if (video == null) return NotFound();

    //     video.IzlenmeSayisi++;
    //     await _context.SaveChangesAsync();

    //     return Ok(new { yeniIzlenmeSayisi = video.IzlenmeSayisi });
    // }

    // 📌 İZLENME SAYISI ARTIRMA
    [HttpPost("{id}/izlenme")]
    public async Task<IActionResult> IncreaseViewCount(int id, [FromQuery] int? userId)
    {
        var video = await _context.Videolar
            .Include(v => v.Kullanici)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (video == null) return NotFound(new { message = "Video bulunamadı." });

        video.IzlenmeSayisi++;

        if (userId.HasValue)
        {
            var mevcutGecmis = await _context.GecmisListesi
                .FirstOrDefaultAsync(g => g.KullaniciId == userId && g.VideoId == id);

            if (mevcutGecmis == null)
            {
                _context.GecmisListesi.Add(new Gecmis
                {
                    KullaniciId = userId.Value,
                    VideoId = id,
                    IzlenmeTarihi = DateTime.Now
                });
            }
            else
            {
                mevcutGecmis.IzlenmeTarihi = DateTime.Now;
            }
        }

        await _context.SaveChangesAsync();

        return Ok(new { yeniIzlenmeSayisi = video.IzlenmeSayisi });
    }
    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetUserVideos(int userId)
    {
        var videolar = await _context.Videolar
            .Include(v => v.Kullanici) // Kullanıcı bilgisini dahil et
            .Where(v => v.KullaniciId == userId)
            .OrderByDescending(v => v.YuklenmeTarihi)
            .ToListAsync();

        // Flutter'daki VideoModel.fromJson yapısına uygun DTO listesine çeviriyoruz
        var dtoler = new List<VideoDto>();
        foreach (var v in videolar)
        {
            dtoler.Add(await MapToDtoWithCounts(v)); // Kendi yazdığınız yardımcı metodu kullanın
        }

        return Ok(dtoler);
    }
    // GET: api/VideoApi/{id}
    [HttpGet("{id}")]
public async Task<ActionResult<VideoDto>> GetVideoById(int id)
{
    var video = await _context.Videolar
        .Include(v => v.Kullanici)
        .FirstOrDefaultAsync(v => v.Id == id);

    if (video == null)
        return NotFound(new { message = "Video bulunamadı." });

    var dto = new VideoDto
    {
        Id = video.Id,
        Baslik = video.Baslik,
        VideoUrl = video.VideoUrl ?? "",
        KapakResmiUrl = video.KapakResmiUrl ?? "",
        Aciklama = video.Aciklama ?? "",
        YuklenmeTarihi = video.YuklenmeTarihi,
        IzlenmeSayisi = video.IzlenmeSayisi,
        LikeSayisi = video.LikeSayisi,
        KullaniciId = video.KullaniciId,
        KullaniciAdi = video.Kullanici?.KullaniciAdi ?? "Bilinmeyen"
    };

    return Ok(dto);
}

}