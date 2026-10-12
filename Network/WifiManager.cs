using System.Diagnostics;
using System.Net.Sockets;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CapaOptimizer.Core;

namespace CapaOptimizer.Network;

public sealed record WifiConnection(string InterfaceName, string Ssid, string Bssid, string Signal, string State)
{
    public int SignalPercent
    {
        get
        {
            var match = Regex.Match(Signal, @"\d{1,3}");
            return match.Success && int.TryParse(match.Value, out var value)
                ? Math.Clamp(value, 0, 100)
                : 0;
        }
    }
}
public sealed record WifiNetwork(string Ssid, int SignalPercent, string Security);
public sealed record TcpConnectivityResult(bool IsReachable, int? LatencyMilliseconds, string Message);

public sealed class WifiManager
{
    private readonly ProcessRunner _processRunner;
    public string LastParseDiagnostic { get; private set; } = "Parser henüz çalışmadı.";
    private static readonly Regex BssidRegex = new(@"(?<value>[0-9A-Fa-f]{2}(?:[:-][0-9A-Fa-f]{2}){5})", RegexOptions.Compiled);

    public WifiManager(ProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public async Task<WifiConnection?> GetActiveConnectionAsync(CancellationToken cancellationToken = default)
    {
        var result = await _processRunner.RunCmdAsync("netsh wlan show interfaces", cancellationToken);
        if (result.ExitCode != 0)
        {
            LastParseDiagnostic = $"netsh başarısız. ExitCode={result.ExitCode}; STDERR={FormatDiagnosticValue(result.StandardError)}";
            return null;
        }

        string? interfaceName = null;
        string? ssid = null;
        string? bssid = null;
        string? signal = null;
        string? state = null;
        var parsedFields = new List<string>();
        var ignoredFields = new List<string>();
        var lineCount = 0;
        foreach (var line in result.StandardOutput.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            lineCount++;
            var separator = line.IndexOf(':');
            if (separator <= 0)
                continue;

            var key = NormalizeFieldName(line[..separator]);
            var value = line[(separator + 1)..].Trim();
            if (key is "name" or "ad" or "arayuzadi" or "interfacename")
            {
                interfaceName ??= value;
                parsedFields.Add($"interfaceName <- [{key}] '{value}'");
            }
            else if (key == "ssid")
            {
                ssid ??= value;
                parsedFields.Add($"ssid <- [{key}] '{value}'");
            }
            else if (key is "bssid" or "apbssid")
            {
                bssid ??= BssidRegex.Match(value).Groups["value"].Value;
                parsedFields.Add($"bssid <- [{key}] '{value}' => '{bssid}'");
            }
            else if (key is "signal" or "sinyal")
            {
                signal ??= value;
                parsedFields.Add($"signal <- [{key}] '{value}'");
            }
            else if (key is "state" or "durum")
            {
                state ??= value;
                parsedFields.Add($"state <- [{key}] '{value}'");
            }
            else if (parsedFields.Count < 20)
            {
                ignoredFields.Add($"[{key}] '{value}'");
            }
        }

        var missing = string.Join(", ", new[]
        {
            string.IsNullOrWhiteSpace(interfaceName) ? "interfaceName" : null,
            string.IsNullOrWhiteSpace(ssid) ? "ssid" : null,
            string.IsNullOrWhiteSpace(bssid) ? "bssid" : null
        }.Where(static value => value is not null));
        LastParseDiagnostic = string.Join(Environment.NewLine,
            $"netsh başarılı. ExitCode={result.ExitCode}; Satır={lineCount}",
            $"Sonuç: interfaceName='{interfaceName ?? "<null>"}'; ssid='{ssid ?? "<null>"}'; bssid='{bssid ?? "<null>"}'; signal='{signal ?? "<null>"}'; state='{state ?? "<null>"}'",
            $"Eksik alanlar: {(string.IsNullOrWhiteSpace(missing) ? "yok" : missing)}",
            $"Yakalanan alanlar: {(parsedFields.Count == 0 ? "<yok>" : string.Join(" | ", parsedFields))}",
            $"Tanınmayan alanlar: {(ignoredFields.Count == 0 ? "<yok>" : string.Join(" | ", ignoredFields))}");

        if (string.IsNullOrWhiteSpace(interfaceName) || string.IsNullOrWhiteSpace(ssid) ||
            string.IsNullOrWhiteSpace(bssid))
            return null;

        return new WifiConnection(interfaceName, ssid, bssid, signal ?? "-", state ?? "unknown");
    }

    public async Task<IReadOnlyList<WifiNetwork>> ScanNetworksAsync(CancellationToken cancellationToken = default)
    {
        var interfaceName = await GetInterfaceNameAsync(cancellationToken) ?? "Wi-Fi";
        var wasAutoConfigEnabled = await IsAutoConfigEnabledAsync(interfaceName, cancellationToken);
        try
        {
            if (!wasAutoConfigEnabled)
                await RunCheckedCmdAsync($"netsh wlan set autoconfig enabled=yes interface=\"{EscapeCommandValue(interfaceName)}\"", cancellationToken);

            var showNetworksCommand =
                $"netsh wlan show networks mode=bssid interface=\"{EscapeCommandValue(interfaceName)}\"";
            var result = await _processRunner.RunCmdAsync(showNetworksCommand, cancellationToken);
            if (result.ExitCode == 0 &&
                result.StandardOutput.Contains("no networks", StringComparison.OrdinalIgnoreCase))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(1200), cancellationToken);
                result = await _processRunner.RunCmdAsync(showNetworksCommand, cancellationToken);
            }
            if (result.ExitCode != 0 || result.StandardOutput.Contains("cannot be displayed", StringComparison.OrdinalIgnoreCase) ||
                result.StandardOutput.Contains("görüntülenemez", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.StandardError)
                    ? "Wi-Fi ağları taranamadı. Windows adaptör taramasına izin vermedi."
                    : result.StandardError.Trim());

            var networks = new List<WifiNetwork>();
            string? currentSsid = null;
            var currentSignal = 0;
            var currentSecurity = "Unknown";
            foreach (var rawLine in result.StandardOutput.Split(["\r\n", "\n"], StringSplitOptions.None))
            {
                var line = rawLine.Trim();
                var ssidMatch = Regex.Match(line, @"^(?:SSID|A[Iİ]D)\s+\d+\s*:\s*(.*)$", RegexOptions.IgnoreCase);
                if (ssidMatch.Success)
                {
                    AddNetwork();
                    currentSsid = ssidMatch.Groups[1].Value.Trim();
                    currentSignal = 0;
                    currentSecurity = "Unknown";
                    continue;
                }

                if (currentSsid is null)
                    continue;

                var signalMatch = Regex.Match(line, @"^(?:Signal|S[Iİ]NYAL)\s*:\s*(\d{1,3})%", RegexOptions.IgnoreCase);
                if (signalMatch.Success && int.TryParse(signalMatch.Groups[1].Value, out var signal))
                    currentSignal = Math.Clamp(signal, 0, 100);
                var securityMatch = Regex.Match(line, @"^(?:Authentication|Kimlik do[ğg]rulama)\s*:\s*(.*)$", RegexOptions.IgnoreCase);
                if (securityMatch.Success)
                    currentSecurity = securityMatch.Groups[1].Value.Trim();
            }
            AddNetwork();
            return networks;

            void AddNetwork()
            {
                if (!string.IsNullOrWhiteSpace(currentSsid) &&
                    !networks.Any(network => network.Ssid.Equals(currentSsid, StringComparison.Ordinal)))
                    networks.Add(new WifiNetwork(currentSsid, currentSignal, currentSecurity));
            }
        }
        finally
        {
            if (!wasAutoConfigEnabled)
                await RunCheckedCmdAsync($"netsh wlan set autoconfig enabled=no interface=\"{EscapeCommandValue(interfaceName)}\"", cancellationToken);
        }
    }

    private async Task<string?> GetInterfaceNameAsync(CancellationToken cancellationToken)
    {
        var result = await _processRunner.RunCmdAsync("netsh wlan show interfaces", cancellationToken);
        foreach (var line in result.StandardOutput.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            var separator = line.IndexOf(':');
            if (separator <= 0)
                continue;
            var key = NormalizeFieldName(line[..separator]);
            if (key is "name" or "ad" or "arayuzadi" or "interfacename")
                return line[(separator + 1)..].Trim();
        }
        return null;
    }

    private async Task<bool> IsAutoConfigEnabledAsync(string interfaceName, CancellationToken cancellationToken)
    {
        var result = await _processRunner.RunCmdAsync("netsh wlan show settings", cancellationToken);
        var normalizedInterface = interfaceName.Trim();
        return result.StandardOutput.Split(["\r\n", "\n"], StringSplitOptions.None)
            .Any(line => line.Contains(normalizedInterface, StringComparison.OrdinalIgnoreCase) &&
                (line.Contains("enabled", StringComparison.OrdinalIgnoreCase) ||
                 line.Contains("etkin", StringComparison.OrdinalIgnoreCase)));
    }

    public async Task ConnectToNetworkAsync(string ssid, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ssid))
            throw new ArgumentException("Wi-Fi ağ adı boş olamaz.", nameof(ssid));

        var interfaceName = (await GetActiveConnectionAsync(cancellationToken))?.InterfaceName ?? "Wi-Fi";
        await RunCheckedCmdAsync(
            $"netsh wlan connect name=\"{EscapeCommandValue(ssid)}\" interface=\"{EscapeCommandValue(interfaceName)}\"",
            cancellationToken);
    }

    private static string EscapeCommandValue(string value) => value.Replace("\"", "\"\"", StringComparison.Ordinal);

    private static string FormatDiagnosticValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "<boş>" : value.Replace(Environment.NewLine, " ", StringComparison.Ordinal).Trim();

    private static string NormalizeFieldName(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(char.ToLowerInvariant(character));
        }

        return Regex.Replace(builder.ToString(), @"[^a-z0-9]", string.Empty);
    }

    public async Task OptimizeAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var connection = await GetActiveConnectionAsync(cancellationToken)
            ?? throw new InvalidOperationException("Aktif Wi-Fi bağlantısı bulunamadı.");

        progress?.Report($"Adaptör ayarları uygulanıyor: {connection.InterfaceName}");
        await ConfigureAdapterAsync(connection.InterfaceName, cancellationToken);
        await RunCheckedCmdAsync($"netsh wlan set autoconfig enabled=yes interface=\"{connection.InterfaceName}\"", cancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        await RunCheckedCmdAsync($"netsh wlan set autoconfig enabled=no interface=\"{connection.InterfaceName}\"", cancellationToken);
        progress?.Report($"Bağlantı kesilmeden BSSID korundu: {connection.Bssid}");
    }

    private async Task<bool> WaitForConnectedAsync(string interfaceName, string ssid, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= 12; attempt++)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            var current = await GetActiveConnectionAsync(cancellationToken);
            if (current is not null &&
                current.InterfaceName.Equals(interfaceName, StringComparison.OrdinalIgnoreCase) &&
                current.Ssid.Equals(ssid, StringComparison.Ordinal) &&
                current.State.Equals("connected", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public async Task UnlockAsync(CancellationToken cancellationToken = default)
    {
        var connection = await GetActiveConnectionAsync(cancellationToken);
        var interfaceName = connection?.InterfaceName ?? "Wi-Fi";
        await RunCheckedCmdAsync($"netsh wlan set autoconfig enabled=yes interface=\"{interfaceName}\"", cancellationToken);
    }

    public async Task<TcpConnectivityResult> TcpPingAsync(string host = "8.8.8.8", int port = 53, CancellationToken cancellationToken = default)
    {
        using var client = new TcpClient();
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await client.ConnectAsync(host, port, cancellationToken);
            stopwatch.Stop();
            return new TcpConnectivityResult(true, (int)Math.Max(1, stopwatch.Elapsed.TotalMilliseconds),
                $"{host}:{port} TCP bağlantısı başarılı.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SocketException ex)
        {
            stopwatch.Stop();
            return new TcpConnectivityResult(false, null,
                $"{host}:{port} TCP bağlantısı kurulamadı ({ex.SocketErrorCode}). Wi-Fi bağlı olabilir; ağ ICMP/TCP 53 trafiğini engelliyor olabilir.");
        }
    }

    private async Task ConfigureAdapterAsync(string interfaceName, CancellationToken cancellationToken)
    {
        var escaped = interfaceName.Replace("'", "''", StringComparison.Ordinal);
        const string script = """
            $ErrorActionPreference = 'Stop'
            try {
                $adapter = Get-NetAdapter -Name 'INTERFACE_NAME' -ErrorAction Stop
                $properties = @(Get-NetAdapterAdvancedProperty -Name $adapter.Name -ErrorAction Stop)
                $rules = @(
                    @{ Pattern = 'Transmit Power|Sendeleistung'; Value = '5'; Label = 'Transmit Power' },
                    @{ Pattern = 'Roaming Aggressiveness|Roaming.*Aggress'; Value = '1'; Label = 'Roaming Aggressiveness' },
                    @{ Pattern = 'MIMO Power Save Mode|MIMO.*SMPS'; Value = 'Auto SMPS'; Label = 'MIMO Power Save Mode' }
                )
                foreach ($rule in $rules) {
                    $property = $properties | Where-Object {
                        $_.DisplayName -match $rule.Pattern -or
                        $_.RegistryKeyword -match 'TransmitPower|Roam|MIMO|SMPS'
                    } | Select-Object -First 1
                    if ($null -eq $property) {
                        Write-Output "WARN: $($rule.Label) adaptör tarafından sunulmuyor."
                        continue
                    }
                    try {
                        Set-NetAdapterAdvancedProperty -Name $adapter.Name -RegistryKeyword $property.RegistryKeyword -RegistryValue $rule.Value -NoRestart -ErrorAction Stop
                        Write-Output "OK: $($rule.Label) -> $($rule.Value)"
                    }
                    catch {
                        Write-Output "WARN: $($rule.Label) uygulanamadı: $($_.Exception.Message)"
                    }
                }
            }
            catch {
                [Console]::Error.WriteLine("Adaptör ayarları okunamadı: $($_.Exception.Message)")
                exit 1
            }
            """;
        await RunCheckedPowerShellAsync(script.Replace("INTERFACE_NAME", escaped, StringComparison.Ordinal), cancellationToken);
    }

    private async Task RunCheckedCmdAsync(string command, CancellationToken cancellationToken)
    {
        var result = await _processRunner.RunCmdAsync(command, cancellationToken);
        if (result.ExitCode != 0)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError);
    }

    private async Task RunCheckedPowerShellAsync(string script, CancellationToken cancellationToken)
    {
        var result = await _processRunner.RunPowerShellAsync(script, cancellationToken);
        if (result.ExitCode != 0)
        {
            var details = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
            throw new InvalidOperationException($"PowerShell adaptör ayarı başarısız oldu (ExitCode={result.ExitCode}). {details}".Trim());
        }
    }
}
