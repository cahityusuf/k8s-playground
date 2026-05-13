# Kubernetes Playground (ASP.NET Core 8 MVC)

Kursta Kubernetes'in temel ve ileri özelliklerini **canlı olarak deneyimletmek**
için tasarlanmış bir test uygulaması. Her sayfa bir K8s konusunu hedefler.

## İçindekiler

- [Özellikler](#özellikler)
- [Hızlı başlangıç (Minikube)](#hızlı-başlangıç-minikube)
- [Yerel geliştirme (cluster'sız)](#yerel-geliştirme-clustersız)
- [Lab senaryoları (eğitmen rehberi)](#lab-senaryoları-eğitmen-rehberi)
- [Proje yapısı](#proje-yapısı)
- [Güvenlik notu](#güvenlik-notu)

## Özellikler

| Sayfa | URL | K8s konusu |
|---|---|---|
| Pod Info | `/Info` | Downward API, Pod / Node metadata, init container çıktısı, ServiceAccount token mount |
| Env | `/Env` | ConfigMap / Secret env enjeksiyonu, env ezme önceliği |
| Config / Secret | `/Config` | Volume olarak mount edilen ConfigMap ve Secret dosyaları, anlık güncelleme |
| Health | `/Health` | Readiness toggle → Service endpoint listesinden düşmeyi gözlemleme |
| Stress | `/Stress` | CPU yakma (HPA tetikleme), bellek ayırma (OOMKilled) |
| Storage | `/Storage` | PVC ile kalıcı depolama; Pod yeniden oluşturulduğunda kalıcılık testi |
| Network | `/Network` | DNS sorgusu, service-to-service çağrı, Ingress başlıkları, NetworkPolicy demosu |
| Logs | `/Logs` | Farklı seviyelerde log üretimi, `kubectl logs` ve stderr ayrımı |
| Chaos | `/Chaos` | Crash (CrashLoopBackOff), unhandled exception, hang (liveness fail), graceful exit |

### Probe / Metrics endpoint'leri

| Endpoint | Amaç |
|---|---|
| `GET /healthz/live` | Liveness probe |
| `GET /healthz/ready` | Readiness probe (toggle Health sayfasından) |
| `GET /healthz/startup` | Startup probe |
| `GET /metrics` | Basit Prometheus formatı metrikler |

## Hızlı başlangıç (Minikube)

```bash
# 1) Cluster
minikube start --driver=docker --cpus=2 --memory=4g
minikube addons enable metrics-server
minikube addons enable ingress

# 2) Image'ı build edip Minikube'e yükle
docker build -t k8s-playground:1.0.0 .
minikube image load k8s-playground:1.0.0

# 3) Dağıt
kubectl apply -f k8s/

# 4) Eriş
kubectl -n playground port-forward svc/playground 8080:80
# Tarayıcı: http://localhost:8080
```

## Yerel geliştirme (cluster'sız)

```bash
cd src/K8sPlayground.Web
dotnet run
# http://localhost:5000
```

Yerel modda env değişkenleri olmadığı için Pod Info sayfasında değerler boş
veya `(local)` görünür — bu beklenen davranıştır.

## Lab senaryoları (eğitmen rehberi)

### 1) Env ezme önceliği
1. Uygulamayı dağıt, `/Env` sayfasında `GREETING` değerini gözlemle.
2. `kubectl -n playground edit configmap playground-config` ile değiştir.
3. `kubectl -n playground rollout restart deployment/playground` çalıştır.
4. Sayfa yenilenince yeni değer görünür.
5. **İleri seviye:** Deployment manifest'ine aynı isimli env elle ekle ve
   ConfigMap'i ezdiğini göster.

### 2) Readiness vs Liveness
1. `/Health` sayfasından "Set NOT READY" yap.
2. Başka terminalde:
   ```bash
   kubectl -n playground get endpoints playground -w
   ```
3. Pod'un endpoint listesinden düştüğünü gözlemle (Pod hâlâ çalışıyor ama
   trafik almıyor). Geri açınca yeniden eklenir.

### 3) ConfigMap canlı güncelleme
1. `/Config` sayfasını aç, `banner.txt`'in içeriğini gör.
2. `kubectl -n playground edit configmap playground-config` ile düzenle.
3. 30–60 sn içinde sayfayı yenile — yeni içerik dosyada görünür (env'ler
   tazelemez, ancak dosya mount edilen ConfigMap volume'leri tazelenir).

### 4) PVC kalıcılığı
1. `/Storage` sayfasından bir-iki dosya yaz.
2. `kubectl -n playground delete pod -l app=playground` ile Pod'ları sil.
3. Yeni Pod ayağa kalktıktan sonra sayfayı yenile: dosyalar duruyor.
4. **Karşılaştırma:** Deployment'ta `volumes.data` bölümünü `emptyDir: {}`
   ile değiştir, aynı testi tekrarla — dosyalar kaybolur.

### 5) CrashLoopBackOff
1. `/Chaos` → **Crash** butonuna 4–5 kez bas.
2. `kubectl -n playground get pods` — Pod'un `CrashLoopBackOff` durumuna
   düştüğünü göster. `kubectl describe pod` ile `Last State: Terminated`
   ve `Exit Code` görülebilir.

### 6) Liveness probe failure
1. `/Chaos` → **Hang** (40 sn).
2. Probe 3 defa fail olduktan sonra K8s konteyneri kill eder ve yeniden
   başlatır. `kubectl describe pod` çıktısında `Liveness probe failed`
   görülebilir.

### 7) HPA otomatik ölçekleme
1. Önkoşul: `minikube addons enable metrics-server`.
2. `kubectl apply -f k8s/07-hpa.yaml`
3. Başka terminal: `kubectl -n playground get hpa playground -w`
4. `/Stress` sayfasından CPU yak (60 sn, 4 worker).
5. Replika sayısının 2'den 4–6'ya çıktığını gözlemle.

### 8) OOMKilled
1. `resources.limits.memory: 256Mi` olarak yapılandırıldı.
2. `/Stress` sayfasından **300 MB** bellek ayır.
3. Pod `OOMKilled` ile sonlanır, yeniden başlatılır.
4. `kubectl describe pod` çıktısında `Last State: Terminated, Reason:
   OOMKilled` satırını göster.

### 9) Graceful shutdown
1. `kubectl logs -f -l app=playground` aç.
2. `/Chaos` → **Graceful Exit**.
3. Log'da `[preStop] 5 sn bekleniyor...` ve sonrasında `[SHUTDOWN] SIGTERM
   alındı` satırlarını göster. `terminationGracePeriodSeconds`'in nasıl
   işlediğini anlat.

### 10) Service load balancing
1. Replikaları 3'e çıkar: `kubectl -n playground scale deploy playground --replicas=3`
2. `/Info` sayfasını birkaç kez yenile — Pod adı her seferinde
   değişiyor olmalı (Service round-robin dağıtımı).

### 11) Service-to-Service ve NetworkPolicy (ileri)
1. Aynı namespace'e basit bir Pod aç: `kubectl -n playground run probe --image=busybox --restart=Never -- sleep 3600`
2. `/Network` sayfasından <code>http://playground.playground.svc.cluster.local</code> adresine GET at; çalışmalı.
3. `kubectl apply -f k8s/08-networkpolicy.yaml`
4. Aynı çağrıyı tekrar dene; NetworkPolicy podSelector'ı yalnızca `app=playground` etiketli Pod'lardan gelen trafiğe izin verdiği için reddedilir.

## Proje yapısı

```
k8s-playground-mvc/
├── Dockerfile
├── .dockerignore
├── README.md
├── k8s/
│   ├── 00-namespace.yaml
│   ├── 01-configmap.yaml
│   ├── 02-secret.yaml
│   ├── 03-pvc.yaml
│   ├── 04-deployment.yaml      ← initContainer, probes, downward API, lifecycle
│   ├── 05-service.yaml         ← ClusterIP + NodePort
│   ├── 06-ingress.yaml
│   ├── 07-hpa.yaml
│   ├── 08-networkpolicy.yaml
│   └── README.md
└── src/K8sPlayground.Web/
    ├── K8sPlayground.Web.csproj
    ├── Program.cs              ← probe ve metrics endpoint'leri burada
    ├── appsettings.json
    ├── Services/
    │   └── ReadinessState.cs   ← readiness toggle + memory ballast
    ├── Controllers/
    │   ├── HomeController.cs
    │   ├── InfoController.cs
    │   ├── EnvController.cs
    │   ├── ConfigController.cs
    │   ├── HealthController.cs
    │   ├── StressController.cs
    │   ├── StorageController.cs
    │   ├── NetworkController.cs
    │   ├── LogsController.cs
    │   └── ChaosController.cs
    └── Views/
        ├── _ViewImports.cshtml
        ├── _ViewStart.cshtml
        ├── Shared/_Layout.cshtml
        ├── Home/Index.cshtml
        └── (her controller için ayrı Index.cshtml)
```

## Güvenlik notu

Bu uygulama, **eğitim ortamında** çalıştırılmak üzere tasarlanmıştır:
- Crash, hang, exception ve bellek ayırma endpoint'leri authentication
  gerektirmez.
- Secret içerikleri maskeli de olsa görüntülenebilir.
- CPU/memory yakma fonksiyonları kontrolsüz çağrılırsa Pod'u
  sonlandırabilir.

Üretim cluster'ında **kullanmayın**. İstenirse `[Authorize]` attribute'u
veya RBAC ile koruma eklenebilir.
