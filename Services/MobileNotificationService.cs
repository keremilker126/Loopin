using Loopin.Data;
using Loopin.Models;
using Microsoft.EntityFrameworkCore;

namespace Loopin.Services
{
    public class MobileNotificationService
    {
        private const string NewVideoType = "new_video";

        private readonly AppDbContext _context;
        private readonly ILogger<MobileNotificationService> _logger;

        public MobileNotificationService(AppDbContext context, ILogger<MobileNotificationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<int> CreateNewVideoNotificationsAsync(Video video)
        {
            try
            {
                var uploader = video.Kullanici
                    ?? await _context.Kullanicilar.AsNoTracking()
                        .FirstOrDefaultAsync(k => k.Id == video.KullaniciId);

                if (uploader == null)
                {
                    return 0;
                }

                var subscriberIds = await _context.Abonelikler
                    .AsNoTracking()
                    .Where(a => a.AboneOlunanId == video.KullaniciId)
                    .Select(a => a.AboneOlanId)
                    .Where(id => id != video.KullaniciId)
                    .Distinct()
                    .ToListAsync();

                if (subscriberIds.Count == 0)
                {
                    return 0;
                }

                var channelName = string.IsNullOrWhiteSpace(uploader.KullaniciAdi)
                    ? "Loopin kanali"
                    : uploader.KullaniciAdi.Trim();
                var title = $"{channelName} yeni video paylasti";
                var message = video.Baslik;
                var now = DateTime.Now;

                foreach (var subscriberId in subscriberIds)
                {
                    _context.MobilBildirimleri.Add(new MobilBildirim
                    {
                        KullaniciId = subscriberId,
                        VideoId = video.Id,
                        KanalKullaniciId = video.KullaniciId,
                        Tip = NewVideoType,
                        Baslik = title,
                        Mesaj = message,
                        Okundu = false,
                        OlusturulmaTarihi = now
                    });
                }

                await _context.SaveChangesAsync();

                var registeredDeviceCount = await _context.MobilCihazTokenlari
                    .AsNoTracking()
                    .CountAsync(t => t.Aktif && subscriberIds.Contains(t.KullaniciId));

                _logger.LogInformation(
                    "Mobil yeni video bildirimi olusturuldu. VideoId: {VideoId}, KullaniciSayisi: {UserCount}, AktifCihazSayisi: {DeviceCount}",
                    video.Id,
                    subscriberIds.Count,
                    registeredDeviceCount);

                return subscriberIds.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Mobil yeni video bildirimi olusturulamadi. VideoId: {VideoId}", video.Id);
                return 0;
            }
        }
    }
}
