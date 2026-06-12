using Loopin.Data;
using Loopin.Models;
using Loopin.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Loopin.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class VideoApiController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly MobileNotificationService _mobileNotificationService;
    private readonly ILogger<VideoApiController> _logger;

    public VideoApiController(
        AppDbContext context,
        MobileNotificationService mobileNotificationService,
        ILogger<VideoApiController> logger)
    {
        _context = context;
        _mobileNotificationService = mobileNotificationService;
        _logger = logger;
    }

    private void TryDeletePublicFile(string? publicUrl)
    {
        if (string.IsNullOrWhiteSpace(publicUrl))
            return;

        var relativePath = publicUrl.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(relativePath))
        {
            _logger.LogWarning("Gecersiz medya dosyasi yolu reddedildi. PublicUrl: {PublicUrl}", publicUrl);
            return;
        }

        var webRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"));
        var filePath = Path.GetFullPath(Path.Combine(webRoot, relativePath));

        if (!filePath.StartsWith(webRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("wwwroot disindaki medya dosyasi silme istegi reddedildi. PublicUrl: {PublicUrl}", publicUrl);
            return;
        }

        try
        {
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Medya dosyasi silinemedi. Dosya: {FilePath}", filePath);
        }
    }

    private async Task DeleteVideoWithRelationsAsync(Video video)
    {
        _context.Yorumlar.RemoveRange(await _context.Yorumlar.Where(y => y.VideoId == video.Id).ToListAsync());
        _context.Begenmeler.RemoveRange(await _context.Begenmeler.Where(b => b.VideoId == video.Id).ToListAsync());
        _context.GecmisListesi.RemoveRange(await _context.GecmisListesi.Where(g => g.VideoId == video.Id).ToListAsync());
        _context.DahaSonraIzleListesi.RemoveRange(await _context.DahaSonraIzleListesi.Where(d => d.VideoId == video.Id).ToListAsync());
        _context.KanalEtkilesimleri.RemoveRange(await _context.KanalEtkilesimleri.Where(e => e.VideoId == video.Id).ToListAsync());

        TryDeletePublicFile(video.VideoUrl);
        TryDeletePublicFile(video.KapakResmiUrl);

        _context.Videolar.Remove(video);
        await _context.SaveChangesAsync();
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var videolar = await _context.Videolar
                .Include(v => v.Kullanici)
                .OrderByDescending(v => v.YuklenmeTarihi)
                .Take(75)
                .ToListAsync();

            var dtoler = new List<VideoDto>();
            foreach (var video in videolar)
                dtoler.Add(await MapToDtoWithCounts(video));

            return Ok(dtoler);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tum videolar listelenirken hata olustu.");
            return StatusCode(500, new { message = "Videolar getirilemedi. Lutfen tekrar deneyin." });
        }
    }

    [HttpGet("trends")]
    public async Task<IActionResult> GetTrends()
    {
        try
        {
            var trendler = await _context.Videolar
                .Include(v => v.Kullanici)
                .OrderByDescending(v => v.LikeSayisi)
                .ThenByDescending(v => v.IzlenmeSayisi)
                .Take(75)
                .ToListAsync();

            var dtoler = new List<VideoDto>();
            foreach (var video in trendler)
                dtoler.Add(await MapToDtoWithCounts(video));

            return Ok(dtoler);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Trend videolar listelenirken hata olustu.");
            return StatusCode(500, new { message = "Trend videolar getirilemedi. Lutfen tekrar deneyin." });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id, [FromQuery] int? userId)
    {
        if (id <= 0)
            return BadRequest(new { message = "Gecerli bir videoId gereklidir." });

        if (userId.HasValue && userId <= 0)
            return BadRequest(new { message = "Gecerli bir userId gereklidir." });

        try
        {
            var video = await _context.Videolar
                .Include(x => x.Kullanici)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (video == null)
                return NotFound(new { message = "Video bulunamadi." });

            var izlenmeArtir = true;

            if (userId.HasValue)
            {
                var mevcutGecmis = await _context.GecmisListesi
                    .FirstOrDefaultAsync(g => g.KullaniciId == userId.Value && g.VideoId == id);

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
                    izlenmeArtir = false;
                    mevcutGecmis.IzlenmeTarihi = DateTime.Now;
                }
            }

            if (izlenmeArtir)
            {
                video.IzlenmeSayisi++;
                _context.KanalEtkilesimleri.Add(new KanalEtkilesimi
                {
                    VideoId = id,
                    Tur = KanalEtkilesimi.Goruntulenme,
                    Tarih = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();

            return Ok(await MapToDtoWithCounts(video));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Video getirilirken hata olustu. VideoId: {VideoId}, KullaniciId: {KullaniciId}", id, userId);
            return StatusCode(500, new { message = "Video getirilemedi. Lutfen tekrar deneyin." });
        }
    }

    [HttpPost("upload")]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> Upload(
        [FromForm] IFormFile? videoDosyasi,
        [FromForm] IFormFile? kapakResmi,
        [FromForm] string baslik,
        [FromForm] string aciklama,
        [FromForm] int kullaniciId)
    {
        baslik = (baslik ?? "").Trim();
        aciklama = (aciklama ?? "").Trim();

        if (baslik.Length < 3 || baslik.Length > 120)
            return BadRequest(new { message = "Video basligi 3-120 karakter arasinda olmalidir." });

        if (aciklama.Length > 1000)
            return BadRequest(new { message = "Aciklama 1000 karakterden uzun olamaz." });

        if (videoDosyasi == null || videoDosyasi.Length == 0)
            return BadRequest(new { message = "Video dosyasi gereklidir." });

        if (videoDosyasi.Length > 300 * 1024 * 1024)
            return BadRequest(new { message = "Video cok buyuk. (Max 300MB)" });

        if (kapakResmi != null && kapakResmi.Length > 5 * 1024 * 1024)
            return BadRequest(new { message = "Kapak resmi cok buyuk. (Max 5MB)" });

        string? savedVideoUrl = null;
        string? savedImageUrl = null;

        try
        {
            if (!await _context.Kullanicilar.AnyAsync(k => k.Id == kullaniciId))
                return BadRequest(new { message = "Gecerli bir kullanici gereklidir." });

            var videoExt = Path.GetExtension(videoDosyasi.FileName).ToLowerInvariant();
            if (videoExt != ".mp4")
                return BadRequest(new { message = "Sadece MP4 video yukleyebilirsiniz." });

            if (kapakResmi != null && kapakResmi.Length > 0)
            {
                var imageExt = Path.GetExtension(kapakResmi.FileName).ToLowerInvariant();
                var allowedImageExtensions = new[] { ".png", ".jpg", ".jpeg" };

                if (!allowedImageExtensions.Contains(imageExt))
                    return BadRequest(new { message = "Kapak resmi PNG, JPG veya JPEG olmalidir." });
            }

            var videoFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/videos");
            var imageFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images");

            Directory.CreateDirectory(videoFolder);
            Directory.CreateDirectory(imageFolder);

            var videoFileName = Guid.NewGuid() + videoExt;
            var videoPath = Path.Combine(videoFolder, videoFileName);

            await using (var stream = new FileStream(videoPath, FileMode.Create))
            {
                await videoDosyasi.CopyToAsync(stream);
            }

            savedVideoUrl = "/videos/" + videoFileName;

            var imgFileName = "default_thumb.jpg";

            if (kapakResmi != null && kapakResmi.Length > 0)
            {
                imgFileName = Guid.NewGuid() + Path.GetExtension(kapakResmi.FileName).ToLowerInvariant();
                var imgPath = Path.Combine(imageFolder, imgFileName);

                await using (var stream = new FileStream(imgPath, FileMode.Create))
                {
                    await kapakResmi.CopyToAsync(stream);
                }

                savedImageUrl = "/images/" + imgFileName;
            }

            var yeniVideo = new Video
            {
                Baslik = baslik,
                Aciklama = aciklama,
                KullaniciId = kullaniciId,
                VideoUrl = savedVideoUrl,
                KapakResmiUrl = savedImageUrl ?? "/images/" + imgFileName,
                YuklenmeTarihi = DateTime.Now,
                IzlenmeSayisi = 0,
                LikeSayisi = 0
            };

            _context.Videolar.Add(yeniVideo);
            await _context.SaveChangesAsync();

            var mobileNotificationCount = await _mobileNotificationService.CreateNewVideoNotificationsAsync(yeniVideo);

            return Ok(new
            {
                message = "Video basariyla yuklendi.",
                videoId = yeniVideo.Id,
                mobileNotificationCount
            });
        }
        catch (Exception ex)
        {
            TryDeletePublicFile(savedVideoUrl);
            TryDeletePublicFile(savedImageUrl);
            _logger.LogError(ex, "Video API yukleme sirasinda hata olustu. KullaniciId: {KullaniciId}", kullaniciId);

            return StatusCode(500, new { message = "Yukleme sirasinda hata olustu. Lutfen tekrar deneyin." });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (id <= 0)
            return BadRequest(new { message = "Gecerli bir videoId gereklidir." });

        try
        {
            var video = await _context.Videolar.FindAsync(id);
            if (video == null)
                return NotFound(new { message = "Video bulunamadi." });

            await DeleteVideoWithRelationsAsync(video);

            return Ok(new { message = "Video silindi." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Video silinirken hata olustu. VideoId: {VideoId}", id);
            return StatusCode(500, new { message = "Video silinemedi. Lutfen tekrar deneyin." });
        }
    }

    private async Task<VideoDto> MapToDtoWithCounts(Video video)
    {
        var aboneSayisi = await _context.Abonelikler
            .CountAsync(a => a.AboneOlunanId == video.KullaniciId);

        return new VideoDto
        {
            Id = video.Id,
            Baslik = video.Baslik,
            VideoUrl = video.VideoUrl,
            KapakResmiUrl = video.KapakResmiUrl,
            Aciklama = video.Aciklama,
            YuklenmeTarihi = video.YuklenmeTarihi,
            IzlenmeSayisi = video.IzlenmeSayisi,
            LikeSayisi = video.LikeSayisi,
            KullaniciId = video.KullaniciId,
            KullaniciAdi = video.Kullanici?.KullaniciAdi ?? "Bilinmeyen Kullanici",
            AboneSayisi = aboneSayisi
        };
    }

    [HttpPost("{id}/izlenme")]
    public async Task<IActionResult> IncreaseViewCount(int id, [FromQuery] int? userId)
    {
        if (id <= 0)
            return BadRequest(new { message = "Gecerli bir videoId gereklidir." });

        if (userId.HasValue && userId <= 0)
            return BadRequest(new { message = "Gecerli bir userId gereklidir." });

        try
        {
            var video = await _context.Videolar
                .Include(v => v.Kullanici)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (video == null)
                return NotFound(new { message = "Video bulunamadi." });

            var izlenmeArtir = true;

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
                    izlenmeArtir = false;
                    mevcutGecmis.IzlenmeTarihi = DateTime.Now;
                }
            }

            if (izlenmeArtir)
            {
                video.IzlenmeSayisi++;
                _context.KanalEtkilesimleri.Add(new KanalEtkilesimi
                {
                    VideoId = id,
                    Tur = KanalEtkilesimi.Goruntulenme,
                    Tarih = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();
            return Ok(new { yeniIzlenmeSayisi = video.IzlenmeSayisi, izlenmeArtti = izlenmeArtir });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Video izlenme guncellenirken hata olustu. VideoId: {VideoId}, KullaniciId: {KullaniciId}", id, userId);
            return StatusCode(500, new { message = "Izlenme guncellenirken hata olustu. Lutfen tekrar deneyin." });
        }
    }

    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetUserVideos(int userId)
    {
        if (userId <= 0)
            return BadRequest(new { message = "Gecerli bir kullaniciId gereklidir." });

        try
        {
            var videolar = await _context.Videolar
                .Include(v => v.Kullanici)
                .Where(v => v.KullaniciId == userId)
                .OrderByDescending(v => v.YuklenmeTarihi)
                .ToListAsync();

            var dtoler = new List<VideoDto>();
            foreach (var video in videolar)
                dtoler.Add(await MapToDtoWithCounts(video));

            return Ok(dtoler);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kullanici videolari listelenirken hata olustu. KullaniciId: {KullaniciId}", userId);
            return StatusCode(500, new { message = "Kullanici videolari getirilemedi. Lutfen tekrar deneyin." });
        }
    }
}
