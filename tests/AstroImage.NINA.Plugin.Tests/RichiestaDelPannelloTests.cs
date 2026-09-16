using System.Linq;
using System.Text.Json.Nodes;
using AstroImage.NINA.Plugin.Models;
using AstroImage.NINA.Plugin.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace AstroImage.NINA.Plugin.Tests {

    /*  LE ORFANE NON RENDONO PERCORRIBILE UNA STRADA (regia, 16 settembre 2026).
     *
     *  Una voce orfana — un filtro dichiarato, poi rinominato o tolto in N.I.N.A. — entrava nella `ruota` della
     *  domanda e cadeva solo alla consegna: la strada era stata scelta su una ruota che conteneva filtri che non
     *  c'erano. Adesso la domanda parte con la ruota dichiarata meno le orfane, le orfane si dicono alla
     *  richiesta, e una dichiarazione fatta solo di orfane non parte: una ruota vuota il servizio la legge come
     *  «non dichiarata» e usa i filtri di serie del motore, che non sono i tuoi.
     */
    [TestClass]
    public class RichiestaDelPannelloTests {

        private static RuotaVirtuale Dichiarata(params (string nina, string? id)[] voci) =>
            new RuotaVirtuale {
                Vetri = voci.Select(v => new VoceRuota { Nina = v.nina, Motore = v.id }).ToList()
            };

        private static string[] RuotaPartita(RichiestaDelPannello.Esito e) =>
            (JsonNode.Parse(e.Corpo!)!["ruota"] as JsonArray)?.Select(x => (string)x!).ToArray()
            ?? new string[0];

        [TestMethod]
        public void UnaVoceOrfana_NonParteNellaRuota_ESiDice() {
            var dich = Dichiarata(("HA7", "ha7"), ("ULTIMATE", "lult"), ("LPS_P2 IDAS", "idas"), ("V4 IDAS", null));
            var e = RichiestaDelPannello.Componi(new JsonObject { ["bersaglio"] = new JsonObject { ["id"] = "ngc6888" } },
                dich, new[] { "ultimate ", "LPS_P2 IDAS", "Rosso" });

            Assert.IsNotNull(e.Corpo, "con due filtri dichiarati in ruota la domanda parte");
            CollectionAssert.AreEquivalent(new[] { "lult", "idas" }, RuotaPartita(e),
                "l'Ha7 non e' piu' in ruota: non deve rendere percorribile nessuna strada");
            CollectionAssert.AreEquivalent(new[] { "lult", "idas" }, e.RuotaAggiunta!.ToArray(),
                "la pagina deve leggere la ruota che e' partita davvero");
            CollectionAssert.AreEqual(new[] { "HA7" }, e.Orfane.Select(o => o.Nina).ToArray(),
                "l'orfana si dice alla richiesta, non con un fallimento alla consegna; e una voce mai " +
                "dichiarata (V4 IDAS, senza identificativo) non e' un'orfana: non era mai entrata");
            Assert.AreEqual("ngc6888", (string?)JsonNode.Parse(e.Corpo!)!["bersaglio"]!["id"],
                "il resto del corpo parte come la pagina l'ha scritto");
        }

        [TestMethod]
        public void SoloOrfane_LaDomandaNonParte_ENonCadeSuiFiltriDiSerie() {
            var e = RichiestaDelPannello.Componi(new JsonObject(), Dichiarata(("HA7", "ha7"), ("OIII", "o3")),
                new[] { "L", "R" });

            Assert.IsNull(e.Corpo,
                "senza filtri dichiarati in ruota non c'e' niente da prescrivere, e una ruota vuota diventerebbe " +
                "quella di serie del motore");
            Assert.IsFalse(string.IsNullOrWhiteSpace(e.Rifiuto), "un rifiuto senza motivo e' un silenzio");
            StringAssert.Contains(e.Rifiuto!, "HA7", "il rifiuto nomina le voci che non ci sono piu'");
            StringAssert.Contains(e.Rifiuto!, "OIII");
            Assert.AreEqual(2, e.Orfane.Count);
        }

        [TestMethod]
        public void LoStessoIdentificativo_SottoUnNomeChiEInRuota_ParteLoStesso() {
            var e = RichiestaDelPannello.Componi(null, Dichiarata(("HO A", "lult"), ("HO B", "lult")), new[] { "HO B" });

            CollectionAssert.AreEqual(new[] { "lult" }, RuotaPartita(e),
                "il filtro c'e', sotto un altro nome: la strada resta percorribile");
            CollectionAssert.AreEqual(new[] { "HO A" }, e.Orfane.Select(o => o.Nina).ToArray(),
                "e il nome sparito si dice lo stesso");
        }

        /*  ASSICURAZIONI, verdi anche prima della regola: il comportamento di prima che la regola non deve toccare. */
        [TestMethod]
        public void NienteDichiarato_NonAggiungeRuota_ENonRifiuta() {
            var e = RichiestaDelPannello.Componi(new JsonObject(), new RuotaVirtuale(), new[] { "L", "R" });
            Assert.IsNotNull(e.Corpo);
            Assert.IsNull(JsonNode.Parse(e.Corpo!)!["ruota"], "vuoto e non dichiarato restano due cose diverse");
            Assert.IsNull(e.RuotaAggiunta);
            Assert.IsNull(e.Rifiuto);
        }

        [TestMethod]
        public void UnClienteCheDichiaraLaSuaRuota_NonVieneToccato() {
            var corpo = new JsonObject { ["ruota"] = new JsonArray("lenh") };
            var e = RichiestaDelPannello.Componi(corpo, Dichiarata(("HA7", "ha7")), new string[0]);
            CollectionAssert.AreEqual(new[] { "lenh" }, RuotaPartita(e));
            Assert.IsNull(e.RuotaAggiunta);
            Assert.IsNull(e.Rifiuto);
        }
    }
}
