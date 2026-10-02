using System;
using System.Collections.Generic;
using AstroImage.NINA.Plugin.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace AstroImage.NINA.Plugin.Tests {

    /*  SOSTITUIRE UN BERSAGLIO, provato senza N.I.N.A.: il posto e' una lista, e i suoi quattro gesti si possono far
     *  sbagliare uno per uno. Quello che deve reggere: il nuovo va nella stessa posizione del vecchio, i fratelli non si
     *  muovono, un elenco vecchio non sostituisce un altro bersaglio, e se il gesto non attecchisce il vecchio torna. */
    [TestClass]
    public class SostituzioneDelBersaglioTests {

        /*  Un posto finto: una lista, con due modi di guastarsi a comando. */
        private sealed class Posto : IPostoNellaSequenza {
            public readonly List<object> Fratelli;
            public bool MettiSolleva;
            public bool MettiAltrove;
            public object? NonSiTogli;
            public Posto(params object[] fratelli) => Fratelli = new List<object>(fratelli);
            public int Indice(object elemento) => Fratelli.IndexOf(elemento);
            public bool Togli(object elemento) => !Equals(elemento, NonSiTogli) && Fratelli.Remove(elemento);
            public void Metti(int indice, object elemento) {
                if (MettiSolleva) { MettiSolleva = false; throw new InvalidOperationException("il Sequenziatore ha rifiutato"); }
                if (MettiAltrove) { MettiAltrove = false; Fratelli.Add(elemento); return; }
                Fratelli.Insert(indice, elemento);
            }
            public object? In(int indice) => indice >= 0 && indice < Fratelli.Count ? Fratelli[indice] : null;
        }

        [TestMethod]
        public void IlNuovoPrendeLaStessaPosizione_EIFratelliNonSiMuovono() {
            var posto = new Posto("Start", "M33", "NGC 7000", "End");
            Assert.IsTrue(SostituzioneDelBersaglio.Sostituisci(posto, "M33", "M31", out var perche), perche);
            CollectionAssert.AreEqual(new object[] { "Start", "M31", "NGC 7000", "End" }, posto.Fratelli,
                "il nuovo bersaglio non e' al posto del vecchio, o un fratello si e' mosso");
        }

        [TestMethod]
        public void SeIlSequenziatoreRifiuta_IlVecchioTornaDovEra() {
            var posto = new Posto("Start", "M33", "End") { MettiSolleva = true };
            Assert.IsFalse(SostituzioneDelBersaglio.Sostituisci(posto, "M33", "M31", out var perche));
            Assert.IsNotNull(perche, "un rifiuto senza perche'");
            CollectionAssert.AreEqual(new object[] { "Start", "M33", "End" }, posto.Fratelli,
                "il bersaglio vecchio e' sparito senza sostituto");
        }

        [TestMethod]
        public void SeRilettoIlPostoNonHaIlNuovo_SiDice_EIlVecchioTorna() {
            var posto = new Posto("Start", "M33", "End") { MettiAltrove = true };
            Assert.IsFalse(SostituzioneDelBersaglio.Sostituisci(posto, "M33", "M31", out var perche),
                "scrivere non e' aver scritto: il nuovo e' finito in fondo e la sostituzione e' passata");
            Assert.IsNotNull(perche);
            CollectionAssert.AreEqual(new object[] { "Start", "M33", "End" }, posto.Fratelli,
                "il nuovo e' rimasto nel posto sbagliato, o il vecchio non e' tornato");
        }

        [TestMethod]
        public void SeIlVecchioNonSiLasciaTogliere_IlNuovoVaVia_ENonRestanoDue() {
            var posto = new Posto("Start", "M33", "End") { NonSiTogli = "M33" };
            Assert.IsFalse(SostituzioneDelBersaglio.Sostituisci(posto, "M33", "M31", out var perche),
                "il vecchio e' rimasto accanto al nuovo, e la sostituzione e' passata");
            Assert.IsNotNull(perche);
            CollectionAssert.AreEqual(new object[] { "Start", "M33", "End" }, posto.Fratelli,
                "due bersagli nella sequenza, o il vecchio sparito");
        }

        [TestMethod]
        public void UnBersaglioCheNonCe_NonSiSostituisce() {
            var posto = new Posto("Start", "NGC 7000", "End");
            Assert.IsFalse(SostituzioneDelBersaglio.Sostituisci(posto, "M33", "M31", out var perche));
            Assert.IsNotNull(perche);
            CollectionAssert.AreEqual(new object[] { "Start", "NGC 7000", "End" }, posto.Fratelli);
        }

        [TestMethod]
        public void LElencoVisto_DeveIndicareAncoraLoStessoBersaglio() {
            var presenti = new List<string?> { "M33", "NGC 7000" };
            Assert.IsTrue(SostituzioneDelBersaglio.AncoraQuello(presenti, 1, " ngc 7000 ", out var perche), perche);
            Assert.IsFalse(SostituzioneDelBersaglio.AncoraQuello(presenti, 0, "NGC 7000", out perche),
                "l'elenco e' cambiato sotto la pagina, e la sostituzione avrebbe colpito M33");
            Assert.IsNotNull(perche);
            Assert.IsFalse(SostituzioneDelBersaglio.AncoraQuello(presenti, 2, "NGC 7000", out perche), "un indice fuori");
            Assert.IsFalse(SostituzioneDelBersaglio.AncoraQuello(presenti, -1, "M33", out perche), "un indice negativo");
            Assert.IsFalse(SostituzioneDelBersaglio.AncoraQuello(presenti, 0, null, out perche), "senza nome non si sostituisce");
        }
    }
}
