# Models

Le forme dei dati che attraversano il confine, e nient'altro.

Qui vive `SequenceModel`: la trascrizione in C# del contratto che AstroImage-Strategy
già produce — bersaglio con le coordinate risolte, ottica, sito, capacità del banco,
blocchi di ripresa, dithering. Circa un chilobyte di JSON, senza un solo nome di tipo
di N.I.N.A. dentro.

**Non ci va**: nessun calcolo. Un modello che sa fare qualcosa non è un modello.

Il modello e' **dati e basta**: gli unici metodi sono `Leggi` e `Scrivi`, e un test per
riflessione fallisce se ne compare un terzo. Un altro test controlla che nessun tipo qui
dentro venga da un assembly di N.I.N.A., e i test girano senza una sola DLL di N.I.N.A.
accanto: se il modello ne toccasse un tipo, non partirebbero proprio.
