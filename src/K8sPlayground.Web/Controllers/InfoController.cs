using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Mvc;

namespace K8sPlayground.Web.Controllers;

/// <summary>
/// Çalışma ortamı ile ilgili her şey: Pod adı, Namespace, Node, image versiyonu,
/// .NET sürümü, CPU sayısı, init container çıktısı, vs.
/// Service yük dengeleme demosunda Pod ismini görmek için ana sayfa.
/// </summary>
public class InfoController : Controller
{
    private readonly IConfiguration _cfg;
    public InfoController(IConfiguration cfg) => _cfg = cfg;

    public IActionResult Index()
    {
        ViewBag.Pod = new
        {
            Name        = Environment.GetEnvironmentVariable("POD_NAME") ?? Environment.MachineName,
            Namespace   = Environment.GetEnvironmentVariable("POD_NAMESPACE") ?? "(local)",
            Ip          = Environment.GetEnvironmentVariable("POD_IP") ?? "(yok)",
            ServiceAcc  = Environment.GetEnvironmentVariable("POD_SERVICE_ACCOUNT") ?? "(yok)",
            NodeName    = Environment.GetEnvironmentVariable("NODE_NAME") ?? "(yok)",
            NodeIp      = Environment.GetEnvironmentVariable("NODE_IP") ?? "(yok)",
        };
        ViewBag.App = new
        {
            Version  = Environment.GetEnvironmentVariable("APP_VERSION") ?? _cfg["App:Version"],
            Display  = _cfg["App:DisplayName"],
            Started  = System.Diagnostics.Process.GetCurrentProcess().StartTime,
        };
        ViewBag.Runtime = new
        {
            Os         = RuntimeInformation.OSDescription,
            Arch       = RuntimeInformation.OSArchitecture.ToString(),
            Framework  = RuntimeInformation.FrameworkDescription,
            Cpus       = Environment.ProcessorCount,
            User       = Environment.UserName,
            WorkingSet = System.Diagnostics.Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024,
        };

        // Init container'ın yazdığı dosyayı oku (varsa)
        var initMarker = "/data/init-marker.txt";
        ViewBag.InitMarker = System.IO.File.Exists(initMarker)
            ? System.IO.File.ReadAllText(initMarker)
            : null;

        // ServiceAccount token'ı (klasik konum)
        var saTokenPath = "/var/run/secrets/kubernetes.io/serviceaccount/token";
        ViewBag.ServiceAccountTokenMounted = System.IO.File.Exists(saTokenPath);

        return View();
    }
}
