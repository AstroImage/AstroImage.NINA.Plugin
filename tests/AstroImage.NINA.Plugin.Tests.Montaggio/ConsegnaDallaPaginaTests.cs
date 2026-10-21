using System;
using System.IO;
using System.Linq;
using AstroImage.NINA.Plugin.Models;
using AstroImage.NINA.Plugin.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace AstroImage.NINA.Plugin.Tests.Montaggio {

    /*  LA CONSEGNA CHIESTA DALLA PAGINA VERA, col montaggio vero (passo 3, 21 ottobre 2026).
     *
     *  Qui si provano i rifiuti, che sono la parte che decide: un filtro che non e' nella ruota ferma il bersaglio
     *  intero, e il Sequenziatore non si tocca — il mediatore finto solleva se gli si chiede di aggiungere un bersaglio,
     *  quindi una consegna partita per sbaglio fa cadere la prova. Il bersaglio costruito davvero si vede solo dentro
     *  N.I.N.A., come per «Manda»: e' dichiarato, non sottinteso.
     */
    [TestClass]
    public class ConsegnaDallaPaginaTests {

        private static SequenceModel Modello(string nome) => SequenceModel.Leggi(File.ReadAllText(Path.Combine(
            Path.GetDirectoryName(typeof(ConsegnaDallaPaginaTests).Assembly.Location)!, "Fixtures", nome + ".json")))!;

        [TestMethod]
        public void UnFiltroCheNonENellaRuota_FermaTutto_EIlSequenziatoreNonSiTocca() {
            var mediatore = new MediatoreFinto();
            var e = ConsegnaDallaPagina.Esegui(null, new SequenceBuilder(FonteFinta.Bugiarda(), null!), mediatore,
                                               Modello("mono"), null, null);
            Assert.IsFalse(e.Fatta);
            Assert.IsTrue(e.Scartati.Any(s => s.Contains("NON è stato consegnato")), string.Join(" | ", e.Scartati));
            StringAssert.Contains(e.PerCheNo, "«O»", "il filtro che manca va nominato nel perche'");
            Assert.AreEqual(0, e.Blocchi, "un bersaglio rifiutato non dichiara blocchi");
            /*  se la consegna fosse partita, il mediatore finto avrebbe sollevato e il perche' sarebbe il suo, non il filtro */
            Assert.IsFalse(e.PerCheNo!.Contains("non deve chiamare"), e.PerCheNo);
        }

        [TestMethod]
        public void SenzaChiConsegna_SiDiceIlPerche_ENonSiCostruisce() {
            var e = ConsegnaDallaPagina.Esegui("il Sequenziatore non c'e'", new SequenceBuilder(FonteFinta.Bugiarda(), null!),
                                               new MediatoreFinto(), Modello("mono"), null, null);
            Assert.IsFalse(e.Fatta);
            Assert.AreEqual("il Sequenziatore non c'e'", e.PerCheNo);
            var senza = ConsegnaDallaPagina.Esegui(null, null, null, Modello("mono"), null, null);
            Assert.IsFalse(senza.Fatta);
            Assert.IsFalse(string.IsNullOrWhiteSpace(senza.PerCheNo), "senza mediatore il rifiuto non dice perche'");
        }

        [TestMethod]
        public void UnElencoCheNonSiLegge_EVuoto_ENonFaCadereNiente() {
            Assert.AreEqual(0, ConsegnaDallaPagina.Bersagli(null).Count);
            /*  il mediatore finto solleva su GetAllTargetsInAdvancedSequence: e' il caso di un Sequenziatore che non risponde */
            Assert.AreEqual(0, ConsegnaDallaPagina.Bersagli(new MediatoreFinto()).Count);
        }
    }
}
