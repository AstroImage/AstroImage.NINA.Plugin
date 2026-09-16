using System.Collections.Generic;
using AstroImage.NINA.Plugin.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace AstroImage.NINA.Plugin.Tests {

    /*  LA FEDELTA' FRA CIO' CHE STRATEGY DECIDE E CIO' CHE N.I.N.A. ESEGUE.
     *
     *  Questi test nascono da una consegna vera, andata a buon fine e sbagliata. Il
     *  bersaglio e' comparso nel Sequenziatore con pose, tempi, guadagno, offset e
     *  coordinate esatti — e con il filtro del primo blocco lasciato su «(Corrente)»,
     *  perche' la ruota non aveva l'HO che la prescrizione chiedeva. Ventinove pose da
     *  seicento secondi: quasi cinque ore riprese con qualunque vetro fosse montato,
     *  senza che una riga lo dicesse.
     *
     *  Non sostituire il filtro era ed e' giusto. Tacere no: trasforma
     *  un'impossibilita' strumentale in un'acquisizione apparentemente valida, che e'
     *  il difetto peggiore che questo progetto possa produrre.
     *
     *  Girano nel banco senza N.I.N.A. perche' la regola e' fatta di stringhe e numeri:
     *  e' stata messa li' apposta, dopo aver visto che tutto cio' che vive piu' in
     *  basso nel montaggio si puo' provare solo con N.I.N.A. accesa.
     */
    [TestClass]
    public class FedeltaAllaPrescrizioneTests {

        private static readonly IReadOnlyList<string> Ruota =
            new List<string> { "LPS_P2 IDAS", "ENHA", "ULTIMATE", "HA 7NM", "L" };

        // ------------------------------------------------ quando il vetro c'e', o non serve

        [TestMethod]
        public void NessunFiltroPrescritto_NonEUnProblema() {
            /*  Una camera a colori senza ruota riprende lo stesso: se la prescrizione
             *  non nomina un vetro, non c'e' niente da rispettare. */
            Assert.IsNull(SequenceBuilder.ScartoPerFiltro("HOO", null, Ruota, 29, 600, 4.833333333333333));
            Assert.IsNull(SequenceBuilder.ScartoPerFiltro("HOO", "   ", Ruota, 29, 600, 4.833333333333333));
        }

        [DataTestMethod]
        [DataRow("L")]
        [DataRow("l")]
        [DataRow("  L  ")]
        [DataRow("ha 7nm")]
        public void FiltroPresente_ConQualsiasiScrittura_VaBene(string richiesto) {
            /*  «Ha» e «ha » sono lo stesso vetro per chiunque tranne che per un
             *  confronto di stringhe. */
            Assert.IsNull(SequenceBuilder.ScartoPerFiltro("banda stretta", richiesto, Ruota, 29, 600, 4.833333333333333));
        }

        // ---------------------------------------------------- quando il vetro non c'e'

        [TestMethod]
        public void FiltroAssente_LoDice_ConIlNomeEQuantoCosta() {
            var s = SequenceBuilder.ScartoPerFiltro("HO", "HO", Ruota, 29, 600, 4.833333333333333);

            Assert.IsNotNull(s, "un filtro che non c'e' non puo' passare in silenzio");
            StringAssert.Contains(s!, "«HO»", "si dice quale vetro si cercava");
            StringAssert.Contains(s!, "29 pose", "e quante pose sono in ballo");
            StringAssert.Contains(s!, "600", "e quanto lunghe");
            StringAssert.Contains(s!, "4,83 h", "e soprattutto quante ORE: e' il numero che fa capire");
        }

        [TestMethod]
        public void RuotaVuota_ELoStessoUnProblema() {
            var s = SequenceBuilder.ScartoPerFiltro("HO", "HO", new List<string>(), 29, 600, 4.833333333333333);
            Assert.IsNotNull(s);
            var senzaRuota = SequenceBuilder.ScartoPerFiltro("HO", "HO", null, 29, 600, 4.833333333333333);
            Assert.IsNotNull(senzaRuota, "nessuna ruota non e' un permesso di ignorare il filtro");
        }

        /*  IL NUMERO CHE FA CAPIRE. Un messaggio che dicesse solo «filtro mancante»
         *  verrebbe letto e chiuso; «4,83 h con il vetro montato adesso» no. */
        /*  Dal 16 settembre 2026 le ore le manda il motore, nel blocco: qui si passano come le manderebbe lui, e la frase
         *  le scrive. */
        [DataTestMethod]
        [DataRow(29, 600.0, 4.833333333333333, "4,83 h")]
        [DataRow(18, 60.0, 0.3, "0,30 h")]
        [DataRow(1, 3600.0, 1.0, "1,00 h")]
        public void IlMessaggioDiceLeOre(int pose, double secondi, double ore, string atteso) {
            var s = SequenceBuilder.ScartoPerFiltro("x", "NONESISTE", Ruota, pose, secondi, ore);
            StringAssert.Contains(s!, atteso);
        }

        /*  UN MOTORE CHE NON MANDA LE ORE non le fa ricavare: la frase resta, col filtro, le pose e la posa, e senza un
         *  numero di ore che nessuno ha calcolato. */
        [TestMethod]
        public void SenzaLeOreDelMotore_LaFraseNonNeInventa() {
            var s = SequenceBuilder.ScartoPerFiltro("HO", "HO", Ruota, 29, 600, null);
            Assert.IsNotNull(s, "un filtro che non c'e' non passa in silenzio neanche senza le ore");
            StringAssert.Contains(s!, "«HO»");
            StringAssert.Contains(s!, "29 pose");
            Assert.IsFalse(s!.Contains(" h "), "un numero di ore ricavato dal Ponte: " + s);
        }

        [TestMethod]
        public void SenzaEtichetta_IlMessaggioRestaLeggibile() {
            var s = SequenceBuilder.ScartoPerFiltro(null, "HO", Ruota, 29, 600, 4.833333333333333);
            Assert.IsNotNull(s);
            StringAssert.StartsWith(s!, "un blocco");
        }
    }
}
