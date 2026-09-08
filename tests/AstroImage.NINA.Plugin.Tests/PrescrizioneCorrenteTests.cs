using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AstroImage.NINA.Plugin.Models;
using AstroImage.NINA.Plugin.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace AstroImage.NINA.Plugin.Tests {

    /*  LA GUARDIA CONTRO IL GESTO PIU' NORMALE CHE CI SIA.
     *
     *  Chiedi una prescrizione per NGC 6888. Ne chiedi una per M 31 perche' vuoi
     *  confrontare. Poi premi «manda» sulla riga di prima, che e' ancora sullo schermo.
     *  Senza una guardia il ponte manderebbe M 31 chiamandolo NGC 6888 — e nessuno se
     *  ne accorgerebbe fino a mattina.
     *
     *  Questi test girano senza N.I.N.A. perche' PrescrizioneCorrente non la nomina: e'
     *  la ragione per cui la scelta della notte sta in una classe pura invece che
     *  dentro il gestore dei messaggi del pannello, dove sarebbe stata inverificabile.
     */
    [TestClass]
    public class PrescrizioneCorrenteTests {

        private static string Fixture(string nome) =>
            File.ReadAllText(Path.Combine(
                Path.GetDirectoryName(typeof(PrescrizioneCorrenteTests).Assembly.Location)!,
                "Fixtures", "servizio", nome + ".json"));

        private sealed class Trasporto : HttpMessageHandler {
            private readonly string _corpo;
            public Trasporto(string corpo) => _corpo = corpo;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new StringContent(_corpo, Encoding.UTF8, "application/json") });
        }

        /// <summary>Un esito vero, ottenuto dalla risposta vera del servizio.</summary>
        private static async Task<EsitoPrescrizione> EsitoVero() {
            var c = new ClienteStrategy(new HttpClient(new Trasporto(Fixture("prescrizione-ok"))),
                                        new Uri("http://esempio.invalid/"));
            return await c.Prescrizione("{}");
        }

        private static EsitoPrescrizione ConNotti(params int[] notti) {
            var seq = new List<SequenzaDiNotte>();
            foreach (var n in notti)
                seq.Add(new SequenzaDiNotte { Notte = n, Modello = new SequenceModel() });
            return typeof(EsitoPrescrizione)
                .GetMethod("Riuscita", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .Invoke(null, new object?[] { 200, "{}", seq, 0, "1", 1.0, null }) as EsitoPrescrizione
                ?? throw new InvalidOperationException();
        }

        // ------------------------------------------------------------- il giro normale

        [TestMethod]
        public async Task Prendi_UnEsitoVero_DaUnIdentificativoETieneLeNotti() {
            var p = new PrescrizioneCorrente();
            var id = p.Prendi(await EsitoVero());

            Assert.IsFalse(string.IsNullOrWhiteSpace(id), "ogni risposta riceve un identificativo");
            Assert.AreEqual(id, p.Id);
            Assert.AreEqual(1, p.Notti, "la fixture porta una notte");

            var s = p.Notte(id, 1, out var codice, out var motivo);
            Assert.IsNotNull(s, motivo);
            Assert.IsNull(codice);
            Assert.IsNotNull(s!.Modello);
        }

        [TestMethod]
        public void Prendi_DueVolte_CambiaIdentificativo() {
            var p = new PrescrizioneCorrente();
            var primo = p.Prendi(ConNotti(1, 2));
            var secondo = p.Prendi(ConNotti(1, 2));

            Assert.AreNotEqual(primo, secondo,
                "due risposte diverse non possono avere lo stesso identificativo");
        }

        // ------------------------------------------------------------------- la guardia

        /*  IL CASO PER CUI TUTTO QUESTO ESISTE. */
        [TestMethod]
        public void Notte_ConLIdentificativoDellaPrescrizionePrecedente_Rifiuta() {
            var p = new PrescrizioneCorrente();
            var idPrimo = p.Prendi(ConNotti(1, 2, 3));   // NGC 6888, diciamo
            p.Prendi(ConNotti(1, 2, 3));                 // poi si chiede M 31

            var s = p.Notte(idPrimo, 1, out var codice, out var motivo);

            Assert.IsNull(s, "la riga vecchia non deve consegnare l'oggetto nuovo");
            Assert.AreEqual("prescrizione_scaduta", codice);
            StringAssert.Contains(motivo ?? "", "precedente");
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("un-identificativo-inventato")]
        public void Notte_SenzaIdentificativoGiusto_Rifiuta(string? id) {
            var p = new PrescrizioneCorrente();
            p.Prendi(ConNotti(1));

            Assert.IsNull(p.Notte(id, 1, out var codice, out _));
            Assert.AreEqual("prescrizione_scaduta", codice);
        }

        [TestMethod]
        public void Notte_SenzaNessunaPrescrizione_LoDice() {
            var p = new PrescrizioneCorrente();

            Assert.IsNull(p.Notte("qualsiasi", 1, out var codice, out var motivo));
            Assert.AreEqual("nessuna_prescrizione", codice);
            StringAssert.Contains(motivo ?? "", "chiedine una");
        }

        [TestMethod]
        public void Notte_UnaNotteCheNonCE_LoDiceEDiceQuanteCeNeSono() {
            var p = new PrescrizioneCorrente();
            var id = p.Prendi(ConNotti(1, 2, 3));

            Assert.IsNull(p.Notte(id, 7, out var codice, out var motivo));
            Assert.AreEqual("notte_assente", codice);
            StringAssert.Contains(motivo ?? "", "3", "e dice quante ne ha");
        }

        /*  Il numero della notte non e' un indice: le notti del piano possono non
         *  partire da uno ne' essere contigue, e cercarle per posizione manderebbe la
         *  notte sbagliata senza sbagliare niente di visibile. */
        [TestMethod]
        public void Notte_SiCercaPerNumeroENonPerPosizione() {
            var p = new PrescrizioneCorrente();
            var id = p.Prendi(ConNotti(4, 7, 9));

            Assert.IsNotNull(p.Notte(id, 7, out _, out _), "la notte 7 c'e'");
            Assert.IsNull(p.Notte(id, 1, out var codice, out _), "la notte 1 no");
            Assert.AreEqual("notte_assente", codice);
            Assert.IsNull(p.Notte(id, 0, out _, out _), "e zero nemmeno");
        }

        // ------------------------------------------------------------- quando non c'e'

        [TestMethod]
        public void Prendi_UnEsitoFallito_NonTieneNiente() {
            var p = new PrescrizioneCorrente();
            p.Prendi(ConNotti(1));
            var id = p.Prendi(null);

            Assert.IsNull(id, "da un esito assente non nasce un identificativo");
            Assert.IsNull(p.Id, "e quello di prima non resta in giro");
            Assert.AreEqual(0, p.Notti);
        }

        [TestMethod]
        public void Prendi_UnEsitoSenzaNotti_NonTieneNiente() {
            var p = new PrescrizioneCorrente();

            Assert.IsNull(p.Prendi(ConNotti()), "una prescrizione senza notti non e' consegnabile");
            Assert.AreEqual(0, p.Notti);
        }

        [TestMethod]
        public void Lascia_DimenticaTutto() {
            var p = new PrescrizioneCorrente();
            var id = p.Prendi(ConNotti(1));
            p.Lascia();

            Assert.IsNull(p.Id);
            Assert.IsNull(p.Notte(id, 1, out var codice, out _));
            Assert.AreEqual("nessuna_prescrizione", codice);
        }

        // ------------------------------------------------- non sa niente di N.I.N.A.

        [TestMethod]
        public void PrescrizioneCorrente_NonNominaNina() {
            foreach (var m in typeof(PrescrizioneCorrente).GetMethods()) {
                foreach (var par in m.GetParameters())
                    Assert.IsFalse((par.ParameterType.FullName ?? "").StartsWith("NINA."),
                        $"{m.Name} prende {par.ParameterType.Name}");
                Assert.IsFalse((m.ReturnType.FullName ?? "").StartsWith("NINA."),
                    $"{m.Name} restituisce {m.ReturnType.Name}");
            }
        }
    }
}
