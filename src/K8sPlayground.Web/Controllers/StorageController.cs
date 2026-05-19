using Microsoft.AspNetCore.Mvc;

namespace K8sPlayground.Web.Controllers;

/// <summary>
/// PVC (PersistentVolumeClaim) testi.
/// /data dizinine dosya yazar ve listeler. Pod silinip yeniden oluşturulduğunda
/// dosyaların kaybolup kaybolmadığını test ederek emptyDir vs PVC farkını
/// göstermek için kullanılır.
/// </summary>
public class StorageController : Controller
{
    private const long MaxUploadBytes = 10L * 1024 * 1024;   // 10 MB
    private const string UploadsSubdir = "uploads";

    private readonly string _dataPath;
    private readonly ILogger<StorageController> _log;

    public StorageController(IConfiguration cfg, ILogger<StorageController> log)
    {
        _dataPath = cfg["App:DataPath"] ?? "/data";
        _log = log;
    }

    private string UploadsPath => Path.Combine(_dataPath, UploadsSubdir);

    public IActionResult Index()
    {
        ViewBag.DataPath = _dataPath;
        ViewBag.UploadsPath = UploadsPath;
        ViewBag.Exists = Directory.Exists(_dataPath);
        ViewBag.Writable = IsWritable(_dataPath);
        ViewBag.MaxUploadMb = MaxUploadBytes / 1024 / 1024;

        ViewBag.Files = ListFiles(_dataPath);
        ViewBag.Uploads = ListFiles(UploadsPath);

        try
        {
            var di = new DriveInfo(Path.GetPathRoot(_dataPath) ?? "/");
            ViewBag.FreeMb = di.AvailableFreeSpace / 1024 / 1024;
            ViewBag.TotalMb = di.TotalSize / 1024 / 1024;
        }
        catch { }

        return View();
    }

    private static List<(string Name, long Size, DateTime Modified)> ListFiles(string path)
    {
        var files = new List<(string, long, DateTime)>();
        if (!Directory.Exists(path)) return files;
        foreach (var f in Directory.EnumerateFiles(path).Take(200))
        {
            var info = new FileInfo(f);
            files.Add((info.Name, info.Length, info.LastWriteTimeUtc));
        }
        return files;
    }

    [HttpPost]
    public IActionResult Write(string? name, string? content)
    {
        if (!Directory.Exists(_dataPath))
        {
            TempData["msg"] = $"Hata: {_dataPath} dizini yok. PVC bağlı mı?";
            return RedirectToAction(nameof(Index));
        }
        name = string.IsNullOrWhiteSpace(name)
            ? $"note-{DateTime.UtcNow:yyyyMMdd-HHmmss}.txt"
            : Path.GetFileName(name);
        content ??= $"Pod: {Environment.GetEnvironmentVariable("POD_NAME") ?? Environment.MachineName}\n" +
                    $"Tarih: {DateTime.UtcNow:O}\n";

        var full = Path.Combine(_dataPath, name);
        System.IO.File.WriteAllText(full, content);
        TempData["msg"] = $"{name} yazıldı ({content.Length} bayt).";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult Delete(string name, string? folder = null)
    {
        // 'folder' parametresi 'uploads' ise alt dizinden sil, aksi hâlde kök /data.
        var dir = folder == UploadsSubdir ? UploadsPath : _dataPath;
        var full = Path.Combine(dir, Path.GetFileName(name));
        if (System.IO.File.Exists(full))
        {
            System.IO.File.Delete(full);
            TempData["msg"] = $"{name} silindi ({Path.GetRelativePath(_dataPath, full)}).";
        }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Dosya yükler. /data/uploads/ altına kaydedilir; PVC mount edildiyse Pod
    /// silinse bile dosya korunur. Eğitim için RW-once + tek replikalı Deployment'a
    /// uygundur.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
    public async Task<IActionResult> Upload(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            TempData["msg"] = "Hata: Bir dosya seçmediniz.";
            return RedirectToAction(nameof(Index));
        }
        if (file.Length > MaxUploadBytes)
        {
            TempData["msg"] = $"Hata: Dosya {MaxUploadBytes / 1024 / 1024} MB sınırını aşıyor.";
            return RedirectToAction(nameof(Index));
        }
        if (!Directory.Exists(_dataPath))
        {
            TempData["msg"] = $"Hata: {_dataPath} dizini yok. PVC bağlı mı?";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            Directory.CreateDirectory(UploadsPath);

            // Path traversal güvenliği: yalnızca dosya adını al, dizin parçalarını at.
            var safeName = Path.GetFileName(file.FileName);
            if (string.IsNullOrWhiteSpace(safeName))
            {
                TempData["msg"] = "Hata: Geçersiz dosya adı.";
                return RedirectToAction(nameof(Index));
            }

            // Çakışma durumunda üzerine yazma; yeni isim üret.
            var target = Path.Combine(UploadsPath, safeName);
            if (System.IO.File.Exists(target))
            {
                var name = Path.GetFileNameWithoutExtension(safeName);
                var ext  = Path.GetExtension(safeName);
                target = Path.Combine(UploadsPath, $"{name}-{DateTime.UtcNow:yyyyMMddHHmmss}{ext}");
            }

            await using (var fs = new FileStream(target, FileMode.CreateNew, FileAccess.Write))
            {
                await file.CopyToAsync(fs);
            }

            var pod = Environment.GetEnvironmentVariable("POD_NAME") ?? Environment.MachineName;
            _log.LogInformation("Upload: {Name} ({Bytes} B) → {Path} pod={Pod}",
                safeName, file.Length, target, pod);

            TempData["msg"] =
                $"'{safeName}' yüklendi ({file.Length / 1024.0:F1} KB) → " +
                $"{Path.GetRelativePath(_dataPath, target)}. " +
                $"Pod'u silip yeniden başlatın; dosya hâlâ orada olacak.";
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Upload hatası");
            TempData["msg"] = $"Hata: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>Yüklenen dosyayı indir (anti-traversal koruması ile).</summary>
    [HttpGet]
    public IActionResult Download(string name)
    {
        var safeName = Path.GetFileName(name);
        var full = Path.Combine(UploadsPath, safeName);
        if (!System.IO.File.Exists(full)) return NotFound();
        return PhysicalFile(full, "application/octet-stream", safeName);
    }

    private static bool IsWritable(string path)
    {
        try
        {
            if (!Directory.Exists(path)) return false;
            var probe = Path.Combine(path, ".write-probe");
            System.IO.File.WriteAllText(probe, "x");
            System.IO.File.Delete(probe);
            return true;
        }
        catch { return false; }
    }
}
