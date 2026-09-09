using AstroImage.NINA.Plugin.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace AstroImage.NINA.Plugin.Tests {

    /*  QUANTE POSE, SU DUE VERSIONI DI N.I.N.A. CHE NON HANNO LA STESSA FORMA.
     *
     *  Il banco vero mostrava «# 20 · Progresso 0/151»: il numero chiesto era 151,
     *  quello nella casella 20 — il valore del modello clonato. Fra i due c'e' la
     *  differenza fra cinque ore e quaranta minuti.
     *
     *  Le due forme, lette dai metadati delle assembly:
     *
     *      3.2   SmartExposure : Attempts, ErrorBehavior
     *            il numero di pose vive nel LoopCondition, e basta.
     *
     *      3.3   SmartExposure : + Iterations:Int32, IterationsDefinition:String,
     *                              IterationsExpression:Expression
     *            e un BackfillIterationsExpressionFromLoopCondition che al clone copia
     *            il numero VECCHIO dentro la definizione — che poi comanda.
     *
     *  Qui non c'e' N.I.N.A.: ci sono due sosia con la stessa FORMA delle due versioni.
     *  Provano quello che serve provare — che si scrive nel posto giusto in entrambi i
     *  casi, e che quando non si riesce lo si dice invece di consegnare.
     */
    [TestClass]
    public class IterazioniTests {

        /// <summary>Un LoopCondition come lo espone la 3.2: solo un intero.</summary>
        private sealed class CondizioneVecchia {
            public int Iterations { get; set; } = 20;
            public int CompletedIterations { get; set; }
        }

        /// <summary>Uno SmartExposure come lo espone la 3.2: nessun conteggio suo.</summary>
        private sealed class PosaVecchia {
            public int Attempts { get; set; }
        }

        /*  La 3.3: la definizione e' una STRINGA, ed e' lei che comanda. L'intero la
         *  segue, come fa N.I.N.A. quando ricalcola l'espressione — ed e' proprio per
         *  questo che scrivere solo l'intero non serviva a niente. */
        private sealed class PosaNuova {
            private string _def = "20";
            public int Iterations { get; set; } = 20;
            public string IterationsDefinition {
                get => _def;
                set { _def = value; if (int.TryParse(value, out var n)) Iterations = n; }
            }
        }

        private sealed class CondizioneNuova {
            private string _def = "20";
            public int Iterations { get; set; } = 20;
            public int CompletedIterations { get; set; }
            public string IterationsDefinition {
                get => _def;
                set { _def = value; if (int.TryParse(value, out var n)) Iterations = n; }
            }
        }

        [TestMethod]
        public void Su32_IlNumeroVaNellaCondizione() {
            var posa = new PosaVecchia();
            var cond = new CondizioneVecchia();

            var ok = Iterazioni.Imposta(posa, cond, 29, out var perCheNo);

            Assert.IsTrue(ok, perCheNo);
            Assert.AreEqual(29, cond.Iterations, "sulla 3.2 e' l'unico posto dove il numero vive");
            Assert.AreEqual(29, Iterazioni.Effettivo(posa, cond));
        }

        [TestMethod]
        public void Su33_IlNumeroVaNELLA_DEFINIZIONE_CheEUnaStringa() {
            var posa = new PosaNuova();
            var cond = new CondizioneNuova();

            var ok = Iterazioni.Imposta(posa, cond, 151, out var perCheNo);

            Assert.IsTrue(ok, perCheNo);
            Assert.AreEqual("151", posa.IterationsDefinition,
                "e' la definizione a comandare: scrivere solo l'intero lasciava «# 20»");
            Assert.AreEqual(151, posa.Iterations);
            Assert.AreEqual("151", cond.IterationsDefinition);
            Assert.AreEqual(151, Iterazioni.Effettivo(posa, cond));
        }

        [TestMethod]
        public void IlValoreDelMODELLO_NonSopravvive() {
            /*  E' esattamente il difetto visto sul campo: il modello clonato portava 20,
                il backfill della 3.3 lo fissava nella definizione, e la nostra scrittura
                sull'intero non lo spostava. */
            var posa = new PosaNuova();
            Assert.AreEqual("20", posa.IterationsDefinition, "il modello parte da 20");

            Iterazioni.Imposta(posa, new CondizioneNuova(), 29, out _);

            Assert.AreEqual(29, Iterazioni.Effettivo(posa, null),
                "dopo la scrittura non deve restare traccia del numero del modello");
        }

        [TestMethod]
        public void SeNonAttECCHISCE_LoDICE_InveceDiLasciarPassare() {
            /*  Il caso che conta davvero: una versione futura in cui la definizione non
                si lascia scrivere. Meglio nessuna consegna che una sequenza che dice 20
                dove la prescrizione diceva 151. */
            var ok = Iterazioni.Imposta(new PosaTestarda(), null, 151, out var perCheNo);

            Assert.IsFalse(ok, "una scrittura che non attecchisce non e' un successo");
            StringAssert.Contains(perCheNo ?? "", "151");
            StringAssert.Contains(perCheNo ?? "", "20", "e deve dire anche che cosa c'e' scritto davvero");
        }

        /// <summary>Una posa che accetta la scrittura e non cambia: il guasto peggiore,
        /// perche' non solleva niente.</summary>
        private sealed class PosaTestarda {
            public int Iterations { get => 20; set { } }
            public string IterationsDefinition { get => "20"; set { } }
        }

        [TestMethod]
        public void UnOggettoSenzaNienteDiRICONOSCIBILE_NonPassaPerBuono() {
            var ok = Iterazioni.Imposta(new PosaVecchia(), null, 10, out var perCheNo);
            Assert.IsFalse(ok, "senza un posto dove leggere il numero non si puo' garantire niente");
            Assert.IsFalse(string.IsNullOrWhiteSpace(perCheNo));
        }

        [TestMethod]
        public void ZeroPose_NonSiScrive() {
            Assert.IsFalse(Iterazioni.Imposta(new PosaNuova(), new CondizioneNuova(), 0, out var p));
            Assert.IsFalse(string.IsNullOrWhiteSpace(p));
        }
    }
}
