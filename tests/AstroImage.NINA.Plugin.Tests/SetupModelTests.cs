using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using AstroImage.NINA.Plugin.Models.Setup;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace AstroImage.NINA.Plugin.Tests {

    /*  L'ALTRA META' DEL CONTRATTO, PROVATA SENZA N.I.N.A.
     *
     *  Le fixture non sono inventate, ma non hanno la stessa provenienza di quelle del
     *  SequenceModel e va detto:
     *
     *    profilo-osc, profilo-mono   generate dai file .profile VERI di N.I.N.A. su
     *                                questa macchina. Focale, rapporto, passo del
     *                                pixel, disegno di matrice, sito, guadagno e
     *                                offset sono quelli salvati da N.I.N.A. La parte
     *                                dei dispositivi e' nulla, ed e' la verita': un
     *                                profilo letto a telescopio spento non sa che cosa
     *                                dichiari un driver.
     *
     *    collegato                   LETTA DAL MINI PC OPERATIVO IN CAMPO, N.I.N.A.
     *                                3.3, attraverso l'Advanced API e in sola lettura.
     *                                AM5 + ASI 2600MC + Askar 71F, con camera,
     *                                montatura, ruota EFW, focheggiatore EAF e guida
     *                                PHD2 collegati; rotatore e meteo no. Nessun
     *                                numero e' scritto a mano — compreso l'RMS a zero,
     *                                che e' quello che PHD2 riporta da fermo.
     *
     *  La distinzione conta: qui non c'e' un motore da far girare come per le
     *  sequenze. La sorgente e' N.I.N.A., e N.I.N.A. non si puo' mettere in un test.
     */
    [TestClass]
    public class SetupModelTests {

        private const string SPAZIO = "AstroImage.NINA.Plugin.Models.Setup";

        private static string Cartella =>
            Path.Combine(Path.GetDirectoryName(typeof(SetupModelTests).Assembly.Location)!,
                         "Fixtures", "setup");

        private static string Testo(string nome) => File.ReadAllText(Path.Combine(Cartella, nome + ".json"));
        private static SetupModel M(string nome) => SetupModel.Leggi(Testo(nome))!;

        public static IEnumerable<object[]> Fixture =>
            Directory.EnumerateFiles(Cartella, "*.json")
                     .Select(f => new object[] { Path.GetFileNameWithoutExtension(f) })
                     .OrderBy(x => (string)x[0]);

        // ---------------------------------------------------------------- il giro

        [TestMethod]
        [DynamicData(nameof(Fixture))]
        public void OgniFixture_SiLegge(string nome) {
            var m = M(nome);
            Assert.IsNotNull(m);
            Assert.AreEqual(1, m.Versione, "la versione della forma del contratto");
            Assert.IsNotNull(m.Letto, "senza l'istante della lettura lo stato vivo non e' valutabile");
            Assert.AreEqual(TimeSpan.Zero, m.Letto!.Value.Offset, "l'istante deve essere in UTC");
        }

        [TestMethod]
        [DynamicData(nameof(Fixture))]
        public void OgniFixture_TornaIdenticaComeTesto(string nome) {
            /*  Come per il SequenceModel: se il testo coincide, coincidono anche
             *  l'ordine delle chiavi, la scrittura dei numeri e il rientro. E' la
             *  garanzia che il ponte non riscriva di nascosto cio' che ha ricevuto —
             *  e vale per QUALUNQUE trasporto, perche' e' una proprieta' del JSON. */
            var partenza = Testo(nome).Replace("\r\n", "\n").TrimEnd('\n');
            var ritorno = M(nome).Scrivi().Replace("\r\n", "\n").TrimEnd('\n');
            Assert.AreEqual(partenza, ritorno);
        }

        [TestMethod]
        public void CampoSconosciuto_SopravviveAlGiro() {
            /*  Il ponte non butta via quello che non capisce, a nessun livello. E' cio'
             *  che permettera' a una versione futura del motore di aggiungere un campo
             *  senza che questa versione del plugin lo perda. */
            var j = JsonNode.Parse(Testo("collegato"))!.AsObject();
            j["campoNuovo"] = 42;
            j["camera"]!.AsObject()["raffreddamentoLiquido"] = true;
            j["guida"]!.AsObject()["rms"]!.AsObject()["rollio"] = 0.5;

            var dopo = JsonNode.Parse(SetupModel.Leggi(j.ToJsonString())!.Scrivi())!;
            Assert.AreEqual(42, dopo["campoNuovo"]!.GetValue<int>());
            Assert.IsTrue(dopo["camera"]!["raffreddamentoLiquido"]!.GetValue<bool>());
            Assert.AreEqual(0.5, dopo["guida"]!["rms"]!["rollio"]!.GetValue<double>());
        }

        // ------------------------------------------------- profilo contro dispositivo

        [TestMethod]
        public void Profilo_SenzaDispositiviSaSoloCioCheEConfigurato() {
            /*  LA DISTINZIONE CHE REGGE TUTTO IL MODELLO. A telescopio spento il
             *  profilo esiste ancora e dice focale, sito, passo del pixel dichiarato.
             *  Ma il passo del pixel del DRIVER, le dimensioni del sensore, il tipo di
             *  matrice reale: quelli non si sanno, e restano nulli. */
            var m = M("profilo-osc");

            Assert.AreEqual(800d, m.Ottica!.FocaleMm, "focale dal profilo vero");
            Assert.AreEqual(6.9d, m.Ottica.Rapporto);
            Assert.AreEqual(3.76d, m.Camera!.PixelUmProfilo, "il passo dichiarato nel profilo");
            Assert.AreEqual("RGGB", m.Camera.MatriceProfilo);

            Assert.IsFalse(m.Camera.Collegato, "falso e' un'informazione: c'e' ma non e' collegata");
            Assert.IsNull(m.Camera.PixelUm, "il passo del DRIVER non si sa a camera spenta");
            Assert.IsNull(m.Camera.LarghezzaPx); Assert.IsNull(m.Camera.AltezzaPx);
            Assert.IsNull(m.Camera.Sensore, "il tipo di sensore lo dichiara il driver, non il profilo");
            Assert.IsNull(m.Camera.Monocromatico, "e quindi nemmeno questo si sa");
            Assert.IsNull(m.Camera.Bit); Assert.IsNull(m.Camera.ElettroniPerAdu);
            Assert.IsNull(m.Camera.ModiLettura); Assert.IsNull(m.Camera.Binning);

            /*  Il guadagno del profilo si sa lo stesso: e' una decisione dell'utente,
             *  non una capacita' del driver. */
            Assert.AreEqual(100, m.Camera.Gain!.Profilo);
            Assert.IsNull(m.Camera.Gain.Max, "gli estremi invece li dichiara la camera");

            /*  ZERO E' UN VALORE, NON UN'ASSENZA. Il banco in campo lavora a guadagno 0,
             *  che sulla 2600MM e' il modo LCG: scartarlo come «non impostato»
             *  cancellerebbe una scelta. E i minuti di attesa del raffreddamento hanno
             *  la stessa natura — zero vuol dire «non aspettare». */
            var campo = M("campo-mono");
            Assert.AreEqual(0, campo.Camera!.Gain!.Profilo, "guadagno 0 = modo LCG");
            Assert.AreEqual(50, campo.Camera.Offset!.Profilo);
            Assert.AreEqual(1d, campo.Camera.Raffreddamento!.MinutiFreddo);
            Assert.AreEqual(5d, campo.Camera.Raffreddamento.MinutiCaldo);
            Assert.AreEqual(-10d, campo.Camera.Raffreddamento.SetpointProfiloC);
        }

        [TestMethod]
        public void Collegato_IlDispositivoDiceLaSua_ESiVedonoTutteEDue() {
            var m = M("collegato");
            Assert.IsTrue(m.Camera!.Collegato);
            Assert.AreEqual(3.76d, m.Camera.PixelUm, "dal driver");
            Assert.AreEqual(3.76d, m.Camera.PixelUmProfilo, "dal profilo");
            Assert.AreEqual(6248, m.Camera.LarghezzaPx);
            Assert.AreEqual(4176, m.Camera.AltezzaPx);
            Assert.AreEqual(16, m.Camera.Bit);
            CollectionAssert.Contains(m.Camera.Binning, "2x2");

            /*  IL GUADAGNO MINIMO E' -25, e non e' un errore di lettura: la 2600MC lo
             *  dichiara cosi'. E' il numero che da solo giustifica di non aver messo un
             *  filtro «solo valori positivi» sugli estremi: l'avrebbe cancellato. */
            Assert.AreEqual(-25, m.Camera.Gain!.Min, "il driver dichiara un minimo negativo");
            Assert.AreEqual(700, m.Camera.Gain.Max);
            Assert.AreEqual(0, m.Camera.Gain.Attuale, "e lavora a zero, che e' un valore");
            Assert.IsNull(m.Camera.Gain.Valori, "questa camera espone un intervallo, non un elenco");
            Assert.AreEqual(240, m.Camera.Offset!.Max);

            /*  Il modello porta i due numeri e NON dice chi ha ragione. Confrontarli e'
             *  lavoro di chi riceve; qui si garantisce solo che siano tutti e due
             *  raggiungibili, perche' e' la sola cosa che rende il confronto possibile. */
            Assert.IsNotNull(m.Camera.PixelUm);
            Assert.IsNotNull(m.Camera.PixelUmProfilo);
        }

        [TestMethod]
        public void MonoControMatrice_SiLeggeDalSensore_NonSiDeduce() {
            var osc = M("collegato");
            Assert.AreEqual("RGGB", osc.Camera!.Sensore, "il nome esatto, non «a colori»");
            Assert.AreEqual("RGGB", osc.Camera.MatriceProfilo, "e qui profilo e driver vanno d'accordo");
            Assert.IsFalse(osc.Camera.Monocromatico);

            /*  Il banco in campo dichiara «None» come disegno di matrice: e' cosi'
             *  che N.I.N.A. scrive un sensore senza matrice, e va portato com'e'
             *  invece di essere tradotto in un booleano che perde quale matrice fosse. */
            var mono = M("campo-mono");
            Assert.AreEqual("None", mono.Camera!.MatriceProfilo);
            Assert.AreEqual(1624d, mono.Ottica!.FocaleMm, "RC8 del banco in campo");
            Assert.AreEqual(8d, mono.Ottica.Rapporto);
            Assert.IsNull(mono.Camera.Monocromatico,
                "a camera scollegata non si sa: il profilo dice il disegno, non il sensore");
        }

        // -------------------------------------------------------------- i filtri

        [TestMethod]
        public void Filtri_VuotoENulloSonoDueCoseDiverse() {
            /*  Sui sette profili di questa macchina l'elenco dei filtri e' vuoto in
             *  tutti e sette: e' un caso normale, non un guasto. Vuoto vuol dire
             *  «ruota configurata senza filtri»; nullo vorrebbe dire «non si e' potuto
             *  leggere». Confonderli farebbe sparire una ruota che c'e'. */
            var senza = M("profilo-osc");
            Assert.IsNotNull(senza.Ruota!.Filtri, "l'elenco esiste");
            Assert.AreEqual(0, senza.Ruota.Filtri!.Count, "ed e' vuoto");
            Assert.IsNull(senza.Ruota.FiltroAttuale, "a ruota scollegata nessuno slot e' montato");

            var con = M("collegato");
            Assert.AreEqual(5, con.Ruota!.Filtri!.Count, "la EFW del banco in campo");
            Assert.AreEqual("LPS_P2 IDAS", con.Ruota.FiltroAttuale!.Nome, "il vetro montato adesso");
            Assert.AreEqual(0, con.Ruota.FiltroAttuale.Posizione);

            /*  E la ruota VERA del banco in campo: sette vetri, LRGB piu' SHO, con gli
             *  offset di fuoco misurati da chi ci riprende davvero. E' il caso che
             *  nessuno dei profili di casa copriva, perche' li' la ruota e' vuota. */
            var campo = M("campo-mono");
            Assert.AreEqual(7, campo.Ruota!.Filtri!.Count);
            CollectionAssert.AreEqual(new[] { "L", "R", "G", "B", "S", "H", "O" },
                campo.Ruota.Filtri.Select(f => f.Nome).ToArray(),
                "nomi e ORDINE come stanno nella ruota");
            CollectionAssert.AreEqual(new int?[] { 0, 1, 2, 3, 4, 5, 6 },
                campo.Ruota.Filtri.Select(f => f.Posizione).ToArray());
            /*  Gli offset di fuoco sono NEGATIVI, e devono restare tali: un filtro a
             *  banda stretta mette a fuoco piu' dentro. Un lettore che scartasse i
             *  numeri non positivi li perderebbe tutti tranne il primo. */
            Assert.AreEqual(0, campo.Ruota.Filtri[0].OffsetFuoco, "la luminanza e' il riferimento");
            Assert.AreEqual(-25, campo.Ruota.Filtri[4].OffsetFuoco, "SII, il piu' lontano");
            Assert.IsTrue(campo.Ruota.Filtri.Skip(1).All(f => f.OffsetFuoco < 0));
            /*  E l'autofocus cambia binning fra banda larga e banda stretta. */
            Assert.AreEqual("1x1", campo.Ruota.Filtri[0].AutofocusBinning);
            Assert.AreEqual("2x2", campo.Ruota.Filtri[5].AutofocusBinning);
        }

        [TestMethod]
        public void Filtri_PortanoIlNomeELaPosizione_MaiUnaBanda() {
            /*  LA REGOLA PIU' IMPORTANTE DI QUESTO CONTRATTO. N.I.N.A. sa che quel
             *  filtro si chiama «Ha 3nm» e sta in posizione 1. Non sa che sia centrato
             *  su 656,3 nm con 3 nm di larghezza — quello lo sa il catalogo, dall'altra
             *  parte del confine. Un adattatore che ricavasse nanometri da un nome
             *  starebbe inventando fisica, e «Ha» puo' essere un 3 nm o un 12 nm.
             *
             *  Questa verifica guarda i NOMI DEI CAMPI del tipo: se un giorno comparisse
             *  una banda, una larghezza o una lunghezza d'onda, diventa rossa. */
            var vietati = new[] { "nm", "banda", "larghezza", "lunghezza", "onda", "fwhm", "centro" };
            var campi = typeof(Filtro).GetProperties()
                .Select(p => p.Name.ToLowerInvariant()).ToList();
            var colpevoli = campi.Where(c => vietati.Any(v => c.Contains(v))).ToList();
            Assert.AreEqual(0, colpevoli.Count,
                "il filtro ha preso una proprieta' fisica che N.I.N.A. non conosce: " +
                string.Join(", ", colpevoli));

            var f = M("collegato").Ruota!.Filtri![3];
            Assert.AreEqual("HA 7NM", f.Nome,
                "il nome si porta com'e', «7NM» compreso: sono lettere, non nanometri");
            Assert.AreEqual(6d, f.AutofocusPosaS);
            Assert.AreEqual("2x2", f.AutofocusBinning);
        }

        // ------------------------------------------------------------ sito e guida

        [TestMethod]
        public void Sito_NeEsistonoDue_EIlModelloLiPortaEntrambi() {
            var m = M("collegato");
            Assert.AreEqual(45.5d, m.Sito!.Lat!.Value, 1e-9, "dal profilo, arrotondato");
            Assert.IsNotNull(m.Montatura!.Sito, "e la montatura ne dichiara uno suo");
            Assert.AreEqual(45.49988888888889d, m.Montatura.Sito!.Lat!.Value, 1e-12,
                "dalla montatura, con tutte le cifre che ha");

            /*  I DUE NUMERI NON SONO UGUALI, e il modello non li appiana: differiscono
             *  di un decimillesimo di grado perche' il profilo li salva arrotondati.
             *  Sono undici metri, cioe' niente, ma un confronto scritto con l'uguale
             *  griderebbe a una differenza che non c'e'. Il modello porta tutti e due i
             *  numeri; la tolleranza se la sceglie chi confronta. */
            Assert.AreNotEqual(m.Sito.Lat, m.Montatura.Sito.Lat, "arrotondati diversamente");
            Assert.IsTrue(Math.Abs(m.Sito.Lat!.Value - m.Montatura.Sito.Lat!.Value) < 1e-3,
                "ma dicono lo stesso posto");

            /*  Quando i due non coincidono qualcuno sta per riprendere con effemeridi
             *  sbagliate. Il modello non lo giudica: lo rende visibile. */
            var solo = M("profilo-osc");
            Assert.IsNotNull(solo.Sito, "il sito del profilo esiste anche a montatura spenta");
            Assert.IsNull(solo.Montatura, "quello della montatura no");
        }

        [TestMethod]
        public void Rms_PortaTutteEDueLeUnita_SenzaConvertire() {
            /*  N.I.N.A. espone ogni valore gia' in pixel E in secondi d'arco. Portarle
             *  tutte e due costa niente ed evita la conversione, che e' il posto dove
             *  un giorno si moltiplica due volte. */
            /*  PHD2 sul banco in campo: collegato, con la sua scala vera. */
            var g = M("collegato").Guida!;
            Assert.IsTrue(g.Collegato);
            Assert.AreEqual("PHD2", g.Nome);
            Assert.AreEqual(0.476289d, g.ScalaArcsecPx, "la scala della ASI120 dietro il 71F");

            /*  E QUI C'E' IL CASO CHE NON AVREI SAPUTO INVENTARE: l'RMS arriva a ZERO
             *  su tutti e cinque i valori, perche' PHD2 era connesso ma fermo. Le due
             *  unita' ci sono entrambe e sono entrambe zero.
             *
             *  Zero non e' inseguimento perfetto: quasi sempre vuol dire «non sta
             *  guidando». Il modello non lo interpreta, perche' lo stato del guider non
             *  e' fra le proprieta' che GuiderInfo espone; lo dichiara nel commento e lo
             *  lascia a chi consuma. Una fixture costruita a tavolino avrebbe avuto
             *  numeri belli e questa ambiguita' non l'avrebbe mai mostrata. */
            Assert.IsNotNull(g.Rms, "l'oggetto c'e'");
            Assert.AreEqual(0d, g.Rms!.Totale!.Px, "e vale zero, perche' non stava guidando");
            Assert.AreEqual(0d, g.Rms.Totale.Arcsec);
            Assert.IsNotNull(g.Rms.Ra); Assert.IsNotNull(g.Rms.Dec);
            Assert.IsNotNull(g.Rms.PiccoRa); Assert.IsNotNull(g.Rms.PiccoDec);

            /*  La prova che le due unita' NON sono la stessa cosa: la forma le tiene
             *  separate anche quando i numeri coincidono. */
            var j = JsonNode.Parse(Testo("collegato"))!.AsObject();
            j["guida"]!["rms"]!["totale"] = new JsonObject { ["px"] = 0.41, ["arcsec"] = 0.195 };
            var mosso = SetupModel.Leggi(j.ToJsonString())!.Guida!;
            Assert.AreEqual(0.41d, mosso.Rms!.Totale!.Px);
            Assert.AreEqual(0.195d, mosso.Rms.Totale.Arcsec,
                "con una scala di 0,48 arcsec/px i secondi d'arco sono il numero piu' PICCOLO");

            Assert.IsNull(M("profilo-osc").Guida, "a N.I.N.A. spenta non c'e' nemmeno il dispositivo");
        }

        // --------------------------------------------- capacita' e stato vivo

        [TestMethod]
        public void Capacita_ScollegatoNonEIgnoto() {
            /*  Tre stati diversi, e il modello li tiene distinti: il rotatore di questo
             *  caso c'e' nel profilo ma non e' collegato, quindi `collegato: false` e
             *  tutto il resto nullo. Se fosse `collegato: null` vorrebbe dire che non si
             *  e' potuto nemmeno chiedere. */
            var m = M("collegato");

            /*  PRIMO STATO: dichiarato ma spento. Il meteo c'e' nel profilo, non e'
             *  collegato, e quindi non misura niente. `collegato: false` e' una
             *  informazione; se fosse `null` vorrebbe dire che non si e' potuto
             *  nemmeno chiedere. */
            Assert.IsFalse(m.Meteo!.Collegato);
            Assert.IsNull(m.Meteo.Sqm);

            /*  SECONDO STATO, ED E' QUELLO CHE MANCAVA: collegato, e con un numero che
             *  NON SIGNIFICA NIENTE. Il rotatore manuale — quello che usa chiunque non
             *  ne abbia uno fisico — risulta connesso e dichiara posizione 0 con
             *  `Synced` falso: nessuno gli ha ancora detto dove si trova, perche' non
             *  e' partita nessuna sequenza con un bersaglio.
             *
             *  Chi leggesse la sola posizione crederebbe che il campo e' dritto e ci
             *  costruirebbe sopra un'inquadratura. E' il motivo per cui
             *  `sincronizzato` esiste: senza, quello zero passerebbe per una misura. */
            Assert.IsTrue(m.Rotatore!.Collegato, "il rotatore manuale e' connesso");
            Assert.AreEqual("Rotator Manuale", m.Rotatore.Nome);
            Assert.AreEqual(0d, m.Rotatore.PosizioneGradi, "e dichiara zero");
            Assert.IsFalse(m.Rotatore.Sincronizzato,
                "ma non e' sincronizzato: quello zero non e' un angolo misurato");
            Assert.IsTrue(m.Rotatore.PuoInvertire);
            Assert.IsTrue(m.Rotatore.Invertito, "e su questo banco il verso e' invertito");

            /*  TERZO STATO: collegato e con dati veri. */
            Assert.IsTrue(m.Camera!.Collegato);
            Assert.IsNotNull(m.Camera.Sensore);

            Assert.IsTrue(m.Montatura!.PuoTornareACasa);
            Assert.IsTrue(m.Montatura.PuoParcheggiare);
            Assert.IsFalse(m.Montatura.InParcheggio);
            Assert.IsFalse(m.Montatura.Insegue, "ferma, come dev'essere di giorno");
            Assert.IsTrue(m.Camera!.Raffreddamento!.Disponibile);

            /*  Il focheggiatore dichiara un passo di 600000. ASCOM lo definirebbe in
             *  micrometri, che sarebbero sessanta centimetri per passo: il campo si
             *  chiama `passo` e non `passo_um` proprio per non affermare un'unita' che
             *  il valore vero smentisce. */
            Assert.AreEqual(600000d, m.Focheggiatore!.Passo);
            Assert.AreEqual(10395, m.Focheggiatore.Posizione);
        }

        [TestMethod]
        public void StatoVivo_CEOppureNonCE_MaiUnNumeroDiComodo() {
            /*  Lo stato vivo che il banco in campo aveva davvero: la camera a 28,2
             *  gradi con il raffreddamento appena acceso. Non e' una bella temperatura
             *  di lavoro, ed e' proprio per questo che vale come dato: e' quella vera
             *  delle 13:17, non quella che avrei scritto io. */
            var con = M("collegato");
            Assert.AreEqual(25.5d, con.Camera!.Raffreddamento!.TemperaturaC!.Value, 0.01);
            Assert.IsTrue(con.Camera.Raffreddamento.Acceso, "stava scendendo verso il setpoint");
            Assert.AreEqual(37.21d, con.Focheggiatore!.TemperaturaC!.Value, 0.01);

            /*  La stazione meteo non c'era, quindi niente SQM e niente seeing. Nullo
             *  vuol dire «non misurato», e va benissimo: una prescrizione fatta su un
             *  fondo cielo stimato e' meglio di una fatta su un numero inventato qui. */
            Assert.IsFalse(con.Meteo!.Collegato);
            Assert.IsNull(con.Meteo.Sqm);
            Assert.IsNull(con.Meteo.FwhmArcsec);

            var senza = M("profilo-osc");
            Assert.IsNull(senza.Meteo, "e a N.I.N.A. spenta non c'e' nemmeno il dispositivo");
            Assert.IsNull(senza.Camera!.Raffreddamento!.TemperaturaC);
        }

        [TestMethod]
        public void ChiaveAssente_NonDiventaZero() {
            var j = JsonNode.Parse(Testo("collegato"))!.AsObject();
            j["camera"]!.AsObject().Remove("pixel_um");
            j["ottica"]!.AsObject().Remove("focale_mm");
            j["sito"]!.AsObject().Remove("lat");
            j.Remove("letto");

            var m = SetupModel.Leggi(j.ToJsonString())!;
            Assert.IsNull(m.Camera!.PixelUm, "un pixel assente non e' un pixel da zero micrometri");
            Assert.IsNull(m.Ottica!.FocaleMm, "una focale assente non e' una focale di zero millimetri");
            Assert.IsNull(m.Sito!.Lat, "una latitudine assente non e' l'equatore");
            Assert.IsNull(m.Letto);
            Assert.AreEqual(4176, m.Camera.AltezzaPx, "e il resto arriva come prima");
        }

        // ------------------------------------------- il contratto e il trasporto

        [TestMethod]
        public void IlContrattoNonSaNienteDiComeViaggia() {
            /*  Il trasporto non e' ancora deciso, e il modello non deve costringere la
             *  scelta. Qui si verifica che nessun tipo del contratto nomini un
             *  trasporto: niente HTTP, niente WebView, niente socket, niente token.
             *  Se un giorno comparisse, la scelta sarebbe gia' stata fatta di nascosto. */
            var vietati = new[] { "http", "url", "uri", "endpoint", "token", "webview",
                                  "socket", "porta", "auth", "cookie", "header" };
            var colpevoli = new List<string>();
            foreach (var t in TipiDelPonte.Nel(SPAZIO)) {
                foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance |
                                                  BindingFlags.DeclaredOnly)) {
                    var n = p.Name.ToLowerInvariant();
                    if (vietati.Any(v => n.Contains(v))) colpevoli.Add(t.Name + "." + p.Name);
                }
            }
            Assert.AreEqual(0, colpevoli.Count,
                "il contratto ha cominciato a sapere come viaggia:\n  " + string.Join("\n  ", colpevoli));
        }

        [TestMethod]
        public void IlContrattoHaEsattamenteQuestiTipi() {
            /*  Un numero esatto, come per il contratto della sequenza: cosi' un tipo che
             *  compare o sparisce si vede, invece di passare in mezzo. */
            var attesi = new[] { "SetupModel", "Applicazione", "Profilo", "Sito", "Ottica",
                                 "Camera", "Scala", "Raffreddamento", "Montatura", "Ruota",
                                 "Filtro", "Focheggiatore", "Rotatore", "Guida", "Rms",
                                 "Misura", "Meteo" };
            var trovati = TipiDelPonte.Nel(SPAZIO)
                .Select(t => t.Name).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(attesi.OrderBy(x => x, StringComparer.Ordinal).ToArray(), trovati,
                "tipi del contratto: " + string.Join(", ", trovati));
        }
    }
}
