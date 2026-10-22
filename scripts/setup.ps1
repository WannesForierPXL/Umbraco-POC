<#
.SYNOPSIS
  Zet een lokale ontwikkelomgeving van de LLASER Umbraco-POC op.

.DESCRIPTION
  - maakt .env aan met een willekeurig SQL Server-wachtwoord (als .env nog niet bestaat)
  - zet alle secrets in dotnet user-secrets voor Llaser.Cms en Llaser.ContentStub
    (enkel wat nog ontbreekt; opnieuw uitvoeren is veilig)
  - de admin-login is vast (zie README): admin@llaser.test / LlaserPoc2026!

  Secrets komen nooit in git: .env staat in .gitignore, user-secrets staan in je gebruikersprofiel.

.EXAMPLE
  pwsh ./scripts/setup.ps1
#>
param(
    [string]$Database = "LlaserCms"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$cms = Join-Path $root "src/Llaser.Cms"
$stub = Join-Path $root "src/Llaser.ContentStub"

# Werkt in Windows PowerShell 5.1 én PowerShell 7
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
function Willekeurige-Bytes([int]$aantal) { $b = New-Object byte[] $aantal; $rng.GetBytes($b); $b }
function Nieuw-Secret([int]$lengte = 24) {
    $tekens = [char[]]"ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789"
    -join ((Willekeurige-Bytes $lengte) | ForEach-Object { $tekens[$_ % $tekens.Length] })
}
# Voldoet aan de regels van SQL Server en Umbraco (lengte, hoofd- en kleine letters, cijfer, symbool)
function Nieuw-Wachtwoord { (Nieuw-Secret 20) + "Aa1!" }

function Lees-Secrets([string]$project) {
    $lijst = @{}
    dotnet user-secrets list --project $project 2>$null | ForEach-Object {
        if ($_ -match '^(.+?) = (.*)$') { $lijst[$Matches[1]] = $Matches[2] }
    }
    $lijst
}
function Zet-Secret([string]$project, [hashtable]$bestaand, [string]$sleutel, [string]$waarde, [switch]$Overschrijf) {
    if ($bestaand.ContainsKey($sleutel) -and -not $Overschrijf) { return $bestaand[$sleutel] }
    $uitvoer = dotnet user-secrets set $sleutel $waarde --project $project 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Kon secret '$sleutel' niet instellen voor $project`n$uitvoer" }
    $bestaand[$sleutel] = $waarde
    $waarde
}

Write-Host "== Controle van de vereisten"
foreach ($cmd in "dotnet", "docker") {
    if (-not (Get-Command $cmd -ErrorAction SilentlyContinue)) { throw "'$cmd' niet gevonden. Installeer het eerst (zie README)." }
}
$sdk = dotnet --list-sdks | Where-Object { $_ -match '^10\.' }
if (-not $sdk) { throw ".NET 10 SDK niet gevonden. Installeer die via https://dotnet.microsoft.com/download/dotnet/10.0" }
dotnet dev-certs https --check --trust *> $null
if ($LASTEXITCODE -ne 0) {
    Write-Warning "Het HTTPS-ontwikkelcertificaat is nog niet vertrouwd. Voer uit: dotnet dev-certs https --trust"
}

Write-Host "== .env (SQL Server-wachtwoord)"
$envBestand = Join-Path $root ".env"
if (-not (Test-Path $envBestand)) {
    $sa = Nieuw-Wachtwoord
    "# Gegenereerd door scripts/setup.ps1. Niet in git.`nMSSQL_SA_PASSWORD=$sa" | Set-Content -Path $envBestand -Encoding utf8
    Write-Host "   .env aangemaakt"
} else {
    Write-Host "   .env bestaat al, wordt hergebruikt"
}
$sa = (Get-Content $envBestand | Where-Object { $_ -match '^MSSQL_SA_PASSWORD=' }) -replace '^MSSQL_SA_PASSWORD=', ''
if (-not $sa) { throw "MSSQL_SA_PASSWORD ontbreekt in .env" }

Write-Host "== user-secrets voor Llaser.Cms"
$c = Lees-Secrets $cms
Zet-Secret $cms $c "ConnectionStrings:umbracoDbDSN" "Server=localhost,1433;Database=$Database;User Id=sa;Password=$sa;TrustServerCertificate=True" -Overschrijf | Out-Null
Zet-Secret $cms $c "ConnectionStrings:umbracoDbDSN_ProviderName" "Microsoft.Data.SqlClient" | Out-Null
Zet-Secret $cms $c "Umbraco:CMS:Imaging:HMACSecretKey" ([Convert]::ToBase64String((Willekeurige-Bytes 64))) | Out-Null
$apiKey = Zet-Secret $cms $c "Umbraco:CMS:DeliveryApi:ApiKey" (Nieuw-Secret 48)
$webhook = Zet-Secret $cms $c "Llaser:Webhook:Secret" (Nieuw-Secret 48)

Write-Host "== user-secrets voor Llaser.ContentStub"
$s = Lees-Secrets $stub
Zet-Secret $stub $s "Cms:ApiKey" $apiKey -Overschrijf | Out-Null
Zet-Secret $stub $s "Webhook:Secret" $webhook -Overschrijf | Out-Null

Write-Host "== Controle"
$nodigCms = "ConnectionStrings:umbracoDbDSN", "ConnectionStrings:umbracoDbDSN_ProviderName",
    "Umbraco:CMS:Imaging:HMACSecretKey", "Umbraco:CMS:DeliveryApi:ApiKey", "Llaser:Webhook:Secret"
$nodigStub = "Cms:ApiKey", "Webhook:Secret"
$c = Lees-Secrets $cms; $s = Lees-Secrets $stub
$mist = @($nodigCms | Where-Object { -not $c[$_] } | ForEach-Object { "Llaser.Cms: $_" }) + @($nodigStub | Where-Object { -not $s[$_] } | ForEach-Object { "Llaser.ContentStub: $_" })
if ($mist.Count -gt 0) { throw "Deze secrets ontbreken nog:`n  " + ($mist -join "`n  ") }
if ($c["Umbraco:CMS:DeliveryApi:ApiKey"] -ne $s["Cms:ApiKey"]) { throw "De API-key van de stub verschilt van die van Umbraco" }
Write-Host "   alle secrets aanwezig"

Write-Host ""
Write-Host "Klaar. Volgende stappen (zie README):" -ForegroundColor Green
Write-Host "  docker compose up -d"
Write-Host "  dotnet run --project src/Llaser.Cms --launch-profile Umbraco.Web.UI"
Write-Host "  dotnet run --project src/Llaser.ContentStub --launch-profile https"
Write-Host ""
Write-Host "Backoffice:  https://localhost:44310/umbraco"
Write-Host "  e-mail:     admin@llaser.test"
Write-Host "  wachtwoord: LlaserPoc2026!"
Write-Host "Demopagina:  https://localhost:5001"
Write-Host ""
Write-Host "Let op: de admin wordt aangemaakt bij de EERSTE start op een lege database." -ForegroundColor Yellow
