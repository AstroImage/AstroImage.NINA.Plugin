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
      5. nella CARTELLA DEI PLUGIN di N.I.N.A. non arriva nient'altro che il DLL del
         plugin: N.I.N.A. distribuisce gia' WebView2, e una seconda copia LI' DENTRO
         vincerebbe sulla sua. Nella cartella di build quei file ci sono e non si
         possono togliere — li mette il targets del pacchetto WebView2 — ma non
         vengono distribuiti, e il controllo guarda cio' che si distribuisce;
      6. la WebView2 con cui il plugin e' compilato e quella che N.I.N.A. distribuisce
         sono la stessa: non portandosela dietro, la prende dall'ospite, e due versioni
         diverse darebbero un pannello morto senza colpa dell'utente.

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
# QUESTO CONTROLLO GUARDAVA LA CARTELLA SBAGLIATA, e per un anno e' stato rosso a
# torto. Pretendeva che nella cartella di BUILD non ci fosse nessun DLL oltre al
# proprio, come surrogato di «non si distribuisce una seconda copia di WebView2, che
# N.I.N.A. porta gia'». Il surrogato valeva quando distribuire voleva dire zippare la
# cartella di uscita; da quando si installa copiando $(TargetPath) — un file solo — la
# cartella di build non ha piu' rapporto con cio' che arriva a N.I.N.A.
#
# E quei file non si possono togliere. Non e' il .csproj a metterli: e' il
# build\Common.targets DEL PACCHETTO WebView2, che aggiunge i tre assembly come
# <Reference> copy-local e il loader nativo come <Content CopyToOutputDirectory>.
# `ExcludeAssets: runtime` esclude gli asset di tipo RUNTIME, non quelli di tipo
# BUILD: quel targets gira comunque, ed e' cosi' per disegno del pacchetto.
#
# Restano pero' due cose vere da misurare, e sono diverse fra loro.
$estranei = @(Get-ChildItem $uscita -Recurse -File |
    Where-Object { $_.Name -match '\.dll$' -and $_.Name -ne 'AstroImage.NINA.Plugin.dll' } |
    ForEach-Object { $_.Name })
$dllNina = @($estranei | Where-Object { $_ -like 'NINA*' })
$dllWv2  = @($estranei | Where-Object { $_ -like 'Microsoft.Web.WebView2*' -or $_ -eq 'WebView2Loader.dll' })
$dllAltri = @($estranei | Where-Object {
    -not ($_ -like 'NINA*') -and -not ($_ -like 'Microsoft.Web.WebView2*') -and $_ -ne 'WebView2Loader.dll' })

# QUESTO SI' che `ExcludeAssets: runtime` lo governa davvero: il pacchetto NINA.Plugin
# non ha un proprio targets che forzi la copia, quindi se un DLL di N.I.N.A. comparisse
# qui vorrebbe dire che quella riga e' saltata — ed e' una regressione vera.
Verifica "la compilazione non produce DLL di N.I.N.A." ($dllNina.Count -eq 0) `
    $(if ($dllNina.Count) { $dllNina -join ', ' } else { 'ExcludeAssets: runtime tiene' })

# E nient'altro di inatteso: una dipendenza nuova che si porta dietro assembly va vista
# adesso, non il giorno in cui litiga con qualcosa dentro N.I.N.A.
Verifica "  ne altri DLL oltre a quelli di WebView2" ($dllAltri.Count -eq 0) `
    $(if ($dllAltri.Count) { $dllAltri -join ', ' } else { 'nessuno' })

Write-Host ("  --    WebView2 nella cartella di build: " + $dllWv2.Count +
            " file, e li mette il targets del pacchetto. Non arrivano a N.I.N.A.: si veda sotto.")

Write-Host "`n--- che cosa viene DISTRIBUITO ---"
# LA GARANZIA CONTRO IL CONFLITTO STA QUI, e non nella cartella di build. Cio' che
# N.I.N.A. carica e' quello che sta nella sua cartella dei plugin: se li' dentro
# comparisse una seconda copia di WebView2, quella vincerebbe sulla propria e il
# conflitto sarebbe reale. Finche' si copia il solo $(TargetPath), non puo' succedere —
# e questa riga e' cio' che se ne accorgerebbe se un giorno il passo di installazione
# diventasse «copia tutta la cartella».
$cartellaNina = Join-Path $env:LOCALAPPDATA 'NINA\Plugins\3.0.0\AstroImage.NINA.Plugin'
if (Test-Path $cartellaNina) {
    $spediti = @(Get-ChildItem $cartellaNina -Recurse -File |
        Where-Object { $_.Name -match '\.(dll|exe)$' -and $_.Name -ne 'AstroImage.NINA.Plugin.dll' } |
        ForEach-Object { $_.Name })
    Verifica "in N.I.N.A. arriva il solo DLL del plugin" ($spediti.Count -eq 0) `
        $(if ($spediti.Count) { 'ANCHE: ' + ($spediti -join ', ') } else { 'nient altro' })
} else {
    Write-Host "  --    non installato   [dotnet build -c Release lo installa]"
}

Write-Host "`n--- WebView2: si usa quella di N.I.N.A., e allora deve essere la stessa ---"
# IL ROVESCIO DELLA MEDAGLIA DI NON PORTARSELA DIETRO. Il plugin compila contro
# WebView2 e a runtime la risolve dalla cartella di N.I.N.A., che la distribuisce. Va
# bene finche' le due versioni coincidono; il giorno in cui N.I.N.A. ne distribuisse
# una piu' vecchia di quella con cui si e' compilato, il plugin morirebbe al primo uso
# del pannello con un MissingMethod, e l'utente non avrebbe sbagliato niente. E' lo
# stesso guasto per cui Target Scheduler non si carica su questa macchina, letto
# sull'altro versante: li' e' il plugin a chiedere troppo, qui sarebbe l'ospite a dare
# troppo poco. Non si controlla il .csproj ma il deps.json, cioe' cio' contro cui si e'
# compilato davvero.
# Il deps.json si rilegge qui invece di riusare quello della sezione seguente: questa
# sezione viene PRIMA, e appoggiarsi a una variabile definita dopo funzionerebbe solo
# finche' nessuno sposta un blocco.
$radiciProg = @($env:ProgramFiles, ${env:ProgramFiles(x86)}) | Where-Object { $_ -and (Test-Path $_) }
$dirNina = @(Get-ChildItem -Path $radiciProg -Directory -ErrorAction SilentlyContinue |
    Where-Object { Test-Path (Join-Path $_.FullName 'NINA.exe') } |
    ForEach-Object { $_.FullName })
$depsQui = Join-Path $uscita 'AstroImage.NINA.Plugin.deps.json'
if ((Test-Path $depsQui) -and $dirNina.Count) {
    $jQui = Get-Content $depsQui -Raw | ConvertFrom-Json
    $nostra = ($jQui.libraries.PSObject.Properties.Name |
        Where-Object { $_ -like 'Microsoft.Web.WebView2.Core/*' } |
        Select-Object -First 1) -replace '^Microsoft\.Web\.WebView2\.Core/', ''
    $core = Join-Path $dirNina[0] 'Microsoft.Web.WebView2.Core.dll'
    if ($nostra -and (Test-Path $core)) {
        $sua = (Get-Item $core).VersionInfo.FileVersion
        Verifica "la WebView2 del plugin e quella di N.I.N.A. sono la stessa" ($nostra -eq $sua) `
            "$nostra contro $sua"
    } else {
        Write-Host "  --    WebView2 non trovata in N.I.N.A.: controllo saltato"
    }
} else {
    Write-Host "  --    N.I.N.A. non installato qui, oppure manca deps.json: controllo saltato"
}

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
