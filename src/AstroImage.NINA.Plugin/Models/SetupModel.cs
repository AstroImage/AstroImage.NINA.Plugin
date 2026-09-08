using System;
using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

#nullable enable

/*  PERCHE' UN NAMESPACE SUO.
 *
 *  `Sito` e `Ottica` esistono gia' nel contratto della sequenza, e vogliono dire cose
 *  vicine ma non uguali: li' il sito e' quello per cui la notte e' stata calcolata,
 *  qui e' quello configurato nel profilo. Rinominarli per far posto avrebbe voluto dire
 *  toccare un contratto gia' chiuso e gia' provato.
 *
 *  Due contratti sono due contratti: stanno in due stanze. Le guardie per riflessione
 *  coprono `Models` e tutto cio' che ci sta sotto, quindi anche questa stanza e'
 *  sorvegliata — niente metodi oltre alla serializzazione, niente proprieta' calcolate,
 *  nessun tipo di N.I.N.A.
 */
namespace AstroImage.NINA.Plugin.Models.Setup {

    /*  L'ALTRA META' DEL CONTRATTO.
     *
     *  SequenceModel va da Strategy a N.I.N.A. e dice che cosa riprendere. Questo va
     *  nel verso opposto e dice CHE COSA C'E' AL TELESCOPIO. Insieme chiudono il giro:
     *  il motore non deve piu' chiedere a nessuno che attrezzatura abbia, la legge.
     *
     *  E' la ragione per cui questo plugin ha senso di esistere. Un pianificatore che
     *  gira nel browser puo' fare tutto tranne una cosa: sapere che cosa e' collegato
     *  adesso. Qui dentro invece c'e' il passo del pixel del sensore vero, i modi di
     *  guadagno che quella camera espone davvero, il fondo cielo misurato stanotte.
     *
     *  TRE COSE DIVERSE CHE QUI RESTANO DISTINTE, perche' confonderle e' il modo piu'
     *  facile di prescrivere per un telescopio che non c'e'.
     *
     *  1. IL PROFILO — cio' che l'utente ha configurato in N.I.N.A.: focale, rapporto,
     *     sito, elenco dei filtri, passo del pixel dichiarato. Esiste anche a
     *     telescopio spento, ed e' un'INTENZIONE.
     *
     *  2. IL DISPOSITIVO COLLEGATO — cio' che il driver dichiara: dimensioni del
     *     sensore, tipo di matrice, guadagni disponibili, profondita' in bit. Esiste
     *     solo se `collegato` e' vero, ed e' un FATTO.
     *
     *  3. LO STATO VIVO — cio' che sta cambiando adesso: temperatura del sensore, RMS
     *     della guida, fondo cielo, seeing. Vale nell'istante scritto in `letto`, e
     *     fra un'ora non vale piu'.
     *
     *  Dove la stessa grandezza esiste in due posti — il passo del pixel, il sito —
     *  QUESTO MODELLO LE PORTA ENTRAMBE invece di sceglierne una. Non e' pignoleria:
     *  un profilo che dice 3,76 µm e una camera che ne dichiara 2,4 e' un utente che
     *  sta per ricevere una prescrizione per uno strumento che non ha. Il ponte non
     *  decide chi ha ragione; lo dice, e la differenza si vede.
     *
     *  NIENTE VIENE INVENTATO. Un dato che non c'e' e' `null`, mai zero, mai falso,
     *  mai stringa vuota. `collegato: false` e' un'informazione; `collegato: null`
     *  vuol dire che non si e' potuto nemmeno chiedere.
     *
     *  E NIENTE VIENE DEDOTTO. In particolare: dai nomi dei filtri non si ricava la
     *  banda. N.I.N.A. sa che quel filtro si chiama «Ha» e sta in posizione 2; non sa
     *  che sia a 3 nm centrato su 656,3. Chiamare nanometri un nome sarebbe inventare
     *  fisica dentro un adattatore, ed e' esattamente cio' che questo lato del confine
     *  non fa. Il vetro lo riconosce il catalogo, dall'altra parte.
     */

    /// <summary>
    /// Che cosa c'e' al telescopio, come N.I.N.A. lo conosce nell'istante della lettura.
    /// </summary>
    public sealed class SetupModel {

        /// <summary>
        /// Versione della FORMA di questo contratto. Si alza solo per un cambiamento
        /// che rompe chi legge: un campo che sparisce, un tipo che cambia, un
        /// significato che si sposta. Aggiungere un campo non la tocca — a quello
        /// bastano <c>Extra</c> e i nulli.
        /// </summary>
        [JsonPropertyName("versione")]
        public int Versione { get; set; } = 1;

        /// <summary>
        /// Quando la lettura e' stata fatta, in UTC. Non e' un dettaglio: meta' di
        /// questo modello e' stato vivo, e un fondo cielo di tre ore fa non e' un fondo
        /// cielo. Chi riceve deve poter decidere se e' ancora buono.
        /// </summary>
        [JsonPropertyName("letto")]
        public DateTimeOffset? Letto { get; set; }

        /// <summary>Chi ha letto: nome e versione dell'applicazione ospite.</summary>
        [JsonPropertyName("applicazione")]
        public Applicazione? Applicazione { get; set; }

        /// <summary>Il profilo attivo di N.I.N.A. da cui viene la parte configurata.</summary>
        [JsonPropertyName("profilo")]
        public Profilo? Profilo { get; set; }

        /// <summary>Il sito come CONFIGURATO nel profilo. La montatura ne ha uno suo.</summary>
        [JsonPropertyName("sito")]
        public Sito? Sito { get; set; }

        /// <summary>
        /// L'ottica, che e' interamente configurazione: N.I.N.A. non ha modo di
        /// misurare la focale di un tubo. Se qui c'e' 800 mm e' perche' qualcuno l'ha
        /// scritto, e se e' sbagliato tutto il campionamento a valle e' sbagliato.
        /// </summary>
        [JsonPropertyName("ottica")]
        public Ottica? Ottica { get; set; }

        [JsonPropertyName("camera")]
        public Camera? Camera { get; set; }

        [JsonPropertyName("montatura")]
        public Montatura? Montatura { get; set; }

        [JsonPropertyName("ruota")]
        public Ruota? Ruota { get; set; }

        [JsonPropertyName("focheggiatore")]
        public Focheggiatore? Focheggiatore { get; set; }

        [JsonPropertyName("rotatore")]
        public Rotatore? Rotatore { get; set; }

        [JsonPropertyName("guida")]
        public Guida? Guida { get; set; }

        /// <summary>Il cielo misurato, se c'e' una stazione meteo che lo misura.</summary>
        [JsonPropertyName("meteo")]
        public Meteo? Meteo { get; set; }

        /// <summary>
        /// Cio' che una versione futura aggiungera' e questa non sa leggere. Stessa
        /// ragione di SequenceModel: un ponte non deve perdere in silenzio quello che
        /// non capisce.
        /// </summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }

        /// <summary>
        /// Le stesse opzioni di SequenceModel, e per la stessa ragione: leggere con
        /// certe regole e riscrivere con altre e' il modo classico di perdere dati
        /// senza accorgersene. Il convertitore degli istanti e' condiviso.
        /// </summary>
        public static readonly JsonSerializerOptions OpzioniJson = new JsonSerializerOptions {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Converters = { new Json.IstanteUtcConverter() },
        };

        public static SetupModel? Leggi(string json) =>
            JsonSerializer.Deserialize<SetupModel>(json, OpzioniJson);

        public string Scrivi() =>
            JsonSerializer.Serialize(this, OpzioniJson);
    }

    /// <summary>Chi ha prodotto la lettura.</summary>
    public sealed class Applicazione {
        [JsonPropertyName("nome")] public string? Nome { get; set; }
        [JsonPropertyName("versione")] public string? Versione { get; set; }
        [JsonPropertyName("ponte")] public string? Ponte { get; set; }
        [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
    }

    /// <summary>Il profilo attivo. Un utente ne ha spesso uno per setup.</summary>
    public sealed class Profilo {
        [JsonPropertyName("nome")] public string? Nome { get; set; }
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
    }

    /// <summary>Dove si riprende. Gradi decimali, longitudine positiva a est.</summary>
    public sealed class Sito {
        [JsonPropertyName("lat")] public double? Lat { get; set; }
        [JsonPropertyName("lon")] public double? Lon { get; set; }
        [JsonPropertyName("elevazione_m")] public double? ElevazioneM { get; set; }
        [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
    }

    /// <summary>Tubo e riduttore, per quel poco che N.I.N.A. ne sa: solo numeri configurati.</summary>
    public sealed class Ottica {
        /// <summary>Nome dato al telescopio nel profilo, se ne ha uno.</summary>
        [JsonPropertyName("nome")] public string? Nome { get; set; }
        /// <summary>Focale RISULTANTE in millimetri, riduttore gia' compreso se l'utente l'ha considerato.</summary>
        [JsonPropertyName("focale_mm")] public double? FocaleMm { get; set; }
        /// <summary>Rapporto focale configurato. N.I.N.A. non lo ricava dall'apertura: e' un numero scritto.</summary>
        [JsonPropertyName("rapporto")] public double? Rapporto { get; set; }
        [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
    }

    /// <summary>
    /// La camera. Qui vive la differenza piu' importante fra profilo e dispositivo: il
    /// passo del pixel e il tipo di sensore esistono in tutti e due, e possono
    /// smentirsi.
    /// </summary>
    public sealed class Camera {
        /// <summary>Nullo se non si e' potuto chiedere; falso e' un'informazione, non un'assenza.</summary>
        [JsonPropertyName("collegato")] public bool? Collegato { get; set; }
        [JsonPropertyName("nome")] public string? Nome { get; set; }
        [JsonPropertyName("driver")] public string? Driver { get; set; }

        /// <summary>Passo del pixel in µm come lo dichiara il DRIVER.</summary>
        [JsonPropertyName("pixel_um")] public double? PixelUm { get; set; }
        /// <summary>Passo del pixel in µm come e' scritto nel PROFILO. Puo' non coincidere.</summary>
        [JsonPropertyName("pixel_um_profilo")] public double? PixelUmProfilo { get; set; }

        [JsonPropertyName("larghezza_px")] public int? LarghezzaPx { get; set; }
        [JsonPropertyName("altezza_px")] public int? AltezzaPx { get; set; }

        /// <summary>
        /// Il tipo di sensore come lo NOMINA N.I.N.A.: «Monochrome», «RGGB», «GBRG»…
        /// Si porta la stringa e non una classificazione, perche' sapere QUALE matrice
        /// e' conta, e ricondurre tutto a «a colori» perderebbe l'informazione.
        /// </summary>
        [JsonPropertyName("sensore")] public string? Sensore { get; set; }
        /// <summary>Vero solo se il sensore e' dichiarato monocromatico. Nullo se non si sa.</summary>
        [JsonPropertyName("monocromatico")] public bool? Monocromatico { get; set; }
        /// <summary>Il disegno di matrice configurato nel profilo. Anche questo puo' smentire il driver.</summary>
        [JsonPropertyName("matrice_profilo")] public string? MatriceProfilo { get; set; }

        [JsonPropertyName("bit")] public int? Bit { get; set; }
        [JsonPropertyName("elettroni_per_adu")] public double? ElettroniPerAdu { get; set; }
        [JsonPropertyName("posa_min_s")] public double? PosaMinS { get; set; }
        [JsonPropertyName("posa_max_s")] public double? PosaMaxS { get; set; }

        [JsonPropertyName("gain")] public Scala? Gain { get; set; }
        [JsonPropertyName("offset")] public Scala? Offset { get; set; }

        /// <summary>
        /// I modi di lettura come li nomina il driver, nell'ordine in cui li espone:
        /// l'indice in questo elenco E' il valore che N.I.N.A. usa.
        /// </summary>
        [JsonPropertyName("modi_lettura")] public List<string>? ModiLettura { get; set; }
        [JsonPropertyName("modo_lettura")] public int? ModoLettura { get; set; }

        /// <summary>I binning che la camera dichiara, come «1x1», «2x2».</summary>
        [JsonPropertyName("binning")] public List<string>? Binning { get; set; }

        [JsonPropertyName("raffreddamento")] public Raffreddamento? Raffreddamento { get; set; }

        [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
    }

    /// <summary>
    /// Una grandezza regolabile con i suoi estremi: guadagno, offset. Tutto annullabile
    /// perche' un driver puo' esporre il valore e non gli estremi, o viceversa.
    /// </summary>
    public sealed class Scala {
        [JsonPropertyName("attuale")] public int? Attuale { get; set; }
        [JsonPropertyName("min")] public int? Min { get; set; }
        [JsonPropertyName("max")] public int? Max { get; set; }
        [JsonPropertyName("predefinito")] public int? Predefinito { get; set; }
        /// <summary>
        /// I valori ammessi, quando la camera espone un elenco chiuso invece di un
        /// intervallo. Alcune camere hanno l'uno, alcune l'altro, alcune tutti e due.
        /// </summary>
        [JsonPropertyName("valori")] public List<int>? Valori { get; set; }
        [JsonPropertyName("profilo")] public int? Profilo { get; set; }
        [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
    }

    /// <summary>Il raffreddamento: che cosa sa fare, e a che punto e' adesso.</summary>
    public sealed class Raffreddamento {
        [JsonPropertyName("disponibile")] public bool? Disponibile { get; set; }
        /// <summary>Temperatura del sensore adesso. Stato vivo: vale all'istante `letto`.</summary>
        [JsonPropertyName("temperatura_c")] public double? TemperaturaC { get; set; }
        [JsonPropertyName("setpoint_c")] public double? SetpointC { get; set; }
        [JsonPropertyName("setpoint_profilo_c")] public double? SetpointProfiloC { get; set; }
        [JsonPropertyName("acceso")] public bool? Acceso { get; set; }
        [JsonPropertyName("potenza_pct")] public double? PotenzaPct { get; set; }
        [JsonPropertyName("minuti_freddo")] public double? MinutiFreddo { get; set; }
        [JsonPropertyName("minuti_caldo")] public double? MinutiCaldo { get; set; }
        [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
    }

    /// <summary>
    /// La montatura. Porta un secondo sito, il suo: alcune montature hanno le
    /// coordinate impostate nel controller e possono non coincidere con il profilo.
    /// </summary>
    public sealed class Montatura {
        [JsonPropertyName("collegato")] public bool? Collegato { get; set; }
        [JsonPropertyName("nome")] public string? Nome { get; set; }
        [JsonPropertyName("driver")] public string? Driver { get; set; }
        /// <summary>Il sito come lo dichiara la MONTATURA, non il profilo.</summary>
        [JsonPropertyName("sito")] public Sito? Sito { get; set; }
        [JsonPropertyName("insegue")] public bool? Insegue { get; set; }
        [JsonPropertyName("puo_tornare_a_casa")] public bool? PuoTornareACasa { get; set; }
        [JsonPropertyName("puo_parcheggiare")] public bool? PuoParcheggiare { get; set; }
        [JsonPropertyName("in_parcheggio")] public bool? InParcheggio { get; set; }
        [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
    }

    /// <summary>
    /// La ruota portafiltri. L'elenco dei filtri NON viene dalla ruota: viene dal
    /// profilo. Il dispositivo sa solo quale slot e' selezionato adesso. E' una
    /// distinzione che conta, perche' l'elenco esiste anche a ruota scollegata.
    /// </summary>
    public sealed class Ruota {
        [JsonPropertyName("collegato")] public bool? Collegato { get; set; }
        [JsonPropertyName("nome")] public string? Nome { get; set; }
        [JsonPropertyName("driver")] public string? Driver { get; set; }
        /// <summary>Dal PROFILO. Vuoto e nullo sono cose diverse: vuoto = configurata senza filtri.</summary>
        [JsonPropertyName("filtri")] public List<Filtro>? Filtri { get; set; }
        /// <summary>Dal DISPOSITIVO: quale slot e' montato adesso.</summary>
        [JsonPropertyName("filtro_attuale")] public Filtro? FiltroAttuale { get; set; }
        [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
    }

    /// <summary>
    /// Un filtro come N.I.N.A. lo conosce: un NOME e una POSIZIONE, piu' cio' che serve
    /// all'autofocus. Nient'altro.
    ///
    /// <para>
    /// Non c'e' la banda, non c'e' la larghezza, non c'e' il tipo. N.I.N.A. non li sa,
    /// e ricavarli dal nome sarebbe inventare: «Ha» puo' essere un 3 nm o un 12 nm, e
    /// «L» puo' essere una luminanza o un taglio UV/IR. Il vetro lo riconosce il
    /// catalogo dall'altra parte del confine, dove ci sono i dati per farlo.
    /// </para>
    /// </summary>
    public sealed class Filtro {
        [JsonPropertyName("nome")] public string? Nome { get; set; }
        [JsonPropertyName("posizione")] public int? Posizione { get; set; }
        [JsonPropertyName("offset_fuoco")] public int? OffsetFuoco { get; set; }
        [JsonPropertyName("autofocus_posa_s")] public double? AutofocusPosaS { get; set; }
        [JsonPropertyName("autofocus_binning")] public string? AutofocusBinning { get; set; }
        [JsonPropertyName("autofocus_gain")] public int? AutofocusGain { get; set; }
        [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
    }

    public sealed class Focheggiatore {
        [JsonPropertyName("collegato")] public bool? Collegato { get; set; }
        [JsonPropertyName("nome")] public string? Nome { get; set; }
        [JsonPropertyName("driver")] public string? Driver { get; set; }
        [JsonPropertyName("posizione")] public int? Posizione { get; set; }
        [JsonPropertyName("temperatura_c")] public double? TemperaturaC { get; set; }
        /// <summary>
        /// Il passo del focheggiatore COME LO DICHIARA IL DRIVER, senza unita' di
        /// misura dichiarata.
        ///
        /// <para>
        /// ASCOM nominalmente lo definisce in micrometri, ma i driver non lo
        /// rispettano: l'EAF sul banco in campo risponde 600000, che in micrometri
        /// sarebbero sessanta centimetri di corsa per passo. Questo campo prima si
        /// chiamava <c>passo_um</c>, e quel nome affermava un'unita' che il valore
        /// vero smentisce. Si porta il numero e non si dice che cos'e': chi lo userà
        /// dovrà sapere che driver ha davanti, e almeno lo saprà.
        /// </para>
        /// </summary>
        [JsonPropertyName("passo")] public double? Passo { get; set; }
        [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
    }

    /// <summary>
    /// Il rotatore di campo, fisico o virtuale.
    ///
    /// <para>
    /// <b>SENZA <see cref="Sincronizzato"/> LA POSIZIONE NON VUOL DIRE NIENTE, e questo
    /// e' il caso normale, non l'eccezione.</b> Chi non ha un rotatore fisico usa
    /// quello manuale che N.I.N.A. mette a disposizione: si collega, risulta connesso,
    /// e dichiara posizione 0 con <c>Synced = false</c>. Quello zero non vuol dire
    /// «il campo e' dritto»: vuol dire «nessuno mi ha detto dove sono».
    /// </para>
    ///
    /// <para>
    /// Misurato sul banco in campo: rotatore manuale collegato, Position 0,
    /// MechanicalPosition 0, Synced false. Un consumatore che leggesse la sola
    /// posizione crederebbe a un angolo che nessuno ha mai misurato, e ci
    /// costruirebbe sopra un'inquadratura.
    /// </para>
    /// </summary>
    public sealed class Rotatore {
        [JsonPropertyName("collegato")] public bool? Collegato { get; set; }
        [JsonPropertyName("nome")] public string? Nome { get; set; }
        [JsonPropertyName("driver")] public string? Driver { get; set; }
        /// <summary>
        /// Angolo di posa del campo, in gradi. Da leggere SOLO se
        /// <see cref="Sincronizzato"/> e' vero.
        /// </summary>
        [JsonPropertyName("posizione_gradi")] public double? PosizioneGradi { get; set; }
        /// <summary>Angolo meccanico, che non e' l'angolo sul cielo.</summary>
        [JsonPropertyName("meccanica_gradi")] public double? MeccanicaGradi { get; set; }
        /// <summary>
        /// Vero se qualcuno ha detto al rotatore dove si trova davvero — con una
        /// risoluzione di campo o a mano. Falso, e la posizione e' un numero senza
        /// significato. Nullo, e non si sa nemmeno questo.
        /// </summary>
        [JsonPropertyName("sincronizzato")] public bool? Sincronizzato { get; set; }
        [JsonPropertyName("puo_invertire")] public bool? PuoInvertire { get; set; }
        /// <summary>Vero se il verso di rotazione e' invertito rispetto al predefinito.</summary>
        [JsonPropertyName("invertito")] public bool? Invertito { get; set; }
        [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
    }

    /// <summary>
    /// L'autoguida. La scala in secondi d'arco per pixel e' quella della CAMERA DI
    /// GUIDA, non della camera di ripresa.
    /// </summary>
    public sealed class Guida {
        [JsonPropertyName("collegato")] public bool? Collegato { get; set; }
        [JsonPropertyName("nome")] public string? Nome { get; set; }
        [JsonPropertyName("driver")] public string? Driver { get; set; }
        [JsonPropertyName("scala_arcsec_px")] public double? ScalaArcsecPx { get; set; }
        [JsonPropertyName("rms")] public Rms? Rms { get; set; }
        [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
    }

    /// <summary>
    /// L'errore di inseguimento come N.I.N.A. lo riporta.
    ///
    /// <para>
    /// Niente conversioni: N.I.N.A. espone ogni valore GIA' nelle due unita' che
    /// servono — pixel della camera di guida e secondi d'arco sul cielo — e si portano
    /// tutte e due. Un adattatore che ne calcolasse una dall'altra sarebbe un
    /// adattatore che un giorno converte due volte.
    /// </para>
    ///
    /// <para>
    /// <b>ZERO E' AMBIGUO, e va saputo.</b> Un guider collegato ma fermo riporta tutto
    /// a zero: sul banco in campo PHD2 risultava connesso, con scala 0,476 arcsec/px e
    /// RMS 0,00 su tutti e cinque i valori, semplicemente perche' non stava guidando.
    /// Zero non vuol dire inseguimento perfetto — quello non esiste — ma quasi sempre
    /// «non sta guidando». Il modello non lo interpreta perche' non ha come saperlo:
    /// lo stato del guider non e' fra le proprieta' che GuiderInfo espone nella 3.2.
    /// Chi consuma questi numeri deve trattare uno zero secco come «nessuna misura».
    /// </para>
    /// </summary>
    public sealed class Rms {
        [JsonPropertyName("ra")] public Misura? Ra { get; set; }
        [JsonPropertyName("dec")] public Misura? Dec { get; set; }
        [JsonPropertyName("totale")] public Misura? Totale { get; set; }
        [JsonPropertyName("picco_ra")] public Misura? PiccoRa { get; set; }
        [JsonPropertyName("picco_dec")] public Misura? PiccoDec { get; set; }
        [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
    }

    /// <summary>Una grandezza che N.I.N.A. esprime in due unita' insieme.</summary>
    public sealed class Misura {
        [JsonPropertyName("px")] public double? Px { get; set; }
        [JsonPropertyName("arcsec")] public double? Arcsec { get; set; }
        [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
    }

    /// <summary>
    /// Il cielo misurato. Quasi sempre tutto nullo, perche' quasi nessuno ha una
    /// stazione meteo collegata — e va bene cosi': nullo vuol dire «non misurato», e
    /// una prescrizione fatta su un fondo cielo stimato e' meglio di una fatta su un
    /// numero inventato.
    /// </summary>
    public sealed class Meteo {
        [JsonPropertyName("collegato")] public bool? Collegato { get; set; }
        [JsonPropertyName("nome")] public string? Nome { get; set; }
        [JsonPropertyName("driver")] public string? Driver { get; set; }
        /// <summary>Fondo cielo in magnitudini per secondo d'arco quadrato, se misurato.</summary>
        [JsonPropertyName("sqm")] public double? Sqm { get; set; }
        /// <summary>Larghezza a mezza altezza delle stelle, in secondi d'arco, se misurata.</summary>
        [JsonPropertyName("fwhm_arcsec")] public double? FwhmArcsec { get; set; }
        [JsonPropertyName("temperatura_c")] public double? TemperaturaC { get; set; }
        [JsonPropertyName("umidita_pct")] public double? UmiditaPct { get; set; }
        [JsonPropertyName("punto_rugiada_c")] public double? PuntoRugiadaC { get; set; }
        [JsonPropertyName("nuvole_pct")] public double? NuvolePct { get; set; }
        [JsonPropertyName("vento_ms")] public double? VentoMs { get; set; }
        [JsonPropertyName("pressione_hpa")] public double? PressioneHpa { get; set; }
        [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
    }
}
