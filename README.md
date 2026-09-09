# AstroImage Strategy Bridge

Un plugin per [N.I.N.A.](https://nighttime-imaging.eu/) che porta dentro il
Sequenziatore Avanzato la prescrizione decisa da **AstroImage-Strategy**.

> **Stato: il giro si chiude, e regge sul campo.** Il pannello chiede una prescrizione
> al servizio di Strategy, la mostra, e su richiesta costruisce il bersaglio nel
> Sequenziatore Avanzato. Provato su una montatura vera con filtri veri: nebulosa a
> banda stretta, planetaria e ammasso globulare, su N.I.N.A. 3.3 con un plugin
> compilato per la 3.2.
>
> Manca la pagina delle Opzioni vera — la configurazione dei filtri vive per ora nella
> pagina di prova dentro il pannello — e manca la traduzione da equipaggiamento di
> N.I.N.A. a banco del motore, che oggi si sceglie a mano.

## Ti serve anche il motore

Questo plugin **da solo non fa niente**: è un ponte, e dall'altra parte deve esserci
AstroImage-Strategy che risponde su una porta HTTP. Senza, il pannello si apre e dice
che il motore non risponde.

È voluto, ed è la forma che il regolamento del catalogo di N.I.N.A. descrive: un plugin
aperto che fa da mediatore verso un servizio esterno. Il ponte è il tubo; quello che ci
passa dentro è un'altra cosa.

## L'idea

Il progetto nasce da una convinzione precisa: **la pianificazione di una ripresa non è
una tabella di consigli, è una conseguenza.** Conseguenza della fisica del soggetto, del
sensore che hai, del vetro che hai davanti e del cielo di quella notte in quel posto. Se
si parte da lì, la domanda «quante ore di Ha su questo oggetto» ha una risposta che si
può derivare e discutere — non una regola del pollice da ripetere.

Da questa premessa discendono tre regole che si ritrovano in ogni file di entrambi i
repository, e che non sono scelte di programmazione:

**Ogni numero porta la sua provenienza.** Nel motore un valore misurato non si confonde
con uno derivato né con una stima: `fonte`, `confidenza`, `derivazione` e dominio di
validità viaggiano accanto al dato. Dove la misura non c'è, si dichiara che non c'è —
non si inventa un coefficiente plausibile.

**Non si deduce, si dichiara.** Dal nome di un filtro non escono nanometri: «HA» può
stare davanti a un L-Ultimate, e solo chi l'ha comprato sa che cos'è. Per questo il
ponte fa dichiarare i vetri invece di indovinarli, e non fa mai analisi dei nomi.

**Si rifiuta, non si sostituisce in silenzio.** È la regola che decide di più:
*una sequenza che non rispetta la prescrizione, guardandola, non si distingue da una che
la rispetta.* Fra saltare un blocco e rifiutare tutto vince l'asimmetria del danno — un
rifiuto si corregge in un clic, cinque ore riprese col vetro sbagliato non si
recuperano. Da qui vengono tutti i rifiuti di questo codice, e anche la scelta di quali
NON meritano un rifiuto.

## Chi l'ha fatto, e come

**Autore e manutentore: Alessandro Curci.** Sono sue l'idea sopra e le regole che ne
discendono, l'architettura, le decisioni di progetto e la verifica sul campo; ed è suo
il motore — il modello fisico, il catalogo curato, la disciplina della provenienza —
cioè tutto ciò che rende utile questo ponte.

La stesura del codice è stata fatta **in larga parte da un assistente di intelligenza
artificiale** (Claude, di Anthropic), su sua direzione e sotto la sua revisione. Ogni
commit lo dichiara con un trailer `Co-Authored-By`.

La divisione del lavoro è stata questa, ed è verificabile nella storia del repository:
dove passa il confine col motore, che cosa fa rifiutare una consegna e che cosa no, se
il dither sia una prescrizione o un'impostazione, come si dichiara un filtro — sono
decisioni prese dal manutentore, in più di un caso **contro** la prima proposta
dell'assistente. E i difetti che contavano — il filtro sostituito in silenzio, il dither
ereditato dal modello, il numero di pose che N.I.N.A. 3.3 non lasciava scrivere — li ha
trovati il suo banco con montatura e filtri veri, non il laboratorio.

La dichiarazione non è una formalità: il catalogo dei plugin di N.I.N.A. chiede che
l'uso sostanziale dell'IA sia dichiarato e che ogni plugin abbia un manutentore umano
responsabile, capace di spiegare e mantenere il codice. È anche la ragione per cui i
commenti qui dentro sono così fitti: ogni scelta non ovvia porta scritto accanto il
perché, spesso con il difetto che l'ha causata — perché il codice va difeso da chi lo
mantiene, non da chi l'ha battuto.

## Che cosa fa, e che cosa non fa

AstroImage-Strategy decide **che cosa riprendere**: quali canali hanno senso su quel
soggetto con quel sensore e quel vetro, quante ore a ciascuno, quale posa, quale modo di
guadagno. Lo decide dalla fisica — brillanza del soggetto, fondo cielo, rumore di
lettura, pozzetto, saturazione, campionamento.

Questo ponte **non decide niente**. Riceve una prescrizione già presa e la costruisce
nel Sequenziatore Avanzato di N.I.N.A.

Non contiene, e non conterrà: fotometria, modello del cielo, modello dei filtri, scelta
dei canali, allocazione delle ore, strategie di posa, pianificazione delle notti,
cataloghi. Quella roba sta nel motore, che è un altro programma.

C'è un modo semplice di verificare che il confine sia nel posto giusto: chi legge questo
sorgente impara **come si parla a N.I.N.A.**, che è già pubblico, e **nulla** su come si
decide che cosa riprendere.

## Il contratto

Fra il motore e il ponte passa una cosa sola: un **modello di sequenza**, circa un
chilobyte di JSON che non nomina N.I.N.A. da nessuna parte.

```json
{
  "notte": 1,
  "quando":    { "data": "2026-09-01", "inizio": "2026-09-01T19:45:00.000Z",
                 "fine": "2026-09-02T01:55:00.000Z", "oreUtili": 5.317 },
  "bersaglio": { "nome": "IC 1318", "rot": 245, "ra_deg": 305.55, "dec_deg": 40.25 },
  "ottica":    { "camera": "…", "focale_mm": 367, "pixel_um": 3.76, "bin": 1, "matrice": false },
  "sito":      { "lat": 45.9, "lon": 10.2 },
  "cap":       { "raffredda": true, "ruota": true, "guida": true, "rotatore": false },
  "blocchi":   [ { "canali": ["OIII"], "filtro": "O", "sec": 600, "n": 22,
                   "gain": 100, "offset": 50 } ],
  "dither":    { "ogniPose": 2 }
}
```

Il motore lo produce già, ed è verificato che sopravvive intatto a un giro di JSON —
anzi che torna indietro identico carattere per carattere: è quello che gli permette di
attraversare un confine (un file, gli appunti, una richiesta locale) senza portarsi
dietro mezzo programma.

`quando` merita qualche riga. `notte: 1` da solo è un indice dentro un piano che sta
dall'altra parte del confine, e chi riceve il modello quel piano non ce l'ha. `data` è la
sera in forma civile; `inizio` e `fine` sono gli estremi dell'arco utile, in UTC. Senza
di loro il ponte, per sapere quando cominciare, dovrebbe rifare crepuscoli e altezze:
cioè rifare l'astronomia che sta dall'altra parte.

**`oreUtili` è il campo che impedisce di leggere male gli altri due.** L'arco fra
`inizio` e `fine` è un *inviluppo*, non una finestra piena: è il primo e l'ultimo
campione sopra la soglia di altezza, e in mezzo ci può essere un tratto in cui il
soggetto è sotto il pavimento. Su IC 1396 da Roma il 31 gennaio l'arco copre dieci ore e
cinquantacinque mentre le ore vere sono 1,67: il soggetto tramonta e risorge. Chi
trattasse l'arco come una finestra di ripresa comanderebbe nove ore di pose con l'oggetto
troppo basso. `oreUtili` sono le ore che il piano assegna davvero a quella notte,
overhead già tolto: se è molto minore dell'arco, l'arco non è pieno.

## Compilare

Serve l'SDK .NET 8 e nient'altro: il pacchetto NuGet `NINA.Plugin` porta i riferimenti
necessari, e non occorre compilare N.I.N.A.

I comandi vanno dati **dalla radice di questo repository**, quella che contiene il file
`.sln`. Altrove `dotnet` non trova nulla da compilare e risponde `MSB1003`.

```
cd C:\Users\<nome>\Documents\AstroImage.NINA.Plugin
dotnet build -c Release
```

Il DLL esce sempre in `src/AstroImage.NINA.Plugin/bin/x64/Release` — uno solo, una
decina di KB — **e viene installato subito** in
`%LOCALAPPDATA%\NINA\Plugins\3.0.0\AstroImage.NINA.Plugin`,
così quello che si prova è sempre l'ultimo compilato. N.I.N.A. legge quella cartella
all'avvio: se è già aperto, il plugin compare al riavvio successivo. Per toglierlo si
cancella la cartella.

Per compilare senza installare:

```
dotnet build -c Release -p:DeployToNina=false
```

Per controllare che sia un plugin e non solo un DLL che compila:

```
powershell -File scripts/verifica-scheletro.ps1
```

Per eseguire i test del contratto:

```
dotnet test -c Release
```

I due controlli guardano cose diverse. Lo script verifica che N.I.N.A. riconoscerebbe
questo DLL come plugin; i test verificano che il modello di sequenza sia il gemello
fedele del contratto che il motore produce, usando come fixture JSON usciti dal motore
vero. I test **non installano mai niente**, nemmeno lanciati in Release: installare è un atto
della compilazione, e collaudare non lo è. Senza quella guardia `dotnet test -c Release`
metteva il plugin in N.I.N.A. *prima* di sapere se i test passavano.

## Requisiti

| | |
|---|---|
| N.I.N.A. | 3.2.0.9001 o successiva, comprese le nightly 3.3 |
| Piattaforma | Windows x64 |
| Framework | .NET 8 (WPF) |

## Licenza

MPL-2.0 — vedi [LICENSE](LICENSE). È la stessa licenza di N.I.N.A.: chi modifica il ponte condivide le modifiche, ma può combinarlo con codice proprietario.

Il catalogo ufficiale dei plugin di N.I.N.A. non accetta plugin a sorgente chiuso, e fa
bene. Che questo ponte sia aperto non è in contraddizione con un motore commerciale: il
regolamento del catalogo prevede esattamente il caso di un plugin che «fa da mediatore»
verso un'applicazione chiusa o un servizio a pagamento. Il ponte è il tubo; il valore sta
in quello che ci passa dentro.
