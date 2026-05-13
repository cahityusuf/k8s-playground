using Microsoft.AspNetCore.Mvc;

namespace K8sPlayground.Web.Controllers;

/// <summary>
/// Environment değişkenlerini görüntüler.
/// ConfigMap, Secret ve Downward API ile enjekte edilen değerleri
/// bu sayfada doğrulayabilirsiniz.
/// </summary>
public class EnvController : Controller
{
    // Gizli görünüm için maskelenecek anahtar kalıpları
    private static readonly string[] SensitivePatterns =
        { "PASSWORD", "SECRET", "TOKEN", "KEY", "PWD" };

    public IActionResult Index(string? filter = null, bool showSecrets = false)
    {
        var all = Environment.GetEnvironmentVariables();
        var items = new List<(string Key, string Value, bool Sensitive)>();

        foreach (System.Collections.DictionaryEntry e in all)
        {
            var key = e.Key?.ToString() ?? "";
            var val = e.Value?.ToString() ?? "";

            if (!string.IsNullOrEmpty(filter) &&
                !key.Contains(filter, StringComparison.OrdinalIgnoreCase))
                continue;

            var sensitive = SensitivePatterns.Any(p =>
                key.Contains(p, StringComparison.OrdinalIgnoreCase));

            if (sensitive && !showSecrets)
                val = new string('*', Math.Min(val.Length, 12));

            items.Add((key, val, sensitive));
        }

        ViewBag.Filter = filter;
        ViewBag.ShowSecrets = showSecrets;
        return View(items.OrderBy(i => i.Key).ToList());
    }

    [HttpGet]
    public IActionResult Single(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return Json(new { name, value, found = value != null });
    }
}
