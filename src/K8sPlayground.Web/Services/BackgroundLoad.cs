namespace K8sPlayground.Web.Services;

/// <summary>
/// Arka planda sürekli CPU yükü üreten singleton servis.
///
/// Kullanım amacı: HPA (Horizontal Pod Autoscaler) demosu için kullanıcının
/// tek tıkla "kapatana kadar" yük başlatmasına izin verir. Tek seferlik
/// /Stress/Cpu endpoint'i yalnızca 1-120 sn yük üretebilir; HPA'nın 60-180
/// saniyelik stabilization window'una çoğu zaman yetmez.
///
/// İç tasarım:
///   - Her worker ayrı bir <see cref="Thread"/> üzerinde çalışır
///     (ThreadPool'a yük bindirmeyiz, web request'leri akmaya devam eder).
///   - Worker thread'leri <see cref="CancellationTokenSource"/> ile durdurulur.
///   - <see cref="Start"/> idempotent değildir: önceki yük varsa durdurulup
///     yeni worker sayısı ile yeniden başlatılır.
///   - Uygulama kapanırken (SIGTERM) IDisposable üzerinden worker'lar nazikçe
///     durdurulur — graceful shutdown'ı geciktirmez.
/// </summary>
public sealed class BackgroundLoad : IDisposable
{
    private readonly object _lock = new();
    private CancellationTokenSource? _cts;
    private List<Thread> _workers = new();
    private DateTime? _startedAtUtc;
    private long _totalLoadTicks; // tamamlanan yük sürelerinin toplamı (Stopwatch ticks)
    private long _currentRunStartTicks;

    /// <summary>Aktif worker sayısı. 0 ise yük yok.</summary>
    public int ActiveWorkers
    {
        get { lock (_lock) { return _cts is null || _cts.IsCancellationRequested ? 0 : _workers.Count; } }
    }

    /// <summary>Yük başlatıldıysa UTC zamanı, aksi hâlde null.</summary>
    public DateTime? StartedAtUtc
    {
        get { lock (_lock) { return _startedAtUtc; } }
    }

    /// <summary>Şimdiye kadar üretilmiş toplam CPU yükü süresi (saniye).</summary>
    public double TotalLoadSeconds
    {
        get
        {
            lock (_lock)
            {
                var ticks = _totalLoadTicks;
                if (_cts is not null && !_cts.IsCancellationRequested)
                {
                    ticks += System.Diagnostics.Stopwatch.GetTimestamp() - _currentRunStartTicks;
                }
                return TimeSpan.FromSeconds(
                    ticks / (double)System.Diagnostics.Stopwatch.Frequency).TotalSeconds;
            }
        }
    }

    /// <summary>
    /// Yükü başlatır. Zaten çalışıyorsa önce durdurur, sonra yeni worker
    /// sayısı ile yeniden başlatır.
    /// </summary>
    /// <param name="workers">Eşzamanlı CPU yakacak thread sayısı.</param>
    /// <returns>Gerçekten başlatılan worker sayısı.</returns>
    public int Start(int workers)
    {
        // Üst sınır: container CPU limit'ini host CPU sayısından TAM olarak
        // bilemeyiz (cgroup okumak ek bir kütüphane gerektirir). Pratik bir
        // tavan koyuyoruz: host CPU × 2. Worker thread'leri spinwait yaptığı
        // için container limit'ini aşan worker'lar CFS throttling ile zaten
        // sınırlanır; HPA scale-up için bu fazlasıyla yeterli.
        workers = Math.Clamp(workers, 1, Math.Max(1, Environment.ProcessorCount * 2));

        lock (_lock)
        {
            StopUnsafe(); // varsa eskiyi durdur

            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _workers = new List<Thread>(workers);
            _startedAtUtc = DateTime.UtcNow;
            _currentRunStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();

            for (var i = 0; i < workers; i++)
            {
                var thread = new Thread(() => Burn(token))
                {
                    IsBackground = true,
                    Name = $"k8sp-cpu-burner-{i}",
                    Priority = ThreadPriority.BelowNormal,
                };
                thread.Start();
                _workers.Add(thread);
            }
            return _workers.Count;
        }
    }

    /// <summary>Yükü durdurur. Yük yoksa no-op.</summary>
    public void Stop()
    {
        lock (_lock)
        {
            StopUnsafe();
        }
    }

    private void StopUnsafe()
    {
        if (_cts is null) return;

        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Dispose edilmişse cancel zaten gereksiz
        }

        if (_startedAtUtc is not null)
        {
            _totalLoadTicks += System.Diagnostics.Stopwatch.GetTimestamp() - _currentRunStartTicks;
        }

        // Worker'ların temiz çıkışını beklemiyoruz (her biri en geç ~1 ms
        // içinde cancellation token'ı görüp döner). Test ortamında tam
        // join istersek burada thread.Join çağrılabilir; eğitim demosunda
        // beklemeden dönmek isabetli.
        _cts.Dispose();
        _cts = null;
        _workers = new List<Thread>();
        _startedAtUtc = null;
    }

    /// <summary>
    /// Tek bir worker'ın yaptığı iş: cancellation token iptal edilene kadar
    /// FPU üzerinden boş yere hesap yapmak. <c>Math.Sqrt</c> + birikim
    /// optimizasyona dirençlidir; JIT işi atlayamaz.
    /// </summary>
    private static void Burn(CancellationToken token)
    {
        var x = 1.0001;
        // Her ~10k iterasyonda bir token'ı kontrol ediyoruz: Volatile read
        // sıcak döngüye az yük getirir ama cancellation'a duyarlıdır.
        while (!token.IsCancellationRequested)
        {
            for (var i = 0; i < 10_000 && !token.IsCancellationRequested; i++)
            {
                x = Math.Sqrt(x + 1.0001) * 1.000001;
            }
            // GC'nin nefes alması için minik bir nokta — Thread.Yield()
            // çoğu OS'ta 0 ms ama scheduler'a 'ben hazırım' der.
            Thread.Yield();
        }
        // x'i kullanmak zorundayız ki JIT döngüyü elimine etmesin:
        GC.KeepAlive(x);
    }

    public void Dispose()
    {
        Stop();
    }
}
