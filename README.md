# Kubernetes Playground

.NET 8 ASP.NET Core MVC ile yazılmış, Kubernetes'in **temel ve ileri kavramlarını canlı olarak gözlemlemek için** kullanılan eğitim uygulamasıdır. Her sayfa bir K8s kavramına karşılık gelir; arka taraftaki `kubectl` çıktısı ile birlikte takip ederek konuları somut deneyimle pekiştirebilirsiniz.

> ⚠️ Bu uygulama **eğitim amaçlıdır**. Üretim ortamında kullanmayın — kasıtlı olarak crash, OOMKilled, hang vb. işlemleri tetikler.

---

## İçindekiler
- [Mimari ve Sayfalar](#mimari-ve-sayfalar)
- [Hızlı Başlangıç](#hızlı-başlangıç)
- [Kubernetes'e Dağıtım](#kubernetese-dağıtım)
- [Eğitim Senaryoları](#eğitim-senaryoları)
- [Proje Yapısı](#proje-yapısı)

---

## Mimari ve Sayfalar

| URL | Konu | Demo edilen K8s özellikleri |
|---|---|---|
| `/` | Karşılama | Genel bakış |
| `/Info` | Pod / Node bilgileri | Downward API, init container, ServiceAccount token |
| `/Env` | Environment değişkenleri | ConfigMap & Secret `envFrom`, env önceliği |
| `/Config` | ConfigMap / Secret dosyaları | Volume mount, kubelet refresh (~60 sn) |
| `/Health` | Liveness / Readiness | Probe toggle → Service endpoint listesinden düşme |
| `/Stress` | CPU / Memory yük | HPA, `resources.limits`, OOMKilled |
| `/Storage` | PVC | Metin yazma + dosya **upload** (`/data/uploads`), `emptyDir` vs PVC farkı |
| `/Network` | DNS / S2S | Cluster DNS, ClusterIP, NetworkPolicy, Ingress header'ları |
| `/Logs` | Log üretici | `kubectl logs`, stdout/stderr, log seviyeleri |
| `/Chaos` | Hata enjeksiyonu | CrashLoopBackOff, SIGTERM, `terminationGracePeriodSeconds` |
| `/healthz/live` | Liveness probe | Her zaman 200 |
| `/healthz/ready` | Readiness probe | Toggle'a göre 200/503 |
| `/healthz/startup` | Startup probe | Başlangıçta 200 |
| `/metrics` | Prometheus benzeri | `app_ready`, `app_memory_bytes`, `app_ballast_bytes` |

---

## Hızlı Başlangıç

### Yerelde (kubernetes olmadan)
```bash
cd src/K8sPlayground.Web
ASPNETCORE_ENVIRONMENT=Development dotnet run
# Tarayıcı: http://localhost:5000
```

### Docker imajını derle
```bash
docker build -t k8s-playground:1.0.0 .
docker run --rm -p 8080:8080 \
  -e APP_VERSION=1.0.0 \
  -e POD_NAME=local-test \
  -e POD_NAMESPACE=demo \
  k8s-playground:1.0.0
# Tarayıcı: http://localhost:8080
```

---

## Kubernetes'e Dağıtım

`k8s/` klasöründeki manifestler **kustomize** ile sıralı olarak uygulanır.

### 1. Hazır multi-platform imaj
Manifest, Docker Hub'daki hazır imaja işaret eder:
```
docker.io/cahityusuf/k8s-playground:v1.0.0   # linux/amd64 + linux/arm64
```

Kendi registry'nize push etmek isterseniz:
```bash
docker buildx build \
  --platform linux/amd64,linux/arm64 \
  -t <REGISTRY>/k8s-playground:v1.0.0 \
  --push .
# Ardından k8s/30-deployment.yaml içindeki 'image:' alanını güncelleyin.
```

Yerel `kind`/`minikube`:
```bash
docker build -t k8s-playground:v1.0.0 .
kind load docker-image k8s-playground:v1.0.0       # kind
minikube image load k8s-playground:v1.0.0          # minikube
# 30-deployment.yaml içindeki image alanını k8s-playground:v1.0.0 yapın.
```

### 2. Tüm kaynakları uygula
```bash
kubectl apply -k k8s/
kubectl get pods,svc,ingress,hpa,pvc -n k8s-playground -w
```

### 3. Erişim
- **Port-forward** ile hızlı erişim:
  ```bash
  kubectl -n k8s-playground port-forward svc/k8sp 8080:80
  # http://localhost:8080
  ```
- **Ingress** ile: `/etc/hosts`'a Ingress IP'sini `k8sp.local` olarak ekleyin, sonra `http://k8sp.local`.

### 4. Temizlik
```bash
kubectl delete -k k8s/
```

---

## Eğitim Senaryoları

Aşağıdaki sıra ile takip etmenizi öneririz.

### Senaryo 1 — Pod / Service / Endpoints
1. `/Info` sayfasını birkaç kez yenileyin; replika sayısı 2 olduğundan **POD_NAME değişerek dönecektir** (Service load balancing).
2. Bir terminalde izleyin: `kubectl get endpoints k8sp -n k8s-playground -o wide`

### Senaryo 2 — ConfigMap & Secret tazeleme
1. `/Config` sayfasını açın, mount edilmiş dosyaları görün.
2. `kubectl edit configmap k8sp-config -n k8s-playground` ile `greeting.txt` içeriğini değiştirin.
3. ~30–60 sn sonra `/Config` sayfasını yenileyin: içeriğin **Pod yeniden başlatılmadan** güncellendiğini göreceksiniz (sadece volume mount için; `envFrom` Pod restart ister).

### Senaryo 3 — Readiness toggle
1. `/Health` → "Set NOT READY".
2. Terminalden: `kubectl get endpoints k8sp -n k8s-playground` → Pod IP'lerinden biri listeden düşer.
3. Bu sırada Service üzerinden gelen istekler diğer hazır Pod'lara yönlendirilir.

### Senaryo 4 — HPA + CPU yükü
1. `kubectl top pods -n k8s-playground -w` (Metrics Server şart).
2. `/Stress` → 60 sn × 2 worker CPU yakın.
3. `kubectl get hpa,pods -n k8s-playground -w` → replika 2'den yukarı çıkar.

### Senaryo 5 — OOMKilled
1. `/Stress` → "Bellek ayır" → 300 MB (limit 256 Mi).
2. `kubectl get pod -n k8s-playground` → Pod `OOMKilled` ile yeniden başlar.
3. `kubectl describe pod <pod>` son satırlarında nedeni görürsünüz.

### Senaryo 6 — PVC kalıcılığı (dosya upload ile)

> Ana Deployment `replicas: 1` + `strategy: Recreate` ile çalışır ve `/data` dizinini bir PVC'den mount eder. Pod silinse de PVC node'da kalır, yeniden gelen Pod aynı diski tekrar bağlar.

#### Hazırlık
```bash
kubectl apply -k k8s/
kubectl get pvc,pod -n k8s-playground
```

Şunu görmelisiniz:

```
NAME                              STATUS   VOLUME    CAPACITY   ACCESS MODES
persistentvolumeclaim/k8sp-data   Bound    pvc-...   1Gi        RWO

NAME                    READY   STATUS    RESTARTS   AGE
pod/k8sp-xxxxxxxx-yyyy  1/1     Running   0          1m
```

#### Adım 1 — Bir dosya yükle
```bash
kubectl -n k8s-playground port-forward deploy/k8sp 8080:8080
# Tarayıcı: http://localhost:8080/Storage
# "Dosya yükle" formundan bir dosya seçip "Yükle" deyin.
```

Sayfanın altındaki **"Yüklenen dosyalar — /data/uploads"** listesinde dosyanız görünür. Container içinde doğrudan doğrulamak için:

```bash
kubectl -n k8s-playground exec deploy/k8sp -- ls -la /data/uploads
```

#### Adım 2 — Pod'u sil, dosyanın hayatta kaldığını kanıtla
```bash
kubectl -n k8s-playground delete pod -l app.kubernetes.io/name=k8sp
kubectl -n k8s-playground get pod -w     # yeni Pod ayağa kalkar
```

Yeni Pod `Running` olduktan sonra port-forward'u tekrar açıp `/Storage` sayfasını yenileyin → **dosyanız hâlâ uploads listesinde**.

Alternatif (UI'a girmeden):
```bash
kubectl -n k8s-playground exec deploy/k8sp -- ls -la /data/uploads
```

#### Adım 3 — PVC vs emptyDir farkı
`/tmp` dizini Deployment'ta `emptyDir` ile mount edildi. Aynı testi orada yapın:

```bash
kubectl -n k8s-playground exec deploy/k8sp -- sh -c 'echo gecici > /tmp/gecici.txt && ls /tmp'
kubectl -n k8s-playground delete pod -l app.kubernetes.io/name=k8sp
# yeni Pod hazır olduğunda:
kubectl -n k8s-playground exec deploy/k8sp -- ls /tmp
```

`gecici.txt` **kaybolur** — emptyDir Pod yaşam döngüsüne bağlıdır. PVC ise değildir.

#### Adım 4 — Temizlik
```bash
kubectl delete -k k8s/
# PVC retain politikasına göre disk hâlâ duruyor olabilir:
kubectl get pv | grep k8sp-data
```

### Senaryo 7 — Graceful shutdown
1. `/Chaos` → "Graceful Exit".
2. `kubectl logs -f <pod>` çıktısında `[SHUTDOWN] SIGTERM alındı...` mesajı görünür.
3. `terminationGracePeriodSeconds: 30` ve `preStop sleep 5` sayesinde Service trafiği önce düşer, sonra Pod kapanır.

### Senaryo 8 — CrashLoopBackOff
1. `/Chaos` → "Crash" → birkaç kez tekrar.
2. `kubectl get pod -n k8s-playground` → Pod `CrashLoopBackOff` durumuna geçer.
3. `kubectl describe pod <pod>` → restart sayısı ve `BackOff` event'i.

### Senaryo 9 — Liveness probe failure
1. `/Chaos` → "Hang" → 30 saniye.
2. `livenessProbe.timeoutSeconds=2` + `failureThreshold=3` × `periodSeconds=10` = ~30 sn'de Pod liveness'ı yitirir ve `kubectl describe pod` `Liveness probe failed` event'i yazar; Pod yeniden başlatılır.

### Senaryo 10 — NetworkPolicy
1. `/Network` → başka bir namespace'te ayağa kaldırdığınız bir servisi çağırın → **çalışır**.
2. `k8s/kustomization.yaml` içinde `70-networkpolicy.yaml` satırını açıp tekrar uygulayın.
3. Aynı çağrıyı tekrarlayın → **time out** olur (CNI NetworkPolicy desteklemiyorsa policy etkisiz kalır; Calico/Cilium gibi bir CNI gerekir).

### Senaryo 11 — DNS keşfi
1. `/Network` → "DNS Çözümle" → `kubernetes.default.svc.cluster.local`
2. Cluster içi DNS'in (CoreDNS) çözümleme yaptığını görürsünüz.

### Senaryo 12 — Log seviyeleri
1. `/Logs` → "Trace" seçin → uyarı: *"Trace seviyesi mevcut log filtresinde KAPALI"*.
2. `kubectl set env deployment/k8sp -n k8s-playground ASPNETCORE_ENVIRONMENT=Development` → Pod yenilenir, Trace açılır.
3. `/Logs` → "Trace" → şimdi log üretilir; `kubectl logs -f <pod>` ile görüntüleyin.

---

## Proje Yapısı

```
k8s-playground/
├── Dockerfile                  # Multi-stage build, non-root user, port 8080
├── README.md
├── k8s/
│   ├── 00-namespace.yaml
│   ├── 10-configmap.yaml
│   ├── 11-secret.yaml
│   ├── 20-pvc.yaml                 # /data için 1Gi RWO PVC
│   ├── 25-serviceaccount.yaml
│   ├── 30-deployment.yaml          # replicas:1, Recreate, PVC mount /data, upload aktif
│   ├── 40-service.yaml             # ClusterIP
│   ├── 50-ingress.yaml             # NGINX ingress
│   ├── 60-hpa.yaml                 # (devre dışı — PVC RWO çoklu replikayı engeller)
│   ├── 61-pdb.yaml                 # (devre dışı — tek replika için anlamsız)
│   ├── 70-networkpolicy.yaml       # (opsiyonel) deny-all + ingress
│   ├── 80-servicemonitor.yaml      # (opsiyonel) Prometheus Operator
│   ├── 90-job-cronjob.yaml         # (opsiyonel) Job + CronJob örnekleri
│   └── kustomization.yaml
└── src/K8sPlayground.Web/
    ├── Controllers/            # Her tab için bir controller
    ├── Services/               # ReadinessState + MemoryBallast singleton'ları
    ├── Views/
    ├── wwwroot/
    ├── appsettings.json        # Logging + App ayarları
    ├── appsettings.Development.json # Trace/Debug seviyeleri açık
    └── Program.cs              # /healthz/{live,ready,startup} ve /metrics
```

---

## Sıkça Sorulanlar

**S: `dotnet run` başlatınca `Failed to load configuration from file 'appsettings.json'` hatası alıyorum.**
C: `appsettings.json` geçersiz olmalı. Repo'daki sürüm geçerlidir; siz değiştirdiyseniz JSON doğrulamasından geçirin. Bkz. [#JSON validator](https://jsonlint.com/).

**S: `/Logs` Trace/Debug butonuna basıyorum ama hiçbir şey üretilmiyor.**
C: Default ortamda (Production) bu seviyeler **filtrelenir**. UI artık bunu açıkça uyarıyor. `ASPNETCORE_ENVIRONMENT=Development` ile çalıştırın veya `appsettings.json` içinde ilgili namespace için `LogLevel: "Trace"` ayarlayın.

**S: HPA replika sayısını artırmıyor.**
C: Metrics Server kurulu mu? `kubectl top pod -n k8s-playground` çalışıyor mu? Çalışmıyorsa: `kubectl apply -f https://github.com/kubernetes-sigs/metrics-server/releases/latest/download/components.yaml`

**S: Ingress 404 dönüyor.**
C: `ingressClassName: nginx` kümenizdekiyle aynı mı? `kubectl get ingressclass` ile kontrol edin ve gerekirse `k8s/50-ingress.yaml`'i güncelleyin. DNS yerine `/etc/hosts` kaydı eklediğinizden emin olun.
