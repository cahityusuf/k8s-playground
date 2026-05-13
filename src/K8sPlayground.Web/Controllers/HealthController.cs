using K8sPlayground.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace K8sPlayground.Web.Controllers;

/// <summary>
/// Liveness ve readiness probe davranışını canlı olarak değiştirmeyi sağlar.
/// readiness'i kapatıp 'kubectl get endpoints' ile Pod'un endpoint listesinden
/// düştüğünü gözlemleyebilirsiniz.
/// </summary>
public class HealthController : Controller
{
    private readonly ReadinessState _state;
    public HealthController(ReadinessState state) => _state = state;

    public IActionResult Index()
    {
        ViewBag.IsReady = _state.IsReady;
        return View();
    }

    [HttpPost]
    public IActionResult ToggleReady()
    {
        var now = _state.Toggle();
        TempData["msg"] = $"Readiness {(now ? "AÇIK" : "KAPALI")}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult SetReady(bool ready)
    {
        _state.SetReady(ready);
        TempData["msg"] = $"Readiness {(ready ? "AÇIK" : "KAPALI")} olarak ayarlandı.";
        return RedirectToAction(nameof(Index));
    }
}
