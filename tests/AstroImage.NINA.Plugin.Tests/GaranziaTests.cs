using AstroImage.NINA.Plugin.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace AstroImage.NINA.Plugin.Tests {

    /*  «SCRIVERE» E «AVER SCRITTO» SONO DUE COSE DIVERSE.
     *
     *  Il banco vero l'ha mostrato una volta e basta: «# 20» accanto a «Progresso
     *  0/151». L'assegnazione era riuscita, il valore era un altro.
     *
     *  Qui non c'e' N.I.N.A.: ci sono sosia con la FORMA delle due versioni. La 3.2
     *  espone il solo valore; la 3.3 gli mette accanto una definizione testuale che in
     *  un caso vince. Si prova che si legge dal tipo giusto, che un valore che non
     *  attecchisce viene visto, e — altrettanto importante — che un valore corretto
     *  non viene rifiutato per un dettaglio che non conta.
     */
    [TestClass]
    public class GaranziaTests {

        /// <summary>La posa come la espone la 3.2: solo i valori.</summary>
        private sealed class Posa32 {
            public double ExposureTime { get; set; }
            public int Gain { get; set; }
            public int Offset { get; set; }
        }

        /*  La 3.3: accanto a ogni valore una definizione testuale. Il guadagno la
         *  propaga, l'offset no — e' esattamente cio' che il Sequenziatore vero mostra
         *  sullo stesso blocco, e sono corretti tutti e due. */
        private sealed class Posa33 {
            private double _t;
            public double ExposureTime {
                get => _t;
                set { _t = value; ExposureTimeDefinition = value.ToString("0.###"); }
            }
            public string ExposureTimeDefinition { get; private set; } = "";
            private int _g;
            public int Gain { get => _g; set { _g = value; GainDefinition = value.ToString(); } }
            public string GainDefinition { get; private set; } = "";
            /*  L'offset non propaga: N.I.N.A. considera «non impostato» cio' che
             *  coincide col profilo, e lo mostra fra graffe. Il valore resta giusto. */
            public int Offset { get; set; }
            public string OffsetDefinition => "";
        }

        /// <summary>Il guasto peggiore: accetta la scrittura e non cambia. Non solleva
        /// niente, e senza rilettura passerebbe per riuscito.</summary>
        private sealed class PosaSorda {
            public double ExposureTime { get => 300; set { } }
        }

        private sealed class Innesco {
            public int AfterExposures { get; set; }
        }

        private sealed class InnescoSordo {
            public int AfterExposures { get => 3; set { } }
        }

        // ───────────────────────────────────────────────────────── la posa

        [TestMethod]
        public void Su32_LaPosaSiRileggeGiusta() {
            var p = new Posa32 { ExposureTime = 600, Gain = 100, Offset = 50 };
            Assert.IsTrue(Garanzia.Numero(p, "ExposureTime", 600, "la posa", out var e), e);
            Assert.IsTrue(Garanzia.Numero(p, "Gain", 100, "il guadagno", out e), e);
            Assert.IsTrue(Garanzia.Numero(p, "Offset", 50, "l'offset", out e), e);
        }

        [TestMethod]
        public void Su33_LaPosaSiRileggeGiusta_ANCHE_QuandoLaDefinizioneResta_VUOTA() {
            /*  E' il caso che una verifica ingenua sbaglierebbe: OffsetDefinition e'
                vuota, ma l'offset che ESEGUE e' 50 ed e' quello prescritto. Rifiutare
                qui vorrebbe dire produrre rifiuti falsi — e un rifiuto falso, a furia di
                ripetersi, insegna a ignorare i rifiuti. */
            var p = new Posa33 { ExposureTime = 600, Gain = 100, Offset = 50 };

            Assert.AreEqual("", p.OffsetDefinition, "la definizione resta vuota, come sul campo");
            Assert.IsTrue(Garanzia.Numero(p, "Offset", 50, "l'offset", out var e), e);
            Assert.IsTrue(Garanzia.Numero(p, "ExposureTime", 600, "la posa", out e), e);
            Assert.AreEqual("600", p.ExposureTimeDefinition, "questa invece propaga");
        }

        [TestMethod]
        public void UnaPosaCheNonAttecchisce_VIENE_VISTA() {
            var ok = Garanzia.Numero(new PosaSorda(), "ExposureTime", 600, "la posa", out var perCheNo);

            Assert.IsFalse(ok, "l'assegnazione riesce e il valore non cambia: e' il guasto silenzioso");
            StringAssert.Contains(perCheNo ?? "", "600");
            StringAssert.Contains(perCheNo ?? "", "300", "e deve dire anche che cosa c'e' scritto davvero");
        }

        [TestMethod]
        public void UnaProprietaCheNonESISTE_NonPassaPerBuona() {
            var ok = Garanzia.Numero(new Posa32(), "NonEsisteProprio", 1, "qualcosa", out var perCheNo);
            Assert.IsFalse(ok, "non poter rileggere non e' la stessa cosa di aver riletto bene");
            Assert.IsFalse(string.IsNullOrWhiteSpace(perCheNo));
        }

        [TestMethod]
        public void UnOggettoNULLO_NonPassaPerBuono() {
            Assert.IsFalse(Garanzia.Numero(null, "ExposureTime", 600, "la posa", out var p));
            Assert.IsFalse(string.IsNullOrWhiteSpace(p));
        }

        [TestMethod]
        public void LaPosaSiConfrontaConTolleranza() {
            /*  I tempi sono in virgola mobile: un confronto esatto fra double e' un
                difetto in attesa di una posa da 2,5 secondi. */
            var p = new Posa32 { ExposureTime = 0.1 + 0.2 };
            Assert.IsTrue(Garanzia.Numero(p, "ExposureTime", 0.3, "la posa", out var e), e);
        }

        // ───────────────────────────────────────────────────────── il filtro

        [TestMethod]
        public void IlFiltroCombacia_ANCHE_ConMaiuscoleESpazi() {
            Assert.IsTrue(Garanzia.Parola("ULTIMATE", "ultimate ", "il filtro", out var e), e);
            Assert.IsTrue(Garanzia.Parola(" LPS_P2 IDAS", "LPS_P2 IDAS", "il filtro", out e), e);
        }

        [TestMethod]
        public void UnFiltroDIVERSO_DaQuelloPrescritto_FERMA_LaConsegna() {
            /*  E' il difetto originale in forma di verifica: N.I.N.A. non segnala un
                nome che non trova, mette il primo filtro e va avanti. */
            var ok = Garanzia.Parola("HA 7NM", "ULTIMATE", "il filtro", out var perCheNo);

            Assert.IsFalse(ok);
            StringAssert.Contains(perCheNo ?? "", "ULTIMATE");
            StringAssert.Contains(perCheNo ?? "", "HA 7NM");
        }

        [TestMethod]
        public void UnFiltroASSENTE_DoveNeServivaUno_FERMA_LaConsegna() {
            Assert.IsFalse(Garanzia.Parola(null, "ULTIMATE", "il filtro", out var p));
            Assert.IsFalse(string.IsNullOrWhiteSpace(p));
        }

        // ───────────────────────────────────────────────────────── il dither

        [TestMethod]
        public void IlDitherSiRileggeGiusto() {
            Assert.IsTrue(Garanzia.Numero(new Innesco { AfterExposures = 2 },
                "AfterExposures", 2, "il dither", out var e), e);
        }

        [TestMethod]
        public void UnDitherEREDITATO_DalModello_VIENE_VISTO() {
            /*  Il modello di serie porta «ogni 3»; la prescrizione diceva «ogni 2». Un
                numero plausibile, al posto giusto, che nessuno guarda due volte — ed era
                il difetto vero trovato in sequenza. */
            var ok = Garanzia.Numero(new InnescoSordo(), "AfterExposures", 2, "il dither", out var perCheNo);

            Assert.IsFalse(ok);
            StringAssert.Contains(perCheNo ?? "", "2");
            StringAssert.Contains(perCheNo ?? "", "3");
        }
    }
}
