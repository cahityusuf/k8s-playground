using Microsoft.AspNetCore.Mvc;

namespace K8sPlayground.Web.Controllers;

/// <summary>
/// Kontrollü hata enjeksiyonu sayfası.
/// CrashLoopBackOff, restartPolicy, liveness probe failure ve graceful
/// shutdown davranışlarını gözlemlemek için kullanılır.
/// </summary>
public class ChaosController : Controller
{
    private readonly IHostApplicationLifetime _lifetime;
    public ChaosController(IHostApplicationLifetime lifetime) => _lifetime = lifetime;

    public IActionResult Index() => View();

    /// <summary>Uygulamayı 0 kodu ile düzgün kapatır (SIGTERM benzeri).</summary>
    [HttpPost]
    public IActionResult GracefulExit()
    {
        TempData["msg"] = "Graceful shutdown tetiklendi. K8s Pod'u yeniden başlatacak.";
        _ = Task.Run(async () =>
        {
            await Task.Delay(500);
            _lifetime.StopApplication();
        });
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Sürecin tamamını öldürür — CrashLoopBackOff'u tetikler.</summary>
    [HttpPost]
    public IActionResult Crash()
    {
        TempData["msg"] = "Crash tetiklendi. Pod CrashLoopBackOff'a girebilir.";
        _ = Task.Run(async () =>
        {
            await Task.Delay(500);
            Environment.FailFast("Kullanıcı kasten crash istedi (eğitim demosu).");
        });
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Yakalanmamış bir exception fırlatır.</summary>
    [HttpPost]
    public IActionResult ThrowUnhandled()
    {
        _ = Task.Run(() =>
        {
            Thread.Sleep(500);
            throw new InvalidOperationException("Eğitim için kasten fırlatılan exception.");
        });
        TempData["msg"] = "0.5 sn içinde yakalanmamış bir exception fırlayacak.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Belirtilen süre boyunca event loop'u meşgul ederek probe'u zorlar.</summary>
    [HttpPost]
    public IActionResult HangFor(int seconds = 15)
    {
        seconds = Math.Clamp(seconds, 1, 120);
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < deadline) Thread.SpinWait(100_000);
        TempData["msg"] = $"{seconds} sn boyunca thread bloklandı.";
        return RedirectToAction(nameof(Index));
    }
}
