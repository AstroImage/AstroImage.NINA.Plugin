# Services

Il lavoro che il ponte fa davvero, e sono quattro cose contate.

- leggere il profilo di N.I.N.A. — camera, ruota e nomi dei filtri, focale, sito,
  accessori — per confrontarlo con il setup della prescrizione;
- ricevere il modello di sequenza da Strategy;
- costruire gli oggetti del Sequenziatore Avanzato e consegnarli;
- dire come è andata.

**Non ci va**: nessuna decisione. Se un servizio si trova a scegliere fra due canali,
due pose o due filtri, il confine è stato attraversato nel verso sbagliato.

## Che cosa c'è adesso

`Traduzione.cs` e `SequenceBuilder.cs`, che sono **due metà dello stesso lavoro tenute
separate di proposito**.

`Traduzione` decide quali VALORI vanno nei campi: l'ascensione retta resta in gradi, una
posa che manca non diventa zero secondi, il `-1` del guadagno non si scrive, un
dithering «ogni 2,5 pose» diventa un conteggio. Non nomina un solo tipo di N.I.N.A., e
per questo si prova con le cinque fixture del motore nel banco che gira **senza una sola
DLL di N.I.N.A. accanto**. È lì che vivono i bug: un fattore quindici, uno zero al posto
di un nulla.

`SequenceBuilder` prende quei valori e riempie gli oggetti veri. È meccanico, e ha due
regole che non sono di gusto:

- **mai `new` su un tipo di N.I.N.A.**, sempre `ISequencerFactory`. Fra la 3.2.0.9001 e
  la 3.3.0.1057-nightly quattro dei tipi che servono qui hanno cambiato il numero di
  parametri del costruttore, mentre le proprietà sono identiche: un `new` scritto a mano
  lega il plugin a una delle due versioni e lo fa morire sull'altra;
- **il ponte non avvia niente.** `ISequenceMediator` espone `StartAdvancedSequence` e
  questo codice non lo nomina.

Nessuna delle due è affidata alla buona volontà: `RegoleDelPonteTests` le legge dai
metadati del binario compilato, e una mutazione che le violi diventa rossa.

Il contenitore si consegna con `AddAdvancedTarget`, cioè **come nuovo bersaglio**: non
sostituisce niente di quello che c'è già in sequenza.
