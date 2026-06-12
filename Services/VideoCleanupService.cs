using Microsoft.EntityFrameworkCore;
using Loopin.Data;

public class VideoCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VideoCleanupService> _logger;

    public VideoCleanupService(IServiceScopeFactory scopeFactory, ILogger<VideoCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TemizlikYap();
                await Task.Delay(TimeSpan.FromDays(7), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Video temizlik servisinde hata olustu.");
                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
            }
        }
    }

    private async Task TemizlikYap()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var root = Directory.GetCurrentDirectory();

        var eskiVideolar = await context.Videolar
            .Where(v =>
                v.YuklenmeTarihi < DateTime.Now.AddDays(-7) &&
                (v.IzlenmeSayisi == 0 || v.LikeSayisi == 0))
            .ToListAsync();

        foreach (var video in eskiVideolar)
        {
            try
            {
                if (!string.IsNullOrEmpty(video.VideoUrl))
                {
                    var videoPath = Path.Combine(root, "wwwroot",
                        video.VideoUrl.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString()));

                    if (System.IO.File.Exists(videoPath))
                        System.IO.File.Delete(videoPath);
                }

                if (!string.IsNullOrEmpty(video.KapakResmiUrl))
                {
                    var imagePath = Path.Combine(root, "wwwroot",
                        video.KapakResmiUrl.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString()));

                    if (System.IO.File.Exists(imagePath))
                        System.IO.File.Delete(imagePath);
                }

                context.Videolar.Remove(video);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Video temizlenirken hata olustu. Video id: {VideoId}", video.Id);
            }
        }

        try
        {
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Video temizlik kayitlari veritabanina yazilirken hata olustu.");
        }
    }
}
