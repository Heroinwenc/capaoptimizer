using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using CapaOptimizer.Core;
using CapaOptimizer.Network;
using Microsoft.Web.WebView2.Core;

namespace CapaOptimizer.UI;

public partial class MainForm : Form
{
    private readonly LanguageManager _languageManager = new();
    private readonly WifiManager _wifiManager = new(new ProcessRunner());
    private bool _isAnchored;
    private bool _isBusy;
    private bool _pageReady;
    private bool _secureMode = true;
    private CancellationTokenSource? _operationCancellation;
    private WifiConnection? _connection;
    private static readonly JsonSerializerOptions PageJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public MainForm()
    {
        InitializeComponent();
        Load += MainForm_Load;
        FormClosing += MainForm_FormClosing;
        _languageManager.LanguageChanged += (_, _) => UpdatePage();
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        _operationCancellation?.Cancel();
        _operationCancellation?.Dispose();
        _operationCancellation = null;
        _pageReady = false;
        if (webView.CoreWebView2 is not null)
        {
            webView.CoreWebView2.WebMessageReceived -= WebMessageReceived;
            webView.CoreWebView2.Stop();
        }
        webView.Dispose();
    }

    private async void MainForm_Load(object? sender, EventArgs e)
    {
        try
        {
            await webView.EnsureCoreWebView2Async();
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            webView.CoreWebView2.WebMessageReceived += WebMessageReceived;
            webView.NavigateToString(HtmlDocument);
            _pageReady = true;
            UpdatePage();
            await RefreshRadarAsync(true);
            await ScanNetworksAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Capa başlatılamadı: {ex.Message}", "Capa", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var action = e.TryGetWebMessageAsString();
            if (action.StartsWith("network:", StringComparison.Ordinal))
            {
                await ConnectToNetworkAsync(action["network:".Length..]);
                return;
            }
            switch (action)
            {
                case "connect":
                    await RefreshRadarAsync(true);
                    break;
                case "scan":
                    await ScanNetworksAsync();
                    break;
                case "optimize":
                    await RunOperationAsync(async progress =>
                    {
                        SetActivity(_languageManager["optimizationStarted"]);
                        await _wifiManager.OptimizeAsync(progress, progressCancellationToken);
                        _isAnchored = true;
                        SetActivity(_languageManager["optimizationDone"]);
                        await RefreshRadarAsync();
                    });
                    break;
                case "unlock":
                    await RunOperationAsync(async _ =>
                    {
                        SetActivity(_languageManager["unlocking"]);
                        await _wifiManager.UnlockAsync(progressCancellationToken);
                        _isAnchored = false;
                        SetActivity(_languageManager["unlocked"]);
                        await RefreshRadarAsync();
                    });
                    break;
                case "ping":
                    await RunOperationAsync(async _ =>
                    {
                        SetActivity(_languageManager["pinging"]);
                        var result = await _wifiManager.TcpPingAsync(cancellationToken: progressCancellationToken);
                        SetActivity(result.IsReachable
                            ? $"{_languageManager["pingResult"]} {result.LatencyMilliseconds} ms"
                            : result.Message);
                    });
                    break;
                case "privacy-on":
                    _secureMode = true;
                    UpdatePage();
                    break;
                case "privacy-off":
                    _secureMode = false;
                    UpdatePage();
                    break;
                case "language-en":
                    _languageManager.SetLanguage(AppLanguage.English);
                    break;
                case "language-tr":
                    _languageManager.SetLanguage(AppLanguage.Turkish);
                    break;
                case "open-site":
                    Process.Start(new ProcessStartInfo("https://omeryigit.tr") { UseShellExecute = true });
                    break;
            }

        }
        catch (Exception ex)
        {
            SetActivity($"{_languageManager["error"]}: {ex.Message}");
        }
    }

    private async Task ScanNetworksAsync()
    {
        try
        {
            SetActivity("Wi-Fi ağları taranıyor...");
            var networks = await _wifiManager.ScanNetworksAsync();
            ExecutePage("updateNetworks", networks);
            SetActivity($"{networks.Count} Wi-Fi ağı bulundu.");
        }
        catch (Exception ex)
        {
            SetActivity($"{_languageManager["error"]}: {ex.Message}");
        }
    }

    private async Task ConnectToNetworkAsync(string ssid)
    {
        await RunOperationAsync(async _ =>
        {
            SetActivity($"'{ssid}' ağına bağlanılıyor...");
            await _wifiManager.ConnectToNetworkAsync(ssid, progressCancellationToken);
            await Task.Delay(1500, progressCancellationToken);
            await RefreshRadarAsync(true);
        });
    }

    private async Task RefreshRadarAsync(bool allowBusy = false)
    {
        if ((!allowBusy && _isBusy) || !_pageReady)
            return;

        try
        {
            SetActivity(_languageManager["refreshing"]);
            _connection = await _wifiManager.GetActiveConnectionAsync();
            if (_connection is null)
            {
                UpdatePage();
                SetActivity(_languageManager["noConnection"]);
                return;
            }

            UpdatePage();
            SetActivity(_languageManager["connectedNow"]);
        }
        catch (Exception ex)
        {
            SetActivity($"{_languageManager["error"]}: {ex.Message}");
        }
    }

    private async Task RunOperationAsync(Func<IProgress<string>, Task> operation)
    {
        if (_isBusy)
            return;

        _isBusy = true;
        _operationCancellation?.Dispose();
        _operationCancellation = new CancellationTokenSource();
        SetBusy(true);
        try
        {
            await operation(new Progress<string>(SetActivity));
        }
        catch (OperationCanceledException)
        {
            SetActivity("İşlem iptal edildi.");
        }
        catch (Exception ex)
        {
            SetActivity($"{_languageManager["error"]}: {ex.Message}");
        }
        finally
        {
            _isBusy = false;
            SetBusy(false);
            _operationCancellation.Dispose();
            _operationCancellation = null;
        }
    }

    private CancellationToken progressCancellationToken =>
        _operationCancellation?.Token ?? CancellationToken.None;

    private void SetActivity(string message) => ExecutePage("setActivity", message);

    private void SetBusy(bool busy) => ExecutePage("setBusy", busy);

    private void UpdatePage()
    {
        if (!_pageReady)
            return;

        var connection = _connection;
        var data = new
        {
            language = _languageManager.CurrentLanguage == AppLanguage.Turkish ? "tr" : "en",
            anchored = _isAnchored,
            secureMode = _secureMode,
            activity = _languageManager["ready"],
            connection = connection is null ? null : new
            {
                ssid = connection.Ssid,
                bssid = _secureMode ? MaskSensitiveValue(connection.Bssid) : connection.Bssid,
                interfaceName = connection.InterfaceName,
                signal = connection.SignalPercent,
                state = connection.State
            }
        };
        ExecutePage("updateState", data);
    }

    private void ExecutePage(string function, object value)
    {
        if (!_pageReady || webView.CoreWebView2 is null)
            return;
        var json = JsonSerializer.Serialize(value, PageJsonOptions);
        webView.CoreWebView2.ExecuteScriptAsync($"window.capa && window.capa.{function}({json});");
    }

    private static string MaskSensitiveValue(string value)
    {
        var parts = value.Split(':', '-', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 6 ? $"{parts[0]}:••:••:••:••:••" : "••••••••••••";
    }

    private static readonly string HtmlDocument = """
<!doctype html>
<html lang="tr">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<style>
:root{color-scheme:dark;font-family:Inter,Segoe UI,Arial,sans-serif;background:#0a0914;color:#f6f1ff}
*{box-sizing:border-box;user-select:none}body{margin:0;min-height:100vh;background:radial-gradient(circle at 80% 0,#36215e 0,#171128 38%,#0a0914 78%);overflow:auto}
.app{max-width:1180px;margin:auto;padding:28px clamp(18px,4vw,54px) 42px}.top{display:flex;align-items:center;justify-content:space-between;gap:18px;margin-bottom:42px}
.brand{display:flex;align-items:center;gap:14px}.logo{display:grid;place-items:center;width:48px;height:48px;border-radius:16px;background:linear-gradient(135deg,#a879ff,#5b2ca5);font-size:25px;box-shadow:0 12px 34px #8d5cf455}.brand h1{margin:0;font-size:clamp(22px,3vw,34px);letter-spacing:-1px}.brand p{margin:4px 0 0;color:#aa9fc4;font-size:13px}
.top-actions{display:flex;align-items:center;gap:12px}.select,.lang{border:1px solid #6b568f;background:#1a1430;color:#eee6ff;border-radius:10px;padding:10px 12px;font:inherit}.lang{padding:9px 10px}
.hero{display:grid;grid-template-columns:minmax(0,1.2fr) minmax(300px,.8fr);gap:22px;align-items:stretch}.panel{background:#151026cc;border:1px solid #4b386d;border-radius:24px;box-shadow:0 20px 70px #0006;backdrop-filter:blur(16px)}
.intro{padding:34px}.eyebrow{color:#b992ff;font-size:12px;letter-spacing:2px;font-weight:700}.intro h2{font-size:clamp(32px,5vw,64px);line-height:1.02;letter-spacing:-3px;margin:18px 0}.intro p{color:#b9aecf;max-width:490px;line-height:1.7;margin-bottom:28px}
.actions{display:flex;flex-wrap:wrap;gap:10px}.btn{border:0;border-radius:12px;padding:13px 16px;color:white;font-weight:700;cursor:pointer;background:#6f42c1;transition:.2s transform,.2s filter}.btn:hover{transform:translateY(-2px);filter:brightness(1.15)}.btn.secondary{background:#30204e}.btn:disabled{opacity:.5;cursor:wait;transform:none}
.radar-panel{padding:26px;display:flex;flex-direction:column;justify-content:space-between;min-height:330px}.panel-title{display:flex;justify-content:space-between;align-items:center;color:#b9aecf;font-size:12px;letter-spacing:1.5px}.online{color:#72edbf}.radar{position:relative;aspect-ratio:1;max-width:245px;width:100%;margin:18px auto}.radar:before,.radar:after{content:"";position:absolute;inset:8%;border:1px solid #8d5cf477;border-radius:50%;box-shadow:0 0 0 42px #8d5cf422,0 0 0 84px #8d5cf411}.radar:after{inset:50% auto auto 8%;width:84%;height:1px;background:#8d5cf466;border:0;box-shadow:none}.sweep{position:absolute;left:50%;top:50%;width:45%;height:2px;transform-origin:left center;background:linear-gradient(90deg,#b992ff,#72edbf00);animation:sweep 3s linear infinite}.dot{position:absolute;left:50%;top:50%;width:16px;height:16px;border-radius:50%;background:#72edbf;box-shadow:0 0 25px #72edbf;transform:translate(-50%,-50%)}@keyframes sweep{to{transform:rotate(360deg)}}
.signal{margin-top:4px}.signal-row{display:flex;justify-content:space-between;align-items:end}.signal-value{font-size:38px;font-weight:800}.signal-label{color:#b9aecf;font-size:12px}.meter{height:8px;background:#2a2041;border-radius:10px;overflow:hidden;margin-top:10px}.meter i{display:block;height:100%;width:0;background:linear-gradient(90deg,#7c4dca,#72edbf);border-radius:inherit;transition:width .5s}
.lower{display:grid;grid-template-columns:1fr 1fr;gap:22px;margin-top:22px}.network,.privacy{padding:25px}.network h3,.privacy h3{margin:0 0 18px}.grid{display:grid;grid-template-columns:1fr 1fr;gap:14px}.item{background:#21183a;border-radius:12px;padding:13px;min-width:0}.item small{display:block;color:#9789ad;font-size:11px;margin-bottom:5px}.item b{display:block;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;font-size:14px}.privacy{display:flex;flex-direction:column;justify-content:space-between}.privacy p{color:#aa9fc4;line-height:1.6;font-size:13px}.toggle{display:flex;align-items:center;gap:12px;color:#d9cced;cursor:pointer}.toggle input{accent-color:#9b6bea;width:18px;height:18px}
.status{margin-top:22px;color:#a99bbd;font-size:13px;min-height:20px}.footer{margin-top:36px;color:#786c8f;font-size:12px;display:flex;justify-content:space-between}.footer a{color:#c19bff;text-decoration:none}
.networks{margin-top:22px;padding:25px}.network-head{display:flex;justify-content:space-between;align-items:center;gap:15px}.network-head h3{margin:0}.scan{background:#30204e;border:0;color:#fff;border-radius:10px;padding:10px 14px;font-weight:700;cursor:pointer}.network-list{display:grid;grid-template-columns:repeat(auto-fill,minmax(190px,1fr));gap:10px;margin-top:16px}.network-card{border:1px solid #493669;background:#21183a;border-radius:13px;padding:13px;text-align:left;color:#fff;cursor:pointer}.network-card:hover{border-color:#b992ff;transform:translateY(-1px)}.network-card strong{display:block;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}.network-card small{display:block;color:#a99bbd;margin-top:6px}
.ad{position:fixed;inset:0;background:#05040bbd;display:grid;place-items:center;padding:20px;z-index:5}.ad-card{max-width:440px;padding:30px;border:1px solid #8561bd;border-radius:24px;background:linear-gradient(145deg,#2a1b49,#120d22);box-shadow:0 25px 90px #000b}.ad-card h2{margin:0 0 12px;font-size:30px}.ad-card p{color:#bdafd1;line-height:1.6}.ad-actions{display:flex;gap:10px;justify-content:flex-end;margin-top:22px}.ad-actions button{border:0;border-radius:10px;padding:12px 15px;color:#fff;font-weight:700;cursor:pointer}.ad-open{background:#7b4fca}.ad-close{background:#34244f}
@media(max-width:800px){.top{align-items:flex-start}.hero,.lower{grid-template-columns:1fr}.radar-panel{min-height:300px}}@media(max-width:520px){.app{padding:18px 14px 30px}.top{margin-bottom:25px}.top-actions{gap:6px}.brand p{display:none}.intro{padding:25px}.intro h2{font-size:40px}.actions .btn{width:100%}.grid{grid-template-columns:1fr}.footer{display:block;line-height:2}}
</style>
</head>
<body>
<main class="app">
<header class="top"><div class="brand"><div class="logo">⚓</div><div><h1>CAPA</h1><p data-i18n="subtitle">Wi-Fi Anchor & Optimizer</p></div></div><div class="top-actions"><select class="lang" id="language"><option value="tr">TR</option><option value="en">EN</option></select></div></header>
<section class="hero"><div class="panel intro"><div class="eyebrow" data-i18n="eyebrow">WI-FI STABILITY SYSTEM</div><h2 data-i18n="headline">Bağlantını<br><span style="color:#b992ff">demirle.</span></h2><p data-i18n="description">Kurumsal ağlarda roaming hareketlerini kontrol et, bağlantını daha kararlı ve görünür bir deneyimle yönet.</p><div class="actions"><button class="btn" data-action="connect" data-i18n="connect">↻ Bağlan</button><button class="btn" data-action="optimize" data-i18n="optimize">⚓ Optimize Et</button><button class="btn secondary" data-action="unlock" data-i18n="unlock">🌊 Demiri Al</button><button class="btn secondary" data-action="ping" data-i18n="ping">◉ Bağlantıyı Sına</button></div></div>
<div class="panel radar-panel"><div class="panel-title"><span data-i18n="radar">CANLI RADAR</span><span class="online" id="radarState">● BEKLENİYOR</span></div><div class="radar"><div class="sweep"></div><div class="dot"></div></div><div class="signal"><div class="signal-row"><span class="signal-label" data-i18n="signal">Sinyal kalitesi</span><strong class="signal-value" id="signalValue">--%</strong></div><div class="meter"><i id="signalMeter"></i></div></div></div></section>
<section class="lower"><div class="panel network"><h3 data-i18n="network">Ağ bilgileri</h3><div class="grid"><div class="item"><small>SSID</small><b id="ssid">--</b></div><div class="item"><small>BSSID</small><b id="bssid">--</b></div><div class="item"><small data-i18n="adapter">Adaptör</small><b id="adapter">--</b></div><div class="item"><small data-i18n="state">Durum</small><b id="state">--</b></div></div></div><div class="panel privacy"><div><h3 data-i18n="privacy">Yayın güvenliği</h3><p data-i18n="privacyText">Canlı yayın sırasında BSSID ve benzeri hassas ağ bilgilerini gizler.</p></div><label class="toggle"><input id="privacy" type="checkbox" checked><span data-i18n="privacyOn">Gizli görünüm açık</span></label></div></section>
<section class="panel networks"><div class="network-head"><h3 data-i18n="available">Yakındaki Wi-Fi ağları</h3><button class="scan" data-action="scan" data-i18n="scan">↻ Tara</button></div><div class="network-list" id="networkList"></div></section>
<div class="status" id="activity">Hazır</div><footer class="footer"><span data-i18n="author">Yapımcı: Ömer YİĞİT</span><a href="https://omeryigit.tr" target="_blank">omeryigit.tr ↗</a></footer>
</main>
<div class="ad" id="ad"><div class="ad-card"><div class="eyebrow">CAPA • ÖMER YİĞİT</div><h2 data-i18n="adTitle">Daha fazlasını keşfet.</h2><p data-i18n="adText">Ürünler, projeler ve yeni çalışmalar için geliştirici web sitemi ziyaret edebilirsin.</p><div class="ad-actions"><button class="ad-close" id="adClose" data-i18n="later">Daha sonra</button><button class="ad-open" id="adOpen" data-i18n="visit">Siteyi ziyaret et ↗</button></div></div></div>
<script>
const tr={subtitle:"Wi-Fi Anchor & Optimizer",eyebrow:"WI-FI STABILITY SYSTEM",headline:"Bağlantını<br><span style='color:#b992ff'>demirle.</span>",description:"Kurumsal ağlarda roaming hareketlerini kontrol et, bağlantını daha kararlı ve görünür bir deneyimle yönet.",connect:"↻ Bağlantıyı yenile",optimize:"⚓ Optimize Et",unlock:"🌊 Demiri Al",ping:"◉ Bağlantıyı Sına",radar:"CANLI RADAR",network:"Ağ bilgileri",adapter:"Adaptör",state:"Durum",signal:"Sinyal kalitesi",privacy:"Yayın güvenliği",privacyText:"Canlı yayın sırasında BSSID ve benzeri hassas ağ bilgilerini gizler.",privacyOn:"Gizli görünüm açık",privacyOff:"Ayrıntılı görünüm açık",author:"Yapımcı: Ömer YİĞİT",available:"Yakındaki Wi-Fi ağları",scan:"↻ Ağları tara",adTitle:"Daha fazlasını keşfet.",adText:"Ürünler, projeler ve yeni çalışmalar için geliştirici web sitemi ziyaret edebilirsin.",later:"Daha sonra",visit:"Siteyi ziyaret et ↗"};
const en={...tr,headline:"Anchor your<br><span style='color:#b992ff'>connection.</span>",description:"Control roaming behavior on enterprise networks and manage your connection through a calmer, clearer experience.",connect:"↻ Refresh connection",optimize:"⚓ Optimize",unlock:"🌊 Weigh Anchor",ping:"◉ Test Connection",radar:"LIVE RADAR",network:"Network information",adapter:"Adapter",state:"State",signal:"Signal quality",privacy:"Stream privacy",privacyText:"Hide BSSID and other sensitive network details while streaming.",privacyOn:"Private view enabled",privacyOff:"Detailed view enabled",author:"Created by: Ömer YİĞİT",available:"Nearby Wi-Fi networks",scan:"↻ Scan networks",adTitle:"Discover more.",adText:"Visit my developer website for products, projects and new work.",later:"Later",visit:"Visit website ↗"};
let dict=tr; const post=a=>chrome.webview.postMessage(a); document.querySelectorAll("[data-action]").forEach(b=>b.onclick=()=>post(b.dataset.action)); language.onchange=e=>post("language-"+e.target.value); privacy.onchange=e=>post(e.target.checked?"privacy-on":"privacy-off"); adClose.onclick=()=>ad.remove(); adOpen.onclick=()=>{post("open-site");ad.remove()};
window.capa={setActivity:m=>activity.textContent=m,setBusy:v=>document.querySelectorAll("button").forEach(b=>b.disabled=v),updateState:s=>{dict=s.language==="en"?en:tr; language.value=s.language; document.querySelectorAll("[data-i18n]").forEach(e=>e.innerHTML=dict[e.dataset.i18n]); privacy.checked=s.secureMode; if(s.connection){ssid.textContent=s.connection.ssid;bssid.textContent=s.connection.bssid;adapter.textContent=s.connection.interfaceName;state.textContent=s.connection.state;signalValue.textContent=s.connection.signal+"%";signalMeter.style.width=s.connection.signal+"%";radarState.textContent=s.anchored?"● ANCHORED":"● CONNECTED";radarState.className="online"}else{signalValue.textContent="--%";signalMeter.style.width="0";radarState.textContent="● WAITING";}},updateNetworks:n=>{networkList.innerHTML=n.length?n.map(x=>`<button class="network-card" data-ssid="${x.ssid.replaceAll('"','&quot;')}"><strong>${x.ssid||"(Hidden network)"}</strong><small>${x.signalPercent}% • ${x.security}</small></button>`).join(""):"<small>No networks found</small>";networkList.querySelectorAll("[data-ssid]").forEach(b=>b.onclick=()=>post("network:"+b.dataset.ssid))}};
</script>
</body></html>
""";
}
