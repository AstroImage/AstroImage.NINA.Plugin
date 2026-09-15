<#
    CONTROLLA CHE CIO' CHE STA PER DIVENTARE PUBBLICO NON DICA DI CHI E' LA CASA.

    Questo repository e' pubblico. Prima di un push si guardano i file cambiati rispetto al ramo remoto -
    anche quelli non ancora committati - e i messaggi dei commit non ancora pubblicati, e si cerca:
      - i nomi delle macchine di casa, letti da scripts/installa.locale.psd1, che git ignora: scriverli in
        questo script vorrebbe dire pubblicarli;
      - il nome dell'utente che lancia lo script, e i percorsi dentro la sua cartella utente;
      - i percorsi di rete (due barre rovesciate e il nome di una macchina), la cartella dei dati di profilo
        di Windows, gli indirizzi di posta;
      - gli indirizzi IPv4. Un numero a quattro parti con una parte oltre 255 non e' un indirizzo: e' una
        versione, come quelle di N.I.N.A.

    GLI ATTESI SONO NOMINATI UNO PER UNO, col motivo, e si stampano. Non passano perche' il controllo e'
    largo: passano perche' qui c'e' scritto che sono giusti. Tutto il resto che colpisce fa fallire.
      - i due indirizzi generici, quello di casa della macchina stessa e quello che vuol dire «tutte»;
      - l'indirizzo di Anthropic, ma solo sulla riga Co-Authored-By: l'attribuzione che ogni commit porta;
      - il nome dell'autore, gia' pubblico nel README;
      - la variabile d'ambiente della cartella locale dei programmi, scritta come variabile.

    QUESTO FILE NON CITA GLI ESEMPI CHE CERCA: li compone al momento, un pezzo alla volta. Altrimenti
    colpirebbe se stesso, e la sua prima uscita vera e' proprio su se stesso.

    LA CONTROPROVA E' DENTRO. Con -Prova lo script esamina un testo costruito al momento - coi nomi veri
    delle macchine e dell'utente, che quindi non stanno scritti qui - e pretende che ogni controllo colpisca e
    che ogni atteso sia riconosciuto. Un controllo che non colpisce mai non controlla niente.

    DUE TRAPPOLE DI POWERSHELL, PAGATE ALLA PRIMA PROVA. I nomi delle variabili non distinguono maiuscole e
    minuscole: una variabile locale $attesi nascondeva l'elenco $ATTESI, e nessun atteso veniva riconosciuto
    (la controprova l'ha preso). E una funzione chiamata Git che dentro chiama git richiama se stessa, perche'
    PowerShell cerca prima le funzioni e poi i programmi: girava fino a esaurirsi e diceva «niente da
    pubblicare» - un verde vuoto che la controprova non poteva vedere, perche' non passa da git. Per questo
    il giro vero adesso pretende che git risponda, e se non risponde fallisce.

    Uso:  powershell -File scripts/controlla-pubblicabile.ps1          i cambiamenti non ancora pubblicati
          powershell -File scripts/controlla-pubblicabile.ps1 -Prova   la controprova
#>
param([switch]$Prova)

# Continue e non Stop: in PowerShell 5.1 lo stderr di git rediretto diventerebbe un'eccezione.
$ErrorActionPreference = 'Continue'
$radice = Split-Path -Parent $PSScriptRoot

$ko = 0
function Riga([string]$esito, [string]$testo) {
    $colore = 'Gray'
    if ($esito -eq 'FALLITO') { $colore = 'Red'; $script:ko++ }
    elseif ($esito -eq 'attento') { $colore = 'Yellow' }
    Write-Host ("  {0,-8} {1}" -f $esito, $testo) -ForegroundColor $colore
}

# I nomi che non si possono scrivere qui: dalla configurazione locale e dall'ambiente.
$nomi = @()
$conf = Join-Path $PSScriptRoot 'installa.locale.psd1'
if (Test-Path $conf) {
    $c = Import-PowerShellDataFile $conf
    foreach ($v in @($c.Campo, $c.Motore)) {
        if (-not $v) { continue }
        $h = $null
        try { $h = ([uri]$v).Host } catch { }
        if ($h) { $nomi += $h; $nomi += ($h -split '\.')[0] }
    }
}
$nomi = @($nomi | Where-Object { $_ -and $_.Length -ge 3 } | Sort-Object -Unique)
$utente = $env:USERNAME

$APP = 'App' + 'Data'
$ELENCO_ATTESI = @(
    @{ Re = '(?<=Co-Authored-By:[^<\r\n]*<)noreply@anthropic\.com(?=>)'; Perche = "l'attribuzione che ogni commit porta" },
    @{ Re = '(?<![\d.])127\.0\.0\.1(?![\d.])'; Perche = 'indirizzo generico, non di una macchina' },
    @{ Re = '(?<![\d.])0\.0\.0\.0(?![\d.])'; Perche = 'indirizzo generico, non di una macchina' },
    @{ Re = '(?<=Version=)\d+\.\d+\.\d+\.\d+(?![\d.])'; Perche = "la versione di un assembly .NET, non un indirizzo (le intestazioni dei resx: Version=4.0.0.0)" },
    @{ Re = 'Alessandro Curci'; Perche = "il credito d'autore, gia' pubblico" },
    @{ Re = '%LOCAL' + $APP.ToUpper() + '%|\$env:LOCAL' + $APP.ToUpper(); Perche = "la variabile d'ambiente, non il percorso di qualcuno" }
)

$ELENCO_CONTROLLI = @()
foreach ($n in $nomi) {
    $ELENCO_CONTROLLI += @{ Nome = 'nome di macchina'; Re = '(?i)(?<![A-Za-z0-9])' + [regex]::Escape($n) + '(?![A-Za-z0-9])' }
}
if ($utente -and $utente.Length -ge 3) {
    $ELENCO_CONTROLLI += @{ Nome = "nome dell'utente"; Re = '(?i)(?<![A-Za-z0-9])' + [regex]::Escape($utente) }
}
$ELENCO_CONTROLLI += @{ Nome = 'percorso utente'; Re = '(?i)[A-Za-z]:[\\/]+Us' + 'ers[\\/]+(?!<)|/ho' + 'me/(?!<)|/Us' + 'ers/(?!<)' }
$ELENCO_CONTROLLI += @{ Nome = 'percorso di rete'; Re = '\\\\[A-Za-z0-9]' }
$ELENCO_CONTROLLI += @{ Nome = 'cartella di profilo'; Re = '(?i)' + $APP }
$ELENCO_CONTROLLI += @{ Nome = 'posta'; Re = '[A-Za-z0-9._-]+@[A-Za-z0-9-]+\.[A-Za-z]{2,}' }

# Una riga: gli attesi si riconoscono e si tolgono, e sul resto girano i controlli.
function Esamina([string]$testo) {
    $colpiDellaRiga = @(); $attesiDellaRiga = @()
    $resto = $testo
    foreach ($a in $ELENCO_ATTESI) {
        if ($resto -match $a.Re) { $attesiDellaRiga += $a.Perche; $resto = [regex]::Replace($resto, $a.Re, '') }
    }
    foreach ($k in $ELENCO_CONTROLLI) { if ($resto -match $k.Re) { $colpiDellaRiga += $k.Nome } }
    foreach ($m in [regex]::Matches($resto, '(?<![\d.])(?:\d{1,3}\.){3}\d{1,3}(?![\d.])')) {
        $parti = $m.Value -split '\.'
        if (-not ($parti | Where-Object { [int]$_ -gt 255 })) { $colpiDellaRiga += 'indirizzo IPv4' }
    }
    return @{ Colpi = @($colpiDellaRiga | Sort-Object -Unique); Attesi = @($attesiDellaRiga | Sort-Object -Unique) }
}

if (-not $nomi.Count) { Riga 'attento' 'nomi delle macchine non noti (manca scripts\installa.locale.psd1): il controllo dei nomi e parziale' }

if ($Prova) {
    Write-Host "`n--- la controprova ---"
    $macchina = 'pc-di-prova'
    if ($nomi.Count) { $macchina = $nomi[0] }
    $b = [char]92
    $campione = @(
        ('C' + ':' + $b + 'Us' + 'ers' + $b + $utente + $b + 'Documents'),
        ("$b$b" + $macchina + $b + 'cartella'),
        ('http://192.168.' + '0.99:1888/'),
        ('scrivimi a nome.cognome' + '@' + 'example.com'),
        ('/ho' + 'me/qualcuno/prova'),
        ('sta in ' + $APP),
        ('Co-Authored-By: Claude Opus 5 <noreply' + '@' + 'anthropic.com>'),
        ('ascolta su 0.0.0.0 e su 127.0.0.1, versione 3.2.0.9001'),
        'Autore: Alessandro Curci',
        ('%LOCAL' + $APP.ToUpper() + '%' + $b + 'NINA')
    )
    foreach ($n in $nomi) { $campione += ('la macchina ' + $n + ' in rete') }
    $visti = @(); $riconosciuti = @(); $pulite = 0
    foreach ($r in $campione) {
        $e = Esamina $r
        $visti += $e.Colpi; $riconosciuti += $e.Attesi
        if (-not $e.Colpi.Count) { $pulite++ }
    }
    $visti = @($visti | Sort-Object -Unique)
    $tutti = @(@($ELENCO_CONTROLLI | ForEach-Object { $_.Nome }) + 'indirizzo IPv4' | Sort-Object -Unique)
    $mancati = @($tutti | Where-Object { $visti -notcontains $_ })
    if ($mancati.Count) { Riga 'FALLITO' ('controlli che non colpiscono mai: ' + ($mancati -join ', ')) }
    else { Riga 'ok' ('ogni controllo colpisce: ' + ($visti -join ', ')) }
    $nRiconosciuti = @($riconosciuti | Sort-Object -Unique).Count
    if ($nRiconosciuti -ne 4) { Riga 'FALLITO' ("attesi riconosciuti: $nRiconosciuti motivi su 4") }
    else { Riga 'ok' 'ogni atteso e riconosciuto col suo motivo, e non colpisce' }
    if ($pulite -ne 4) { Riga 'FALLITO' ("righe senza colpi: $pulite, attese 4 (attribuzione, indirizzi generici con una versione, autore, variabile)") }
    else { Riga 'ok' 'le righe fatte solo di attesi, o di una versione, restano pulite' }
    Write-Host ""
    if ($ko) { exit 1 }
    exit 0
}

function ChiediAGit([string[]]$argomenti) { & git.exe -C $radice @argomenti 2>$null }
if (-not (ChiediAGit @('rev-parse', 'HEAD'))) {
    Riga 'FALLITO' "git non risponde in $radice : non e' stato controllato niente"
    Write-Host ""
    exit 1
}
$monte = ChiediAGit @('rev-parse', '--abbrev-ref', '@{u}')
if (-not $monte) { $monte = 'origin/master' }
Write-Host "`n--- che cosa diventerebbe pubblico, rispetto a $monte ---"
$file = @(ChiediAGit @('diff', '--name-only', "$monte...HEAD")) + @(ChiediAGit @('diff', '--name-only', 'HEAD')) + @(ChiediAGit @('ls-files', '--others', '--exclude-standard'))
$file = @($file | Where-Object { $_ } | Sort-Object -Unique)
$messaggi = @(ChiediAGit @('log', "$monte..HEAD", '--format=%B'))

$sorgenti = @()
foreach ($f in $file) {
    $p = Join-Path $radice $f
    if (-not (Test-Path -LiteralPath $p)) { continue }
    $byte = [System.IO.File]::ReadAllBytes($p)
    if ($byte -contains 0) { Riga '--' "$f binario, non si legge"; continue }
    $sorgenti += @{ Nome = $f; Righe = ([System.Text.Encoding]::UTF8.GetString($byte) -split "`r?`n") }
}
if ($messaggi.Count) { $sorgenti += @{ Nome = 'messaggi dei commit non pubblicati'; Righe = $messaggi } }
if (-not $sorgenti.Count) { Riga '--' 'niente da pubblicare: nessun file cambiato e nessun commit non pubblicato'; Write-Host ""; exit 0 }

foreach ($s in $sorgenti) {
    $colpite = @(); $attese = @()
    for ($i = 0; $i -lt $s.Righe.Count; $i++) {
        $e = Esamina ([string]$s.Righe[$i])
        if ($e.Colpi.Count) { $colpite += ('riga {0}: {1}' -f ($i + 1), ($e.Colpi -join ', ')) }
        foreach ($a in $e.Attesi) { $attese += ('riga {0}: {1}' -f ($i + 1), $a) }
    }
    if ($colpite.Count) { Riga 'FALLITO' ($s.Nome + ' - ' + ($colpite -join ' / ')) }
    else { Riga 'ok' $s.Nome }
    foreach ($a in $attese) { Riga '--' ('   atteso, ' + $a) }
}
Write-Host ""
if ($ko) { Write-Host "$ko file con qualcosa che non si pubblica.`n" -ForegroundColor Red; exit 1 }
Write-Host "Niente che dica di chi e' la casa.`n"
