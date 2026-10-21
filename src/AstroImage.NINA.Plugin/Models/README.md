# Models

Le forme dei dati che attraversano il confine, e nient'altro.

Qui vive `SequenceModel`: la trascrizione in C# del contratto che AstroImage-Strategy
già produce — bersaglio con le coordinate risolte, ottica, sito, capacità del banco,
blocchi di ripresa, totali, serie corta, dithering. Pochi chilobyte di JSON, senza un solo
nome di tipo di N.I.N.A. dentro.

**Non ci va**: nessun calcolo. Un modello che sa fare qualcosa non è un modello.

Il modello e' **dati e basta**: gli unici metodi sono `Leggi` e `Scrivi`, e un test per
riflessione fallisce se ne compare un terzo. Un altro test controlla che nessun tipo qui
dentro venga da un assembly di N.I.N.A., e i test girano senza una sola DLL di N.I.N.A.
accanto: se il modello ne toccasse un tipo, non partirebbero proprio.

## I due contratti, e perché stanno in due stanze

`SequenceModel` (namespace `Models`) va **da Strategy a N.I.N.A.**: dice che cosa
riprendere. `SetupModel` (namespace `Models.Setup`) va **nel verso opposto**: dice che
cosa c'è al telescopio. Insieme chiudono il giro — il motore non deve più chiedere a
nessuno che attrezzatura si abbia, la legge.

Stanno in due namespace perché `Sito` e `Ottica` vogliono dire cose vicine ma non
uguali nei due contratti, e rinominarli avrebbe voluto dire toccare un contratto già
chiuso e già provato.

`SetupModel` tiene distinte tre cose che è facile confondere, e confonderle significa
prescrivere per un telescopio che non c'è:

- il **profilo** — configurato, esiste anche a telescopio spento, è un'intenzione;
- il **dispositivo collegato** — quello che il driver dichiara, è un fatto;
- lo **stato vivo** — temperatura, RMS, fondo cielo: vale all'istante scritto in `letto`.

Dove la stessa grandezza esiste in due posti — il passo del pixel, il sito — le porta
**entrambe** invece di sceglierne una. Il ponte non decide chi ha ragione: rende la
differenza visibile.

E dai nomi dei filtri non ricava bande. N.I.N.A. sa che quel filtro si chiama «Ha 3nm» e
sta in posizione 1; non sa che sia centrato su 656,3 nm. C'è un test che fallisce se un
giorno comparisse un campo con dentro dei nanometri.


## Gli altri

- `RispostaPrescrizione` — la busta del servizio, e non un centimetro più di quanto serva.
- `CatalogoDelMotore` — i filtri che il motore conosce: l'elenco fra cui chi riprende dichiara.
- `RuotaVirtuale` — che cosa sono, fisicamente, i filtri che hai in ruota.
- `BancoDichiarato` — quello che di un banco non sanno né N.I.N.A. né il riconoscimento del
  catalogo, e che si dichiara.
- `SitoDiRipresa` — da dove stai riprendendo, e da dove lo sappiamo.
- `ProfiloPerLaPagina` — il banco del profilo attivo che l'ospite dà alla pagina vera di
  AstroImage-Strategy, coi suoi `FiltroDelProfilo` e `OrizzonteDelProfilo`: solo i campi del banco,
  non le chiavi e i percorsi che il profilo tiene accanto.
