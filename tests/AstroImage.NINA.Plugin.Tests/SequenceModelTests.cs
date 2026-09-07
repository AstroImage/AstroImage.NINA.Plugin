using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using AstroImage.NINA.Plugin.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace AstroImage.NINA.Plugin.Tests {

    /*  I TEST DEL CONTRATTO, E DA DOVE VENGONO LE FIXTURE.
     *
     *  I file in tests/Fixtures NON sono scritti a mano: sono usciti dal motore vero
     *  di AstroImage-Strategy, facendo girare `sequenceModel` su banchi ottici veri
     *  del suo catalogo. E' l'unica cosa che rende utile un test sul contratto: un
     *  JSON inventato prova solo che chi l'ha scritto e chi legge hanno avuto la
     *  stessa idea sbagliata.
     *
     *  Cinque casi, scelti per coprire i rami che il contratto ha davvero:
     *    mono      - monocromatica, cinque filtri, niente offset, capacita' di serie;
     *    osc       - camera a matrice: i canali larghi si fondono in un blocco solo,
     *                e cio' che non si e' potuto fondere e' dichiarato in nonFusi;
     *    osc-hdr   - matrice con serie corta: due pose diverse sullo stesso vetro;
     *    completo  - campo spostato, rotazione, rotatore dichiarato, dither ogni 3;
     *    scarno    - il caso povero: niente sito, niente autoguida, niente ottica.
     *
     *  Il motore, il catalogo e i dati fotometrici restano fuori da questo
     *  repository: il .gitignore li vieta per nome. Quindi le fixture sono file
     *  statici versionati, e vanno rigenerate da chi ha il motore quando il
     *  contratto cambia.
     */
    [TestClass]
    public class SequenceModelTests {

        private static string CartellaFixture =>
            Path.Combine(Path.GetDirectoryName(typeof(SequenceModelTests).Assembly.Location)!, "Fixtures");

        private static string Testo(string nome) =>
            File.ReadAllText(Path.Combine(CartellaFixture, nome + ".json"));

        /// <summary>Tutte le fixture presenti, cosi' che aggiungerne una la metta subito sotto test.</summary>
        public static IEnumerable<object[]> Fixture =>
            Directory.EnumerateFiles(CartellaFixture, "*.json")
                     .Select(f => new object[] { Path.GetFileNameWithoutExtension(f) })
                     .OrderBy(x => (string)x[0]);

        /*  IL CONFRONTO, e perche' non e' un confronto di stringhe.
         *
         *  Due JSON dicono la stessa cosa anche se una chiave e' assente da una parte
         *  e nulla dall'altra: sono i due modi di scrivere "questo dato non c'e'".
         *  La forma canonica toglie i nulli e ordina le chiavi, cosi' il confronto
         *  parla di DATI. L'ordine delle chiavi ha un test suo, piu' sotto, perche'
         *  e' una proprieta' diversa e va persa separatamente. */
        private static string Canonico(JsonNode? n) {
            switch (n) {
                case JsonObject o:
                    var vive = o.Where(p => p.Value is not null)
                                .OrderBy(p => p.Key, StringComparer.Ordinal)
                                .Select(p => JsonSerializer.Serialize(p.Key) + ":" + Canonico(p.Value));
                    return "{" + string.Join(",", vive) + "}";
                case JsonArray a:
                    return "[" + string.Join(",", a.Select(Canonico)) + "]";
                case null:
                    return "null";
                default:
                    return n.ToJsonString();
            }
        }

        private static string Canonico(string json) => Canonico(JsonNode.Parse(json));

        // ------------------------------------------------------------------ il giro

        [TestMethod]
        [DynamicData(nameof(Fixture))]
        public void OgniFixture_SiLegge(string nome) {
            var m = SequenceModel.Leggi(Testo(nome));
            Assert.IsNotNull(m, "il JSON del motore non si e' trasformato in un modello");
            Assert.IsNotNull(m!.Bersaglio, "senza bersaglio non c'e' niente da riprendere");
            Assert.IsNotNull(m.Cap, "le capacita' del banco sono sempre nel contratto");
            Assert.IsTrue(m.Blocchi.Count > 0, "una sequenza senza blocchi non e' una sequenza");
        }

        [TestMethod]
        [DynamicData(nameof(Fixture))]
        public void OgniFixture_FaIlGiroSenzaPerdereNiente(string nome) {
            var partenza = Testo(nome);
            var ritorno = SequenceModel.Leggi(partenza)!.Scrivi();
            Assert.AreEqual(Canonico(partenza), Canonico(ritorno),
                "il giro JSON -> modello -> JSON ha cambiato i dati");
        }

        /*  PIU' FORTE DEL GIRO: il file torna IDENTICO, byte per byte.
         *
         *  Non e' pignoleria estetica. Se il testo riesce a coincidere significa che
         *  coincidono anche le cose che un confronto sui dati non guarda: l'ordine
         *  delle chiavi, il modo di scrivere i numeri, il rientro, il trattamento
         *  degli accenti. Il giorno in cui il ponte rimandasse indietro un modello,
         *  chi lo riceve non troverebbe un file diverso da spiegare.
         *
         *  Vale perche' le fixture non hanno chiavi assenti: il motore le riduce a
         *  null. Quel caso ha il suo test a parte, ed e' l'unico dove il testo
         *  cambia -- il modello riscrive `null` dove il JSON non aveva la chiave. */
        [TestMethod]
        [DynamicData(nameof(Fixture))]
        public void OgniFixture_TornaIdenticaAncheComeTesto(string nome) {
            var partenza = Testo(nome).Replace("\r\n", "\n").TrimEnd('\n');
            var ritorno = SequenceModel.Leggi(partenza)!.Scrivi().Replace("\r\n", "\n").TrimEnd('\n');
            Assert.AreEqual(partenza, ritorno, "il file riscritto non e' quello di partenza");
        }

        // ------------------------------------------------------- i casi, uno per uno

        [TestMethod]
        public void Mono_OgniCanaleHaIlSuoVetro() {
            var m = SequenceModel.Leggi(Testo("mono"))!;
            Assert.IsFalse(m.Ottica!.Matrice, "il caso mono non deve avere il sensore a matrice");
            Assert.IsTrue(m.Blocchi.Count >= 4, "cinque filtri, non uno");
            Assert.IsTrue(m.Blocchi.All(b => b.Canali.Count == 1),
                "sul monocromatico nessun blocco copre piu' di un canale: non c'e' niente da fondere");
            CollectionAssert.AllItemsAreUnique(m.Blocchi.Select(b => b.Filtro).ToList(),
                "due blocchi sullo stesso vetro sul mono vorrebbero dire una fusione mancata");
            Assert.IsNull(m.Bersaglio!.Off, "questo caso non ha spostamento di inquadratura");
            Assert.IsFalse(m.Bersaglio.Spostato);
        }

        [TestMethod]
        public void Osc_IcanaliLarghiDiventanoUnaRipresaSola() {
            var m = SequenceModel.Leggi(Testo("osc"))!;
            Assert.IsTrue(m.Ottica!.Matrice, "il caso OSC deve dichiarare il sensore a matrice");
            Assert.IsTrue(m.Blocchi.Any(b => b.Canali.Count > 1),
                "su una matrice almeno un blocco deve coprire piu' canali: e' la fusione");
            Assert.IsTrue(m.NonFusi.Count > 0,
                "questo caso ha una fusione impossibile, e il motore la dichiara invece di appianarla");
        }

        [TestMethod]
        public void OscHdr_LaSerieCortaRestaUnBloccoASe() {
            var m = SequenceModel.Leggi(Testo("osc-hdr"))!;
            Assert.IsTrue(m.Ottica!.Matrice);
            var perFiltro = m.Blocchi.GroupBy(b => b.Filtro).Where(g => g.Count() > 1).ToList();
            Assert.IsTrue(perFiltro.Any(),
                "la serie corta e la lunga vanno allo stesso vetro e restano due blocchi");
            Assert.IsTrue(perFiltro.All(g => g.Select(b => b.Sec).Distinct().Count() > 1),
                "due blocchi sullo stesso filtro hanno senso solo se la posa e' diversa");
            Assert.IsTrue(m.NonFusi.Count > 0, "e la cosa va dichiarata");
        }

        [TestMethod]
        public void Completo_PortaSpostamentoRotazioneECapacitaDichiarate() {
            var m = SequenceModel.Leggi(Testo("completo"))!;
            var b = m.Bersaglio!;

            Assert.IsNotNull(b.Off, "questo caso ha un campo spostato");
            Assert.IsTrue(b.Off!.Du != 0 || b.Off.Dv != 0, "uno spostamento nullo non e' uno spostamento");
            Assert.IsTrue(b.Spostato, "e il motore lo dichiara");
            Assert.AreNotEqual(0d, b.Rot, "questo caso e' ruotato");

            Assert.IsTrue(m.Cap!.Rotatore, "il rotatore e' DICHIARATO, non dedotto dall'angolo");
            Assert.IsNotNull(m.Dither);
            Assert.AreNotEqual(2d, m.Dither!.OgniPose, "questo caso non usa il dithering di serie");

            /*  Le coordinate sono quelle del centro SCELTO, e il modello le porta gia'
             *  risolte: e' la ragione per cui il disegnatore della sequenza non ha
             *  bisogno della scheda del bersaglio. Qui si controlla solo che siano
             *  numeri sensati, non si ricalcolano: rifare il conto da questa parte
             *  del confine sarebbe esattamente cio' che il modello serve a evitare. */
            Assert.IsTrue(b.RaDeg >= 0 && b.RaDeg < 360, "ascensione retta fuori giro");
            Assert.IsTrue(b.DecDeg >= -90 && b.DecDeg <= 90, "declinazione fuori scala");
        }

        [TestMethod]
        public void Scarno_DoveNonC_eNienteRestaNiente() {
            var m = SequenceModel.Leggi(Testo("scarno"))!;

            Assert.IsNull(m.Sito, "senza sito il modello non deve inventare un punto sulla Terra");
            Assert.IsNull(m.Dither, "senza autoguida non c'e' dithering, e non c'e' un dithering a zero");
            Assert.IsFalse(m.Cap!.Guida);

            var o = m.Ottica!;
            Assert.IsNull(o.Tel); Assert.IsNull(o.Camera); Assert.IsNull(o.Montatura);
            Assert.IsNull(o.FocaleMm, "una focale sconosciuta non e' una focale di zero millimetri");
            Assert.IsNull(o.PixelUm); Assert.IsNull(o.Riduttore); Assert.IsNull(o.ScalaArcsecPx);

            Assert.IsTrue(m.Blocchi.Count > 0, "le pose ci sono lo stesso: si riprende anche cosi'");
        }

        // ------------------------------------------- i rami che nessuna fixture copre

        /*  I due test che seguono non usano un JSON inventato: prendono una fixture
         *  VERA e la degradano nel modo esatto in cui il motore puo' degradarla. E'
         *  la differenza fra provare qualcosa e disegnarselo addosso. */

        [TestMethod]
        public void ChiaveAssente_NonDiventaZero() {
            /*  Il motore non riduce sempre a null: `tel:dv&&dv.t&&dv.t.name` e
             *  `bin:dv?dv.bin:1` producono `undefined` in certi rami, e JSON.stringify
             *  la chiave la TOGLIE. Un int non annullabile la leggerebbe come zero:
             *  binning zero, posa di zero secondi, zero pose. Nessuno di quei valori
             *  e' un dato, e nessuno griderebbe. */
            var j = JsonNode.Parse(Testo("mono"))!.AsObject();
            j["ottica"]!.AsObject().Remove("tel");
            j["ottica"]!.AsObject().Remove("bin");
            j["blocchi"]![0]!.AsObject().Remove("sec");
            j["blocchi"]![0]!.AsObject().Remove("n");
            j.Remove("notte");

            var m = SequenceModel.Leggi(j.ToJsonString())!;
            Assert.IsNull(m.Notte, "notte assente non e' la notte numero zero");
            Assert.IsNull(m.Ottica!.Tel);
            Assert.IsNull(m.Ottica.Bin, "binning assente non e' binning zero");
            Assert.IsNull(m.Blocchi[0].Sec, "posa assente non e' una posa da zero secondi");
            Assert.IsNull(m.Blocchi[0].N, "pose assenti non sono zero pose");

            /*  E il resto del modello non ne risente: una chiave che manca non
             *  compromette quelle accanto. */
            Assert.IsNotNull(m.Ottica.Camera);
            Assert.IsTrue(m.Blocchi.Count > 1);
        }

        [TestMethod]
        public void Sentinella_MenoUno_ArrivaIntatta() {
            /*  Quando il modo di guadagno non dichiara guadagno e offset, il motore
             *  scrive -1. Non e' un guadagno negativo: e' "non specificato", e deve
             *  arrivare fin qui riconoscibile invece di essere normalizzato a zero da
             *  qualcuno che voleva essere gentile. */
            var j = JsonNode.Parse(Testo("mono"))!.AsObject();
            var b0 = j["blocchi"]![0]!.AsObject();
            b0["gain"] = -1; b0["offset"] = -1; b0["modo"] = null;

            var m = SequenceModel.Leggi(j.ToJsonString())!;
            Assert.AreEqual(-1, m.Blocchi[0].Gain);
            Assert.AreEqual(-1, m.Blocchi[0].Offset);
            Assert.IsNull(m.Blocchi[0].Modo);
            Assert.AreEqual(-1, JsonNode.Parse(m.Scrivi())!["blocchi"]![0]!["gain"]!.GetValue<int>(),
                "e riesce intatta anche dall'altra parte");
        }

        [TestMethod]
        public void CampoSconosciuto_SopravviveAlGiro() {
            /*  IL PONTE NON BUTTA VIA CIO' CHE NON CAPISCE.
             *
             *  Il giorno in cui Strategy aggiunge un campo, un modello scritto senza
             *  JsonExtensionData lo perde in silenzio: legge, scarta, riscrive senza.
             *  Chi guarda il file rimanda indietro meno di quello che aveva ricevuto e
             *  non c'e' un punto in cui la cosa si veda. `cap` in particolare arriva
             *  da un deposito del browser, quindi chiavi in piu' sono possibili oggi,
             *  non in futuro. */
            var j = JsonNode.Parse(Testo("completo"))!.AsObject();
            j["campoNuovoDiStrategy"] = 42;
            j["bersaglio"]!.AsObject()["catalogo"] = "Sh2";
            j["cap"]!.AsObject()["cupola"] = true;
            j["blocchi"]![0]!.AsObject()["priorita"] = 3;

            var dopo = JsonNode.Parse(SequenceModel.Leggi(j.ToJsonString())!.Scrivi())!;
            Assert.AreEqual(42, dopo["campoNuovoDiStrategy"]!.GetValue<int>());
            Assert.AreEqual("Sh2", dopo["bersaglio"]!["catalogo"]!.GetValue<string>());
            Assert.IsTrue(dopo["cap"]!["cupola"]!.GetValue<bool>());
            Assert.AreEqual(3, dopo["blocchi"]![0]!["priorita"]!.GetValue<int>());
        }

        [TestMethod]
        public void OrdineDelleChiavi_ComeLoScriveIlMotore() {
            /*  L'ordine non cambia il significato del JSON, ma cambia la leggibilita'
             *  di un confronto fra due file, e qui costa solo tenere le proprieta'
             *  nell'ordine in cui il motore le costruisce. Se un giorno qualcuno
             *  spostasse una proprieta' del modello, questo test lo dice subito. */
            var m = SequenceModel.Leggi(Testo("completo"))!;
            var dopo = JsonNode.Parse(m.Scrivi())!.AsObject();

            CollectionAssert.AreEqual(
                new[] { "notte", "nome", "bersaglio", "ottica", "sito", "cap", "blocchi", "nonFusi", "dither", "flip" },
                dopo.Select(p => p.Key).ToArray());
            CollectionAssert.AreEqual(
                new[] { "nome", "rot", "off", "ra_deg", "dec_deg", "spostato" },
                dopo["bersaglio"]!.AsObject().Select(p => p.Key).ToArray());
            CollectionAssert.AreEqual(
                new[] { "tel", "camera", "montatura", "focale_mm", "rapporto", "pixel_um",
                        "riduttore", "scala_arcsec_px", "bin", "matrice" },
                dopo["ottica"]!.AsObject().Select(p => p.Key).ToArray());
            CollectionAssert.AreEqual(
                new[] { "raffredda", "ruota", "focheggiatore", "guida", "rotatore", "home",
                        "tempC", "minutiFreddo", "minutiCaldo" },
                dopo["cap"]!.AsObject().Select(p => p.Key).ToArray());
            CollectionAssert.AreEqual(
                new[] { "canali", "filtro", "sec", "n", "gain", "offset", "modo" },
                dopo["blocchi"]![0]!.AsObject().Select(p => p.Key).ToArray());
        }
    }
}
