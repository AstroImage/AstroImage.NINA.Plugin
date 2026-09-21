using System.IO;
using System.Linq;
using System.Reflection;
using AstroImage.NINA.Plugin.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AstroImage.NINA.Plugin.Tests {

    /*  IL TESTIMONE DELLA POSA — e perche' non c'era.
     *  ═══════════════════════════════════════════════════════════════════════════════════════════════════════
     *  Il 20 settembre 2026 abbiamo portato una posa da 600 a 1800 secondi dentro una fixture — un fattore tre,
     *  sullo stesso oggetto e sulla stessa montatura — e abbiamo lanciato tutta la suite. Sette prove su quella
     *  fixture, sette verdi. NESSUNA guardava la posa.
     *
     *  Le quattro volte che `Sec` compariva nelle prove: una in un commento; una che chiedeva che due blocchi sullo
     *  stesso filtro avessero pose DIVERSE, non quali; una che chiedeva che una posa assente fosse nulla e non zero;
     *  una che confrontava la traduzione interna con se' stessa. Il valore non lo controllava nessuno, mentre il
     *  Ponte lo trasportava fino al Sequenziatore.
     *
     *  QUI NON SI CONTROLLA UN NUMERO, SI CONTROLLA UNA RELAZIONE. Un'asserzione tipo «OIII sta a 900 s» cadrebbe
     *  ogni volta che il cielo, il banco o la scala cambiano, e a furia di aggiornarla si smetterebbe di leggerla.
     *  Le tre grandezze del canale — la posa, le pose e le ore — sono invece legate fra loro dal motore, e quel
     *  legame regge qualunque numero: `oreCanale = poseCanale × sec / 3600`. Se qualcuno tocca la posa e lascia
     *  indietro gli altri due, questa prova cade. Se il motore cambia scala, non cade: e' quello che deve fare.
     *
     *  E LA NOTTE E' UNA FETTA DEL CANALE, mai il contrario: `n` non puo' superare `poseCanale`. Un conteggio di pose
     *  scritto senza dire se sia della notte o del canale e' un numero che promette un canale e consegna una notte:
     *  chi legge «600 s, 12 pose» pensa al progetto, e le dodici sono di stanotte.                                 */
    [TestClass]
    public class PosaDelCanaleTests {

        private static readonly string[] Vive = { "mono", "completo", "osc", "osc-hdr" };

        private static string CartellaFixture =>
            Path.Combine(Path.GetDirectoryName(typeof(PosaDelCanaleTests).Assembly.Location)!, "Fixtures");

        private static SequenceModel Modello(string nome) =>
            SequenceModel.Leggi(File.ReadAllText(Path.Combine(CartellaFixture, nome + ".json")))!;

        [TestMethod]
        public void OgniBloccoHaUnaPosa() {
            foreach (var nome in Vive) {
                var m = Modello(nome);
                Assert.IsTrue(m.Blocchi.Count > 0, $"{nome}: nessun blocco");
                foreach (var b in m.Blocchi) {
                    Assert.IsNotNull(b.Sec, $"{nome}/{string.Join("+", b.Canali)}: la posa manca");
                    Assert.IsTrue(b.Sec > 0, $"{nome}/{string.Join("+", b.Canali)}: posa {b.Sec} s");
                }
            }
        }

        /*  LA RELAZIONE. Il motore somma le pose del canale sulle notti e ne dichiara le ore: i due numeri devono
         *  tornare con la posa. La tolleranza e' un secondo in ore, non una percentuale: le ore sono un derivato
         *  esatto, non una stima, e una percentuale nasconderebbe uno scarto grande su un canale lungo. */
        [TestMethod]
        public void LeOreDelCanaleTornanoConLaPosaELePose() {
            var provati = 0;
            foreach (var nome in Vive) {
                foreach (var b in Modello(nome).Blocchi) {
                    if (b.PoseCanale is null || b.OreCanale is null || b.Sec is null) continue;
                    provati++;
                    var atteso = b.PoseCanale.Value * b.Sec.Value / 3600.0;
                    Assert.AreEqual(atteso, b.OreCanale.Value, 1.0 / 3600.0,
                        $"{nome}/{string.Join("+", b.Canali)}: {b.PoseCanale} pose da {b.Sec} s fanno {atteso:F4} h, " +
                        $"il motore dice {b.OreCanale:F4} h");
                }
            }
            /*  SENZA UN CASO DA CONTROLLARE QUESTA PROVA NON DICE NIENTE. Se nessuna fixture portasse i campi del
             *  canale il ciclo girerebbe a vuoto e il verde sarebbe contro il nulla: allora si dichiara. */
            Assert.IsTrue(provati > 0,
                "nessuna fixture porta le pose del canale: non c'e' niente da verificare, e questo verde non vale");
        }

        [TestMethod]
        public void LaNotteEUnaFettaDelCanale() {
            var provati = 0;
            foreach (var nome in Vive) {
                foreach (var b in Modello(nome).Blocchi) {
                    if (b.PoseCanale is null || b.N is null) continue;
                    provati++;
                    Assert.IsTrue(b.N.Value <= b.PoseCanale.Value,
                        $"{nome}/{string.Join("+", b.Canali)}: stanotte {b.N} pose su {b.PoseCanale} del canale — " +
                        "una notte non puo' portarne piu' di tutto il progetto");
                }
            }
            Assert.IsTrue(provati > 0, "nessuna fixture porta le pose del canale: questo verde non vale");
        }

        /*  IL DOMINIO VIAGGIA COL NUMERO, o chi disegna deve inventarselo. `perBanda` dice se `poseCanale` e'
         *  di ciascuna banda o del blocco intero: senza, unire tre bande in una riga sola e scrivere «sul canale»
         *  farebbe leggere un terzo delle pose come se fossero tutte. */
        [TestMethod]
        public void IlNumeroDelCanaleNonViaggiaSenzaIlSuoDominio() {
            var provati = 0;
            foreach (var nome in Vive) {
                foreach (var b in Modello(nome).Blocchi) {
                    if (b.PoseCanale is null) continue;
                    provati++;
                    Assert.IsNotNull(b.PerBanda,
                        $"{nome}/{string.Join("+", b.Canali)}: il numero del canale arriva senza dire di che cosa e'");
                }
            }
            Assert.IsTrue(provati > 0, "nessuna fixture porta le pose del canale: questo verde non vale");
        }

        /*  E UN BLOCCO CHE HA FUSO PIU' CANALI NON PUO' DIRE «PER BANDA»: quel numero li ha sommati. Su una camera a
         *  matrice R, G e B sono la stessa ripresa, il motore li fonde e somma — la parola cambia col numero. */
        [TestMethod]
        public void UnBloccoFusoNonDiceDiEsserePerBanda() {
            foreach (var nome in Vive) {
                foreach (var b in Modello(nome).Blocchi) {
                    if (b.PerBanda is null || b.Canali.Count <= 1) continue;
                    Assert.IsFalse(b.PerBanda.Value,
                        $"{nome}/{string.Join("+", b.Canali)}: {b.Canali.Count} canali fusi in un blocco, " +
                        "e il numero si dichiara ancora «per banda»");
                }
            }
        }

        /*  L'OROLOGIO NON E' MAI MENO DEI FOTONI. Lo scarico e l'assestamento dopo il dither non si recuperano, e
         *  un orologio piu' corto dell'integrazione sarebbe una notte che finisce prima di essere cominciata. */
        [TestMethod]
        public void LOrologioNonEMaiMenoDellIntegrazione() {
            var provati = 0;
            foreach (var nome in Vive) {
                var t = Modello(nome).Totale;
                if (t?.Ore is null || t.Orologio is null) continue;
                provati++;
                Assert.IsTrue(t.Orologio.Value >= t.Ore.Value - 1e-9,
                    $"{nome}: {t.Ore:F2} h di fotoni in {t.Orologio:F2} h di orologio");
            }
            Assert.IsTrue(provati > 0, "nessuna fixture porta l'orologio: questo verde non vale");
        }
    }
}
