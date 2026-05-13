using Microsoft.AspNetCore.Mvc;

namespace K8sPlayground.Web.Controllers;

/// <summary>
/// ConfigMap ve Secret'ların volume olarak Pod'a mount edildiği
/// dizinlerin içeriğini gösterir. ConfigMap güncellendiğinde içeriğin
/// otomatik tazelendiğini canlı olarak gözlemlemek için idealdir.
/// </summary>
public class ConfigController : Controller
{
    private readonly IConfiguration _config;
    public ConfigController(IConfiguration config) => _config = config;

    public IActionResult Index()
    {
        var configPath = _config["App:ConfigPath"] ?? "/etc/k8s-config";
        var secretPath = _config["App:SecretPath"] ?? "/etc/k8s-secret";

        ViewBag.ConfigPath = configPath;
        ViewBag.SecretPath = secretPath;
        ViewBag.ConfigFiles = ListFiles(configPath, mask: false);
        ViewBag.SecretFiles = ListFiles(secretPath, mask: true);
        return View();
    }

    private static List<(string Name, string Content, long Size)> ListFiles(string path, bool mask)
    {
        var result = new List<(string, string, long)>();
        if (!Directory.Exists(path)) return result;

        foreach (var f in Directory.EnumerateFiles(path))
        {
            try
            {
                var info = new FileInfo(f);
                string content;
                if (info.Length > 4096)
                    content = $"(dosya {info.Length} byte, ilk 4 KB gösteriliyor)\n" +
                              System.IO.File.ReadAllText(f)[..4096];
                else
                    content = System.IO.File.ReadAllText(f);

                if (mask)
                    content = string.Join("\n",
                        content.Split('\n').Select(l =>
                            l.Length > 4 ? l[..2] + new string('*', l.Length - 2) : "**"));

                result.Add((info.Name, content, info.Length));
            }
            catch (Exception ex)
            {
                result.Add((Path.GetFileName(f), $"(okunamadı: {ex.Message})", 0));
            }
        }
        return result;
    }
}
