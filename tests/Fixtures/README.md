# Fixture del contratto

Questi JSON **non sono scritti a mano**. Sono usciti dalla funzione `sequenceModel` di
AstroImage-Strategy, fatta girare su banchi ottici veri del suo catalogo. È l'unica cosa
che rende utile un test sul contratto: un JSON inventato dimostra soltanto che chi l'ha
scritto e chi lo legge hanno avuto la stessa idea sbagliata.

## I cinque casi, e perché proprio questi

| file | che cosa esercita |
|---|---|
| `mono.json` | camera monocromatica, cinque filtri, ogni canale il suo vetro: nessuna fusione |
| `osc.json` | sensore a matrice: la banda larga arriva come un canale solo, `RGB`, in un blocco; niente da fondere, e `nonFusi` vuoto |
| `osc-hdr.json` | matrice con serie corta: due pose diverse sullo stesso filtro, che restano due blocchi e sono dichiarate in `nonFusi` |
| `completo.json` | campo spostato (`off` con `spostato: true`), rotazione, rotatore **dichiarato**, dither ogni 3 pose |
| `scarno.json` | il caso povero: niente sito, niente autoguida, nessun nome di attrezzatura — tutti i campi che devono restare `null` |

Tutte e cinque portano `quando`, perché il motore lo riempie da una notte vera. Il caso
con `quando` nullo — una notte costruita a mano, senza tempo — non è raggiungibile dalla
pagina, e i test lo ottengono degradando una fixture invece di inventarne una.

Insieme coprono i rami che il contratto ha davvero: `nome` nullo e non nullo, `sito`
nullo, `dither` nullo per assenza di guida, `matrice` vera e falsa, `off` presente e
assente, `nonFusi` pieno e vuoto.

**Non coprono** la sentinella `-1` di guadagno e offset, né una chiave del tutto assente:
sono rami che il motore raggiunge solo con attrezzatura senza modi di guadagno o senza
scheda. I test li producono degradando una fixture vera, invece di inventare un JSON.

## Rigenerate il 15 settembre 2026

Tre fixture portavano ancora l'RGB su matrice nella forma di prima: R, G e B fusi in un blocco, e in
`osc.json` una L con un'altra posa dichiarata in `nonFusi`. Dal 14 settembre 2026 davanti a una matrice
la L non esiste, e il colore arriva come un canale solo, `RGB`. Sono state rifatte chiedendole al
servizio, con la ruota L-Ultimate + P2:

| file | richiesta |
|---|---|
| `osc.json` | M31 · RedCat 51 + ASI 2600MC + AM5 · 1 settembre 2026 · una notte |
| `osc-hdr.json` | M27 · Askar 71F 0,75× + ASI 2600MC + AM5 · 1 settembre 2026 · una notte (la serie corta è della notte 1) |
| `servizio/prescrizione-ok.json` | NGC 6888 · Askar 71F 0,75× + ASI 2600MC + AM5 · 15 settembre 2026 · una notte |

Una conseguenza da sapere: **un blocco su più canali nessuna risposta vera lo porta più.** Il contratto
lo permette ancora — il motore fonde due canali larghi con lo stesso filtro, la stessa posa e lo stesso
guadagno —, e il ponte lo sa ancora trattare. Le prove lo esercitano su una fixture vera degradata
(`TraduzioneTests.UnBloccoSuPiuCanali_LEtichettaLiNominaTutti`,
`RuotaVirtualeTests.UnBloccoSuPiuCanali_HaUnVetroSOLO`), invece di lasciarlo un ramo che nessuno esegue.

## Una fixture porta solo i campi che la prova legge

Questo repository è pubblico, il motore no. Una risposta del servizio porta la scheda
dell'oggetto, le trappole di elaborazione, il comportamento dei filtri e note di
provenienza che servono a chi scrive il motore, non a chi riceve la risposta: niente di
questo deve attraversare. Una fixture presa dal servizio, prima di entrare qui, si riduce
ai campi che le prove leggono. Dove una prova guarda la forma del contratto — che una
chiave arrivi — la chiave resta con un segnaposto vuoto; la prosa non resta mai.

`servizio/prescrizione-ok.json` porta `contratto`, `prodotto.bersaglio`,
`prodotto.sequenze` intere e `prodotto.posa.<canale>.ex.spec.filter.id` per ogni canale;
`valutazione`, `prescrizione`, `piano` e la chiave di servizio `posa.__modes` sono oggetti
vuoti. `misura` non c'è: nessuna prova la legge, e porta il tempo della chiamata, che cambia
ogni volta — una fixture che non si rigenera identica non si può verificare rigenerandola.
La risposta intera pesava 138 620 byte, la fixture ne pesa 1 180. Le altre fixture non
portano prosa: nessuna stringa oltre 80 byte.

Fino al 15 settembre 2026 la regola era un'altra — la prosa sostituita da
`(prosa non pubblicata)`, tutti i campi al loro posto — e stava scritta soltanto in un
commento di `ClienteStrategyTests`. Rigenerando la fixture dal servizio è stata disfatta
senza che nessuno la vedesse, e la versione con la prosa non è mai stata pubblicata. Per
questo la regola adesso sta qui, accanto al modo di rigenerare.

Una conseguenza da dichiarare: la chiave `__modes` vera non porta un filtro, quindi la
verifica «le chiavi di servizio non sono canali» di
`RuotaVirtualeTests.DallaRispostaVERA_SiLeggeIlVetroPerOgniCanale` passerebbe anche se il
ponte non le saltasse. È un'assicurazione, non una prova.

## Come si rigenerano

Non da qui. Il motore, il catalogo e i dati fotometrici stanno in un altro repository, e
il `.gitignore` di questo li vieta per nome — non è un promemoria, è una rete. Chi ha il
motore le rigenera chiamando `sequenceModel(night, plan, tg, dv, site, opts)` sui casi
qui sopra e serializzando con `JSON.stringify(modello, null, 2)`. Le fixture di `servizio/`
si chiedono al servizio acceso, e prima di entrare qui passano per la riduzione della
sezione precedente.

Quando il contratto cambia, questi file vanno rifatti **prima** di adeguare il C#: sono
loro la fonte, il modello è la trascrizione.

## `setup/` — le fixture dell'altro contratto

Provenienza **diversa**, e va detto, perché qui non c'è un motore da far girare: la
sorgente è N.I.N.A., e N.I.N.A. non si mette dentro un test.

| file | da dove viene |
|---|---|
| `profilo-osc.json` | generata dal file `.profile` **vero** di N.I.N.A.: focale 800, f/6.9, pixel 3,76 µm, matrice RGGB, sito 45,9 / 10,2 a 1000 m, guadagno 100, offset 50, **ruota vuota** |
| `campo-mono.json` | letta dal **mini PC operativo in campo**, N.I.N.A. 3.3, attraverso l'Advanced API: CEM70 + ASI 2600MM + RC8, focale 1624 f/8, matrice `None`, guadagno 0 (modo LCG), e una **ruota con sette vetri** — L R G B S H O con gli offset di fuoco veri, negativi, e binning di autofocus diverso fra banda larga e stretta |
| `collegato.json` | **letta dal mini PC in campo**, N.I.N.A. 3.3, Advanced API in sola lettura: AM5 + ASI 2600MC + Askar 71F a 490 mm f/6.9, ruota EFW con cinque vetri, focheggiatore EAF, guida PHD2, rotatore manuale. Solo il meteo non è collegato. **Nessun numero è scritto a mano**, tranne le coordinate: vedi in fondo |

Nelle due generate dai profili la parte dei **dispositivi è nulla**, e non è una
semplificazione: è la verità. Un profilo letto a telescopio spento non sa che cosa
dichiari un driver, e il modello lo dice invece di riempire il vuoto.

Tutte e tre passano per il modello prima di essere salvate, quindi sono nella forma
canonica che il serializzatore produce: è l'unico modo perché il giro torni identico
carattere per carattere senza aggiustare i numeri a occhio.

Due cose che una fixture inventata non avrebbe mai mostrato, e che hanno corretto il
codice:

- il guadagno minimo della 2600MC è **−25**. Un filtro «solo valori positivi» sugli
  estremi l'avrebbe cancellato;
- il focheggiatore EAF dichiara un passo di **600000**. Il campo si chiamava `passo_um`,
  e in micrometri sarebbero sessanta centimetri per passo: ora si chiama `passo` e non
  afferma più un'unità che il valore smentisce.

- il **rotatore manuale** — quello che usa chiunque non abbia un rotatore fisico —
  risulta collegato e dichiara posizione **0** con `Synced` falso. Il modello non aveva
  quel campo: senza, quello zero sarebbe passato per un angolo misurato, e chi legge ci
  avrebbe costruito sopra un'inquadratura. Ora c'è `sincronizzato`.

E una che resta come avvertenza invece che come correzione: l'RMS di PHD2 è **zero su
tutti e cinque i valori**, perché al momento della lettura era collegato ma fermo. Zero
non è inseguimento perfetto: quasi sempre vuol dire «non sta guidando».

Il filo comune delle quattro è lo stesso: **un dispositivo collegato non è un
dispositivo che sta dicendo qualcosa.** Servono tre stati, non due — spento, acceso e
significativo, acceso e non ancora significativo.

`campo-mono.json` è quella che copre il caso più difficile: la ruota piena. Nei profili
di casa è vuota in tutti e sette, e senza il banco in campo l'elenco dei filtri sarebbe
rimasto la parte meno provata del contratto.

Una nota che vale per tutte e tre: i numeri interi si scrivono senza decimali (`2000`,
non `2000.0`), perché è la forma che il serializzatore produce e il giro deve tornare
identico carattere per carattere.

## L'unico campo che non è la lettura vera: le coordinate

Va detto, perché altrove questo file promette che nessun numero è scritto a mano.

`lat` e `lon` sono state **sfocate a un decimale** — circa undici chilometri — prima che
questo repository diventasse pubblico. Le letture originali arrivavano al sesto decimale,
cioè a una decina di centimetri, e dicevano da dove osserva una persona: un dato che non
ha niente a che fare con quello che queste fixture verificano, ma che una volta pubblicato
non si ritira più.

Tutto il resto è intatto: focale, apertura, pixel, matrice, guadagni, offset di fuoco dei
sette filtri, passo del focheggiatore, RMS, stato del rotatore. Sono quelli i numeri che
hanno corretto il codice, ed è su quelli che i test lavorano.

Una cosa è stata conservata di proposito, perché è un fenomeno e non un dettaglio: in
`collegato.json` il sito del **profilo** e quello della **montatura** restano due numeri
diversi. N.I.N.A. salva nel profilo un valore arrotondato a tre decimali mentre la
montatura dichiara tutte le cifre che ha, e il modello porta entrambi invece di appianarli.
Lo sfocamento è stato costruito perché quella relazione valga ancora: il valore del profilo
è esattamente l'arrotondamento a tre decimali di quello della montatura, e i due
differiscono di meno di un millesimo di grado. `Sito_NeEsistonoDue_EIlModelloLiPortaEntrambi`
continua quindi a verificare la cosa vera, su un posto che non è più il posto di nessuno.

