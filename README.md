# Capa Wi-Fi Anchor & Optimizer

Capa Wi-Fi Anchor & Optimizer; aynı SSID altında çok sayıda modem veya Access Point bulunan kampüs, kütüphane, yurt ve kurumsal ağlarda Wi-Fi bağlantısının daha kararlı kalmasına yardımcı olmak için geliştirilmiş bir Windows masaüstü uygulamasıdır.

## Capa ne demek?

“Capa”, Türkçedeki **çapa** kelimesinin Latin harfleriyle yazılmış marka biçimidir. Denizcilikte çapa, teknenin bulunduğu noktada daha kararlı kalmasını sağlar. Uygulamadaki “demir atma” yaklaşımı da aynı metaforu kullanır: amaç, Windows’un aynı SSID altındaki Access Point’ler arasında gereksiz veya agresif geçişler yapmasını azaltarak mevcut bağlantıyı daha istikrarlı tutmaya yardımcı olmaktır.

## Neden geliştirildi?

Bu proje özellikle:

- çoklu modem veya Access Point ile dağıtılan kampüs Wi-Fi ağları,
- üniversite ve kütüphane Wi-Fi ağları,
- KYK yurtları,
- aynı SSID’yi kullanan kurumsal Enterprise ağlar

için geliştirildi. Geliştiricisi olarak ben de öğrenciyim; uygulamayı kendi ihtiyacım doğrultusunda, yapay zekâdan yardım alarak ve Windows ağ davranışlarını inceleyerek kendi çapımda geliştirdim.

Bu araç bir router yönetim paneli değildir. Bağlanılan kurumun ağ altyapısını veya Access Point ayarlarını değiştirmez; yalnızca çalıştığı Windows bilgisayarın Wi-Fi adaptörü ve WLAN AutoConfig davranışı üzerinde işlem yapar.

## Özellikler

- .NET 8 Windows Forms uygulaması
- Yönetici yetkisiyle çalışma
- Aktif arayüz, SSID, BSSID ve sinyal gücünü görüntüleme
- Aynı SSID altındaki yakındaki Wi-Fi ağlarını tarama ve kayıtlı profile bağlanma
- Roaming davranışını azaltmaya yönelik “Optimize & Drop Anchor” akışı
- TCP üzerinden bağlantı testi
- Türkçe / İngilizce arayüz
- Koyu mor, WebView2 tabanlı responsive kullanıcı arayüzü
- Yayın sırasında hassas bilgileri maskeleyen güvenli görünüm
- Komut pencerelerini kullanıcıya göstermeden arka planda çalışma

## Windows üzerinde hangi ayarlarla oynar?

Uygulama yalnızca yerel Wi-Fi adaptörü ve WLAN AutoConfig ile ilgili aşağıdaki işlemleri kullanır. Komutlar uygulama içinde gizli çalıştırılır; açık kaynak kodu inceleyen kişiler ilgili kodu [Network/WifiManager.cs](Network/WifiManager.cs) ve [Core/ProcessRunner.cs](Core/ProcessRunner.cs) dosyalarında görebilir.

### Okunan bilgiler

`netsh wlan show interfaces` ile:

- Wi-Fi arayüz adı
- bağlantı durumu
- SSID
- Access Point BSSID
- sinyal yüzdesi
- Windows’un bildirdiği bağlantı bilgileri

okunur. Bu bilgiler yönlendiriciye gönderilmez.

### Ağ tarama ve profil bağlantısı

Yakındaki ağları listelemek için:

```text
netsh wlan show networks mode=bssid interface="Wi-Fi"
```

kullanılır. Windows tarama için WLAN AutoConfig’i kapalı bırakmışsa uygulama tarama süresince geçici olarak:

```text
netsh wlan set autoconfig enabled=yes interface="Wi-Fi"
```

çalıştırır ve önceki durum kapalıysa tarama sonunda tekrar kapatır.

Kullanıcı bir ağ kartına bastığında uygulama yalnızca Windows’ta kayıtlı bir profil üzerinden bağlantı istemi gönderir:

```text
netsh wlan connect name="SSID" interface="Wi-Fi"
```

Uygulama Wi-Fi parolalarını okumaz veya saklamaz. Kayıtlı olmayan Enterprise ağlarda bağlantı, Windows’un kendi kimlik doğrulama ekranı veya kurum politikası tarafından yönetilir.

### Adaptör gelişmiş ayarları

Optimize işlemi, sürücü destekliyorsa Windows ağ adaptörünün şu gelişmiş özelliklerini değiştirmeyi dener:

- **Transmit Power / Send Power:** 5. en yüksek değer
- **Roaming Aggressiveness / Roaming Aggressiveness:** 1. en düşük değer
- **MIMO Power Save Mode / MIMO Power Save:** Auto SMPS

Her Wi-Fi sürücüsü aynı özellik adlarını veya aynı değer aralığını desteklemez. Desteklenmeyen bir ayar hata olarak gizlenmez; uygulama bunu uyarı olarak ele alır ve diğer işlemleri sürdürebilir.

### Roaming kilidi ve normal moda dönüş

Optimize akışında WLAN AutoConfig, bağlantıyı zorla koparmadan kısa süreli kontrol edilir ve sonrasında roaming’i azaltmak amacıyla kapatılabilir:

```text
netsh wlan set autoconfig enabled=no interface="Wi-Fi"
```

“Demiri Al” işlemi normal davranışı geri getirmek için tekrar şunu çalıştırır:

```text
netsh wlan set autoconfig enabled=yes interface="Wi-Fi"
```

Bu ayarlar Wi-Fi sürücüsünün ve Windows sürümünün davranışına bağlıdır. Bazı sürücülerde roaming kilidi etkisiz kalabilir; bazı ağlarda ise tarama, Enterprise kimlik doğrulaması veya kurum politikaları nedeniyle bağlantı değişebilir.

### TCP bağlantı testi

ICMP/Ping bazı üniversite ve kurumsal ağlarda engellenebildiği için standart `Ping` sınıfı kullanılmaz. Uygulama `TcpClient` ile `8.8.8.8:53` adresine bağlantı kurmayı dener ve süreyi milisaniye olarak ölçer. Bu test başarısızsa Wi-Fi kesinlikle bozuk demek değildir; kurum ağı TCP/53 çıkışını veya Google DNS erişimini engelliyor olabilir.

## Güvenlik ve gizlilik

- Uygulama yönetici yetkisi ister; bu yetki adaptör ayarlarını değiştirmek için gereklidir.
- IP, MAC ve BSSID gibi bilgiler ağ dışına gönderilmez.
- Güvenli görünüm varsayılan olarak açıktır ve hassas tanımlayıcıları ekranda maskeler.
- Ağ parolaları okunmaz, kaydedilmez veya kaynak koda yazılmaz.
- Uygulama router veya Access Point yapılandırmasını değiştirmez.
- Yönetici olarak çalıştırmadan önce açık kaynak kodunu ve yayınlanan EXE dosyasını incelemeniz önerilir.

## Gereksinimler

- Windows 10 veya Windows 11, x64
- Microsoft WebView2 Runtime
- Wi-Fi adaptörü
- Adaptör ayarlarını değiştirecek işlemler için yönetici yetkisi

WebView2 Runtime çoğu güncel Windows 10/11 kurulumunda bulunur. Eksikse Microsoft’un resmi Evergreen Standalone Runtime paketinden kurulmalıdır.

## GitHub Release EXE kurulumu

Release sayfasındaki `CapaOptimizer-Setup.exe` dosyası kullanıcı profilindeki şu klasöre kurulum yapar:

```text
%LOCALAPPDATA%\CapaOptimizer
```

Başlat menüsüne ve masaüstüne kısayol oluşturur. Uygulama ilk açılışta ağ ayarlarını değiştirecek işlemler için UAC yönetici onayı ister.

Kurulum istemeyen kullanıcılar `CapaOptimizer.exe` dosyasını doğrudan çalıştırabilir. GitHub Release için önerilen dosyalar:

- `CapaOptimizer-Setup.exe`: tek dosyalık kurulum paketi
- `CapaOptimizer-win-x64.zip`: kurulum istemeyen taşınabilir yayın paketi
- `CapaOptimizer.exe`: self-contained ana uygulama dosyası

## Kaynaktan derleme

```powershell
dotnet restore
dotnet build
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Geliştirme sırasında tek bir çıktı klasörü kullanmak için `bin` ve `obj` klasörlerini kaynak koda göndermeyin. Release dosyaları `artifacts\release` altında toplanabilir.

## Teknik yapı

- `Core/LanguageManager.cs`: Türkçe/İngilizce metinler
- `Core/ProcessRunner.cs`: gizli cmd/PowerShell süreçleri ve çıktı yönetimi
- `Network/WifiManager.cs`: Wi-Fi okuma, tarama, profil bağlantısı, adaptör ayarları ve TCP testi
- `UI/MainForm.cs`: WebView2 arayüzü ve kullanıcı eylemleri
- `UI/MainForm.Designer.cs`: WinForms/WebView2 kabuğu

## Hata ayıklama

Bir sorun yaşanırsa şu sırayla kontrol edin:

1. Windows Ayarlarındaki Wi-Fi bağlantısı çalışıyor mu?
2. `netsh wlan show interfaces` aktif bir SSID bildiriyor mu?
3. WebView2 Runtime kurulu mu?
4. Uygulama UAC onayıyla yönetici olarak açıldı mı?
5. Wi-Fi sürücüsü gelişmiş ayar adlarını ve değerlerini destekliyor mu?
6. Kurum ağı `8.8.8.8:53` TCP bağlantısını engelliyor mu?

Gerekirse komutları yönetici PowerShell’inde elle çalıştırarak Windows’un verdiği sonucu karşılaştırabilirsiniz. Kaynak kodu kopyalayıp kendi projenizde kullanabilir, değiştirebilir ve geliştirebilirsiniz. Sorularınız, hata bildirimleriniz veya kendi uyarlamanız için bana danışabilirsiniz; “doğrudan kullanabilirsiniz” ifadesi de kaynak kodun lisans koşullarına uyarak kendi projenizde yeniden kullanılabileceği anlamına gelir.

## Sorumluluk bildirimi

Sonuçlar Windows sürümüne, Wi-Fi sürücüsüne, Access Point yapılandırmasına ve kurum ağ politikalarına göre değişebilir. Uygulama bağlantı kararlılığını iyileştirmeyi amaçlar; güvenlik duvarı, kimlik doğrulama, ağ politikası veya kurum altyapısı üzerinde yetkisiz değişiklik yapmaz.

## Yapımcı

**Ömer YİĞİT**

Website: [omeryigit.tr](https://omeryigit.tr)

## Lisans

Bu proje MIT License altında açık kaynak olarak paylaşılmaktadır.

Projeyi kopyalayabilir, kullanabilir, değiştirebilir, birleştirebilir, yayımlayabilir ve dağıtabilirsiniz. Yeniden dağıtım sırasında mevcut telif ve lisans bildirimlerinin korunması gerekir ve bildirilmesi gerekir.

Bu yazılım herhangi bir garanti olmaksızın sunulmaktadır. Kullanım sonucu oluşabilecek bağlantı, sistem veya ağ sorunlarından geliştirici sorumlu tutulamaz.
