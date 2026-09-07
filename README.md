# AstroImage Strategy Bridge

Un plugin per [N.I.N.A.](https://nighttime-imaging.eu/) che porta dentro il
Sequenziatore Avanzato la prescrizione decisa da **AstroImage-Strategy**.

> **Stato: scheletro.** Questa versione non fa ancora nulla. Esiste per fissare
> l'identità del plugin, i suoi punti di innesto e il confine con il motore.

## Che cosa fa, e che cosa non fa

AstroImage-Strategy decide **che cosa riprendere**: quali canali hanno senso su quel
soggetto con quel sensore e quel vetro, quante ore a ciascuno, quale posa, quale modo di
guadagno. Lo decide dalla fisica — brillanza del soggetto, fondo cielo, rumore di
lettura, pozzetto, saturazione, campionamento.

Questo ponte **non decide niente**. Riceve una prescrizione già presa e la costruisce
nel Sequenziatore Avanzato di N.I.N.A.

Non contiene, e non conterrà: fotometria, modello del cielo, modello dei filtri, scelta
dei canali, allocazione delle ore, strategie di posa, pianificazione delle notti,
cataloghi. Quella roba sta nel motore, che è un altro programma.

C'è un modo semplice di verificare che il confine sia nel posto giusto: chi legge questo
sorgente impara **come si parla a N.I.N.A.**, che è già pubblico, e **nulla** su come si
decide che cosa riprendere.

## Il contratto

Fra il motore e il ponte passa una cosa sola: un **modello di sequenza**, circa un
chilobyte di JSON che non nomina N.I.N.A. da nessuna parte.

```json
{
  "notte": 1,
  "bersaglio": { "nome": "IC 1318", "rot": 245, "ra_deg": 305.55, "dec_deg": 40.25 },
  "ottica":    { "camera": "…", "focale_mm": 367, "pixel_um": 3.76, "bin": 1, "matrice": false },
  "sito":      { "lat": 45.95, "lon": 10.2019 },
  "cap":       { "raffredda": true, "ruota": true, "guida": true, "rotatore": false },
  "blocchi":   [ { "canali": ["OIII"], "filtro": "O", "sec": 600, "n": 22,
                   "gain": 100, "offset": 50 } ],
  "dither":    { "ogniPose": 2 }
}
```

Il motore lo produce già, ed è verificato che sopravvive intatto a un giro di JSON: è
quello che gli permette di attraversare un confine — un file, gli appunti, una richiesta
locale — senza portarsi dietro mezzo programma.

## Compilare

Serve l'SDK .NET 8 e nient'altro: il pacchetto NuGet `NINA.Plugin` porta i riferimenti
necessari, e non occorre compilare N.I.N.A.

```
dotnet build -c Release
```

Per installarlo nella propria N.I.N.A. mentre si sviluppa:

```
dotnet build -c Release -p:DeployToNina=true
```

La copia automatica è **spenta di proposito**: `dotnet build` non deve installare niente
a sorpresa su un'installazione vera.

## Requisiti

| | |
|---|---|
| N.I.N.A. | 3.2.0.9001 o successiva, comprese le nightly 3.3 |
| Piattaforma | Windows x64 |
| Framework | .NET 8 (WPF) |

## Licenza

MIT — vedi [LICENSE](LICENSE).

Il catalogo ufficiale dei plugin di N.I.N.A. non accetta plugin a sorgente chiuso, e fa
bene. Che questo ponte sia aperto non è in contraddizione con un motore commerciale: il
regolamento del catalogo prevede esattamente il caso di un plugin che «fa da mediatore»
verso un'applicazione chiusa o un servizio a pagamento. Il ponte è il tubo; il valore sta
in quello che ci passa dentro.
