<#
    VERIFICA CHE QUESTO SIA UN PLUGIN, non solo un DLL che compila.

    Un plugin di N.I.N.A. puo' compilare benissimo e non essere caricato, oppure essere
    caricato e mostrare una pagina opzioni vuota, senza che nessuno dica niente. I
    controlli qui sotto sono quelli che colgono in silenzio quei guasti:

      1. esiste una classe esportata come IPluginManifest, ed estende PluginBase;
      2. i metadati che PluginBase legge per riflessione ci sono tutti;
      3. il GUID dell'assembly e la chiave Id del manifest coincidono;
      4. la chiave del DataTemplate delle opzioni combacia con AssemblyTitle
         CARATTERE PER CARATTERE — e' la convenzione, e sbagliarla non da' errore:
         da' una pagina vuota;
      5. la cartella di uscita non contiene DLL di N.I.N.A. o di WebView2, che il
         programma distribuisce gia' e che una seconda copia manderebbe in conflitto.

    Uso:  pwsh -File scripts/verifica-scheletro.ps1
#>

$ErrorActionPreference = 'Stop'
$radice = Split-Path -Parent $PSScriptRoot
$prog   = Join-Path $radice 'src\AstroImage.NINA.Plugin'
$uscita = Join-Path $prog 'bin\x64\Release'
$dll    = Join-Path $uscita 'AstroImage.NINA.Plugin.dll'

$ok = 0; $ko = 0
function Verifica([string]$nome, [bool]$esito, [string]$nota = '') {
    if ($esito) { Write-Host ("  ok    " + $nome + $(if ($nota) { "   [$nota]" })) ; $script:ok++ }
    else        { Write-Host ("  FALLITO " + $nome + $(if ($nota) { "   [$nota]" })) -ForegroundColor Red; $script:ko++ }
}

Write-Host "`n--- il DLL ---"
Verifica "il plugin e' stato compilato" (Test-Path $dll) $(if (Test-Path $dll) { "{0:N0} byte" -f (Get-Item $dll).Length })
if (-not (Test-Path $dll)) { Write-Host "`nCompilalo prima:  dotnet build -c Release`n"; exit 1 }

# I nomi dei tipi vivono nell'heap #Strings dei metadati, in ASCII: si leggono senza
# caricare l'assembly, che richiederebbe le DLL di N.I.N.A. che qui non ci sono.
$byte = [System.IO.File]::ReadAllBytes($dll)
$testo = [System.Text.Encoding]::ASCII.GetString($byte)
foreach ($t in @('IPluginManifest', 'PluginBase', 'AstroImageBridgePlugin', 'ExportAttribute')) {
    Verifica "l'assembly nomina $t" ($testo.Contains($t))
}

Write-Host "`n--- i metadati che N.I.N.A. legge ---"
$info = Get-Content (Join-Path $prog 'Properties\AssemblyInfo.cs') -Raw
foreach ($k in @('Id', 'Name', 'Author', 'License', 'MinimumApplicationVersion')) {
    $m = [regex]::Match($info, 'AssemblyMetadata\("' + $k + '",\s*"([^"]*)"')
    Verifica "  $k dichiarato e non vuoto" ($m.Success -and $m.Groups[1].Value.Trim().Length -gt 0) $m.Groups[1].Value
}

$guid = [regex]::Match($info, 'Guid\("([^"]+)"\)').Groups[1].Value
$idMd = [regex]::Match($info, 'AssemblyMetadata\("Id",\s*"([^"]+)"').Groups[1].Value
Verifica "il GUID dell'assembly e la chiave Id coincidono" ($guid -eq $idMd) $guid

Write-Host "`n--- la pagina delle opzioni ---"
# La trappola: la chiave e' AssemblyTitle + "_Options", e il confronto e' esatto.
$titolo = [regex]::Match($info, 'AssemblyTitle\("([^"]+)"\)').Groups[1].Value
$xaml   = Get-Content (Join-Path $prog 'Resources\DataTemplates.xaml') -Raw
$attesa = $titolo + '_Options'
Verifica "la chiave del template e' AssemblyTitle + _Options" `
    ($xaml.Contains('x:Key="' + $attesa + '"')) $attesa
Verifica "  e il nome non usa caratteri fuori dall'ASCII" `
    (-not ($titolo -match '[^\x20-\x7E]')) $titolo

Write-Host "`n--- che cosa esce dalla compilazione ---"
$estranei = Get-ChildItem $uscita -Recurse -File |
    Where-Object { $_.Name -match '\.dll$' -and $_.Name -ne 'AstroImage.NINA.Plugin.dll' }
Verifica "il plugin non si porta dietro DLL di N.I.N.A. o di WebView2" `
    ($estranei.Count -eq 0) $(if ($estranei.Count) { ($estranei | ForEach-Object { $_.Name }) -join ', ' } else { 'solo il proprio DLL' })

Write-Host "`n--- contro quale N.I.N.A. e' stato compilato ---"
# IL GUASTO DI TARGET SCHEDULER, che su questa macchina si vede nel log di N.I.N.A.:
#   "Could not load type 'NINA.Sequencer.Logic.ISymbolBroker' from assembly
#    'NINA.Sequencer, Version=3.2.0.9001'"
# Quel plugin e' compilato contro una 3.3 nightly e dichiara di bastarsi con meno.
# Il tipo nella 3.2 non esiste, e il plugin muore al caricamento senza che l'utente
# abbia sbagliato niente. La difesa e' che le due versioni coincidano: quella del
# pacchetto con cui si compila, e quella che il manifest dichiara come minima. Se un
# giorno si passa a un pacchetto piu' nuovo per usarne un'API, va alzata anche la
# dichiarazione — e chi ha la 3.2 vedra' un messaggio invece di un plugin morto.
$deps = Join-Path $uscita 'AstroImage.NINA.Plugin.deps.json'
if (Test-Path $deps) {
    $j = Get-Content $deps -Raw | ConvertFrom-Json
    $pkg = ($j.libraries.PSObject.Properties.Name | Where-Object { $_ -like 'NINA.Plugin/*' } |
            Select-Object -First 1) -replace '^NINA\.Plugin/', ''
    $min = [regex]::Match($info, 'AssemblyMetadata\("MinimumApplicationVersion",\s*"([^"]+)"').Groups[1].Value
    Verifica "compila contro la N.I.N.A. che dichiara di richiedere" ($pkg -eq $min) "$pkg contro $min"
} else {
    Write-Host "  --    deps.json assente: compila in Release prima"
}

Write-Host "`n--- la copia installata in N.I.N.A. ---"
# Da quando l'installazione avviene a ogni compilazione, la domanda vera non e' piu'
# "esiste una copia" ma "quella che N.I.N.A. carichera' e' l'ultima compilata". Un DLL
# vecchio in quella cartella non da' nessun errore: da' un plugin che si comporta come
# la versione di ieri, ed e' il modo piu' rapido di perdere un'ora.
$inNina = Join-Path $env:LOCALAPPDATA 'NINA\Plugins\3.0.0\AstroImage.NINA.Plugin\AstroImage.NINA.Plugin.dll'
if (Test-Path $inNina) {
    $a = (Get-FileHash $dll    -Algorithm SHA256).Hash
    $b = (Get-FileHash $inNina -Algorithm SHA256).Hash
    # Se N.I.N.A. e' aperto tiene il proprio DLL bloccato e la copia non e' potuta
    # avvenire: dire "ricompila" sarebbe un consiglio che non funziona, perche' il file
    # resta bloccato quante volte si ricompili. Si dice chi lo blocca.
    $aperta = $null -ne (Get-Process NINA -ErrorAction SilentlyContinue)
    Verifica "N.I.N.A. ha l'ultimo binario compilato" ($a -eq $b) `
        $(if ($a -eq $b) { "identici" }
          elseif ($aperta) { "DIVERSI: N.I.N.A. e' aperto e tiene il file, chiudilo e ricompila" }
          else { "DIVERSI: ricompila" })
} else {
    Write-Host "  --    non installato   [dotnet build -c Release lo installa]"
}

Write-Host "`n$ok verifiche superate, $ko fallite`n"
if ($ko -gt 0) { exit 1 }
