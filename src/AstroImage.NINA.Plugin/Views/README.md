# Views

XAML, e il meno possibile.

Regola una: **nessuno stile proprio**. Si usano quelli di N.I.N.A., altrimenti il
pannello si riconosce subito come estraneo.

Regola due: **non si pesca nell'albero visuale di N.I.N.A.** Cercare a runtime un
controllo altrui per infilarcisi dentro funziona finché qualcuno non rinomina una
classe, e poi smette in silenzio. Le porte legittime sono tre: il pannello dockable,
le istruzioni del sequenziatore, e il DataTemplate delle opzioni.
