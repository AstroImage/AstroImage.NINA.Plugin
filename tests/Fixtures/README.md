# Fixture del contratto

Questi JSON **non sono scritti a mano**. Sono usciti dalla funzione `sequenceModel` di
AstroImage-Strategy, fatta girare su banchi ottici veri del suo catalogo. È l'unica cosa
che rende utile un test sul contratto: un JSON inventato dimostra soltanto che chi l'ha
scritto e chi lo legge hanno avuto la stessa idea sbagliata.

## I cinque casi, e perché proprio questi

| file | che cosa esercita |
|---|---|
| `mono.json` | camera monocromatica, cinque filtri, ogni canale il suo vetro: nessuna fusione |
| `osc.json` | sensore a matrice: i canali larghi diventano una ripresa sola, e ciò che non si è potuto fondere finisce in `nonFusi` |
| `osc-hdr.json` | matrice con serie corta: due pose diverse sullo stesso filtro, che restano due blocchi |
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

## Come si rigenerano

Non da qui. Il motore, il catalogo e i dati fotometrici stanno in un altro repository, e
il `.gitignore` di questo li vieta per nome — non è un promemoria, è una rete. Chi ha il
motore le rigenera chiamando `sequenceModel(night, plan, tg, dv, site, opts)` sui casi
qui sopra e serializzando con `JSON.stringify(modello, null, 2)`.

Quando il contratto cambia, questi file vanno rifatti **prima** di adeguare il C#: sono
loro la fonte, il modello è la trascrizione.

## `setup/` — le fixture dell'altro contratto

Provenienza **diversa**, e va detto, perché qui non c'è un motore da far girare: la
sorgente è N.I.N.A., e N.I.N.A. non si mette dentro un test.

| file | da dove viene |
|---|---|
| `profilo-osc.json` | generata dal file `.profile` **vero** di N.I.N.A.: focale 800, f/6.9, pixel 3,76 µm, matrice RGGB, sito 45,95 / 10,2019 a 1000 m, guadagno 100, offset 50, **ruota vuota** |
| `campo-mono.json` | letta dal **mini PC operativo in campo**, N.I.N.A. 3.3, attraverso l'Advanced API: CEM70 + ASI 2600MM + RC8, focale 1624 f/8, matrice `None`, guadagno 0 (modo LCG), e una **ruota con sette vetri** — L R G B S H O con gli offset di fuoco veri, negativi, e binning di autofocus diverso fra banda larga e stretta |
| `collegato.json` | **costruita.** Il profilo e le specifiche della camera sono veri — catalogo di Strategy, ASI 2600MC Pro, 6248 × 4176 a 3,76 µm — ma lo stato vivo (temperatura, RMS, SQM) è plausibile, non misurato |

Nelle due generate dai profili la parte dei **dispositivi è nulla**, e non è una
semplificazione: è la verità. Un profilo letto a telescopio spento non sa che cosa
dichiari un driver, e il modello lo dice invece di riempire il vuoto.

`collegato.json` va **rigenerata da una lettura vera** con l'attrezzatura collegata. Fino
ad allora prova la forma del contratto, non i numeri.

`campo-mono.json` è quella che copre il caso più difficile: la ruota piena. Nei profili
di casa è vuota in tutti e sette, e senza il banco in campo l'elenco dei filtri sarebbe
rimasto la parte meno provata del contratto.

Una nota che vale per tutte e tre: i numeri interi si scrivono senza decimali (`2000`,
non `2000.0`), perché è la forma che il serializzatore produce e il giro deve tornare
identico carattere per carattere.

