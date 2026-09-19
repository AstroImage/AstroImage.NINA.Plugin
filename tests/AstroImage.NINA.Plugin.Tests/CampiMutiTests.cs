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
     *  e un campo cosi' e' un'assunzione muta travestita da controllo. I loro valori restano salvati nel profilo, senza
     *  effetto finche' i campi non tornano — e senza effetto vuol dire che non partono: il sito che l'ospite consegna alla pagina e' quello che
     *  la pagina rimanda al motore, e se ci restasse un seeing di 2,5″ salvato nel profilo, o misurato da N.I.N.A., il conto
     *  lo userebbe mentre la nota della prescrizione dice il riferimento del motore. Se un salvataggio non ha la chiave,
     *  il valore gia' salvato resta. L'RMS della montatura e' un'altra cosa: fa parte del banco, e conta.
     *
     *  La meta' della pagina e del motore — la richiesta composta dal vero prova.js su un sito che li porta ancora, il
     *  riferimento che il motore applica e dichiara, il giallo vuoto e la nota — si prova dove la pagina si esegue, e non
     *  qui: qui si prova l'ospite.
     */
    [TestClass]
    public class CampiMutiTests {

        /*  La guida del sito, `rms`, dal 18 settembre 2026 non e' piu' muta: e' cancellata. Resta nell'elenco perche' non
         *  deve partire comunque. Il seeing tipico non e' piu' muto dal 19 settembre 2026: torna, e solo dichiarato (sotto). */
        private static readonly string[] Muti = { "rms", "clearFrac" };

        /*  Il caso cattivo: il profilo ha salvato seeing e notti serene, e N.I.N.A. misura il seeing. */
        private static SitoDiRipresa Cattivo() => DichiarazioneSito.Unisci(
            new SitoDiRipresa { Lat = 45.95, Lon = 10.2, Seeing = 3.0 },
            new SitoDichiarato { Sqm = 20.8, Seeing = 2.5, HorizonMin = 20, ClearFrac = 0.5 });

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
                new SitoDichiarato { Sqm = 20.8, Seeing = 2.5, HorizonMin = 20, ClearFrac = 0.5 });

            foreach (var k in Muti)
                Assert.IsFalse(d.ContainsKey(k), k + " arriva alla pagina come valore scritto: " + d.ToJsonString());
            Assert.AreEqual(20.8, (double)d["sqm"]!);
            Assert.AreEqual(20.0, (double)d["horizonMin"]!);
        }

        /*  IL SEEING TIPICO TORNA (regia, 19 settembre 2026): una proprieta' del posto, della stessa specie dell'SQM, che il
         *  motore usa nel giudizio di campionamento e nel consiglio di binning. Si dichiara, e solo si dichiara: la FWHM che
         *  una stazione misura stanotte non e' il seeing del posto — e' la notte, con la guida e l'ottica dentro — e non lo
         *  sostituisce ne' parte al suo posto. Nate rosse contro 3571b41: il seeing non partiva, e se partiva vinceva la
         *  misura di stanotte. */
        [TestMethod]
        public void IlSeeingTipico_ParteDichiarato_ELaNotteMisurataNonLoSostituisce() {
            var s = DichiarazioneSito.Unisci(new SitoDiRipresa { Lat = 45.95, Lon = 10.2, Seeing = 3.0 },
                                             new SitoDichiarato { Sqm = 20.8, Seeing = 2.5 });
            var p = DichiarazioneSito.PerLaPagina(s);

            Assert.IsTrue(p.ContainsKey("seeing"), "il seeing tipico non parte verso la pagina: " + p.ToJsonString());
            Assert.AreEqual(2.5, (double)p["seeing"]!, "parte la FWHM di stanotte al posto del seeing dichiarato");
            Assert.AreEqual(DichiarazioneSito.Dichiarato, s.Provenienza!["seeing"]);
        }

        [TestMethod]
        public void IlSeeingTipico_NonDichiarato_NonLoRiempieLaNotteMisurata() {
            var s = DichiarazioneSito.Unisci(new SitoDiRipresa { Lat = 45.95, Lon = 10.2, Seeing = 3.0 },
                                             new SitoDichiarato { Sqm = 20.8 });
            var p = DichiarazioneSito.PerLaPagina(s);

            Assert.IsTrue(p.ContainsKey("seeing"), "la chiave del seeing tipico non c'e': " + p.ToJsonString());
            Assert.IsNull(p["seeing"], "la FWHM misurata stanotte e' diventata il seeing del sito: " + p.ToJsonString());
            Assert.AreEqual(DichiarazioneSito.Assente, s.Provenienza!["seeing"]);
        }

        [TestMethod]
        public void IlDichiaratoPerLaPagina_PortaIlSeeingTipico() {
            var d = DichiarazioneSito.DichiaratoPerLaPagina(new SitoDichiarato { Sqm = 20.8, Seeing = 2.5, ClearFrac = 0.5 });

            Assert.AreEqual(2.5, (double)d["seeing"]!, "il seeing scritto non torna nel suo campo: " + d.ToJsonString());
        }

        [TestMethod]
        public void UnSalvataggioSenzaICampiMuti_NonLiCancellaDalProfilo() {
            var prima = new SitoDichiarato { Sqm = 20.8, Seeing = 2.5, HorizonMin = 20, ClearFrac = 0.5 };
            var m = JsonNode.Parse("{\"id\":\"r1\",\"azione\":\"salvaSito\",\"corpo\":{\"sito\":{\"sqm\":21.2,\"horizonMin\":null}}}");

            var d = DichiarazioneSito.DalMessaggio(m, out var perCheNo, prima);

            Assert.IsNotNull(d, perCheNo);
            Assert.AreEqual(21.2, d!.Sqm, "il campo che si scrive cambia");
            Assert.IsNull(d.HorizonMin, "un campo svuotato resta vuoto");
            Assert.AreEqual(2.5, d.Seeing, "il seeing del profilo, muto, si e' perso al primo salvataggio");
            Assert.AreEqual(0.5, d.ClearFrac);
        }

        /*  LA GUIDA DEL SITO SI CANCELLA, NON RESTA IN ATTESA (regia, 18 settembre 2026). Un profilo salvato prima la porta
         *  ancora: letta, non c'e' piu' — ne' come campo ne' fra gli extra, che il file riscriverebbe a ogni salvataggio —,
         *  e dal sito per la pagina non parte. */
        [TestMethod]
        public void LaGuidaDelSito_SiCancellaDalProfilo_ENonParte() {
            var d = DichiarazioneSito.Leggi("{\"versione\":1,\"sqm\":20.8,\"seeing\":2.5,\"rms\":1.1,\"clearFrac\":0.5}", out var nota);

            Assert.IsNull(nota, nota);
            Assert.AreEqual(20.8, d.Sqm, "il resto del profilo si legge");
            Assert.AreEqual(2.5, d.Seeing);
            var scritto = DichiarazioneSito.Scrivi(d);
            Assert.IsFalse(scritto.Contains("\"rms\""), "la guida del sito torna nel file al salvataggio: " + scritto);
            var sito = DichiarazioneSito.PerLaPagina(DichiarazioneSito.Unisci(new SitoDiRipresa { Lat = 45.95, Lon = 10.2 }, d));
            Assert.IsFalse(sito.ContainsKey("rms"), "la guida del sito parte verso il motore: " + sito.ToJsonString());
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
