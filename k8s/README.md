# Kubernetes Manifest Seti

Bu dizin, K8s Playground uygulamasını Minikube üzerinde çalıştırmak için
gereken tüm Kubernetes nesnelerini içerir. Dosyalar uygulanma sırasına göre
numaralandırılmıştır.

| Dosya | Nesne | Konu |
|---|---|---|
| `00-namespace.yaml` | Namespace | İzolasyon |
| `01-configmap.yaml` | ConfigMap | Yapılandırma ezme demoları |
| `02-secret.yaml` | Secret | Hassas veri |
| `03-pvc.yaml` | PersistentVolumeClaim | Kalıcı depolama |
| `04-deployment.yaml` | Deployment + initContainer | Pod, probe, resources, downward API, lifecycle |
| `05-service.yaml` | Service (ClusterIP + NodePort) | Service tipleri |
| `06-ingress.yaml` | Ingress | Dış erişim |
| `07-hpa.yaml` | HorizontalPodAutoscaler | Otomatik ölçekleme |
| `08-networkpolicy.yaml` | NetworkPolicy | (ileri) Trafik izolasyonu |

## Hızlı kurulum

```bash
# 1) Cluster'ı başlat
minikube start --driver=docker --cpus=2 --memory=4g
minikube addons enable metrics-server
minikube addons enable ingress

# 2) Image'ı build edip Minikube'e yükle
cd ..  # proje köküne
docker build -t docker.io/cahityusuf/k8s-playground:v1.0.1 .
minikube image load docker.io/cahityusuf/k8s-playground:v1.0.1

# 3) Manifest'leri uygula
kubectl apply -f k8s/

# 4) Pod'ların hazır olmasını bekle
kubectl -n playground get pods -w
```

## Erişim seçenekleri

```bash
# Yöntem 1: port-forward (en hızlı)
kubectl -n playground port-forward svc/playground 8080:80
# tarayıcı: http://localhost:8080

# Yöntem 2: NodePort
minikube service -n playground playground-np --url

# Yöntem 3: Ingress
echo "$(minikube ip)  playground.local" | sudo tee -a /etc/hosts
curl http://playground.local/
```

## Hızlı doğrulama

```bash
kubectl -n playground get all
kubectl -n playground describe pod -l app=playground
kubectl -n playground logs -l app=playground --tail=50
```
