using System.Linq;
using System.Text.Json;
using AstroImage.NINA.Plugin.Localization;
using AstroImage.NINA.Plugin.Models;
using AstroImage.NINA.Plugin.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace AstroImage.NINA.Plugin.Tests {

    /*  L'ORIZZONTE DEL SITO E' UNA MONTAGNA, NON UN NUMERO.
     *
     *  Fino a settembre 2026 questo ponte leggeva latitudine e longitudine dal profilo
     *  di N.I.N.A. e lasciava perdere l'Orizzonte Personalizzato, per una ragione che
     *  stava scritta in DichiarazioneSito e che era giusta: il motore accettava un
     *  numero solo, e ridurre un profilo per azimut a un numero e' una derivazione con
     *  perdita — «il massimo nasconde meta' cielo, il minimo fa riprendere dentro la
     *  casa». Adesso il motore accetta il profilo, e il ponte lo manda intero.
     *
     *  Il difetto che queste prove aspettano ha due facce, e sono opposte:
     *
     *    - il profilo NON arriva, e il motore torna a credere che il cielo sia uguale
     *      in tutte le direzioni. Sul profilo vero di Borno vuol dire regalare ore
     *      verso una montagna di quaranta gradi e negarle verso una valle di otto;
     *    - il profilo arriva RIDOTTO — otto settori invece di trecentosessanta punti,
     *      o peggio un minimo — e la perdita non si vede perche' il numero c'e'.
     */
    [TestClass]
    public class OrizzonteRealeTests {

        [TestCleanup]
        public void Dopo() => Loc.Instance.ForzaLingua("it");

        /*  Il profilo vero di Borno, ridotto a un punto ogni quindici gradi dal file di
         *  360 righe che Alessandro ha scritto nel marzo 2025. Non e' un profilo
         *  inventato per far passare una prova: e' la montagna dietro casa — 39.7 gradi
         *  a nord-est — e la valle davanti — 8.0 a sud. */
        private static double[][] Borno() => new[] {
            new[] {   0d, 33.8 }, new[] {  15d, 38.4 }, new[] {  30d, 41.0 }, new[] {  45d, 39.7 },
            new[] {  60d, 36.2 }, new[] {  75d, 36.9 }, new[] {  90d, 35.8 }, new[] { 105d, 19.1 },
            new[] { 120d, 13.8 }, new[] { 135d, 10.8 }, new[] { 150d, 11.2 }, new[] { 165d, 14.7 },
            new[] { 180d,  8.0 }, new[] { 195d, 11.1 }, new[] { 210d, 10.4 }, new[] { 225d, 13.9 },
            new[] { 240d, 15.9 }, new[] { 255d, 22.1 }, new[] { 270d, 23.8 }, new[] { 285d, 33.5 },
            new[] { 300d, 28.2 }, new[] { 315d, 33.7 }, new[] { 330d, 37.0 }, new[] { 345d, 36.6 },
        };

        private static SitoDiRipresa DaNina(double[][]? oriz, string? file = null) =>
            new SitoDiRipresa { Lat = 45.95, Lon = 10.2, Orizzonte = oriz, OrizzonteFile = file };

        [TestMethod]
        public void IlProfiloArrivaINTERO_ENonRidotto() {
            var s = DichiarazioneSito.Unisci(DaNina(Borno()), new SitoDichiarato { Sqm = 20.8 });
            Assert.IsNotNull(s.Orizzonte, "il profilo non e' arrivato: il motore tornerebbe al numero");
            Assert.AreEqual(24, s.Orizzonte!.Length, "il profilo e' stato ridotto");
            //  I due estremi sono la ragione per cui un numero solo non basta: fra la
            //  montagna e la valle ci sono trentatre gradi.
            var alt = s.Orizzonte.Select(p => p[1]).ToList();
            Assert.AreEqual(8.0, alt.Min(), 1e-9, "la valle a sud e' sparita");
            Assert.AreEqual(41.0, alt.Max(), 1e-9, "la montagna a nord e' sparita");
            Assert.IsTrue(alt.Max() - alt.Min() > 30,
                "un profilo con questa escursione ridotto a un numero sbaglia di trenta gradi");
        }

        /*  E OGNI PUNTO PORTA IL SUO AZIMUT. Un profilo che perdesse l'ordine, o
         *  l'azimut, sarebbe una lista di altezze senza direzione: il motore la
         *  interpolerebbe sul nulla. */
        [TestMethod]
        public void OgniPuntoPortaIlSuoAzimut() {
            var s = DichiarazioneSito.Unisci(DaNina(Borno()), new SitoDichiarato());
            foreach (var p in s.Orizzonte!) {
                Assert.AreEqual(2, p.Length, "un punto non e' [azimut, altezza]");
                Assert.IsTrue(p[0] >= 0 && p[0] < 360, "azimut fuori scala: " + p[0]);
            }
            var az = s.Orizzonte!.Select(p => p[0]).ToList();
            CollectionAssert.AreEqual(az.OrderBy(x => x).ToList(), az, "gli azimut non sono in ordine");
            Assert.AreEqual(az.Count, az.Distinct().Count(), "due punti allo stesso azimut");
        }

        /*  L'ORIZZONTE VIENE DAL PROFILO E NON SI DICHIARA, come le coordinate: nessun
         *  campo di testo puo' contenere una montagna. La prova sta anche nel fatto che
         *  `SitoDichiarato` non ha proprio la proprieta' — se un giorno qualcuno gliela
         *  aggiungesse, questa riga non compilerebbe piu' ed e' esattamente quello che
         *  deve succedere. */
        [TestMethod]
        public void LOrizzonteNonSiDichiara() {
            var campi = typeof(SitoDichiarato).GetProperties().Select(p => p.Name).ToList();
            CollectionAssert.DoesNotContain(campi, "Orizzonte",
                "l'orizzonte e' diventato dichiarabile: una montagna non si scrive in un campo");
            //  La provenienza si confronta con quella della GEOMETRIA, non con una
            //  stringa scritta qui: sono la stessa cosa — dati che vengono dal profilo
            //  e che nessuno dichiara — e scrivere il testo a mano legherebbe la prova
            //  alla lingua invece che al concetto.
            var s = DichiarazioneSito.Unisci(DaNina(Borno()), new SitoDichiarato { HorizonMin = 20 });
            Assert.AreEqual(s.Provenienza!["lat"], s.Provenienza!["orizzonte"],
                "l'orizzonte non ha la stessa provenienza delle coordinate");
        }

        /*  SENZA ORIZZONTE NON SI INVENTA NIENTE, ed e' lo stesso trattamento dell'SQM.
         *  N.I.N.A. non fabbrica un orizzonte piatto a zero quando manca il file, e il
         *  ponte non deve farlo al posto suo. */
        [TestMethod]
        public void SenzaOrizzonteRestaNullo_ENonDiventaPiatto() {
            var s = DichiarazioneSito.Unisci(DaNina(null), new SitoDichiarato { HorizonMin = 20 });
            Assert.IsNull(s.Orizzonte, "e' comparso un orizzonte che nessuno ha dichiarato");
            //  Stesso motivo di sopra: si confronta con un campo che manca davvero.
            var senzaTutto = DichiarazioneSito.Unisci(new SitoDiRipresa(), new SitoDichiarato());
            Assert.AreEqual(senzaTutto.Provenienza!["lat"], s.Provenienza!["orizzonte"],
                "un orizzonte che non c'e' deve dirlo come lo dice un campo assente");
            Assert.AreNotEqual(s.Provenienza!["lat"], s.Provenienza!["orizzonte"],
                "assente e presente si somigliano troppo");
            //  Il ripiego c'e', ed e' il numero che si dichiara.
            Assert.AreEqual(20, s.HorizonMin);
        }

        /*  E ATTRAVERSA IL FILO COM'E'. Il motore sta dietro una porta HTTP e puo' girare
         *  su un'altra macchina: se il profilo non sopravvive alla serializzazione, il
         *  ponte lo legge da N.I.N.A. e lo perde un metro dopo. */
        [TestMethod]
        public void IlProfiloSopravviveAlFilo() {
            var s = DichiarazioneSito.Unisci(DaNina(Borno(), @"C:\Users\x\Borno Casa .hrz"),
                                             new SitoDichiarato { Sqm = 20.8 });
            var json = JsonSerializer.Serialize(s);
            var torna = JsonSerializer.Deserialize<SitoDiRipresa>(json);

            Assert.IsNotNull(torna?.Orizzonte);
            Assert.AreEqual(24, torna!.Orizzonte!.Length);
            for (var i = 0; i < 24; i++) {
                Assert.AreEqual(s.Orizzonte![i][0], torna.Orizzonte[i][0], 1e-9, "azimut " + i);
                Assert.AreEqual(s.Orizzonte![i][1], torna.Orizzonte[i][1], 1e-9, "altezza " + i);
            }
            StringAssert.Contains(json, "\"orizzonte\"", "la chiave sul filo non e' quella che il motore legge");
            //  Il file si dice a chi guarda, ma il motore non lo usa: puo' girare su
            //  un'altra macchina, dove quel percorso non esiste.
            Assert.AreEqual(@"C:\Users\x\Borno Casa .hrz", torna.OrizzonteFile);
        }

        /*  E NON PESA TROPPO: 360 punti sono il caso vero, e il servizio rifiuta i corpi
         *  sopra un megabyte. Se un giorno qualcuno campionasse piu' fitto, questa prova
         *  glielo dice prima che lo scopra un utente con un 413. */
        [TestMethod]
        public void UnProfiloPienoNonSforaIlCorpoDellaRichiesta() {
            var pieno = Enumerable.Range(0, 360)
                .Select(az => new[] { (double)az, 20 + 15 * System.Math.Sin(az * System.Math.PI / 180) })
                .ToArray();
            var s = DichiarazioneSito.Unisci(DaNina(pieno), new SitoDichiarato());
            var byte_ = JsonSerializer.Serialize(s).Length;
            Assert.AreEqual(360, s.Orizzonte!.Length);
            Assert.IsTrue(byte_ < 100_000,
                "un profilo pieno pesa " + byte_ + " byte: troppo per una richiesta");
        }
    }
}
