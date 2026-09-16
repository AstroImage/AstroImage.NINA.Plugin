using System.Text.Json.Nodes;
using AstroImage.NINA.Plugin.Models;
using AstroImage.NINA.Plugin.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace AstroImage.NINA.Plugin.Tests {

    /*  IL BANCO DICHIARATO, SENZA N.I.N.A.
     *
     *  Il Ponte non sa quali chiavi esistano — le pubblica il servizio —, ma sa riconoscere una chiave, sa che un valore
     *  e' un numero o un identificativo, e non perde niente in silenzio: quello che non puo' salvare lo rifiuta e lo dice.
     */
    [TestClass]
    public class DichiarazioneBancoTests {

        private static JsonNode Messaggio(string banco) => JsonNode.Parse("{\"banco\":" + banco + "}")!;

        [TestMethod]
        public void UnMessaggioGiusto_SiSalvaComeArriva_EUnValoreVuotoToglieLaChiave() {
            var b = DichiarazioneBanco.DalMessaggio(
                Messaggio("{\"tel.id\":\" askar71f \",\"tel.apertura_mm\":80,\"tel.ostruzione\":null,\"mnt.id\":\"\"}"),
                out var perCheNo);
            Assert.IsNotNull(b, perCheNo);
            Assert.AreEqual(2, b!.Valori.Count, "il vuoto e il nullo tolgono la chiave");
            Assert.AreEqual("askar71f", b.Valori["tel.id"].GetString(), "l'identificativo si salva senza spazi attorno");
            Assert.AreEqual(80, b.Valori["tel.apertura_mm"].GetDouble());
        }

        [DataTestMethod]
        [DataRow("{\"Tel.Apertura\":80}")]
        [DataRow("{\"tel.apertura mm\":80}")]
        [DataRow("{\"tel.apertura_mm\":{\"valore\":80}}")]
        [DataRow("{\"tel.apertura_mm\":[80]}")]
        [DataRow("{\"tel.apertura_mm\":true}")]
        public void UnaChiaveStortaOUnValoreCheNonENumeroNeTesto_SiRifiuta_ESiDice(string banco) {
            Assert.IsNull(DichiarazioneBanco.DalMessaggio(Messaggio(banco), out var perCheNo));
            Assert.IsFalse(string.IsNullOrWhiteSpace(perCheNo), "un rifiuto senza motivo e' un silenzio");
        }

        [TestMethod]
        public void UnMessaggioSenzaBanco_SiRifiuta() {
            Assert.IsNull(DichiarazioneBanco.DalMessaggio(JsonNode.Parse("{\"sito\":{}}"), out var perCheNo));
            Assert.IsFalse(string.IsNullOrWhiteSpace(perCheNo));
        }

        [TestMethod]
        public void ScrivereELeggere_RestituisceGliStessiValori_ELaPaginaLiRiceveCosiComeSono() {
            var b = DichiarazioneBanco.DalMessaggio(Messaggio("{\"tel.id\":\"askar71f\",\"mnt.rms_caratteristico_arcsec\":1.2}"), out _)!;
            var letto = DichiarazioneBanco.Leggi(DichiarazioneBanco.Scrivi(b), out var nota);
            Assert.IsNull(nota);
            var pagina = DichiarazioneBanco.PerLaPagina(letto);
            Assert.AreEqual("askar71f", (string?)pagina["tel.id"]);
            Assert.AreEqual(1.2, (double?)pagina["mnt.rms_caratteristico_arcsec"]);
        }

        [TestMethod]
        public void UnDocumentoIlleggibile_NonDiventaUnBanco_ESiDice() {
            var b = DichiarazioneBanco.Leggi("{ non e' json", out var nota);
            Assert.AreEqual(0, b.Valori.Count);
            Assert.IsFalse(string.IsNullOrWhiteSpace(nota));
            Assert.AreEqual(0, DichiarazioneBanco.Leggi(null, out var niente).Valori.Count, "assente e' non dichiarato");
            Assert.IsNull(niente);
        }
    }
}
