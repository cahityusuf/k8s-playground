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
    private readonly string _dataPath;
    public StorageController(IConfiguration cfg)
    {
        _dataPath = cfg["App:DataPath"] ?? "/data";
    }

    public IActionResult Index()
    {
        ViewBag.DataPath = _dataPath;
        ViewBag.Exists = Directory.Exists(_dataPath);
        ViewBag.Writable = IsWritable(_dataPath);

        var files = new List<(string Name, long Size, DateTime Modified)>();
        if (Directory.Exists(_dataPath))
        {
            foreach (var f in Directory.EnumerateFiles(_dataPath).Take(200))
            {
                var info = new FileInfo(f);
                files.Add((info.Name, info.Length, info.LastWriteTimeUtc));
            }
        }
        ViewBag.Files = files;

        try
        {
            var di = new DriveInfo(Path.GetPathRoot(_dataPath) ?? "/");
            ViewBag.FreeMb = di.AvailableFreeSpace / 1024 / 1024;
            ViewBag.TotalMb = di.TotalSize / 1024 / 1024;
        }
        catch { }

        return View();
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
    public IActionResult Delete(string name)
    {
        var full = Path.Combine(_dataPath, Path.GetFileName(name));
        if (System.IO.File.Exists(full))
        {
            System.IO.File.Delete(full);
            TempData["msg"] = $"{name} silindi.";
        }
        return RedirectToAction(nameof(Index));
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
