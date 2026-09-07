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
