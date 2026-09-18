using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AstroImage.NINA.Plugin.Localization;
using AstroImage.NINA.Plugin.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace AstroImage.NINA.Plugin.Tests {

    /*  IL MOTIVO DEL MOTORE NON SI SOSTITUISCE (regia, 18 settembre 2026).
     *
     *  Chi il ponte l'ha costruito ha scritto nel banco una camera monocromatica al posto della 2600MC, apposta, e ha chiesto una
     *  prescrizione col pezzo scollegato. Il motore ha rifiutato dicendo strada per strada perche': l'OIII non si separa
     *  con un dual-band su un sensore mono. Il pannello ha scritto invece una frase sua — «con i filtri che hai
     *  dichiarato…» — che dava la colpa ai filtri e mandava a comprarne altri. Non era un'assenza muta: era una
     *  sostituzione, e ha ingannato proprio lui.
     *
     *  La regola: quando il motore manda un motivo, il ponte non lo sostituisce mai con una frase propria. La frase
     *  generica vale solo quando non e' arrivato niente. Il rifiuto qui e' quello vero, uscito dal motore
     *  (dal servizio acceso), ridotto ai campi che il ponte legge.
     */
    [TestClass]
    public class MotiviDelMotoreTests {

        [TestCleanup]
        public void Dopo() => Loc.Instance.ForzaLingua("it");

        private static string Fixture(string nome) => File.ReadAllText(Path.Combine(
            Path.GetDirectoryName(typeof(MotiviDelMotoreTests).Assembly.Location)!, "Fixtures", "servizio", nome + ".json"));

        private sealed class Trasporto : HttpMessageHandler {
            private readonly HttpStatusCode _stato;
            private readonly string _corpo;
            public Trasporto(HttpStatusCode stato, string corpo) { _stato = stato; _corpo = corpo; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) =>
                Task.FromResult(new HttpResponseMessage(_stato) {
                    Content = new StringContent(_corpo, Encoding.UTF8, "application/json") });
        }

        // 1 ──────────────────────────────── il rifiuto vero: i motivi restano, la frase generica no
        [TestMethod]
        public async Task UnRifiutoCoiMotivi_NonDiventaLaFraseGenerica() {
            var corpo = Fixture("rifiuto-camera-mono");
            var cliente = new ClienteStrategy(new HttpClient(new Trasporto((HttpStatusCode)422, corpo)),
                                              new Uri("http://127.0.0.1:8791/"));
            var esito = await cliente.Prescrizione("{}");

            Assert.IsFalse(esito.Riuscito);
            Assert.AreEqual("nessuna_prescrizione", esito.Codice);
            StringAssert.Contains(esito.Corpo, "\"bloccate\"", "le strade bloccate non restano in mano al ponte");
            StringAssert.Contains(esito.Corpo, "\"camera\"", "la camera del calcolo non resta in mano al ponte");
            foreach (var lingua in new[] { "it", "en" }) {
                Loc.Instance.ForzaLingua(lingua);
                var frase = esito.MessaggioTradotto ?? "";
                Assert.AreNotEqual(Loc.T("Motore_NessunaPrescrizioneSenzaDati"), frase,
                    lingua + ": il rifiuto porta i suoi motivi e il pannello scrive la frase generica");
                StringAssert.Contains(frase, "OIII", lingua + ": le bande che il motore dice mancare non sono nella frase");
            }
        }

        // 2 ──────────────────────────────── la generica vale solo quando non e' arrivato niente
        [TestMethod]
        public void LaFraseGenericaValeSoloQuandoNonArrivaNiente() {
            var conDati = MessaggioDelMotore.Rendi("nessuna_prescrizione",
                new Dictionary<string, string> { ["mancano"] = "OIII, SII" }, "la frase del motore");
            StringAssert.Contains(conDati, "OIII, SII", "con le bande che mancano la frase non le dice");

            Assert.AreEqual("la frase del motore", MessaggioDelMotore.Rendi("nessuna_prescrizione",
                new Dictionary<string, string>(), "la frase del motore"),
                "senza le bande ma con la frase del motore, vince la frase del motore: e' il suo motivo");
            Assert.AreEqual("la frase del motore", MessaggioDelMotore.Rendi("nessuna_prescrizione",
                new Dictionary<string, string> { ["mancano"] = "  " }, "la frase del motore"),
                "un campo vuoto vale come assente, e non si compone una frase col buco");

            var generica = Loc.T("Motore_NessunaPrescrizioneSenzaDati");
            Assert.AreEqual(generica, MessaggioDelMotore.Rendi("nessuna_prescrizione", null, null),
                "quando non arriva niente, la frase generica");
            Assert.IsFalse(generica.IndexOf("filtr", StringComparison.OrdinalIgnoreCase) >= 0,
                "la frase generica da' la colpa ai filtri, e il motore non l'ha detto");
        }

        // 3 ──────────────────────────────── l'ospite passa il rifiuto intero alla pagina
        [TestMethod]
        public void LOspitePassaAllaPaginaIlRifiutoIntero() {
            string? radice = null;
            var su = new DirectoryInfo(AppContext.BaseDirectory);
            for (var n = 0; n < 8 && su is not null; n++, su = su.Parent)
                if (Directory.Exists(Path.Combine(su.FullName, "src", "AstroImage.NINA.Plugin"))) {
                    radice = Path.Combine(su.FullName, "src", "AstroImage.NINA.Plugin"); break;
                }
            Assert.IsNotNull(radice, "sorgenti non trovati accanto alle prove");
            var vista = File.ReadAllText(Path.Combine(radice!, "Views", "PannelloStrategyView.xaml.cs"));
            StringAssert.Contains(vista, "if (!esito.Riuscito) { Rispondi(id, false, esito.Corpo, esito.Codice, esito.MessaggioTradotto); return; }",
                "la pagina riceve la frase e non i motivi: non puo' scriverli");
        }
    }
}
