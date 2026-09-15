# AstroImage Strategy Bridge — come si lavora qui

Questo repository è **pubblico** (MPL-2.0). Il motore che decide le prescrizioni, AstroImage-Strategy, è un
altro programma e sta altrove.

## La lingua

**I rapporti si scrivono in italiano**: il rapporto di fine turno, i riepiloghi, le domande, le proposte.
Chi mantiene il progetto non legge l'inglese con scioltezza, e un rapporto che non può leggere è un
rapporto che non può supervisionare. Vale anche quando il contesto arriva in inglese. Nei testi che si
leggono si scrive «filtro», mai «vetro»; nei commenti e negli identificatori resta.

## Il confine di licenza

**Niente del motore attraversa**: non righe di codice, non commenti, non documenti, non prosa nelle
fixture. Il ponte non contiene conoscenza che il servizio non gli abbia mandato, e questo è insieme il suo
disegno e il confine di licenza. Il `.gitignore` vieta per nome i file del motore: è una rete, non un
promemoria. Una fixture presa dal servizio porta solo i campi che le prove leggono
([`tests/Fixtures/README.md`](tests/Fixtures/README.md)).

## Prima di ogni push

- Si lavora su un ramo, e il push si fa col via di chi mantiene il progetto.
- Nei file che si pubblicano non va nessun nome di macchina, nessun indirizzo, nessun percorso utente. I
  nomi delle macchine per l'installazione stanno in `scripts/installa.locale.psd1`, che git ignora. Prima
  del push si lancia `powershell -File scripts/controlla-pubblicabile.ps1`: guarda i file cambiati e i
  messaggi dei commit non pubblicati, nomina uno per uno gli attesi, e con `-Prova` si vede colpire.

## La prova

- Su ogni commit del ramo girano le prove automatiche: `dotnet test -c Release`.
- **Un ramo si fonde su master solo dopo la prova in N.I.N.A.**: il plugin costruito e installato con
  `scripts/installa.ps1`, caricato, una prescrizione vera, la sequenza nell'Advanced Sequencer. Non basta
  che la pagina si apra. **La prova a schermo non vale se non si sa quale binario sta girando**: lo script
  confronta l'impronta di ogni copia installata con quella compilata.
- Una prova nuova si vede fallire per la ragione giusta prima di fidarsene, e un risultato nullo vale solo
  col suo controllo positivo.
