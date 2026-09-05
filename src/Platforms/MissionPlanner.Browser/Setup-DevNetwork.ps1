[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$address = '192.168.1.175'
$subject = 'CN=localhost'
$publicCertificatePath = Join-Path $PSScriptRoot 'dev-certs/MissionPlanner-Browser.cer'

# Keep the private key in the Windows user certificate store, never in the repository.
$certificate = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object { $_.Subject -eq $subject -and $_.FriendlyName -eq 'MissionPlanner Browser Development' -and $_.HasPrivateKey -and $_.NotAfter -gt (Get-Date).AddDays(30) } |
    Sort-Object NotAfter -Descending | Select-Object -First 1
if (-not $certificate) {
    $certificate = New-SelfSignedCertificate -Type Custom -Subject $subject `
        -FriendlyName 'MissionPlanner Browser Development' -CertStoreLocation Cert:\CurrentUser\My `
        -KeyAlgorithm RSA -KeyLength 2048 -HashAlgorithm SHA256 -KeyExportPolicy NonExportable `
        -KeyUsage DigitalSignature,KeyEncipherment -NotAfter (Get-Date).AddMonths(12) `
        -TextExtension @(
            '2.5.29.17={text}DNS=localhost&DNS=*.dev.localhost&DNS=*.dev.internal&DNS=host.docker.internal&DNS=host.containers.internal&IPAddress=127.0.0.1&IPAddress=::1&IPAddress=192.168.1.175',
            '2.5.29.37={text}1.3.6.1.5.5.7.3.1',
            '2.5.29.19={text}CA=false',
            # ASP.NET Core development certificate marker, version 6 (.NET 10).
            '1.3.6.1.4.1.311.84.1.1={hex}06'
        )
}
New-Item -ItemType Directory -Path (Split-Path $publicCertificatePath) -Force | Out-Null
Export-Certificate -Cert $certificate -FilePath $publicCertificatePath -Force | Out-Null
if (-not (Test-Path "Cert:\CurrentUser\Root\$($certificate.Thumbprint)")) {
    certutil.exe -user -addstore Root $publicCertificatePath | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not trust the development certificate.' }
}
Write-Output "Trusted certificate: $($certificate.Thumbprint)"
Write-Output "Public certificate for other PCs: $publicCertificatePath"

$networkAddress = Get-NetIPAddress -AddressFamily IPv4 -IPAddress $address
if ($networkAddress.PrefixLength -ne 24) {
    throw 'Expected home subnet 192.168.1.0/24. Review the firewall scope before proceeding.'
}
$ruleName = 'MissionPlanner-Browser-HomeNetwork'
$ruleOptions = @{
    Direction = 'Inbound'
    Action = 'Allow'
    Enabled = 'True'
    Profile = 'Any'
    Protocol = 'TCP'
    LocalPort = @('5235', '7169')
    LocalAddress = $address
    RemoteAddress = '192.168.1.0/24'
    InterfaceAlias = $networkAddress.InterfaceAlias
    Program = (Get-Command dotnet.exe).Source
}
if (Get-NetFirewallRule -Name $ruleName -ErrorAction SilentlyContinue) {
    Set-NetFirewallRule -Name $ruleName @ruleOptions | Out-Null
} else {
    New-NetFirewallRule -Name $ruleName -DisplayName 'MissionPlanner Browser - home network' @ruleOptions | Out-Null
}
Write-Output 'Firewall configured for home-subnet TCP access to ports 5235 and 7169.'
