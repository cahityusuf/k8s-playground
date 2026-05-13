using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Mvc;

namespace K8sPlayground.Web.Controllers;

/// <summary>
/// Network / Service Discovery test sayfası.
/// - Pod IP, host adı, DNS sorguları
/// - Cluster içi diğer servislere HTTP isteği (Service-to-Service)
/// - Gelen isteğin başlıkları (Ingress, X-Forwarded-For demosu)
/// </summary>
public class NetworkController : Controller
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

    public IActionResult Index()
    {
        ViewBag.Host = Environment.MachineName;
        ViewBag.PodIp = Environment.GetEnvironmentVariable("POD_IP");
        ViewBag.NodeName = Environment.GetEnvironmentVariable("NODE_NAME");

        try
        {
            var addresses = Dns.GetHostAddresses(Dns.GetHostName())
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => a.ToString())
                .ToList();
            ViewBag.Addresses = addresses;
        }
        catch (Exception ex)
        {
            ViewBag.Addresses = new List<string> { $"hata: {ex.Message}" };
        }

        var headers = Request.Headers
            .OrderBy(h => h.Key)
            .Select(h => (h.Key, string.Join(", ", h.Value.ToArray()!)))
            .ToList();
        ViewBag.Headers = headers;
        ViewBag.RemoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        return View();
    }

    /// <summary>
    /// Hedef DNS adını çözümler. Örn: kubernetes.default.svc.cluster.local
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Resolve(string host)
    {
        try
        {
            var addr = await Dns.GetHostAddressesAsync(host);
            TempData["msg"] = $"{host} → {string.Join(", ", addr.Select(a => a.ToString()))}";
        }
        catch (Exception ex)
        {
            TempData["msg"] = $"DNS hatası: {ex.Message}";
        }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Hedef URL'ye GET isteği atar. Service-to-Service çağrı demosu için.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Fetch(string url)
    {
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var resp = await Http.GetAsync(url);
            sw.Stop();
            var body = await resp.Content.ReadAsStringAsync();
            if (body.Length > 1024) body = body[..1024] + "...(kesildi)";
            TempData["msg"] = $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase} " +
                              $"({sw.ElapsedMilliseconds} ms) — {body}";
        }
        catch (Exception ex)
        {
            TempData["msg"] = $"Hata: {ex.Message}";
        }
        return RedirectToAction(nameof(Index));
    }
}
