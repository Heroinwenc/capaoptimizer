namespace CapaOptimizer.Core;

public enum AppLanguage
{
    Turkish,
    English
}

public sealed class LanguageManager
{
    private readonly Dictionary<string, string> _turkish = new()
    {
        ["title"] = "⚓ Capa - Wi-Fi Anchor & Optimizer",
        ["language"] = "Dil",
        ["status"] = "Kilit Durumu",
        ["anchored"] = "⚓ DEMİRLENDİ",
        ["drifting"] = "🌊 SERBEST",
        ["optimize"] = "⚓ Optimize Et ve Demir At",
        ["unlock"] = "🌊 Demiri Al (Normal Mod)",
        ["refresh"] = "🔄 Radarı Yenile",
        ["ping"] = "🌐 Bağlantıyı Sına",
        ["network"] = "Ağ Bilgileri",
        ["ssid"] = "SSID:",
        ["bssid"] = "BSSID:",
        ["signal"] = "Sinyal:",
        ["interface"] = "Adaptör:",
        ["pingResult"] = "TCP Ping:",
        ["ready"] = "Hazır",
        ["unknown"] = "Bilinmiyor",
        ["boot"] = "⚓ Capa Engine Booting...",
        ["refreshing"] = "Radar yenileniyor...",
        ["optimizationStarted"] = "Optimizasyon başlatıldı.",
        ["optimizationDone"] = "Bağlantı demirlendi; roaming devre dışı.",
        ["unlocking"] = "Normal Wi-Fi modu geri yükleniyor...",
        ["unlocked"] = "Demir kaldırıldı; roaming etkin.",
        ["pinging"] = "TCP bağlantı testi yapılıyor...",
        ["noConnection"] = "Aktif Wi-Fi bağlantısı bulunamadı.",
        ["success"] = "Başarılı",
        ["error"] = "Hata"
        ,["subtitle"] = "Wi-Fi Anchor & Optimizer  •  Kurumsal roaming kontrolü"
        ,["author"] = "Yapımcı: Ömer YİĞİT"
        ,["website"] = "omeryigit.tr adresini ziyaret et"
        ,["radar"] = "CANLI RADAR"
        ,["scanning"] = "● AĞ TARANIYOR"
        ,["connected"] = "● BAĞLI • RADAR ÇEVRİMİÇİ"
        ,["waiting"] = "● WI-FI BEKLENİYOR"
        ,["secureMode"] = "Yayın güvenliği"
        ,["secureOn"] = "Gizli görünüm açık"
        ,["secureOff"] = "Ayrıntılı görünüm açık"
        ,["masked"] = "Gizli"
        ,["connect"] = "Bağlan"
        ,["theme"] = "Tema"
        ,["violetTheme"] = "Mor gece"
        ,["midnightTheme"] = "Gece laciverti"
        ,["connectedNow"] = "Bağlantı aktif"
    };

    private readonly Dictionary<string, string> _english = new()
    {
        ["title"] = "⚓ Capa - Wi-Fi Anchor & Optimizer",
        ["language"] = "Language",
        ["status"] = "Anchor Status",
        ["anchored"] = "⚓ ANCHORED",
        ["drifting"] = "🌊 DRIFTING",
        ["optimize"] = "⚓ Optimize & Drop Anchor",
        ["unlock"] = "🌊 Weigh Anchor",
        ["refresh"] = "🔄 Refresh Radar",
        ["ping"] = "🌐 Ping Test",
        ["network"] = "Network Information",
        ["ssid"] = "SSID:",
        ["bssid"] = "BSSID:",
        ["signal"] = "Signal:",
        ["interface"] = "Adapter:",
        ["pingResult"] = "TCP Ping:",
        ["ready"] = "Ready",
        ["unknown"] = "Unknown",
        ["boot"] = "⚓ Capa Engine Booting...",
        ["refreshing"] = "Refreshing radar...",
        ["optimizationStarted"] = "Optimization started.",
        ["optimizationDone"] = "Connection anchored; roaming disabled.",
        ["unlocking"] = "Restoring normal Wi-Fi mode...",
        ["unlocked"] = "Anchor lifted; roaming enabled.",
        ["pinging"] = "Running TCP connectivity test...",
        ["noConnection"] = "No active Wi-Fi connection found.",
        ["success"] = "Success",
        ["error"] = "Error"
        ,["subtitle"] = "Wi-Fi Anchor & Optimizer  •  Enterprise roaming control"
        ,["author"] = "Created by: Ömer YİĞİT"
        ,["website"] = "Visit omeryigit.tr"
        ,["radar"] = "LIVE RADAR"
        ,["scanning"] = "● SCANNING NETWORK"
        ,["connected"] = "● CONNECTED • RADAR ONLINE"
        ,["waiting"] = "● WAITING FOR WI-FI"
        ,["secureMode"] = "Stream privacy"
        ,["secureOn"] = "Private view enabled"
        ,["secureOff"] = "Detailed view enabled"
        ,["masked"] = "Hidden"
        ,["connect"] = "Connect"
        ,["theme"] = "Theme"
        ,["violetTheme"] = "Violet night"
        ,["midnightTheme"] = "Midnight navy"
        ,["connectedNow"] = "Connection active"
    };

    public event EventHandler? LanguageChanged;
    public AppLanguage CurrentLanguage { get; private set; } = AppLanguage.Turkish;

    public string this[string key] => (CurrentLanguage == AppLanguage.Turkish ? _turkish : _english)
        .TryGetValue(key, out var value) ? value : key;

    public void SetLanguage(AppLanguage language)
    {
        if (CurrentLanguage == language)
            return;

        CurrentLanguage = language;
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }
}
