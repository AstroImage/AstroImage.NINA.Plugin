<#
    INSTALLA IL PLUGIN SULLE DUE MACCHINE, e dice quale binario ci e' arrivato.

    La prova a schermo non vale se non si sa quale binario sta girando. Un DLL vecchio
    nella cartella dei plugin non da' nessun errore: da' un pannello che si comporta come
    la versione di tre giorni fa. Questo script compila, copia il DLL nella cartella dei
    plugin di N.I.N.A. di questa macchina e in quella del PC in campo, scrive accanto al
    DLL del PC in campo l'indirizzo del motore, e alla fine confronta l'impronta SHA-256
    di ogni copia con quella appena compilata.

    PRIMA DI COPIARE CONTROLLA TUTTO, e se un controllo cade non copia niente, da nessuna
    parte: una macchina aggiornata e l'altra no e' proprio il caso da evitare.
      - N.I.N.A. chiuso. Qui si guarda il processo. Sul PC in campo il processo non si
        vede: si guarda che l'Advanced API non risponda, che e' un indizio, e che il DLL
        non sia bloccato, che e' la prova — N.I.N.A. aperto tiene il file, ed e' lo stesso
        blocco per cui la copia della compilazione esce con un avviso (vedi il .csproj).
      - Un plugin solo. Se il DLL sta in piu' di una cartella sotto 3.0.0, N.I.N.A. ne
        caricherebbe due: si ferma e le nomina.
      - Nessun altro DLL accanto al plugin. Una seconda copia di WebView2 li' dentro
        vincerebbe su quella di N.I.N.A. (vedi verifica-scheletro.ps1). Lo script li
        elenca e NON li cancella: la cartella e' di chi usa la macchina.

    I NOMI DELLE MACCHINE NON STANNO QUI, perche' il repository e' pubblico: stanno in
    scripts/installa.locale.psd1, che git ignora. Il modello e' installa.esempio.psd1.

    Uso:  powershell -File scripts/installa.ps1                  controlla, compila, installa
          powershell -File scripts/installa.ps1 -SoloControlli   controlla e basta
          powershell -File scripts/installa.ps1 -Dove locale     oppure -Dove campo
#>
param(
    [ValidateSet('tutte', 'locale', 'campo')] [string]$Dove = 'tutte',
    [string]$Campo,
    [string]$Motore,
    [switch]$SoloControlli
)

$ErrorActionPreference = 'Stop'
$radice = Split-Path -Parent $PSScriptRoot
$nome   = 'AstroImage.NINA.Plugin'
$dll    = Join-Path $radice "src\$nome\bin\x64\Release\$nome.dll"
<#  LA CARTELLA E' QUELLA CHE USEREBBE N.I.N.A. (17 settembre 2026).
    Installando il plugin da un repository, N.I.N.A. lo mette in Plugins\3.0.0\<Nome del manifest>, che e'
    l'AssemblyTitle. Finche' lo mettevamo noi in una cartella col nome dell'assembly la differenza non si vedeva; il
    giorno che il plugin si installa anche dall'elenco di N.I.N.A., le due cartelle conterrebbero lo stesso GUID e
    N.I.N.A. caricherebbe due volte lo stesso plugin. Quella vecchia si sposta e si toglie — ma solo se dentro c'e'
    soltanto roba nostra: quello che non abbiamo messo noi non lo togliamo. #>
$cartellaNina = 'AstroImage Strategy Bridge'

$ko = 0
function Riga([string]$esito, [string]$testo) {
    $colore = 'Gray'
    if ($esito -eq 'FALLITO') { $colore = 'Red'; $script:ko++ }
    elseif ($esito -eq 'attento') { $colore = 'Yellow' }
    Write-Host ("  {0,-8} {1}" -f $esito, $testo) -ForegroundColor $colore
}

$conf = Join-Path $PSScriptRoot 'installa.locale.psd1'
if (Test-Path $conf) {
    $c = Import-PowerShellDataFile $conf
    if (-not $Campo -and $c.Campo) { $Campo = $c.Campo }
    if (-not $Motore -and $c.Motore) { $Motore = $c.Motore }
}
$vuoiLocale = $Dove -ne 'campo'
$vuoiCampo  = $Dove -ne 'locale'
if ($vuoiCampo -and (-not $Campo -or -not $Motore)) {
    Write-Host "`nMancano la cartella del PC in campo o l'indirizzo del motore: scrivili in scripts\installa.locale.psd1 (il modello e' installa.esempio.psd1), oppure passa -Campo e -Motore.`n" -ForegroundColor Red
    exit 1
}

function CartelleDelPlugin([string]$radicePlugin) {
    @(Get-ChildItem -LiteralPath $radicePlugin -Recurse -File -Filter "$nome.dll" -ErrorAction SilentlyContinue |
        ForEach-Object { $_.DirectoryName } | Sort-Object -Unique)
}

function Bloccato([string]$file) {
    if (-not (Test-Path -LiteralPath $file)) { return $false }
    try { $h = [System.IO.File]::Open($file, 'Open', 'ReadWrite', 'None'); $h.Close(); return $false }
    catch { return $true }
}

# La cartella in cui va il plugin sotto una radice 3.0.0: quella che lo contiene gia', se
# e' una sola. Controlla anche che accanto non ci siano altri DLL e che il file sia libero.
function Destinazione([string]$chi, [string]$radicePlugin, [ref]$vecchia) {
    # @() anche qui, e non e' ridondante: una funzione che restituisce un elenco di UN
    # elemento lo srotola in una stringa, e $trovate[0] diventa la prima lettera del
    # percorso. Alla prima prova il controllo dei DLL estranei guardava cosi' la cartella
    # «C» e «\», e diceva verde sul PC in campo che ne ha tre.
    $trovate = @(CartelleDelPlugin $radicePlugin)
    $cartella = Join-Path $radicePlugin $cartellaNina
    $altrove  = @($trovate | Where-Object { $_ -ne $cartella })
    if ($altrove.Count -gt 1) {
        Riga 'FALLITO' ("il plugin sta in {0} cartelle, N.I.N.A. ne caricherebbe piu' di uno: {1}" -f $trovate.Count, ($trovate -join ' ; '))
        return $null
    }
    if ($altrove.Count -eq 1) {
        $vecchia.Value = $altrove[0]
        Riga 'attento' "il plugin sta ancora in $($altrove[0]): lo script lo porta in $cartella e toglie la vecchia cartella"
        if (Bloccato (Join-Path $altrove[0] "$nome.dll")) { Riga 'FALLITO' "il DLL nella cartella vecchia e' bloccato: N.I.N.A. e' aperto, chiudilo" }
    }
    if ($trovate -contains $cartella) { Riga 'ok' "un plugin solo, in $cartella" }
    elseif ($altrove.Count -eq 0) { Riga '--' "mai installato qui: va in $cartella" }

    $estranei = @(Get-ChildItem -LiteralPath $cartella -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '\.(dll|exe)$' -and $_.Name -ne "$nome.dll" } | ForEach-Object { $_.Name })
    if ($estranei.Count) {
        Riga 'FALLITO' ("accanto al plugin ci sono altri DLL, che N.I.N.A. caricherebbe al posto dei suoi: {0}. Toglili a mano e rilancia." -f ($estranei -join ', '))
    } else { Riga 'ok' 'accanto al plugin nessun altro DLL' }

    $altri = @(Get-ChildItem -LiteralPath $cartella -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -notmatch '\.(dll|exe)$' -and $_.Name -ne 'strategy.url' } | ForEach-Object { $_.Name })
    if ($altri.Count) { Riga 'attento' ("anche questi file, che lo script lascia dove sono: {0}" -f ($altri -join ', ')) }

    if (Bloccato (Join-Path $cartella "$nome.dll")) { Riga 'FALLITO' "il DLL e' bloccato: N.I.N.A. e' aperto, chiudilo" }
    else { Riga 'ok' "il DLL non e' bloccato da nessuno" }
    return $cartella
}

Write-Host "`n--- quale sorgente ---"
$commit = (& git -C $radice rev-parse --short HEAD | Out-String).Trim()
$ramo   = (& git -C $radice rev-parse --abbrev-ref HEAD | Out-String).Trim()
$sporchi = @(& git -C $radice status --porcelain --untracked-files=no)
if ($sporchi.Count) { Riga 'attento' "$ramo @ $commit con $($sporchi.Count) file modificati e non committati: il binario non corrisponde a nessun commit" }
else { Riga 'ok' "$ramo @ $commit, nessuna modifica in sospeso" }

Write-Host "`n--- compilazione ---"
if ($SoloControlli) {
    Riga '--' "non si compila: si guarda il DLL che c'e' gia'"
} else {
    Push-Location $radice
    try { & dotnet build -c Release -p:DeployToNina=false -nologo -v:q } finally { Pop-Location }
    if ($LASTEXITCODE -ne 0) { Write-Host "`nLa compilazione e' fallita: non si installa niente.`n" -ForegroundColor Red; exit 1 }
    Riga 'ok' "compilato senza installare: installa questo script, che poi verifica"
}
if (-not (Test-Path $dll)) { Write-Host "`nManca $dll`n" -ForegroundColor Red; exit 1 }
$impronta = (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash
Riga '--' ("DLL compilato: {0:N0} byte, {1:yyyy-MM-dd HH:mm}" -f (Get-Item $dll).Length, (Get-Item $dll).LastWriteTime)

$destinazioni = @()

if ($vuoiLocale) {
    Write-Host "`n--- questa macchina ---"
    if ($null -ne (Get-Process NINA -ErrorAction SilentlyContinue)) { Riga 'FALLITO' "N.I.N.A. e' aperto: chiudilo" }
    else { Riga 'ok' "N.I.N.A. chiuso" }
    $r = Join-Path $env:LOCALAPPDATA 'NINA\Plugins\3.0.0'
    $vecchiaLocale = $null
    $cartella = Destinazione 'questa macchina' $r ([ref]$vecchiaLocale)
    # Qui il motore e' in casa: un strategy.url dimenticato manderebbe il pannello altrove.
    if ($cartella -and (Test-Path -LiteralPath (Join-Path $cartella 'strategy.url'))) {
        Riga 'attento' ("c'e' uno strategy.url anche qui, e dice " + (Get-Content -LiteralPath (Join-Path $cartella 'strategy.url') -Raw))
    }
    if ($cartella) { $destinazioni += @{ Nome = 'questa macchina'; Cartella = $cartella; Url = $null; Vecchia = $vecchiaLocale } }
}

if ($vuoiCampo) {
    Write-Host "`n--- il PC in campo ---"
    if (-not (Test-Path -LiteralPath $Campo)) {
        Riga 'FALLITO' "la cartella $Campo non si raggiunge"
    } else {
        $macchina = ([uri]$Campo).Host
        $api = $false
        try { $null = Invoke-RestMethod -Uri "http://${macchina}:1888/v2/api/version" -TimeoutSec 4; $api = $true } catch { }
        if ($api) { Riga 'FALLITO' "l'Advanced API risponde su ${macchina}:1888: N.I.N.A. e' aperto, chiudilo" }
        else { Riga 'ok' "l'Advanced API non risponde su ${macchina}:1888 (un indizio; la prova e' il file)" }
        $vecchiaCampo = $null
        $cartella = Destinazione 'PC in campo' $Campo ([ref]$vecchiaCampo)

        $url = $Motore.Trim()
        if (-not $url.EndsWith('/')) { $url += '/' }
        $u = $null
        if (-not [uri]::TryCreate($url, 'Absolute', [ref]$u) -or ($u.Scheme -ne 'http' -and $u.Scheme -ne 'https')) {
            Riga 'FALLITO' "l'indirizzo del motore $url non e' un indirizzo http"
        } else {
            if ($u.HostNameType -eq 'IPv4' -or $u.HostNameType -eq 'IPv6') {
                Riga 'attento' "l'indirizzo del motore e' un numero: il router lo puo' cambiare, meglio il nome della macchina"
            } else { Riga 'ok' "il motore, visto dal PC in campo: $url" }
            try {
                $s = Invoke-WebRequest -Uri ($url + 'v1/salute') -UseBasicParsing -TimeoutSec 4
                Riga 'ok' "da qui il motore risponde a quell'indirizzo (HTTP $($s.StatusCode))"
            } catch { Riga 'attento' "da qui il motore non risponde a quell'indirizzo: va acceso prima della prova" }
        }
        if ($cartella) { $destinazioni += @{ Nome = 'PC in campo'; Cartella = $cartella; Url = $url; Vecchia = $vecchiaCampo } }
    }
}

Write-Host ""
if ($ko -gt 0) { Write-Host "$ko controlli caduti: non si copia niente, da nessuna parte.`n" -ForegroundColor Red; exit 1 }
if ($SoloControlli) { Write-Host "Controlli superati. Con -SoloControlli non si copia.`n"; exit 0 }

Write-Host "--- copia ---"
foreach ($d in $destinazioni) {
    try {
        New-Item -ItemType Directory -Force -Path $d.Cartella | Out-Null
        Copy-Item -LiteralPath $dll -Destination (Join-Path $d.Cartella "$nome.dll") -Force
        if ($d.Url) {
            [System.IO.File]::WriteAllText((Join-Path $d.Cartella 'strategy.url'), $d.Url, (New-Object System.Text.UTF8Encoding $false))
        }
        Riga 'ok' "$($d.Nome): copiato"
        # LA CARTELLA VECCHIA: si porta via strategy.url se qui non ce n'e' uno, si toglie il nostro DLL, e la cartella
        # si cancella solo se resta vuota. Quello che non abbiamo messo noi resta dov'e', e lo script lo dice.
        if ($d.Vecchia) {
            $vecchioUrl = Join-Path $d.Vecchia 'strategy.url'
            $nuovoUrl   = Join-Path $d.Cartella 'strategy.url'
            if ((Test-Path -LiteralPath $vecchioUrl) -and -not (Test-Path -LiteralPath $nuovoUrl)) {
                Move-Item -LiteralPath $vecchioUrl -Destination $nuovoUrl
                Riga 'ok' "$($d.Nome): strategy.url portato nella cartella nuova"
            } elseif (Test-Path -LiteralPath $vecchioUrl) {
                # Nella cartella nuova ce n'e' gia' uno — l'ha appena scritto questo script: il vecchio e' nostro e va via,
                # altrimenti resterebbe li' una cartella col solo indirizzo dentro, che il prossimo che guarda non capisce.
                Remove-Item -LiteralPath $vecchioUrl -Force
                Riga 'ok' "$($d.Nome): tolto lo strategy.url della cartella vecchia, quello nuovo c'e' gia'"
            }
            Remove-Item -LiteralPath (Join-Path $d.Vecchia "$nome.dll") -Force -ErrorAction SilentlyContinue
            $rimasti = @(Get-ChildItem -LiteralPath $d.Vecchia -Force -ErrorAction SilentlyContinue)
            if ($rimasti.Count -eq 0) {
                Remove-Item -LiteralPath $d.Vecchia -Force -Recurse
                Riga 'ok' "$($d.Nome): tolta la cartella vecchia $($d.Vecchia)"
            } else {
                Riga 'attento' ("$($d.Nome): la cartella vecchia $($d.Vecchia) non si tocca, dentro c'e' dell'altro: {0}" -f (($rimasti | ForEach-Object { $_.Name }) -join ', '))
            }
        }
    } catch { Riga 'FALLITO' "$($d.Nome): $($_.Exception.Message)" }
}

Write-Host "`n--- quale binario c'e' adesso ---"
Write-Host "  compilato  $impronta   $ramo @ $commit"
foreach ($d in $destinazioni) {
    $f = Join-Path $d.Cartella "$nome.dll"
    $h = 'assente'
    if (Test-Path -LiteralPath $f) { $h = (Get-FileHash -LiteralPath $f -Algorithm SHA256).Hash }
    if ($h -eq $impronta) { Riga 'ok' "$h   $($d.Nome), identico" }
    else { Riga 'FALLITO' "$h   $($d.Nome), DIVERSO" }
    if ($d.Url) {
        $file = Join-Path $d.Cartella 'strategy.url'
        $letto = ''
        if (Test-Path -LiteralPath $file) { $letto = ([string](Get-Content -LiteralPath $file -Raw)).Trim() }
        if ($letto -eq $d.Url) { Riga 'ok' "strategy.url dice $letto" }
        else { Riga 'FALLITO' "strategy.url dice '$letto', atteso $($d.Url)" }
    }
}

Write-Host ""
if ($ko -gt 0) { Write-Host "$ko verifiche cadute.`n" -ForegroundColor Red; exit 1 }
Write-Host "Installato e verificato. N.I.N.A. lo carica al prossimo avvio.`n"
