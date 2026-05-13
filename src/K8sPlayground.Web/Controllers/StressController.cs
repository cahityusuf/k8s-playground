using K8sPlayground.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace K8sPlayground.Web.Controllers;

/// <summary>
/// CPU ve bellek stres testleri.
/// HPA (Horizontal Pod Autoscaler), resources.limits ve OOMKilled
/// senaryolarını canlı olarak demo etmek için kullanılır.
/// </summary>
public class StressController : Controller
{
    private readonly MemoryBallast _ballast;
    public StressController(MemoryBallast ballast) => _ballast = ballast;

    public IActionResult Index()
    {
        ViewBag.MemMb = _ballast.AllocatedBytes / 1024 / 1024;
        var proc = System.Diagnostics.Process.GetCurrentProcess();
        ViewBag.WorkingSetMb = proc.WorkingSet64 / 1024 / 1024;
        return View();
    }

    /// <summary>
    /// 'seconds' saniye boyunca 'workers' adet CPU çekirdeğini doldurur.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Cpu(int seconds = 10, int workers = 1)
    {
        seconds = Math.Clamp(seconds, 1, 120);
        workers = Math.Clamp(workers, 1, Environment.ProcessorCount * 2);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
        var tasks = Enumerable.Range(0, workers).Select(_ => Task.Run(() =>
        {
            var x = 0.0001;
            while (!cts.IsCancellationRequested)
            {
                x = Math.Sqrt(x + 1.0001) * 1.000001;
            }
        }, cts.Token)).ToArray();

        try { await Task.WhenAll(tasks); } catch { /* iptal normal */ }

        TempData["msg"] = $"CPU stres tamamlandı: {workers} worker × {seconds} sn.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// 'megabytes' kadar bellek ayırır ve elde tutar.
    /// </summary>
    [HttpPost]
    public IActionResult Memory(int megabytes = 100)
    {
        megabytes = Math.Clamp(megabytes, 1, 4096);
        var total = _ballast.Allocate(megabytes);
        TempData["msg"] = $"{megabytes} MB ayrıldı. Toplam ballast: {total / 1024 / 1024} MB.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult ReleaseMemory()
    {
        _ballast.Release();
        TempData["msg"] = "Bellek serbest bırakıldı.";
        return RedirectToAction(nameof(Index));
    }
}
