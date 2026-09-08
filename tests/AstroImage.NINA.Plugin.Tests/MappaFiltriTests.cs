using System.IO;
using AstroImage.NINA.Plugin.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace AstroImage.NINA.Plugin.Tests {

    /*  QUALE VETRO FA QUALE BANDA, letto da un file accanto al DLL.
     *
     *  Nasce dal banco vero: la prescrizione chiedeva «HO» e «L», in ruota c'erano
     *  LPS_P2 IDAS, ENHA, ULTIMATE, HA 7NM, V4 IDAS. Nomi di banda contro nomi
     *  commerciali, e nessuna regola che li possa indovinare — quale duale serva per
     *  Ha+OIII lo sa solo chi l'ha comprato.
     *
     *  Il motore la mappa la accetta gia': `sequenceModel` fa
     *  `Object.assign({}, NINA_FILTER, o.filterNames||{})`. Non c'era niente da
     *  inventare, solo da collegare.
     */
    [TestClass]
    public class MappaFiltriTests {

        private static string Cartella() {
            var d = Path.Combine(Path.GetTempPath(), "astroimage-mappa-" + Path.GetRandomFileName());
            Directory.CreateDirectory(d);
            return d;
        }

        private static string Con(string contenuto) {
            var d = Cartella();
            File.WriteAllText(Path.Combine(d, MappaFiltri.NomeFile), contenuto);
            return d;
        }

        [TestMethod]
        public void SenzaFile_MappaVuota_ENessunaNota() {
            var m = MappaFiltri.Leggi(Cartella(), out var nota);
            Assert.AreEqual(0, m.Count);
            Assert.IsNull(nota, "non avere una mappa non e' un problema da segnalare");
        }

        [TestMethod]
        public void CartellaNulla_NonEsplode() {
            Assert.AreEqual(0, MappaFiltri.Leggi(null, out _).Count);
            Assert.AreEqual(0, MappaFiltri.Leggi("   ", out _).Count);
        }

        [TestMethod]
        public void UnaMappaVera_SiLegge() {
            var d = Con("{\"Ha+OIII\":\"ULTIMATE\",\"RGB\":\"LPS_P2 IDAS\"}");
            var m = MappaFiltri.Leggi(d, out var nota);

            Assert.AreEqual(2, m.Count);
            Assert.AreEqual("ULTIMATE", m["Ha+OIII"]);
            Assert.AreEqual("LPS_P2 IDAS", m["RGB"]);
            Assert.IsNull(nota);
        }

        [TestMethod]
        public void GliSpaziAiBordiSiTolgono() {
            var d = Con("{\"  Ha+OIII \":\"  ULTIMATE  \"}");
            var m = MappaFiltri.Leggi(d, out _);
            Assert.AreEqual("ULTIMATE", m["Ha+OIII"]);
        }

        /*  Una voce vuota e' peggio di una voce assente: direbbe al motore che quel
         *  canale usa il filtro «», e il nome vuoto non lo trova nessuno. */
        [TestMethod]
        public void LeVociVuoteSiButtano_ELoDice() {
            var d = Con("{\"Ha+OIII\":\"ULTIMATE\",\"RGB\":\"\",\"\":\"X\"}");
            var m = MappaFiltri.Leggi(d, out var nota);

            Assert.AreEqual(1, m.Count);
            Assert.IsTrue(m.ContainsKey("Ha+OIII"));
            StringAssert.Contains(nota ?? "", "vuote");
        }

        /*  Un file rotto non deve impedire di CHIEDERE una prescrizione: deve solo far
         *  sapere che la mappa non c'e'. Il rifiuto sui filtri arrivera' dopo, e sara'
         *  quello a spiegare che cosa manca. */
        [TestMethod]
        public void UnFileRotto_NonImpedisceDiLavorare_MaSiFaSentire() {
            var d = Con("{ questo non e' JSON");
            var m = MappaFiltri.Leggi(d, out var nota);

            Assert.AreEqual(0, m.Count);
            Assert.IsNotNull(nota, "un file che c'e' e non si legge va detto");
            StringAssert.Contains(nota!, MappaFiltri.NomeFile);
        }

        [TestMethod]
        public void UnFileVuoto_LoDice() {
            var m = MappaFiltri.Leggi(Con("{}"), out var nota);
            Assert.AreEqual(0, m.Count);
            StringAssert.Contains(nota ?? "", "non dice niente");
        }
    }
}
