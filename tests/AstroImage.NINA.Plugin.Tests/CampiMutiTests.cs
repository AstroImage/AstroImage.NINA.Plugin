using System;
using System.IO;
using System.Text.Json.Nodes;
using AstroImage.NINA.Plugin.Models;
using AstroImage.NINA.Plugin.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace AstroImage.NINA.Plugin.Tests {

    /*  I CAMPI MUTI NON PARTONO (regia, 18 settembre 2026).
     *
     *  Seeing, guida e notti serene del sito non si offrono piu' nel pannello: nel Ponte non muovevano niente di visibile,
     *  e un campo cosi' e' un'assunzione muta travestita da controllo. I loro valori restano nei profili, muti, per il
     *  giorno in cui rientrano — e muti vuol dire che non partono: il sito che l'ospite consegna alla pagina e' quello che
     *  la pagina rimanda al motore, e se ci restasse un seeing di 2,5″ salvato nel profilo, o misurato da N.I.N.A., il conto
     *  lo userebbe mentre la nota della prescrizione dice il riferimento del motore. Un salvataggio che non porta una chiave
     *  non la cancella. L'RMS caratteristico della montatura non c'entra: sta nel banco, e decide.
     *
     *  La meta' della pagina e del motore — la richiesta composta dal vero prova.js su un sito che li porta ancora, il
     *  riferimento che il motore applica e dichiara, il giallo vuoto e la nota — la prova tools/gate-giallo-pulito.js, nel
     *  repository del motore.
     */
    [TestClass]
    public class CampiMutiTests {

        private static readonly string[] Muti = { "seeing", "rms", "clearFrac" };

        /*  Il caso cattivo: il profilo ha salvato seeing, guida e notti serene, e N.I.N.A. misura seeing e guida. */
        private static SitoDiRipresa Cattivo() => DichiarazioneSito.Unisci(
            new SitoDiRipresa { Lat = 45.95, Lon = 10.2, Seeing = 3.0, Rms = 0.9 },
            new SitoDichiarato { Sqm = 20.8, Seeing = 2.5, Rms = 1.1, HorizonMin = 20, ClearFrac = 0.5 });

        [TestMethod]
        public void IlSitoPerLaPagina_NonPortaICampiMuti_NeSalvatiNeMisurati() {
            var sito = DichiarazioneSito.PerLaPagina(Cattivo());

            foreach (var k in Muti)
                Assert.IsFalse(sito.ContainsKey(k), k + " parte verso la pagina, e da li' verso il motore: " + sito.ToJsonString());
            /*  e il resto c'e': un sito vuoto passerebbe la prova qui sopra senza dire niente */
            Assert.AreEqual(45.95, (double)sito["lat"]!);
            Assert.AreEqual(10.2, (double)sito["lon"]!);
            Assert.AreEqual(20.8, (double)sito["sqm"]!);
            Assert.AreEqual(20.0, (double)sito["horizonMin"]!);
        }

        [TestMethod]
        public void IlDichiaratoPerLaPagina_NonPortaICampiMuti() {
            var d = DichiarazioneSito.DichiaratoPerLaPagina(
                new SitoDichiarato { Sqm = 20.8, Seeing = 2.5, Rms = 1.1, HorizonMin = 20, ClearFrac = 0.5 });

            foreach (var k in Muti)
                Assert.IsFalse(d.ContainsKey(k), k + " arriva alla pagina come valore scritto: " + d.ToJsonString());
            Assert.AreEqual(20.8, (double)d["sqm"]!);
            Assert.AreEqual(20.0, (double)d["horizonMin"]!);
        }

        [TestMethod]
        public void UnSalvataggioSenzaICampiMuti_NonLiCancellaDalProfilo() {
            var prima = new SitoDichiarato { Sqm = 20.8, Seeing = 2.5, Rms = 1.1, HorizonMin = 20, ClearFrac = 0.5 };
            var m = JsonNode.Parse("{\"id\":\"r1\",\"azione\":\"salvaSito\",\"corpo\":{\"sito\":{\"sqm\":21.2,\"horizonMin\":null}}}");

            var d = DichiarazioneSito.DalMessaggio(m, out var perCheNo, prima);

            Assert.IsNotNull(d, perCheNo);
            Assert.AreEqual(21.2, d!.Sqm, "il campo che si scrive cambia");
            Assert.IsNull(d.HorizonMin, "un campo svuotato resta vuoto");
            Assert.AreEqual(2.5, d.Seeing, "il seeing del profilo, muto, si e' perso al primo salvataggio");
            Assert.AreEqual(1.1, d.Rms);
            Assert.AreEqual(0.5, d.ClearFrac);
        }

        /*  L'ospite usa le due funzioni qui sopra, e il salvataggio gli passa il profilo di prima. Strutturale: legge il
         *  sorgente dell'ospite, che nei test non si esegue. */
        [TestMethod]
        public void LOspite_ConsegnaIlSitoDaQui_ESalvaSenzaCancellare() {
            var vista = File.ReadAllText(Path.Combine(Sorgenti(), "Views", "PannelloStrategyView.xaml.cs"));
            StringAssert.Contains(vista, "[\"sito\"] = DichiarazioneSito.PerLaPagina(unito)");
            StringAssert.Contains(vista, "[\"dichiarato\"] = DichiarazioneSito.DichiaratoPerLaPagina(vm.SitoScritto)");
            StringAssert.Contains(vista, "DichiarazioneSito.DalMessaggio(messaggio, out var perCheMalformata, vm.SitoScritto)",
                "il salvataggio del sito non passa il profilo di prima: i campi muti si cancellano");
        }

        private static string Sorgenti() {
            var su = new DirectoryInfo(AppContext.BaseDirectory);
            for (var n = 0; n < 8 && su is not null; n++, su = su.Parent)
                if (Directory.Exists(Path.Combine(su.FullName, "src", "AstroImage.NINA.Plugin")))
                    return Path.Combine(su.FullName, "src", "AstroImage.NINA.Plugin");
            Assert.Fail("sorgenti non trovati accanto ai test");
            return "";
        }
    }
}
