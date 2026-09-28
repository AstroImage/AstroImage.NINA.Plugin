using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using AstroImage.NINA.Plugin.Models;
using AstroImage.NINA.Plugin.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace AstroImage.NINA.Plugin.Tests {

    /*  I FILTRI DELLA MONO E QUELLI DELLA COLORE SULLO STESSO NOME (regia, 28 settembre 2026).
     *
     *  Sullo stesso profilo di N.I.N.A. si dichiara il banco con la camera monocromatica o con quella a colori, e la ruota
     *  ha gli stessi nomi: «LPS_P2» davanti alla colore e' l'IDAS LPS-P2, davanti alla mono e' la luminanza. Con una
     *  dichiarazione sola per nome, cambiando camera partivano i filtri dell'altra — l'RGB puro su una mono, perche' in
     *  ruota c'erano i duali della colore. Adesso ogni nome tiene il filtro con la mono e quello con la colore; parte quello
     *  della camera del banco, come il servizio l'ha riconosciuta; senza la camera parte solo se le due dicono lo stesso;
     *  la consegna usa la camera con cui la domanda e' partita. Una dichiarazione salvata prima vale per tutt'e due, e si
     *  dice.
     */
    [TestClass]
    public class DueCamereTests {

        private static VoceRuota Voce(string nina, string? mono, string? colore) =>
            new VoceRuota { Nina = nina, Mono = mono, Colore = colore };

        private static RuotaVirtuale Doppia() => new RuotaVirtuale { Vetri = {
            Voce("LPS_P2", "lum", "idas"), Voce("LPS_V4", "red", "lps_v4"), Voce("HA", "grn", "ha7"),
            Voce("L-eNhance", "blu", "lenh"), Voce("Ultimate", "s2_3", "lult"), Voce("6", "ha3", null), Voce("7", "o3_3", null) } };

        private static readonly string[] Ruota = { "Ultimate", "LPS_P2", "LPS_V4", "HA", "L-eNhance", "7", "6" };

        private static string[] Partita(RichiestaDelPannello.Esito e) =>
            (JsonNode.Parse(e.Corpo!)!["ruota"] as JsonArray)?.Select(x => (string)x!).ToArray() ?? new string[0];

        [TestMethod]
        public void PARTE_LA_DICHIARAZIONE_DELLA_CAMERA_DEL_BANCO() {
            var mono = RichiestaDelPannello.Componi(new JsonObject(), Doppia(), Ruota, "mono");
            CollectionAssert.AreEquivalent(new[] { "lum", "red", "grn", "blu", "s2_3", "ha3", "o3_3" }, Partita(mono),
                "con la mono partono i filtri della mono");
            Assert.AreEqual("mono", mono.Camera);
            var colore = RichiestaDelPannello.Componi(new JsonObject(), Doppia(), Ruota, "colore");
            CollectionAssert.AreEquivalent(new[] { "idas", "lps_v4", "ha7", "lenh", "lult" }, Partita(colore),
                "con la colore partono quelli della colore, e i nomi senza filtro con la colore non partono");
            Assert.AreEqual("colore", colore.Camera);
        }

        [TestMethod]
        public void SENZA_LA_CAMERA_NON_SI_SCEGLIE_A_CASO() {
            var e = RichiestaDelPannello.Componi(new JsonObject(), Doppia(), Ruota, null);
            Assert.IsNull(e.Corpo, "con due dichiarazioni diverse e la camera ignota la domanda non parte");
            Assert.AreEqual("camera_non_nota", e.Codice);
            Assert.IsFalse(string.IsNullOrWhiteSpace(e.Rifiuto), "e si dice perche'");
            var uguali = new RuotaVirtuale { Vetri = { Voce("L", "lum", "lum"), Voce("ULTIMATE", "lult", "lult") } };
            var parte = RichiestaDelPannello.Componi(new JsonObject(), uguali, new[] { "L", "ULTIMATE" }, null);
            CollectionAssert.AreEquivalent(new[] { "lum", "lult" }, Partita(parte), "se le due camere dicono lo stesso, parte");
            Assert.IsNull(parte.Camera, "e non si finge di aver scelto una camera");
            var orfane = RichiestaDelPannello.Componi(new JsonObject(), new RuotaVirtuale { Vetri = { Voce("VECCHIO", "ha3", "ha3") } },
                new[] { "L" }, "mono");
            Assert.AreEqual("ruota_solo_orfane", orfane.Codice, "il rifiuto di prima ha il suo codice");
        }

        [TestMethod]
        public void LA_CAMERA_E_QUELLA_DETTA_POI_IL_PROFILO_POI_NIENTE() {
            Assert.AreEqual("colore", DichiarazioneRuota.Camera("colore", false), "la camera riconosciuta vince sul profilo");
            Assert.AreEqual("mono", DichiarazioneRuota.Camera(" Mono ", true));
            Assert.AreEqual("colore", DichiarazioneRuota.Camera(null, true), "senza, lo schema di Bayer del profilo");
            Assert.AreEqual("mono", DichiarazioneRuota.Camera("", false));
            Assert.IsNull(DichiarazioneRuota.Camera("rgb", null), "una parola che non e' una delle due non si indovina");
        }

        [TestMethod]
        public void LA_VISTA_PER_UNA_CAMERA_HA_IL_SUO_FILTRO() {
            var m = DichiarazioneRuota.PerCamera(Doppia(), "mono");
            var c = DichiarazioneRuota.PerCamera(Doppia(), "colore");
            Assert.AreEqual("lum", DichiarazioneRuota.IdPerNome(m, "LPS_P2"));
            Assert.AreEqual("idas", DichiarazioneRuota.IdPerNome(c, "LPS_P2"));
            Assert.AreEqual("LPS_P2", DichiarazioneRuota.NomePerId(c, "idas"), "la consegna ritrova il nome col filtro della colore");
            Assert.IsNull(DichiarazioneRuota.NomePerId(m, "idas"), "e con la mono l'IDAS su quel nome non c'e'");
            Assert.AreEqual(0, DichiarazioneRuota.PerCamera(Doppia(), "rgb").Vetri.Count, "una camera che non esiste da' la vista vuota");
            Assert.IsNull(DichiarazioneRuota.IdPerNome(Doppia(), "LPS_P2"),
                "sulla dichiarazione salvata, dove le due camere dicono filtri diversi, senza la camera non c'e' un filtro");
        }

        [TestMethod]
        public void LE_DUE_CAMERE_SI_SALVANO_E_SI_RILEGGONO() {
            var riletta = DichiarazioneRuota.Leggi(DichiarazioneRuota.Scrivi(Doppia()), out var nota);
            Assert.IsNull(nota);
            Assert.AreEqual(2, riletta.Versione);
            var v = riletta.Vetri.First(x => x.Nina == "LPS_P2");
            Assert.AreEqual("lum", v.Mono);
            Assert.AreEqual("idas", v.Colore);
            Assert.IsNull(riletta.Vetri.First(x => x.Nina == "6").Colore, "un nome senza filtro con la colore resta senza");
        }

        [TestMethod]
        public void LA_DICHIARAZIONE_DI_PRIMA_VALE_PER_TUTTE_E_DUE_E_SI_DICE() {
            var r = DichiarazioneRuota.Leggi("{\"versione\":1,\"vetri\":[{\"nina\":\"ULTIMATE\",\"motore\":\"lult\"},{\"nina\":\"L\",\"motore\":null}]}",
                                            out var nota);
            var v = r.Vetri.First(x => x.Nina == "ULTIMATE");
            Assert.AreEqual("lult", v.Mono);
            Assert.AreEqual("lult", v.Colore);
            Assert.IsNull(v.Motore, "la forma di prima non resta nella dichiarazione nuova");
            Assert.IsFalse(string.IsNullOrWhiteSpace(nota), "e si dice che vale per tutt'e due");
            Assert.IsTrue(DichiarazioneRuota.UgualePerLeDueCamere(r), "finche' non le separi, le due camere dicono lo stesso");
        }

        [TestMethod]
        public void LA_PAGINA_MANDA_LE_DUE_CAMERE_E_IL_MESSAGGIO_VECCHIO_VALE_PER_TUTTE_E_DUE() {
            var nuova = DichiarazioneRuota.DalMessaggio(JsonNode.Parse(
                "{\"corpo\":{\"vetri\":[{\"nina\":\"LPS_P2\",\"mono\":\"lum\",\"colore\":\"idas\"},{\"nina\":\"6\",\"mono\":\"ha3\",\"colore\":null}]}}"), out var no);
            Assert.IsNull(no);
            Assert.AreEqual("idas", nuova!.Vetri[0].Colore);
            Assert.IsNull(nuova.Vetri[1].Colore, "un nome tolto dalla colore resta tolto");
            var vecchia = DichiarazioneRuota.DalMessaggio(JsonNode.Parse("{\"corpo\":{\"vetri\":[{\"nina\":\"L\",\"id\":\"lum\"}]}}"), out _);
            Assert.AreEqual("lum", vecchia!.Vetri[0].Mono);
            Assert.AreEqual("lum", vecchia.Vetri[0].Colore);
        }

        [TestMethod]
        public void IL_PANNELLO_MANDA_LA_CAMERA_E_LA_CONSEGNA_USA_QUELLA_DELLA_DOMANDA() {
            string? radice = null;
            var su = new DirectoryInfo(AppContext.BaseDirectory);
            for (var i = 0; i < 8 && su is not null; i++, su = su.Parent)
                if (Directory.Exists(Path.Combine(su.FullName, "src", "AstroImage.NINA.Plugin"))) { radice = Path.Combine(su.FullName, "src"); break; }
            Assert.IsNotNull(radice, "sorgenti non trovati accanto ai test");
            var js = File.ReadAllText(Path.Combine(radice!, "AstroImage.NINA.Plugin", "Views", "Pagina", "prova.js"));
            Assert.IsTrue(js.Split(new[] { "{ camera: cameraDeiFiltri() }" }, StringSplitOptions.None).Length - 1 >= 3,
                "la prescrizione, gli oggetti e la scheda mandano la camera del banco");
            StringAssert.Contains(js, "cameraDelBanco.dati.matrice", "la camera e' quella che il motore ha riconosciuto");
            StringAssert.Contains(js, "data-camera-ruota", "la vista dei filtri sceglie la camera");
            StringAssert.Contains(js, "mono: x.mono ? x.mono.id : null", "il salvataggio manda il filtro della mono");
            StringAssert.Contains(js, "colore: x.colore ? x.colore.id : null", "e quello della colore");
            var vista = File.ReadAllText(Path.Combine(radice!, "AstroImage.NINA.Plugin", "Views", "PannelloStrategyView.xaml.cs"));
            StringAssert.Contains(vista, "DichiarazioneRuota.PerCamera(vm.Dichiarazione, vm.InMano.Camera",
                "la consegna traduce il filtro con la camera della domanda in mano");
            StringAssert.Contains(vista, "vmR?.Ruota.Nomi(), CameraDellaDomanda(messaggio, vmR)", "la prescrizione parte con la camera");
            StringAssert.Contains(vista, "InMano.Prendi(esito, bersaglioChiesto, bancoChiesto, domanda.Camera)",
                "e la risposta in mano la tiene");
        }
    }
}
