<#
    IL PACCHETTO DA MANDARE: LA CARTELLA, NON IL FILE (18 settembre 2026).

    Nella cartella dei plugin di N.I.N.A. un DLL non sta sciolto: sta dentro una cartella riconoscibile. Questo script
    compila, mette il DLL in una cartella col nome che userebbe N.I.N.A. — «AstroImage Strategy Bridge», col file che
    ha lo stesso nome — e fa uno zip che contiene la cartella. Chi lo riceve lo scompatta dentro Plugins\3.0.0\ e ha
    finito: niente cartelle da creare a mano. La regola la applica lo strumento: se accanto allo zip resta un DLL
    sciolto, o se lo zip contiene qualcosa fuori dalla cartella, lo script cade.

    Il binario deve corrispondere a un commit: con file modificati e non committati lo script si ferma.

    Uso:  powershell -File scripts/pacchetto.ps1 [-Uscita <cartella>] [-Istruzioni <file di testo>]
          -Uscita      dove scrivere cartella e zip (di serie: pacchetto\ accanto al repository, che git ignora)
          -Istruzioni  un testo da mettere nella cartella come ISTRUZIONI.txt
#>
param(
    [string]$Uscita,
    [string]$Istruzioni
)

$ErrorActionPreference = 'Stop'
$radice   = Split-Path -Parent $PSScriptRoot
$nome     = 'AstroImage.NINA.Plugin'
$cartella = 'AstroImage Strategy Bridge'
$dll      = Join-Path $radice "src\$nome\bin\x64\Release\$nome.dll"
if (-not $Uscita) { $Uscita = Join-Path $radice 'pacchetto' }

$ko = 0
function Riga([string]$esito, [string]$testo) {
    $colore = 'Gray'
    if ($esito -eq 'FALLITO') { $colore = 'Red'; $script:ko++ }
    Write-Host ("  {0,-8} {1}" -f $esito, $testo) -ForegroundColor $colore
}

Write-Host "`n--- quale sorgente ---"
$commit  = (& git -C $radice rev-parse --short HEAD | Out-String).Trim()
$ramo    = (& git -C $radice rev-parse --abbrev-ref HEAD | Out-String).Trim()
$sporchi = @(& git -C $radice status --porcelain --untracked-files=no)
if ($sporchi.Count) {
    Write-Host "  FALLITO  $ramo @ $commit con $($sporchi.Count) file modificati e non committati: un pacchetto deve corrispondere a un commit`n" -ForegroundColor Red
    exit 1
}
Riga 'ok' "$ramo @ $commit, nessuna modifica in sospeso"

Write-Host "`n--- compilazione ---"
Push-Location $radice
try { & dotnet build -c Release -p:DeployToNina=false -nologo -v:q } finally { Pop-Location }
if ($LASTEXITCODE -ne 0) { Write-Host "`nLa compilazione e' fallita: nessun pacchetto.`n" -ForegroundColor Red; exit 1 }
$versione = [System.Reflection.AssemblyName]::GetAssemblyName($dll).Version.ToString()
Riga 'ok' "compilato, versione $versione"

Write-Host "`n--- la cartella ---"
New-Item -ItemType Directory -Force -Path $Uscita | Out-Null
$dentro = Join-Path $Uscita $cartella
# Si rifa' da capo solo la cartella che questo script produce: nient'altro, in $Uscita, si tocca.
if (Test-Path -LiteralPath $dentro) { Remove-Item -LiteralPath $dentro -Recurse -Force }
New-Item -ItemType Directory -Force -Path $dentro | Out-Null
Copy-Item -LiteralPath $dll -Destination (Join-Path $dentro "$cartella.dll")
if ($Istruzioni) { Copy-Item -LiteralPath $Istruzioni -Destination (Join-Path $dentro 'ISTRUZIONI.txt') }
Riga 'ok' ("$cartella\ con: {0}" -f ((Get-ChildItem -LiteralPath $dentro -File | ForEach-Object { $_.Name }) -join ', '))

Write-Host "`n--- lo zip ---"
$zip = Join-Path $Uscita ("AstroImage-Strategy-Bridge-$versione.zip")
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
Compress-Archive -LiteralPath $dentro -DestinationPath $zip
Add-Type -AssemblyName System.IO.Compression.FileSystem
$voci = @([System.IO.Compression.ZipFile]::OpenRead($zip).Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
$fuori = @($voci | Where-Object { -not $_.StartsWith("$cartella/") })
if ($fuori.Count) { Riga 'FALLITO' ("nello zip c'e' roba fuori dalla cartella: {0}" -f ($fuori -join ', ')) }
else { Riga 'ok' ("lo zip contiene la cartella e basta: {0}" -f ($voci -join ', ')) }

Write-Host "`n--- niente di sciolto ---"
$sciolti = @(Get-ChildItem -LiteralPath $Uscita -File -Filter '*.dll' | ForEach-Object { $_.Name })
if ($sciolti.Count) { Riga 'FALLITO' ("in $Uscita c'e' un DLL sciolto, che qualcuno potrebbe mandare al posto dello zip: {0}. Toglilo." -f ($sciolti -join ', ')) }
else { Riga 'ok' "nessun DLL sciolto accanto allo zip" }

$impronta = (Get-FileHash -LiteralPath (Join-Path $dentro "$cartella.dll") -Algorithm SHA256).Hash
Write-Host ""
Write-Host "  DLL  $impronta   $ramo @ $commit, versione $versione"
Write-Host "  zip  $zip"
Write-Host ""
if ($ko -gt 0) { Write-Host "$ko controlli caduti: il pacchetto non va mandato.`n" -ForegroundColor Red; exit 1 }
Write-Host "Pacchetto pronto: si scompatta dentro Plugins\3.0.0\ e basta.`n"
