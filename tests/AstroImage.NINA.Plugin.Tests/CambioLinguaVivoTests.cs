using System;
using System.Reflection;
using System.Text.RegularExpressions;
using AstroImage.NINA.Plugin.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace AstroImage.NINA.Plugin.Tests {

    /*  GIRARE L'INTERRUTTORE DEVE VEDERSI SUBITO.
     *
     *  Le Opzioni cambiavano lingua all'istante e il pannello no: restava scritto come
     *  prima finche' non lo si chiudeva e riapriva. Non era un guasto — le frasi
     *  nascono nel C# e la pagina le aveva gia' ricevute — ma a chi gira l'interruttore
     *  un pannello che non cambia sembra un plugin rotto, e in un plugin pubblico la
     *  differenza fra «rotto» e «fatto apposta» la fa solo cio' che si vede.
     *
     *  La reazione vera vuole WPF e WebView2, che qui non ci sono. Quello che si puo'
     *  bloccare — e che e' la parte che qualcuno rifara' male fra un anno — e' la
     *  decisione: che cosa si richiede quando la lingua cambia, e cosa no.
     *
     *  Si legge la pagina per riflessione perche' e' internal. E' la stessa strada di
     *  TipiDelPonte, e serve a non aprire l'assembly ai test per una costante sola.
     */
    [TestClass]
    public class CambioLinguaVivoTests {

        /*  La pagina non e' piu' una costante: e' tre risorse dentro la DLL che Pagina
         *  ricompone. Leggerla di qui vuol dire anche PROVARE che la ricomposizione
         *  funziona — se un file non fosse incorporato, o se prova.html avesse perso il
         *  collegamento al foglio o allo script, questo solleverebbe subito invece di
         *  lasciarlo scoprire a pannello aperto. */
        private static string Pagina() {
            var t = typeof(SequenceModel).Assembly.GetType("AstroImage.NINA.Plugin.Views.Pagina");
            Assert.IsNotNull(t, "la pagina di prova non si trova piu': e' stata rinominata?");
            var p = t!.GetProperty("Prova", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(p, "la pagina non si trova piu'");
            return (string)p!.GetValue(null)!;
        }

        [TestMethod]
        public void LaPaginaASCOLTA_AncheCioCheNonHaChiesto() {
            /*  Fino a ieri ogni messaggio dell'ospite era la risposta a una domanda, e
                la pagina buttava via tutto quello che non aveva un id fra le attese. Un
                avviso spinto dall'ospite finiva nel nulla, in silenzio. */
            var p = Pagina();
            StringAssert.Contains(p, "evento === 'lingua'",
                "senza questo ramo il messaggio del cambio lingua viene ignorato senza dire niente");
            StringAssert.Contains(p, "function ridisegna()");
        }

        [TestMethod]
        public void RidisegnandoSiRichiedonoSitoEFiltri_ENONLaPrescrizione() {
            /*  LA DECISIONE, non la meccanica.

                Sito e filtri sono letture: si possono richiedere quanto si vuole e
                tornano nella lingua nuova.

                La prescrizione no. Richiederla vorrebbe dire una chiamata al motore e
                forse numeri diversi per aver girato un interruttore della lingua —
                cambiare i dati mentre l'utente credeva di cambiare le parole. Quello che
                e' gia' sullo schermo e' il resoconto di una cosa avvenuta: resta nella
                lingua in cui e' avvenuta, e la prossima esce nell'altra. */
            var corpo = CorpoDi(Pagina(), "function ridisegna()");

            StringAssert.Contains(corpo, "chiedi('sito')");
            StringAssert.Contains(corpo, "chiedi('filtri')");
            Assert.IsFalse(corpo.Contains("prescrizione"),
                "richiedere la prescrizione al cambio lingua cambierebbe i NUMERI mentre " +
                "l'utente credeva di cambiare le PAROLE");
            Assert.IsFalse(corpo.Contains("manda"),
                "e tantomeno si riconsegna qualcosa a N.I.N.A. per un cambio di lingua");
        }

        [TestMethod]
        public void AllAperturaSiPassaDALLOSTESSOPunto() {
            /*  Se l'avvio chiedesse per conto suo invece di passare da ridisegna, fra sei
                mesi le due strade avrebbero due elenchi diversi e nessuno se ne
                accorgerebbe: il pannello aperto mostrerebbe una cosa, quello aperto e poi
                cambiato di lingua un'altra. */
            var p = Pagina();
            /*  Si contano le CHIAMATE, non la definizione: «function ridisegna()» e'
                la terza occorrenza e non e' un uso. */
            Assert.AreEqual(2, Regex.Matches(p, @"(?<!function )\bridisegna\(\)").Count,
                "ridisegna va chiamata due volte: all'apertura e al cambio lingua. Se sono " +
                "di piu' o di meno, l'avvio e il cambio hanno preso strade diverse");
        }

        /// <summary>Il corpo di una funzione JavaScript, contando le graffe.</summary>
        private static string CorpoDi(string testo, string firma) {
            var i = testo.IndexOf(firma, StringComparison.Ordinal);
            Assert.IsTrue(i >= 0, "non trovo «" + firma + "» nella pagina");
            var apre = testo.IndexOf('{', i);
            var livello = 0;
            for (var j = apre; j < testo.Length; j++) {
                if (testo[j] == '{') livello++;
                else if (testo[j] == '}' && --livello == 0) return testo.Substring(apre, j - apre + 1);
            }
            Assert.Fail("graffe non bilanciate dopo «" + firma + "»");
            return "";
        }
    }
}
