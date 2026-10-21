# Views

XAML, e il meno possibile.

Regola una, per lo XAML: **nessuno stile proprio**. Si usano quelli di N.I.N.A.,
altrimenti si riconosce subito come estraneo. Il pannello è un'altra cosa:
`PannelloStrategyView` è un contenitore WebView2 con un riquadro di ripiego, e quello che si
guarda vive nella pagina — `Pagina/prova.html`, `prova.css`, `prova.js`, incorporati nella
DLL e ricomposti in un documento solo da `Pagina.cs` —, che ha i suoi stili perché è una
pagina web.

Con l'indirizzo di AstroImage-Strategy scritto nelle Opzioni («Pagina del pannello») il
contenitore apre invece l'app vera, e il Ponte le fa da ospite (`Services/OspiteDellaPagina`):
risponde solo a quella pagina, le dà il profilo e consegna i bersagli che lei prepara
(`Services/ConsegnaDallaPagina`), e un collegamento verso altro si apre nel browser di sistema.
Al cambio di profilo N.I.N.A. costruisce un pannello nuovo: il più recente spegne quello di prima,
così una pagina sola parla col Ponte.
Vuoto, resta la pagina di prova.

Regola due: **non si pesca nell'albero visuale di N.I.N.A.** Cercare a runtime un
controllo altrui per infilarcisi dentro funziona finché qualcuno non rinomina una
classe, e poi smette in silenzio. Le porte legittime sono tre: il pannello dockable,
le istruzioni del sequenziatore, e il DataTemplate delle opzioni.
