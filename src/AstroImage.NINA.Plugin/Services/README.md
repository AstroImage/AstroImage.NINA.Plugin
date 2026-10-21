# Services

Il lavoro che il ponte fa davvero, e sono queste cose contate.

- leggere il profilo di N.I.N.A. — camera, ruota e nomi dei filtri, focale, sito,
  accessori — per confrontarlo con il setup della prescrizione;
- tenere quello che chi riprende dichiara per il profilo attivo — sito, banco, filtri;
- chiedere a Strategy la prescrizione col suo modello di sequenza, e quello che serve a
  sceglierla: gli oggetti di stanotte, le schede, la Luna, le voci del banco, i filtri;
- costruire gli oggetti del Sequenziatore Avanzato e consegnarli;
- dire come è andata.

**Non ci va**: nessuna decisione. Se un servizio si trova a scegliere fra due canali,
due pose o due filtri, il confine è stato attraversato nel verso sbagliato.

## Le due metà della consegna

`Traduzione.cs` e `SequenceBuilder.cs` sono **due metà dello stesso lavoro tenute
separate di proposito**.

`Traduzione` decide quali VALORI vanno nei campi: l'ascensione retta resta in gradi, una
posa che manca non diventa zero secondi, il `-1` del guadagno non si scrive, un
dithering «ogni 2,5 pose» diventa un conteggio. Non nomina un solo tipo di N.I.N.A., e
per questo si prova con le tredici fixture del modello nel banco che gira **senza una sola
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

Il contenitore si consegna con `AddAdvancedTarget`, cioè **come nuovo bersaglio**, oppure —
dal 2 ottobre 2026 — al posto di un bersaglio che il Sequenziatore ha già, scelto dall'elenco
come nel Framing Assistant (`SostituzioneDelBersaglio.cs`): stesso contenitore padre, stessa
posizione, e lo Start e l'End della sequenza restano di chi riprende. Si sostituisce solo il
bersaglio indicato, e solo se è ancora quello che si è visto; dopo il gesto si rilegge il posto,
e se il nuovo non c'è il vecchio torna dov'era. N.I.N.A. non ha un «annulla».

## `LettoreSetup.cs`

L'unico pezzo del ponte che tocca l'hardware, ed è di proposito il più stupido che
esista: chiede ai mediatori di N.I.N.A., copia, e dove non trova niente lascia nullo.

Tre regole. **Niente si inventa** — un driver che non espone una grandezza produce un
`null`, mai uno zero. **Niente si deduce** — nessuna banda dai nomi, nessuna
classificazione dal tipo di sensore. **Niente solleva** — i driver sono software di
terzi e un ASCOM che esplode su una proprietà che dichiara di avere non è un caso di
scuola: ogni lettura è protetta da sé, e se la camera fa i capricci si perde la camera,
non il resto della lettura.

Sta qui e non in `Models` perché ha bisogno di N.I.N.A. viva. Il contratto invece si
prova nel banco che gira senza una sola DLL di N.I.N.A. accanto — che è tutto il punto
della separazione.


## Gli altri, per mestiere

| mestiere | file |
|---|---|
| parlare con Strategy | `ClienteStrategy` (il corriere), `RichiestaDelPannello`, `PrescrizioneCorrente`, `MessaggioDelMotore` (la frase la scrive chi ha uno schermo), `IndirizzoDelMotore` |
| le dichiarazioni | `DichiarazioniDelProfilo`, `DichiarazioneSito`, `DichiarazioneBanco`, `DichiarazioneRuota`, `MemoriaRuota`, `MappaFiltri`, `ProgettiDelProfilo`, `ImpostazioniPonte` |
| leggere N.I.N.A. | `LettoreSetup`, `SitoDelProfilo`, `RuotaDelProfilo`, `RiconciliaRuota`, `VetriDellaPrescrizione` |
| il montaggio | `Traduzione`, `SequenceBuilder`, `FonteDaModello`, `IFonteDiPezzi`, `FiltroPerPosizione` (in N.I.N.A. 3.3 il filtro si sceglie per posizione), `Iterazioni`, `Garanzia` (scrivere non è aver scritto), `SostituzioneDelBersaglio` |
