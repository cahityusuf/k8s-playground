using K8sPlayground.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();

// Singleton durumlar: readiness toggle, memory ballast ve arka plan CPU yükü.
// BackgroundLoad uygulama kapanırken Dispose ile worker thread'lerini bırakır.
builder.Services.AddSingleton<ReadinessState>();
builder.Services.AddSingleton<MemoryBallast>();
builder.Services.AddSingleton<BackgroundLoad>();

// Kubernetes ortam değişkenleri (downward API ile gelir)
builder.Services.Configure<HostOptions>(o =>
{
    // SIGTERM geldiğinde graceful shutdown için süre tanı
    o.ShutdownTimeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();

// -----------------------------------------------------------------------
// Probe endpoint'leri (Kubernetes liveness/readiness/startup için)
// -----------------------------------------------------------------------
app.MapGet("/healthz/live", () =>
    Results.Ok(new { status = "live", time = DateTime.UtcNow }));

app.MapGet("/healthz/ready", (ReadinessState state) =>
    state.IsReady
        ? Results.Ok(new { status = "ready", time = DateTime.UtcNow })
        : Results.Json(new { status = "not-ready" }, statusCode: 503));

app.MapGet("/healthz/startup", () =>
    Results.Ok(new { status = "started" }));

// Prometheus benzeri basit metrik endpoint'i (eğitim amaçlı)
app.MapGet("/metrics", (ReadinessState state, MemoryBallast ballast, BackgroundLoad load) =>
{
    var proc = System.Diagnostics.Process.GetCurrentProcess();
    var lines = new[]
    {
        "# HELP app_ready 1 if readiness probe is OK",
        "# TYPE app_ready gauge",
        $"app_ready {(state.IsReady ? 1 : 0)}",
        "# HELP app_memory_bytes Working set bytes",
        "# TYPE app_memory_bytes gauge",
        $"app_memory_bytes {proc.WorkingSet64}",
        "# HELP app_ballast_bytes Ballast bytes intentionally allocated",
        "# TYPE app_ballast_bytes gauge",
        $"app_ballast_bytes {ballast.AllocatedBytes}",
        "# HELP app_load_workers Currently running background CPU burner threads",
        "# TYPE app_load_workers gauge",
        $"app_load_workers {load.ActiveWorkers}",
        "# HELP app_load_seconds_total Cumulative seconds of background CPU load produced since startup",
        "# TYPE app_load_seconds_total counter",
        $"app_load_seconds_total {load.TotalLoadSeconds:F3}",
    };
    return Results.Text(string.Join("\n", lines) + "\n", "text/plain");
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Graceful shutdown davranışı: log'a basıyoruz, eğitmen kubectl logs ile görebilir
app.Lifetime.ApplicationStopping.Register(() =>
{
    Console.WriteLine("[SHUTDOWN] SIGTERM alındı. Graceful shutdown başlıyor...");
});
app.Lifetime.ApplicationStopped.Register(() =>
{
    Console.WriteLine("[SHUTDOWN] Uygulama kapandı.");
});

app.Run();
