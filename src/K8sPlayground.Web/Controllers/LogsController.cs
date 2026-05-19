using Microsoft.AspNetCore.Mvc;

namespace K8sPlayground.Web.Controllers;

/// <summary>
/// Farklı seviyelerde log üretir. 'kubectl logs', log toplama (Fluent Bit,
/// Loki vs.) ve stdout/stderr ayrımını göstermek için kullanılır.
/// </summary>
public class LogsController : Controller
{
    private readonly ILogger<LogsController> _log;
    public LogsController(ILogger<LogsController> log) => _log = log;

    public IActionResult Index() => View();

    [HttpPost]
    public IActionResult Emit(string level = "Information", string message = "Merhaba K8s!", int count = 1)
    {
        count = Math.Clamp(count, 1, 1000);

        // Eğitim için: kullanıcı 'Trace' veya 'Debug' seçtiğinde mevcut log filtresi
        // bu seviyeyi yutuyorsa hiç log çıkmaz. Bunu sessizce yapmak yerine UI'da
        // açıkça söyleyelim ki kursiyer "neden boş?" diye düşünmesin.
        var mappedLevel = level switch
        {
            "Trace"       => LogLevel.Trace,
            "Debug"       => LogLevel.Debug,
            "Information" => LogLevel.Information,
            "Warning"     => LogLevel.Warning,
            "Error"       => LogLevel.Error,
            "Critical"    => LogLevel.Critical,
            _             => LogLevel.Information,
        };

        if (level != "StdErr" && !_log.IsEnabled(mappedLevel))
        {
            TempData["msg"] =
                $"⚠ {level} seviyesi mevcut log filtresinde KAPALI — log üretilmedi. " +
                $"appsettings.json içindeki Logging:LogLevel ayarını veya " +
                $"ASPNETCORE_ENVIRONMENT=Development değişkenini değiştirerek açabilirsiniz.";
            return RedirectToAction(nameof(Index));
        }

        for (var i = 1; i <= count; i++)
        {
            switch (level)
            {
                case "Trace":       _log.LogTrace("{i} {msg}", i, message); break;
                case "Debug":       _log.LogDebug("{i} {msg}", i, message); break;
                case "Warning":     _log.LogWarning("{i} {msg}", i, message); break;
                case "Error":       _log.LogError("{i} {msg}", i, message); break;
                case "Critical":    _log.LogCritical("{i} {msg}", i, message); break;
                case "StdErr":      Console.Error.WriteLine($"[STDERR] {i} {message}"); break;
                default:            _log.LogInformation("{i} {msg}", i, message); break;
            }
        }
        TempData["msg"] = $"{count} adet {level} log üretildi. 'kubectl logs <pod>' ile görüntüleyin.";
        return RedirectToAction(nameof(Index));
    }
}
