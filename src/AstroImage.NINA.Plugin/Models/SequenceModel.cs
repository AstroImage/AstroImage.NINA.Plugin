using System;
using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

#nullable enable

namespace AstroImage.NINA.Plugin.Models {

    /*  IL CONTRATTO, TRASCRITTO E NIENTE PIU'.
     *
     *  Questo file e' il gemello in C# dell'oggetto che AstroImage-Strategy produce
     *  gia' oggi con la funzione `sequenceModel` (index.html, riga 5467). Non e' uno
     *  schema nuovo: e' una trascrizione, e ogni scostamento dal contratto vero e'
     *  un difetto, non una scelta di stile.
     *
     *  Tre regole che valgono per tutto il file.
     *
     *  1. NON C'E' UN SOLO TIPO DI N.I.N.A. QUI DENTRO, e non ce ne sara' mai. Il
     *     modello arriva dal motore, che di N.I.N.A. non sa niente; se un giorno una
     *     proprieta' qui avesse tipo `NINA.qualcosa`, il ponte avrebbe smesso di
     *     essere un ponte. C'e' un test che lo verifica per riflessione, perche' una
     *     regola che nessuno controlla e' un commento.
     *
     *  2. NON C'E' UN SOLO CALCOLO. Nessuna conversione, nessun valore predefinito
     *     di comodo, nessuna coordinata ricavata. Il centro del campo arriva GIA'
     *     risolto dal motore -- ra_deg/dec_deg sono il centro scelto, spostamento di
     *     inquadratura compreso -- proprio perche' da questa parte del confine non
     *     ci sia niente da calcolare.
     *
     *  3. I NOMI SONO I SUOI. Le chiavi JSON sono quelle del motore, dichiarate una
     *     per una con JsonPropertyName invece di affidarsi a una convenzione: una
     *     convenzione si puo' cambiare per sbaglio da un'altra parte del programma,
     *     un attributo scritto sulla proprieta' no.
     */

    /// <summary>
    /// La sequenza di UNA notte, come il motore l'ha decisa.
    /// </summary>
    public sealed class SequenceModel {

        /// <summary>
        /// Numero della notte nel piano. Indice, non durata.
        ///
        /// <para>
        /// Annullabile per la stessa ragione di tutti gli altri numeri annullabili di
        /// questo file: il motore lo legge senza difese, e una chiave che puo' non
        /// arrivare, se cadesse in un <c>int</c>, diventerebbe zero. Zero non e' un
        /// dato mancante, e' la notte numero zero -- cioe' una bugia silenziosa.
        /// Meglio un nullo che qualcuno deve guardare.
        /// </para>
        /// </summary>
        [JsonPropertyName("notte")]
        public int? Notte { get; set; }

        /// <summary>
        /// Quando si riprende. Nullo se la notte non porta tempo: e' una notte costruita
        /// a mano, e un contratto non inventa una data per riempire un campo.
        /// </summary>
        [JsonPropertyName("quando")]
        public Quando? Quando { get; set; }

        /// <summary>
        /// Il nome che il motore propone per la sequenza. Puo' mancare: il motore lo
        /// riempie solo quando chi ha chiesto la sequenza ne aveva uno.
        /// </summary>
        [JsonPropertyName("nome")]
        public string? Nome { get; set; }

        [JsonPropertyName("bersaglio")]
        public Bersaglio? Bersaglio { get; set; }

        [JsonPropertyName("ottica")]
        public Ottica? Ottica { get; set; }

        /// <summary>
        /// Il sito. Nullo quando il motore non ne aveva uno: e' un dato mancante, non
        /// un sito all'origine, e non va sostituito con zero da nessuna parte.
        /// </summary>
        [JsonPropertyName("sito")]
        public Sito? Sito { get; set; }

        [JsonPropertyName("cap")]
        public Capacita? Cap { get; set; }

        [JsonPropertyName("blocchi")]
        public List<Blocco> Blocchi { get; set; } = new List<Blocco>();

        /// <summary>
        /// Le anomalie che il motore ha visto e NON ha appianato: due pose diverse
        /// dietro lo stesso vetro su una camera a matrice. Sono frasi gia' scritte,
        /// destinate a essere mostrate, non interpretate.
        /// </summary>
        [JsonPropertyName("nonFusi")]
        public List<string> NonFusi { get; set; } = new List<string>();

        /// <summary>
        /// Nullo quando il banco non dichiara l'autoguida: senza guida il dithering
        /// non si fa, e il motore lo dice togliendo il campo invece di mettere zero.
        /// </summary>
        [JsonPropertyName("dither")]
        public Dither? Dither { get; set; }

        [JsonPropertyName("flip")]
        public bool Flip { get; set; }

        /*  QUELLO CHE ANCORA NON SAPPIAMO LEGGERE, e che non buttiamo via.
         *
         *  Se una versione futura di Strategy aggiunge un campo, un modello scritto
         *  senza questa riga lo perde in silenzio: il ponte lo legge, lo scarta, lo
         *  riscrive senza. Qui invece sopravvive al giro completo, e resta visibile a
         *  chi vorra' accorgersi che i due lati del confine non sono piu' allineati.
         *  Costa una proprieta' per classe e rende il round-trip davvero senza
         *  perdite. */
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }

        /*  LE OPZIONI DI SERIALIZZAZIONE STANNO QUI, in un posto solo.
         *
         *  Leggere con certe opzioni e riscrivere con altre e' il modo classico di
         *  perdere dati senza che nessuno se ne accorga, quindi le due direzioni
         *  usano lo stesso oggetto e non ce n'e' un altro.
         *
         *  UnsafeRelaxedJsonEscaping: senza, un bersaglio che si chiama "Cuore e
         *  Anima" o una nota con un simbolo di grado escono trasformati in \uXXXX.
         *  Non e' una perdita di dati ma e' un file che nessuno riconosce piu', e il
         *  confronto con quello prodotto dal motore diventa impossibile a occhio.
         *  "Unsafe" riguarda l'inserimento in HTML: questo JSON va in un file o in
         *  una POST, non dentro una pagina.
         *
         *  I null si SCRIVONO. `sito: null` e `dither: null` dicono qualcosa --
         *  "questo dato non c'e'" -- e ometterli renderebbe il file piu' corto ma
         *  meno chiaro. Sul verso della lettura chiave assente e chiave nulla sono la
         *  stessa cosa, ed e' l'unica normalizzazione che questo modello si permette. */
        public static readonly JsonSerializerOptions OpzioniJson = new JsonSerializerOptions {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            /*  Gli istanti si riscrivono nella forma del motore e non in quella di
             *  .NET: stesso momento, altro testo, e il file non tornerebbe piu'
             *  identico a quello ricevuto. Vedi IstanteUtcConverter. */
            Converters = { new Json.IstanteUtcConverter() },
        };

        /// <summary>Legge il modello dal JSON prodotto da Strategy.</summary>
        public static SequenceModel? Leggi(string json) =>
            JsonSerializer.Deserialize<SequenceModel>(json, OpzioniJson);

        /// <summary>Riscrive il modello nella stessa forma in cui e' arrivato.</summary>
        public string Scrivi() =>
            JsonSerializer.Serialize(this, OpzioniJson);
    }

    /// <summary>
    /// QUANDO SI RIPRENDE: una sera e due istanti, che rispondono a due domande diverse.
    ///
    /// <para>
    /// <see cref="Data"/> e' la sera in cui la notte comincia, senza ora e senza fuso.
    /// Risponde a «quale notte e'», ed e' quella che si scrive su un'etichetta. E' la
    /// data CIVILE del luogo in cui il motore ha fatto il conto, non un giorno tradotto
    /// in UTC: le due cose differiscono per mezza giornata di longitudine, e prendere
    /// quella sbagliata sposta la sera di un giorno.
    /// </para>
    ///
    /// <para>
    /// <see cref="Inizio"/> e <see cref="Fine"/> sono i due estremi dell'arco utile,
    /// in UTC perche' un istante non ha bisogno di sapere in che fuso lo si guarda. Li
    /// calcola il motore scandendo il cielo ogni cinque minuti fra i crepuscoli; da
    /// questa parte del confine non c'e' — e non ci sara' — nulla che sappia rifarlo.
    /// </para>
    ///
    /// <para>
    /// <b><see cref="OreUtili"/> E' IL CAMPO CHE IMPEDISCE DI LEGGERE MALE GLI ALTRI
    /// DUE.</b> L'arco fra inizio e fine e' un INVILUPPO, non una finestra piena: e' il
    /// primo e l'ultimo campione sopra la soglia di altezza, e in mezzo ci puo' essere
    /// un tratto in cui il soggetto e' sotto il pavimento. Su IC 1396 da Roma il 31
    /// gennaio l'arco copre dieci ore e cinquantacinque mentre le ore vere sono 1,67:
    /// il soggetto tramonta e risorge. Chi trattasse l'arco come una finestra di
    /// ripresa comanderebbe nove ore di pose con l'oggetto troppo basso.
    /// </para>
    ///
    /// <para>
    /// <see cref="OreUtili"/> sono le ore che il piano assegna davvero a quella notte,
    /// overhead gia' tolto. Se <c>Fine - Inizio</c> e' molto piu' grande di
    /// <see cref="OreUtili"/>, l'arco non e' pieno; anche nella notte piu' ordinaria i
    /// due numeri differiscono di mezz'ora abbondante, che e' il tempo tolto per messa
    /// a fuoco, plate solve, calibrazione della guida e flip.
    /// </para>
    ///
    /// <para>
    /// Due precisazioni. <see cref="Fine"/> e' l'ultimo campione utile piu' il passo di
    /// cinque minuti, quindi puo' cadere qualche minuto dopo la fine del buio
    /// astronomico. E <see cref="Data"/> e' la data CHIESTA da chi pianifica: «la notte
    /// del 15 settembre» e' quella che segue il mezzogiorno solare del 15 AL SITO, e il
    /// fuso della macchina che ha fatto il conto non ci entra. Vale da quando il motore
    /// ancora la notte al sito e non all'orologio di chi guarda; prima, con un
    /// telescopio in hosting dall'altra parte del mondo, non usciva nessun piano.
    /// </para>
    /// </summary>
    public sealed class Quando {

        /// <summary>La sera in cui la notte comincia, data civile.</summary>
        [JsonPropertyName("data")]
        public DateOnly? Data { get; set; }

        /// <summary>Primo istante dell'arco utile, in UTC.</summary>
        [JsonPropertyName("inizio")]
        public DateTimeOffset? Inizio { get; set; }

        /// <summary>Ultimo istante dell'arco utile, in UTC.</summary>
        [JsonPropertyName("fine")]
        public DateTimeOffset? Fine { get; set; }

        /// <summary>
        /// Ore che il piano assegna a questa notte, overhead gia' tolto. Sempre minori
        /// dell'arco: quanto minori dice se l'arco e' pieno o bucato.
        /// </summary>
        [JsonPropertyName("oreUtili")]
        public double? OreUtili { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }
    }

    /// <summary>
    /// Il bersaglio, con il centro GIA' risolto.
    /// </summary>
    public sealed class Bersaglio {

        /// <summary>Il nome dell'oggetto. Resta quello anche se il campo e' spostato.</summary>
        [JsonPropertyName("nome")]
        public string? Nome { get; set; }

        /// <summary>Angolo di posa del sensore, in gradi.</summary>
        [JsonPropertyName("rot")]
        public double Rot { get; set; }

        /// <summary>
        /// Lo spostamento di inquadratura chiesto dall'utente, negli assi del sensore.
        /// Nullo quando non ne e' stato chiesto nessuno. E' gia' stato APPLICATO alle
        /// coordinate qui sotto: viaggia con il modello perche' si possa dire perche'
        /// la montatura va dove va, non perche' qualcuno lo riapplichi.
        /// </summary>
        [JsonPropertyName("off")]
        public Offset? Off { get; set; }

        /// <summary>
        /// Ascensione retta del centro SCELTO, in gradi. Non e' la coordinata di
        /// catalogo dell'oggetto se il campo e' stato spostato: e' dove deve andare
        /// la montatura.
        /// </summary>
        [JsonPropertyName("ra_deg")]
        public double RaDeg { get; set; }

        /// <summary>Declinazione del centro scelto, in gradi.</summary>
        [JsonPropertyName("dec_deg")]
        public double DecDeg { get; set; }

        /// <summary>Vero se le coordinate sopra non sono quelle di catalogo.</summary>
        [JsonPropertyName("spostato")]
        public bool Spostato { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }
    }

    /// <summary>
    /// Lo spostamento del campo, in primi d'arco sugli assi del sensore -- non sul
    /// cielo. La rotazione che li porta su ascensione retta e declinazione l'ha gia'
    /// fatta il motore.
    /// </summary>
    public sealed class Offset {

        [JsonPropertyName("du")]
        public double Du { get; set; }

        [JsonPropertyName("dv")]
        public double Dv { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }
    }

    /// <summary>
    /// Il banco ottico dichiarato in Strategy.
    ///
    /// <para>
    /// Non serve a costruire la sequenza -- al Sequenziatore Avanzato di tutto questo
    /// interessa il solo binning. Serve al confronto con il profilo di N.I.N.A.: e'
    /// il dato che permettera' di dire "attenzione, non e' lo stesso setup" invece di
    /// riprendere una notte con i numeri di un altro strumento.
    /// </para>
    ///
    /// <para>
    /// Quasi tutto qui puo' essere nullo o mancante del tutto. Il motore costruisce
    /// questi campi leggendo la scheda dell'attrezzatura, e quando la scheda non ha
    /// un nome la chiave non compare proprio nel JSON: chi legge deve trattare
    /// "assente" e "nullo" come la stessa cosa, cioe' dato che non c'e'.
    /// </para>
    /// </summary>
    public sealed class Ottica {

        /// <summary>Nome del tubo. Puo' mancare.</summary>
        [JsonPropertyName("tel")]
        public string? Tel { get; set; }

        /// <summary>Nome della camera. Puo' mancare.</summary>
        [JsonPropertyName("camera")]
        public string? Camera { get; set; }

        /// <summary>Nome della montatura. Puo' mancare.</summary>
        [JsonPropertyName("montatura")]
        public string? Montatura { get; set; }

        /// <summary>Focale risultante in millimetri, riduttore compreso.</summary>
        [JsonPropertyName("focale_mm")]
        public double? FocaleMm { get; set; }

        /// <summary>Rapporto focale.</summary>
        [JsonPropertyName("rapporto")]
        public double? Rapporto { get; set; }

        /// <summary>Passo del pixel in micrometri, prima del binning.</summary>
        [JsonPropertyName("pixel_um")]
        public double? PixelUm { get; set; }

        /// <summary>Fattore del riduttore o spianatore. Nullo se non ce n'e' uno.</summary>
        [JsonPropertyName("riduttore")]
        public double? Riduttore { get; set; }

        /// <summary>Scala in secondi d'arco per pixel, binning compreso.</summary>
        [JsonPropertyName("scala_arcsec_px")]
        public double? ScalaArcsecPx { get; set; }

        /// <summary>
        /// Binning. Vale 1 quando il motore non ha una scheda ottica; ma se ce l'ha e
        /// il binning non c'e', la chiave sparisce dal JSON invece di valere 1 --
        /// quindi qui e' annullabile, e nullo significa "non dichiarato". Chi
        /// costruira' la sequenza non deve leggere zero e mandarlo alla camera.
        /// </summary>
        [JsonPropertyName("bin")]
        public int? Bin { get; set; }

        /// <summary>
        /// Vero per un sensore a matrice di Bayer. E' la differenza che decide se i
        /// canali larghi sono una ripresa sola o tre, e va confrontata con il
        /// BayerPattern del profilo di N.I.N.A.
        /// </summary>
        [JsonPropertyName("matrice")]
        public bool Matrice { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }
    }

    /// <summary>
    /// Dove si riprende. In gradi decimali, longitudine positiva a est.
    /// </summary>
    public sealed class Sito {

        [JsonPropertyName("lat")]
        public double Lat { get; set; }

        [JsonPropertyName("lon")]
        public double Lon { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }
    }

    /// <summary>
    /// CHE COSA IL BANCO SA FARE. Dichiarazioni, non supposizioni.
    ///
    /// <para>
    /// Sono le sei capacita' che decidono quali istruzioni ha senso mettere nella
    /// sequenza, piu' i tre numeri del raffreddamento. Chi riprende con una reflex su
    /// un inseguitore le dichiara false e non riceve sei istruzioni ineseguibili.
    /// Nessuna di queste va DEDOTTA dai dati: il rotatore in particolare non si
    /// ricava dall'angolo, perche' con il rotatore montato e l'angolo a zero la
    /// deduzione da' la risposta sbagliata.
    /// </para>
    /// </summary>
    public sealed class Capacita {

        /// <summary>La camera e' raffreddata.</summary>
        [JsonPropertyName("raffredda")]
        public bool Raffredda { get; set; }

        /// <summary>C'e' una ruota portafiltri.</summary>
        [JsonPropertyName("ruota")]
        public bool Ruota { get; set; }

        /// <summary>C'e' un focheggiatore motorizzato.</summary>
        [JsonPropertyName("focheggiatore")]
        public bool Focheggiatore { get; set; }

        /// <summary>C'e' l'autoguida. Senza, non c'e' dithering.</summary>
        [JsonPropertyName("guida")]
        public bool Guida { get; set; }

        /// <summary>C'e' un rotatore di campo.</summary>
        [JsonPropertyName("rotatore")]
        public bool Rotatore { get; set; }

        /// <summary>La montatura sa tornare alla posizione di riposo.</summary>
        [JsonPropertyName("home")]
        public bool Home { get; set; }

        /// <summary>
        /// Temperatura di lavoro del sensore, in gradi Celsius. Decimale: nella
        /// pagina si scrive in un campo che accetta qualsiasi numero.
        /// </summary>
        [JsonPropertyName("tempC")]
        public double TempC { get; set; }

        /// <summary>Minuti concessi al raffreddamento prima di cominciare.</summary>
        [JsonPropertyName("minutiFreddo")]
        public double MinutiFreddo { get; set; }

        /// <summary>Minuti concessi al riscaldamento alla fine.</summary>
        [JsonPropertyName("minutiCaldo")]
        public double MinutiCaldo { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }
    }

    /// <summary>
    /// UN BLOCCO DI RIPRESA: stesso vetro, stessa posa, stesso guadagno.
    ///
    /// <para>
    /// E' l'unita' che il motore ha gia' fuso: se due canali larghi finiscono sullo
    /// stesso filtro di una camera a matrice con la stessa posa, qui arrivano come un
    /// blocco solo con due nomi in <see cref="Canali"/>. Quando la fusione NON si
    /// poteva fare la differenza e' dichiarata in <see cref="SequenceModel.NonFusi"/>
    /// invece di essere appianata.
    /// </para>
    /// </summary>
    public sealed class Blocco {

        /// <summary>
        /// I canali che questo blocco copre: uno sul monocromatico, fino a tre sulla
        /// matrice. E' un elenco e non un nome singolo perche' un nome singolo
        /// sarebbe l'invito a leggere il primo e dimenticare gli altri.
        ///
        /// <para>
        /// Gli elementi sono annullabili: il motore li copia dal piano senza difese, e
        /// un canale senza nome arriva come <c>null</c> dentro l'elenco.
        /// </para>
        /// </summary>
        [JsonPropertyName("canali")]
        public List<string?> Canali { get; set; } = new List<string?>();

        /// <summary>
        /// Il nome del filtro COSI' COME E' SCRITTO SULLA RUOTA di chi riprende, non
        /// il nome del canale. Un nome che nella ruota non esiste N.I.N.A. non lo
        /// segnala: mette il primo filtro e va avanti.
        /// </summary>
        [JsonPropertyName("filtro")]
        public string? Filtro { get; set; }

        /// <summary>
        /// Durata della singola posa in secondi. Numero reale: il tempo di posa e' una
        /// grandezza fisica, e un intero qui rifiuterebbe un contratto valido.
        /// Annullabile perche' la chiave puo' mancare, e una posa da zero secondi
        /// sarebbe l'errore piu' silenzioso di tutti.
        /// </summary>
        [JsonPropertyName("sec")]
        public double? Sec { get; set; }

        /// <summary>
        /// Quante pose. Conteggio intero, e almeno una quando c'e'; annullabile per la
        /// stessa ragione della posa -- zero pose non e' un dato, e' un blocco vuoto
        /// che nessuno ha chiesto.
        /// </summary>
        [JsonPropertyName("n")]
        public int? N { get; set; }

        /// <summary>
        /// Guadagno. Vale -1 quando il modo di guadagno non lo dichiara: e' un
        /// "non specificato", non un guadagno negativo, e va trattato come tale da
        /// chiunque legga.
        /// </summary>
        [JsonPropertyName("gain")]
        public int Gain { get; set; }

        /// <summary>Offset. Stessa convenzione del guadagno: -1 significa assente.</summary>
        [JsonPropertyName("offset")]
        public int Offset { get; set; }

        /// <summary>
        /// Il nome del modo di guadagno della camera, se ne aveva uno. Puo' essere
        /// nullo, ed e' una descrizione: nessuno deve dedurne niente.
        /// </summary>
        [JsonPropertyName("modo")]
        public string? Modo { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }
    }

    /// <summary>
    /// Il dithering. Esiste solo se il banco dichiara l'autoguida.
    /// </summary>
    public sealed class Dither {

        /// <summary>
        /// Ogni quante pose spostare. Il motore lo prende da un campo della pagina
        /// che accetta qualsiasi numero, quindi qui e' un reale anche se un valore
        /// frazionario non avrebbe senso fisico: il modello non rifiuta un numero che
        /// il contratto puo' produrre, e chi costruira' la sequenza decidera' che
        /// farne.
        /// </summary>
        [JsonPropertyName("ogniPose")]
        public double OgniPose { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }
    }
}
