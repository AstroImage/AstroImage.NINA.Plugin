using System.Collections.Generic;
using AstroImage.NINA.Plugin.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace AstroImage.NINA.Plugin.Tests.Montaggio {

    /*  QUANTI BERSAGLI CON QUESTO NOME CI SONO GIA'.
     *
     *  Il ponte AGGIUNGE e non sovrascrive: la sequenza e' di chi riprende, e cancellare
     *  un bersaglio che potrebbe aver modificato a mano sarebbe peggio del problema.
     *  Ma tacere che adesso ce ne sono due e' costato: sul campo, dopo aver cambiato il
     *  cielo e richiesto di nuovo, in sequenza c'erano due «NGC 6888 — notte 1» e per
     *  capire quale fosse il nuovo bisognava cancellare il vecchio a mano.
     *
     *  SI CONTANO SOLO GLI OMONIMI, e questa e' la parte da non sbagliare. Mettere due
     *  bersagli DIVERSI nella stessa notte e' normale, e non deve far comparire niente.
     *  Il nome che il ponte genera porta dentro oggetto, notte e data, quindi due target
     *  diversi hanno nomi diversi — e lo stesso oggetto su notte 1 e notte 2 pure.
     */
    [TestClass]
    public class OmonimiTests {

        private const string Nome = "NGC 6888 — notte 1 — 2026-09-15";

        [TestMethod]
        public void SequenzaVuota_NessunOmonimo() {
            Assert.AreEqual(0, SequenceBuilder.ContaOmonimi(new string?[0], Nome));
        }

        [TestMethod]
        public void DueBersagliDIVERSI_NellaStessaNotte_NonSonoUnDoppione() {
            /*  Il caso normale di chi pianifica una serata: due oggetti, stessa notte.
                Se questo contasse, l'avviso comparirebbe sempre e diventerebbe rumore. */
            var inSequenza = new string?[] {
                "M 31 — notte 1 — 2026-09-15",
                "NGC 7000 — notte 1 — 2026-09-15",
            };
            Assert.AreEqual(0, SequenceBuilder.ContaOmonimi(inSequenza, Nome));
        }

        [TestMethod]
        public void LoStessoOggettoSuNottiDIVERSE_NonEUnDoppione() {
            var inSequenza = new string?[] {
                "NGC 6888 — notte 2 — 2026-09-16",
                "NGC 6888 — notte 3 — 2026-09-17",
            };
            Assert.AreEqual(0, SequenceBuilder.ContaOmonimi(inSequenza, Nome));
        }

        [TestMethod]
        public void LoStessoIDENTICO_SiConta() {
            /*  E' il caso vero: hai cambiato il cielo, hai richiesto, e riconsegni la
                stessa notte dello stesso oggetto. */
            Assert.AreEqual(1, SequenceBuilder.ContaOmonimi(new string?[] { Nome }, Nome));
            Assert.AreEqual(2, SequenceBuilder.ContaOmonimi(new string?[] { Nome, "M 31 — notte 1", Nome }, Nome));
        }

        [TestMethod]
        public void MaiuscoleESpaziNonFannoUnNomeDiverso() {
            var inSequenza = new string?[] { "  ngc 6888 — NOTTE 1 — 2026-09-15  " };
            Assert.AreEqual(1, SequenceBuilder.ContaOmonimi(inSequenza, Nome),
                "un bersaglio rinominato con un maiuscolo in piu' resta lo stesso bersaglio");
        }

        [TestMethod]
        public void UnBersaglioSenzaNome_NonConta() {
            Assert.AreEqual(0, SequenceBuilder.ContaOmonimi(new string?[] { null, "" }, Nome));
        }

        [TestMethod]
        public void SenzaElenco_TornaNULL_ENonZero() {
            /*  «Non lo so» e «nessuno» sono due cose diverse. Se il Sequenziatore non si
                e' potuto leggere — succede quando non e' mai stato aperto — dire zero
                sarebbe una bugia piccola della stessa famiglia di quelle grandi. */
            Assert.IsNull(SequenceBuilder.ContaOmonimi(null, Nome));
            Assert.IsNull(SequenceBuilder.ContaOmonimi(new string?[] { Nome }, null));
        }

        [TestMethod]
        public void SenzaMediatore_TornaNULL() {
            Assert.IsNull(SequenceBuilder.QuantiOmonimi(null, Nome));
        }

        [TestMethod]
        public void UnMediatoreCheSOLLEVA_TornaNULL_ENonFaCadereLaConsegna() {
            /*  MediatoreFinto solleva su tutto cio' che il montaggio non deve chiamare,
                e va benissimo qui: e' il caso del Sequenziatore mai aperto. La consegna
                sarebbe riuscita lo stesso, e non deve cadere per un conteggio. */
            Assert.IsNull(SequenceBuilder.QuantiOmonimi(new MediatoreFinto(), Nome));
        }
    }
}
