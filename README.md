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

Il progetto nasce da una convinzione precisa: **pianificare una sessione non è
consultare una tabella di consigli, è ricavare una conseguenza.** Conseguenza della
fisica del soggetto, del sensore utilizzato, del filtro impiegato e del cielo di quella
notte in quel luogo. Partendo da lì, «quante ore di Ha su questo oggetto» smette di
essere una regola empirica e diventa un numero che si può ricavare, mostrare e
contestare.

Da questa premessa discendono tre regole che si ritrovano in ogni file dei due
repository, e che non sono scelte di programmazione:

**Ogni numero si porta dietro da dove viene.** Un valore misurato non si confonde con
uno ricavato né con una stima: la fonte, quanto è attendibile, come è stato ottenuto ed
entro quali limiti vale viaggiano insieme al dato. Dove la misura non esiste, si dice
che non esiste — non le si sostituisce un coefficiente verosimile.

**Non si deduce, si dichiara.** Dal nome di un filtro non escono nanometri: uno slot
scritto «HA» può avere montato un L-Ultimate, e lo sa solo chi l'ha comprato. Per questo
il ponte fa dichiarare i filtri invece di indovinarli, e non prova mai a interpretarne
il nome.

**Si rifiuta, non si corregge in silenzio.** È la regola che pesa di più: *una sequenza
che non rispetta la prescrizione, a guardarla, è identica a una che la rispetta.* Fra
saltare un blocco e rifiutare tutto decide l'asimmetria del danno — un rifiuto lo
correggi in un clic, cinque ore riprese con il filtro sbagliato non le recuperi. Da qui
viene ogni rifiuto di questo codice, e anche la scelta di quali errori **non** ne
meritino uno.

## Chi l'ha fatto, e come

**Autore e manutentore: Alessandro Curci.** Sono sue l'idea qui sopra e le regole che ne
discendono, l'architettura, le decisioni di progetto e la verifica sul campo; ed è suo
il motore — il modello fisico, il catalogo curato e la disciplina della provenienza —
che costituisce la parte scientifica e decisionale alla quale questo ponte si collega, e
che **non è contenuta in questo repository**.

La stesura del codice è stata fatta **in larga parte da un assistente di intelligenza
artificiale** (Claude, di Anthropic), su sua direzione e sotto la sua revisione.
L'assistente è uno strumento con cui il codice è stato scritto, **non un autore del
progetto**: l'idea, la linea scientifica, l'architettura, le decisioni e la
responsabilità sono del manutentore.

Ogni commit riporta un trailer `Co-Authored-By`. È la convenzione con cui git registra
chi ha contribuito alla stesura di quel commit, e serve alla tracciabilità che il
catalogo dei plugin richiede: non attribuisce la paternità del progetto.

Come si sono divise le parti è verificabile nella storia del repository. Dove passa il
confine con il motore, che cosa fa rifiutare una consegna e che cosa no, se il dither
sia una prescrizione o un'impostazione, come si dichiara un filtro: sono decisioni del
manutentore, in più di un caso prese **contro** la prima proposta dell'assistente. E i
difetti che contavano — il filtro sostituito in silenzio, il dither ereditato dal
template, il numero di pose che N.I.N.A. 3.3 non lasciava scrivere — li ha trovati il
suo setup con montatura e filtri veri, non il banco di prova.

## The idea

The project starts from one conviction: **planning a session isn't looking up a table of
recommendations — it's deriving a consequence.** A consequence of the target's physics,
of the camera and sensor in use, of the filter in front of it, and of the sky over that
site on that night. Start there, and "how many hours of Ha on this object" stops being a
rule of thumb: it becomes a number you can derive, show, and argue with.

Three rules follow from that premise. They show up in every file of both repositories,
and none of them is a programming decision:

**Every number carries where it came from.** A measured value is never conflated with a
derived one or an estimate: source, confidence, how it was obtained and where it stops
being valid all travel with the data. Where no measurement exists, the code says so — it
does not substitute a plausible coefficient.

**Nothing is inferred; everything is declared.** Nanometres don't come out of a filter's
name: a slot labelled "HA" may well hold an L-Ultimate, and only the person who bought
it knows. That's why the bridge makes you declare your filters instead of guessing them,
and never tries to read meaning into their names.

**It refuses rather than silently correcting.** This is the rule that decides the most:
*a sequence that doesn't match the prescription looks exactly like one that does.*
Between skipping a block and refusing the whole target, the asymmetry of harm decides —
a refusal costs one click to fix; five hours shot through the wrong filter cannot be
recovered. Every refusal in this code comes from there — and so does the decision about
which mistakes **don't** deserve one.

## Who made it, and how

**Author and maintainer: Alessandro Curci.** The idea above and the rules that follow
from it are his, as are the architecture, the design decisions and the field
verification. So is the engine — the physical model, the curated catalogue, the
provenance discipline — which is the scientific and decision-making component this
bridge connects to, and which **is not contained in this repository**.

The code itself was written **largely by an AI assistant** (Claude, by Anthropic), under
his direction and review. The assistant is a tool the code was written with, **not an
author of the project**: the idea, the scientific approach, the architecture, the
decisions and the responsibility are the maintainer's.

Every commit carries a `Co-Authored-By` trailer. That is git's convention for recording
who contributed to writing a given commit, and it serves the traceability the plugin
registry asks for; it does not assign authorship of the project.

How the work divided is verifiable in the repository history. Where the boundary with
the engine runs, what makes a delivery fail and what doesn't, whether dithering is a
prescription or a preference, how a filter gets declared: those are the maintainer's
calls, in more than one case made **against** the assistant's first proposal. And the
defects that mattered — the silently substituted filter, the dither inherited from the
template, the frame count N.I.N.A. 3.3 wouldn't let us write — were found by his rig,
with a real mount and real filters, not by the test bench.

## Che cosa fa, e che cosa non fa

AstroImage-Strategy decide **che cosa riprendere**: quali canali hanno senso su quel
soggetto con quel sensore e quel filtro, quante ore a ciascuno, quale posa, quale modo di
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
