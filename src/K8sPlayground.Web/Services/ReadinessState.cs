namespace K8sPlayground.Web.Services;

/// <summary>
/// Readiness probe durumunu tutan singleton.
/// Kursiyerler bu bayrağı kapatarak Pod'un Service endpoint listesinden
/// çıkarıldığını canlı olarak gözlemleyebilir.
/// </summary>
public class ReadinessState
{
    private int _ready = 1; // 1 = ready, 0 = not ready

    public bool IsReady => Volatile.Read(ref _ready) == 1;

    public void SetReady(bool value)
    {
        Volatile.Write(ref _ready, value ? 1 : 0);
    }

    public bool Toggle()
    {
        var current = IsReady;
        SetReady(!current);
        return !current;
    }
}

/// <summary>
/// Memory stress testi için kontrollü bellek tutucu.
/// </summary>
public class MemoryBallast
{
    private readonly List<byte[]> _chunks = new();
    private readonly object _lock = new();
    public long AllocatedBytes { get; private set; }

    public long Allocate(int megabytes)
    {
        lock (_lock)
        {
            for (var i = 0; i < megabytes; i++)
            {
                var chunk = new byte[1024 * 1024];
                // Belleğin gerçekten ayrılmasını zorlamak için doldur
                Array.Fill(chunk, (byte)1);
                _chunks.Add(chunk);
                AllocatedBytes += chunk.Length;
            }
            return AllocatedBytes;
        }
    }

    public void Release()
    {
        lock (_lock)
        {
            _chunks.Clear();
            AllocatedBytes = 0;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}
