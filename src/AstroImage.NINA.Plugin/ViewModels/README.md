# ViewModels

Lo stato di ciò che si vede. Qui vive il pannello agganciato alla scheda Imaging,
`PannelloStrategyVM`: un `IDockableVM` che N.I.N.A. scopre con MEF, e il filo fra la pagina
del pannello e i servizi.

Una misura utile, presa contando le righe di un plugin che fa questo mestiere da anni:
l'interfaccia obbligatoria per esistere sono **26 righe** — il DataTemplate delle
opzioni e il suo code-behind. Tutto il resto è una scelta, e ogni riga in più è una
riga da mantenere allineata a un programma che non è il nostro.
