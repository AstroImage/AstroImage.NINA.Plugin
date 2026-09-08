using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using AstroImage.NINA.Plugin.Models;
using AstroImage.NINA.Plugin.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace AstroImage.NINA.Plugin.Tests {

    /*  LA TRADUZIONE, PROVATA SULLE FIXTURE VERE E SENZA N.I.N.A. ACCANTO.
     *
     *  Questi test girano nello stesso banco del contratto, quello che non ha una sola
     *  DLL di N.I.N.A. nella cartella. Non e' una comodita': e' la dimostrazione che la
     *  parte pericolosa del builder — le conversioni e i campi mancanti — non dipende
     *  da N.I.N.A. e si puo' guardare per intero.
     *
     *  Cio' che resta dall'altra parte, il montaggio degli oggetti, non converte
     *  niente: prende questi valori e li assegna.
     */
    [TestClass]
    public class TraduzioneTests {

        private static string CartellaFixture =>
            Path.Combine(Path.GetDirectoryName(typeof(TraduzioneTests).Assembly.Location)!, "Fixtures");

        private static string Testo(string nome) =>
            File.ReadAllText(Path.Combine(CartellaFixture, nome + ".json"));

        private static SequenceModel Modello(string nome) => SequenceModel.Leggi(Testo(nome))!;

        public static IEnumerable<object[]> Fixture =>
            Directory.EnumerateFiles(CartellaFixture, "*.json")
                     .Select(f => new object[] { Path.GetFileNameWithoutExtension(f) })
                     .OrderBy(x => (string)x[0]);

        // ------------------------------------------------------------------ il giro

        [TestMethod]
        [DynamicData(nameof(Fixture))]
        public void OgniFixture_SiTraduceInQualcosaDiCostruibile(string nome) {
            var r = Traduzione.Traduci(Modello(nome));
            Assert.IsTrue(r.Costruibile, "niente da costruire: " + string.Join(" · ", r.Scartati));
            Assert.IsTrue(r.Blocchi.Count > 0);
            Assert.IsFalse(string.IsNullOrWhiteSpace(r.NomeBersaglio));
            Assert.AreEqual(0, r.Scartati.Count,
                "una fixture del motore non deve avere blocchi scartati: " + string.Join(" · ", r.Scartati));
        }

        [TestMethod]
        [DynamicData(nameof(Fixture))]
        public void OgniFixture_NonPerdeNessunBlocco(string nome) {
            var m = Modello(nome);
            var r = Traduzione.Traduci(m);
            Assert.AreEqual(m.Blocchi.Count, r.Blocchi.Count, "un blocco si e' perso per strada");

            /*  E NELL ORDINE IN CUI IL MOTORE LI HA MESSI. L ordine non e' estetico: la
             *  serie corta va in testa al suo gruppo perche' si fa il nucleo e poi si
             *  posa lungo. Riordinare qui vorrebbe dire decidere, ed e' proprio cio' che
             *  questo lato del confine non deve fare. */
            for (int i = 0; i < m.Blocchi.Count; i++) {
                Assert.AreEqual(m.Blocchi[i].Sec, r.Blocchi[i].Secondi, $"blocco {i}: posa fuori posto");
                Assert.AreEqual(m.Blocchi[i].N, r.Blocchi[i].Pose, $"blocco {i}: pose fuori posto");
            }
        }

        [TestMethod]
        [DynamicData(nameof(Fixture))]
        public void OgniFixture_PortaIlCentroScelto(string nome) {
            var m = Modello(nome);
            var r = Traduzione.Traduci(m);
            /*  In GRADI, gli stessi che il motore ha scritto. Non si converte in ore qui:
             *  la conversione la fa N.I.N.A., e una conversione in meno e' un fattore
             *  quindici in meno che puo' andare storto. */
            Assert.AreEqual(m.Bersaglio!.RaDeg, r.RaGradi, 1e-9);
            Assert.AreEqual(m.Bersaglio.DecDeg, r.DecGradi, 1e-9);
            Assert.AreEqual(m.Bersaglio.Rot, r.AngoloDiPosa, 1e-9);
            Assert.IsTrue(r.RaGradi >= 0 && r.RaGradi < 360, "ascensione retta fuori giro");
            Assert.IsTrue(r.DecGradi >= -90 && r.DecGradi <= 90, "declinazione fuori scala");
        }

        // ------------------------------------------------------- i casi, uno per uno

        [TestMethod]
        public void Mono_UnFiltroPerBlocco() {
            var r = Traduzione.Traduci(Modello("mono"));
            Assert.IsTrue(r.CambiaFiltro, "il banco mono dichiara la ruota");
            Assert.IsTrue(r.Blocchi.All(b => !string.IsNullOrWhiteSpace(b.Filtro)));
            CollectionAssert.AllItemsAreUnique(r.Blocchi.Select(b => b.Filtro).ToList());
            Assert.AreEqual(1, r.Binning);
        }

        [TestMethod]
        public void Completo_SpostamentoRotazioneEDither() {
            var m = Modello("completo");
            var r = Traduzione.Traduci(m);
            Assert.IsTrue(r.Spostato, "questo caso ha il campo spostato");
            Assert.AreNotEqual(0d, r.AngoloDiPosa);
            Assert.AreEqual(3, r.DitherOgniPose, "il dithering di questo caso e' ogni 3 pose");
            Assert.AreEqual(-15d, r.TemperaturaC);
            Assert.AreEqual(12d, r.MinutiFreddo);
            Assert.AreEqual(15d, r.MinutiCaldo);
            /*  I nomi dei filtri sono quelli incisi sulla ruota di chi riprende, non i
             *  nostri: arrivano fin qui senza essere tradotti. */
            CollectionAssert.Contains(r.Blocchi.Select(b => b.Filtro).ToList(), "Ha 3nm");
        }

        [TestMethod]
        public void Scarno_SenzaGuidaNessunDither_SenzaCapacitaNessunaIstruzione() {
            var r = Traduzione.Traduci(Modello("scarno"));
            Assert.IsFalse(r.Guida);
            Assert.IsNull(r.DitherOgniPose, "senza guida non si dithera, e non si dithera ogni zero pose");
            Assert.IsFalse(r.Raffredda); Assert.IsFalse(r.CambiaFiltro);
            Assert.IsFalse(r.Focheggia); Assert.IsFalse(r.TornaACasa);
            /*  Senza ruota il filtro non si tocca: il modello lo dice lo stesso, ma
             *  cambiare vetro su un banco che non ha la ruota e' un'istruzione
             *  ineseguibile. */
            Assert.IsTrue(r.Blocchi.All(b => b.Filtro is null));
            Assert.IsTrue(r.Blocchi.Count > 0, "le pose ci sono lo stesso: si riprende anche cosi'");
        }

        [TestMethod]
        public void Osc_IlBloccoFusoPortaITreCanali() {
            var r = Traduzione.Traduci(Modello("osc"));
            var fuso = r.Blocchi.FirstOrDefault(b => b.Canali.Count > 1);
            Assert.IsNotNull(fuso, "su una matrice i canali larghi sono una ripresa sola");
            Assert.IsTrue(fuso!.Etichetta.Contains("+"), "l'etichetta dice quali canali copre: " + fuso.Etichetta);
        }

        // ------------------------------------------------ cio' che NON si deve fare

        [TestMethod]
        public void PosaMancante_IlBloccoNonSiCostruisce() {
            /*  L ERRORE PIU SILENZIOSO POSSIBILE. Una posa che manca, letta come zero,
             *  darebbe una sequenza che parte, scatta e non mette niente nei file. Il
             *  blocco non si costruisce, e si dice perche'. */
            var j = JsonNode.Parse(Testo("mono"))!.AsObject();
            j["blocchi"]![0]!.AsObject().Remove("sec");
            var r = Traduzione.Traduci(SequenceModel.Leggi(j.ToJsonString()));

            Assert.AreEqual(4, r.Blocchi.Count, "gli altri quattro si costruiscono lo stesso");
            Assert.AreEqual(1, r.Scartati.Count);
            StringAssert.Contains(r.Scartati[0], "durata della posa");
            Assert.IsTrue(r.Costruibile, "un blocco perso non butta via la notte");
        }

        [TestMethod]
        public void PoseMancanti_IlBloccoNonSiCostruisce() {
            var j = JsonNode.Parse(Testo("mono"))!.AsObject();
            j["blocchi"]![1]!.AsObject().Remove("n");
            var r = Traduzione.Traduci(SequenceModel.Leggi(j.ToJsonString()));
            Assert.AreEqual(4, r.Blocchi.Count);
            StringAssert.Contains(r.Scartati[0], "numero di pose");
        }

        [TestMethod]
        public void Sentinella_MenoUno_NonDiventaUnGuadagnoNegativo() {
            /*  -1 vuol dire «non specificato». Scriverlo nella camera vorrebbe dire
             *  chiedere guadagno meno uno; non scriverlo vuol dire lasciare il valore
             *  del profilo, che e' cio' che «non specificato» significa. */
            var j = JsonNode.Parse(Testo("mono"))!.AsObject();
            var b0 = j["blocchi"]![0]!.AsObject();
            b0["gain"] = -1; b0["offset"] = -1;
            var r = Traduzione.Traduci(SequenceModel.Leggi(j.ToJsonString()));

            Assert.IsNull(r.Blocchi[0].Gain, "il guadagno non specificato non si scrive");
            Assert.IsNull(r.Blocchi[0].Offset);
            Assert.IsNotNull(r.Blocchi[1].Gain, "gli altri restano quelli che sono");
            Assert.AreEqual(100, r.Blocchi[1].Gain);
        }

        [TestMethod]
        public void BinningMancante_SiRiprendeUnoAUno_ESiDice() {
            var j = JsonNode.Parse(Testo("mono"))!.AsObject();
            j["ottica"]!.AsObject().Remove("bin");
            var r = Traduzione.Traduci(SequenceModel.Leggi(j.ToJsonString()));
            Assert.AreEqual(1, r.Binning, "binning assente non e' binning zero");
            Assert.IsTrue(r.Note.Any(n => n.Contains("binning")), "e la scelta si dichiara");
            Assert.IsTrue(r.Blocchi.All(b => b.Binning == 1));
        }

        [TestMethod]
        public void DitherFrazionario_DiventaUnConteggio() {
            /*  Il contratto lo esprime come un reale perche' nella pagina si scrive in un
             *  campo che accetta qualsiasi numero. Una camera pero' non sa scattare due
             *  pose e mezzo: qui diventa un conteggio, e l'arrotondamento si dichiara. */
            var j = JsonNode.Parse(Testo("completo"))!.AsObject();
            j["dither"]!.AsObject()["ogniPose"] = 2.5;
            var r = Traduzione.Traduci(SequenceModel.Leggi(j.ToJsonString()));
            Assert.AreEqual(3, r.DitherOgniPose);
            Assert.IsTrue(r.Note.Any(n => n.Contains("arrotondato")), "e lo dice");

            j["dither"]!.AsObject()["ogniPose"] = 0;
            var r0 = Traduzione.Traduci(SequenceModel.Leggi(j.ToJsonString()));
            Assert.IsNull(r0.DitherOgniPose, "ogni zero pose non e' un dithering");
            Assert.IsTrue(r0.Scartati.Any(s => s.Contains("dithering")));
        }

        [TestMethod]
        public void ModelloVuotoONullo_NonCostruisceEDicePerche() {
            var vuoto = Traduzione.Traduci(null);
            Assert.IsFalse(vuoto.Costruibile);
            Assert.IsTrue(vuoto.Scartati.Count > 0);

            var j = JsonNode.Parse(Testo("mono"))!.AsObject();
            j["blocchi"] = new JsonArray();
            var senzaBlocchi = Traduzione.Traduci(SequenceModel.Leggi(j.ToJsonString()));
            Assert.IsFalse(senzaBlocchi.Costruibile);
            StringAssert.Contains(string.Join(" ", senzaBlocchi.Scartati), "blocchi di ripresa");
        }

        [TestMethod]
        public void IlNome_NonInventaUnaData() {
            /*  Il nome proposto dal motore vince. Se manca si compone con cio' che si sa,
             *  e se non si sa la data non la si mette: il nome di una sequenza e' anche
             *  il nome della cartella dove finiranno i file. */
            var m = Modello("mono");
            Assert.IsNull(m.Nome, "questa fixture non porta un nome");
            var r = Traduzione.Traduci(m);
            StringAssert.Contains(r.Nome, m.Bersaglio!.Nome!);
            StringAssert.Contains(r.Nome, "notte 1");
            StringAssert.Contains(r.Nome, m.Quando!.Data!.Value.ToString("yyyy-MM-dd"));

            var j = JsonNode.Parse(Testo("mono"))!.AsObject();
            j["quando"] = null; j["notte"] = null;
            var senza = Traduzione.Traduci(SequenceModel.Leggi(j.ToJsonString()));
            Assert.AreEqual(m.Bersaglio.Nome, senza.Nome, "senza data e senza notte resta il solo nome");
        }
    }
}
