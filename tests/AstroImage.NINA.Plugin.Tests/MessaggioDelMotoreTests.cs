using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Text.Json;
using System.Text.RegularExpressions;
using AstroImage.NINA.Plugin.Localization;
using AstroImage.NINA.Plugin.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace AstroImage.NINA.Plugin.Tests {

    /*  L'ERRORE DEL MOTORE ARRIVA IN ITALIANO E DEVE USCIRE NELLA LINGUA DI CHI GUARDA.
     *
     *  Strategy parla una lingua sola: non e' una svista, e' che non ha un utente.
     *  Questo plugin un utente ce l'ha, ed e' bilingue. Fino a settembre 2026 la frase
     *  dell'errore arrivava gia' scritta dal motore e finiva a schermo com'era: chi
     *  aveva scelto l'inglese, sbagliando a scrivere il nome di un oggetto, si trovava
     *  davanti una riga in italiano. Non e' un caso raro — e' il modo normale di
     *  sbagliare a digitare.
     *
     *  Adesso il motore manda `codice` e `dati`, e la frase si scrive qui. Il difetto
     *  che questi test aspettano ha tre facce:
     *
     *    1. un codice noto al motore e ignoto al ponte — esce in italiano, in
     *       silenzio, e nessuno se ne accorge finche' non lo vede un utente;
     *    2. una frase che chiede tre valori a una tabella che ne dichiara due — il
     *       terzo segnaposto uscirebbe scritto in chiaro, «{2}», dentro il messaggio;
     *    3. un campo mancante che diventa un buco proprio nel punto che serviva a
     *       correggere l'errore: «SQM  non e' una brillanza di cielo».
     */
    [TestClass]
    public class MessaggioDelMotoreTests {

        [TestCleanup]
        public void Dopo() => Loc.Instance.ForzaLingua("it");

        private static Dictionary<string, string> Risorse(string lingua) {
            var rm = new ResourceManager("AstroImage.NINA.Plugin.Localization.Strings_" + lingua,
                                         typeof(Loc).Assembly);
            var set = rm.GetResourceSet(CultureInfo.InvariantCulture, true, true);
            Assert.IsNotNull(set, "le risorse " + lingua + " non sono dentro la DLL");
            var d = new Dictionary<string, string>();
            foreach (System.Collections.DictionaryEntry e in set!) d[(string)e.Key] = (string)(e.Value ?? "");
            return d;
        }

        private static IReadOnlyDictionary<string, string> Dati(string json) =>
            MessaggioDelMotore.Appiattisci(
                JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json));

        /*  I CODICI DEL MOTORE, COPIATI A MANO DA servizio/servizio.js.
         *
         *  A mano apposta. I due repository sono separati e questo non puo' leggere
         *  quell'altro: se un giorno potesse, il plugin pubblico dipenderebbe da un
         *  sorgente proprietario per compilare le sue prove. L'elenco e' quindi una
         *  DICHIARAZIONE, e il suo valore sta proprio nel fatto che qualcuno l'abbia
         *  scritta: dall'altra parte gate-servizio verifica che l'elenco la' sia
         *  chiuso, e quel fallimento e' il promemoria di venire ad aggiornare questo. */
        private static readonly string[] CodiciDelMotore = {
            "cielo_assente", "cielo_non_valido", "cielo_implausibile",
            "bersaglio_sconosciuto", "banco_sconosciuto", "nessuna_prescrizione",
            "via_sconosciuta", "richiesta_incompleta", "richiesta_troppo_grande",
            "json_illeggibile", "motore_in_errore",
        };

        [TestMethod]
        public void OgniCodiceDelMotoreHaUnaFrase() {
            var noti = MessaggioDelMotore.CodiciNoti;
            var senza = CodiciDelMotore.Where(c => !noti.Contains(c)).ToList();
            Assert.AreEqual(0, senza.Count,
                "il motore puo' mandare codici che il ponte non sa scrivere, e uscirebbero "
                + "in italiano: " + string.Join(", ", senza));
            var inventati = noti.Where(c => !CodiciDelMotore.Contains(c)).ToList();
            Assert.AreEqual(0, inventati.Count,
                "il ponte dichiara codici che il motore non emette: " + string.Join(", ", inventati));
        }

        [TestMethod]
        public void OgniFraseEsisteNelleDueLingue() {
            var it = Risorse("it");
            var en = Risorse("en");
            foreach (var codice in MessaggioDelMotore.CodiciNoti) {
                var f = MessaggioDelMotore.FraseDi(codice);
                Assert.IsNotNull(f, codice);
                Assert.IsTrue(it.ContainsKey(f!.Value.Chiave), "manca in italiano: " + f.Value.Chiave);
                Assert.IsTrue(en.ContainsKey(f.Value.Chiave), "manca in inglese: " + f.Value.Chiave);
                Assert.AreNotEqual(string.Empty, it[f.Value.Chiave].Trim(), f.Value.Chiave + " it e' vuota");
                Assert.AreNotEqual(string.Empty, en[f.Value.Chiave].Trim(), f.Value.Chiave + " en e' vuota");
            }
        }

        /*  IL CONTRATTO FRA LA TABELLA E IL TESTO. La tabella dice quali campi
         *  riempiono i segnaposto e in che ordine; la resx dice quanti segnaposto ci
         *  sono. Se i due numeri divergono, string.Format o solleva o lascia «{2}»
         *  scritto in chiaro davanti a un utente. */
        [TestMethod]
        public void ISegnapostoCorrispondonoAiCampiDichiarati() {
            foreach (var lingua in new[] { "it", "en" }) {
                var res = Risorse(lingua);
                foreach (var codice in MessaggioDelMotore.CodiciNoti) {
                    var f = MessaggioDelMotore.FraseDi(codice)!.Value;
                    var testo = res[f.Chiave];
                    var indici = Regex.Matches(testo, @"\{(\d)\}")
                                      .Cast<Match>().Select(m => int.Parse(m.Groups[1].Value))
                                      .Distinct().OrderBy(x => x).ToList();
                    var attesi = Enumerable.Range(0, f.Campi.Length).ToList();
                    CollectionAssert.AreEqual(attesi, indici,
                        lingua + "/" + f.Chiave + ": la tabella dichiara " + f.Campi.Length
                        + " campi (" + string.Join(",", f.Campi) + ") ma il testo usa i segnaposto "
                        + string.Join(",", indici));
                }
            }
        }

        [TestMethod]
        public void LaFraseCambiaConLaLingua() {
            var dati = Dati(@"{""ricevuto"":12,""min"":16,""max"":22}");

            Loc.Instance.ForzaLingua("it");
            var italiano = MessaggioDelMotore.Rendi("cielo_implausibile", dati, "frase del motore");
            Loc.Instance.ForzaLingua("en");
            var inglese = MessaggioDelMotore.Rendi("cielo_implausibile", dati, "frase del motore");

            Assert.AreNotEqual(italiano, inglese, "la frase non cambia con la lingua");
            StringAssert.Contains(inglese, "imaging site", "l'inglese non e' l'inglese");
            StringAssert.Contains(italiano!, "sito di ripresa", "l'italiano non e' l'italiano");
        }

        /*  IL VALORE RICEVUTO DEVE COMPARIRE. Senza, resta «il valore non va bene»:
         *  la meta' inutile del messaggio, quella che non dice come correggerlo. */
        [TestMethod]
        public void IlValoreRicevutoCompareNelleDueLingue() {
            foreach (var lingua in new[] { "it", "en" }) {
                Loc.Instance.ForzaLingua(lingua);
                var s = MessaggioDelMotore.Rendi("cielo_implausibile",
                    Dati(@"{""ricevuto"":12,""min"":16,""max"":22}"), "ripiego");
                StringAssert.Contains(s!, "12", lingua + ": manca il valore ricevuto");
                StringAssert.Contains(s!, "16", lingua + ": manca il limite basso");
                StringAssert.Contains(s!, "22", lingua + ": manca il limite alto");

                var t = MessaggioDelMotore.Rendi("bersaglio_sconosciuto",
                    Dati(@"{""chiesto"":""ngc99999""}"), "ripiego");
                StringAssert.Contains(t!, "ngc99999", lingua + ": manca il nome chiesto");
            }
        }

        /*  I DUE RIPIEGHI. Meglio una frase intera nella lingua sbagliata che una
         *  frase nella lingua giusta con un buco nel punto che conta. */
        [TestMethod]
        public void UnCodiceIgnotoRipiegaSullaFraseDelMotore() {
            Assert.AreEqual("la frase italiana del motore",
                MessaggioDelMotore.Rendi("codice_del_futuro", Dati("{}"), "la frase italiana del motore"));
            Assert.AreEqual("la frase italiana del motore",
                MessaggioDelMotore.Rendi(null, null, "la frase italiana del motore"));
        }

        [TestMethod]
        public void UnCampoMancanteRipiegaInveceDiLasciareUnBuco() {
            Loc.Instance.ForzaLingua("en");
            //  `min` e `max` ci sono, `ricevuto` no: la frase inglese uscirebbe monca
            //  proprio dove va il numero sbagliato.
            var s = MessaggioDelMotore.Rendi("cielo_implausibile",
                Dati(@"{""min"":16,""max"":22}"), "SQM boh non va bene");
            Assert.AreEqual("SQM boh non va bene", s,
                "con un campo mancante deve tornare la frase del motore, non una frase bucata");
        }

        /*  UN MOTORE PIU' VECCHIO DEL CONTRATTO non manda `dati`: il ponte non deve
         *  smettere di dire le cose, deve dirle in italiano. */
        [TestMethod]
        public void UnMotoreSenzaDatiNonRompeNiente() {
            Loc.Instance.ForzaLingua("en");
            Assert.AreEqual("SQM 12 non e' un sito",
                MessaggioDelMotore.Rendi("cielo_implausibile", null, "SQM 12 non e' un sito"));
            //  Ma un codice che non chiede campi si traduce lo stesso, dati o no.
            var s = MessaggioDelMotore.Rendi("nessuna_prescrizione", null, "frase italiana");
            StringAssert.Contains(s!, "no workable road");
        }

        /*  I NUMERI SI SCRIVONO COME OVUNQUE NELL'APP. Un SQM e' 20.8 nella pagina,
         *  nei log del motore e nel file che va al Sequenziatore: farlo diventare 20,8
         *  solo dentro un messaggio d'errore obbligherebbe chi confronta a chiedersi
         *  se sono lo stesso valore. */
        [TestMethod]
        public void INumeriNonSiPieganoAllaCultura() {
            var prima = System.Threading.Thread.CurrentThread.CurrentCulture;
            try {
                System.Threading.Thread.CurrentThread.CurrentCulture = new CultureInfo("it-IT");
                var d = Dati(@"{""ricevuto"":20.8}");
                Assert.AreEqual("20.8", d["ricevuto"], "il punto decimale e' diventato una virgola");
            } finally { System.Threading.Thread.CurrentThread.CurrentCulture = prima; }
        }

        [TestMethod]
        public void UnaListaDiventaUnaRigaSola() {
            Loc.Instance.ForzaLingua("it");
            var d = Dati(@"{""mancano"":[""sito"",""banco""]}");
            Assert.AreEqual("sito, banco", d["mancano"]);
            var s = MessaggioDelMotore.Rendi("richiesta_incompleta", d, "ripiego");
            StringAssert.Contains(s!, "sito, banco");
        }

        [TestMethod]
        public void ILimitiDelCieloArrivanoDalMotoreNonDalPonte() {
            /*  16 e 22 non sono scritti da nessuna parte nel ponte: arrivano dentro
             *  `dati`. Se un giorno il motore cambiasse la forbice, la frase la
             *  seguirebbe da sola — e questa prova e' l'unica cosa che impedisce a
             *  qualcuno di ricablarli qui «per comodita'». */
            Loc.Instance.ForzaLingua("en");
            var s = MessaggioDelMotore.Rendi("cielo_implausibile",
                Dati(@"{""ricevuto"":9,""min"":10,""max"":30}"), "ripiego");
            StringAssert.Contains(s!, "10");
            StringAssert.Contains(s!, "30");
            Assert.IsFalse(s!.Contains("16"), "16 e' cablato nel ponte invece di venire dai dati");
        }

        /*  IL DETTAGLIO GREZZO NON SI TRADUCE, MA NON SI LASCIA NEMMENO SOLO.
         *  E' il messaggio di un'eccezione JavaScript: non lo scriviamo noi, quindi
         *  nessuno lo puo' tradurre. Va incastonato in una frase nostra, cosi' chi
         *  legge sa se e' colpa sua o di un guasto. */
        [TestMethod]
        public void IlDettaglioGrezzoViveDentroUnaFraseNostra() {
            Loc.Instance.ForzaLingua("en");
            var s = MessaggioDelMotore.Rendi("banco_sconosciuto",
                Dati(@"{""dettaglio"":""telescopio non trovato: pippo""}"), "ripiego");
            StringAssert.Contains(s!, "telescopio non trovato: pippo", "il dettaglio e' sparito");
            Assert.IsTrue(s!.Length > 40, "il dettaglio e' rimasto solo, senza una frase intorno");
            StringAssert.Contains(s, "equipment", "la frase intorno non e' nella lingua scelta");
        }
    }
}
