using Microsoft.AspNetCore.Mvc;

namespace K8sPlayground.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        ViewBag.PodName = Environment.GetEnvironmentVariable("POD_NAME") ?? Environment.MachineName;
        ViewBag.Namespace = Environment.GetEnvironmentVariable("POD_NAMESPACE") ?? "(local)";
        ViewBag.Version = Environment.GetEnvironmentVariable("APP_VERSION") ?? "dev";
        return View();
    }
}
