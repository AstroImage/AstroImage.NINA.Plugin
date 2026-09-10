using System;
using System.Linq;
using System.Text.RegularExpressions;
using AstroImage.NINA.Plugin.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace AstroImage.NINA.Plugin.Tests {

    /*  LA RUOTA IN FILA — CIO' CHE SI PUO' DAVVERO SORVEGLIARE.
     *
     *  Le prove di questo progetto girano in MSTest, senza browser: non c'e' modo di
     *  verificare che uno slot si disegni nel posto giusto o che un clic lo selezioni.
     *  Quello si guarda a pannello aperto, ed e' cosi' che sono stati trovati tutti i
     *  difetti seri di questa fase.
     *
     *  Quello che invece si puo' bloccare — e che qualcuno rifara' male fra un anno —
     *  sono le DECISIONI: che la pagina non calcoli, che i colori non siano l'unico
     *  modo di capire, che uno slot senza dati si veda lo stesso, che un dato assente
     *  non diventi un trattino qualunque in mezzo agli altri.
     */
    [TestClass]
    public class RuotaVisualeTests {

        private static string Js() {
            using (var f = typeof(SequenceModel).Assembly
                    .GetManifestResourceStream("AstroImage.NINA.Plugin.Views.Pagina.prova.js")) {
                Assert.IsNotNull(f, "prova.js non e' incorporata");
                using (var l = new System.IO.StreamReader(f!)) { return l.ReadToEnd(); }
            }
        }

        private static string Css() {
            using (var f = typeof(SequenceModel).Assembly
                    .GetManifestResourceStream("AstroImage.NINA.Plugin.Views.Pagina.prova.css")) {
                Assert.IsNotNull(f, "prova.css non e' incorporata");
                using (var l = new System.IO.StreamReader(f!)) { return l.ReadToEnd(); }
            }
        }

        /// <summary>Lo script senza i commenti: quelli restano italiani per scelta.</summary>
        private static string Codice() =>
            Regex.Replace(Js(), @"/\*.*?\*/", "", RegexOptions.Singleline);

        [TestMethod]
        public void LE_BANDE_ARRIVANO_DAL_MOTORE_NonSiCalcolano() {
            /*  Le otto bande che il catalogo del motore usa davvero: Ha, OIII, SII, L,
                R, G, B e «dual». Qui si sceglie solo COME si vedono. Se un giorno
                qualcuno aggiungesse una regola per DEDURRE una banda da un nome o da un
                FWHM, sarebbe il ponte che comincia a decidere. */
            var c = Codice();
            StringAssert.Contains(c, "dellaBanda", "manca la tavolozza delle bande");

            foreach (var vietato in new[] { "includes('Ha')", "indexOf('Ha')", "startsWith('Ha')",
                                            "banda =", "banda=" })
                Assert.IsFalse(c.Contains(vietato),
                    $"«{vietato}»: la banda si riceve, non si ricava");

            /*  `x.banda || null` va benissimo: ripiega su NIENTE, e niente e' la verita'
                quando la banda non c'e'. Quello che non deve esistere e' un ripiego su
                un valore — `x.banda || 'L'` sarebbe una banda inventata, e sarebbe
                indistinguibile da una dichiarata. */
            /*  Lo spazio sta DENTRO la previsione, non prima: scritto come
                `\|\|\s*(?!null)` lo spazio farebbe da via d'uscita — `\s*` torna
                indietro a zero e la previsione guarda lo spazio invece di `null`. */
            Assert.IsFalse(Regex.IsMatch(c, @"\.banda\s*\|\|(?!\s*null)"),
                "la banda non deve avere un ripiego diverso da null: sarebbe inventata");
        }

        [TestMethod]
        public void IL_COLORE_NON_E_MAI_LUNICA_Informazione() {
            /*  La sigla della banda e' sempre scritta dentro la pastiglia — non e' un
                quadratino colorato. Chi non distingue i colori legge la pagina lo
                stesso. */
            var c = Codice();
            StringAssert.Contains(c, "esc(x.banda)", "la pastiglia deve scrivere la sigla, non solo colorarsi");
            StringAssert.Contains(c, "esc(bande[0])", "e anche le due righe di un dual");
        }

        [TestMethod]
        public void LO_STATO_E_SEMPRE_UNA_PAROLA_DaQualcheParte() {
            /*  Nella scheda della fila lo stato e' un segno solo, per far stare dieci
                slot sott'occhio. Ma la PAROLA deve esistere in due posti: nel
                suggerimento del riquadro, e per esteso nella scheda dello slot scelto.
                Un segno con un colore e nient'altro sarebbe un'icona muta — e il
                suggerimento da solo non basta, perche' non si legge senza il mouse.

                Il testo esce dallo stesso dizionario di tutto il resto, quindi cambia
                lingua con la pagina senza doverlo trattare a parte. */
            var c = Codice();
            StringAssert.Contains(c, "spiega[x.stato]",
                "il suggerimento del riquadro deve dire lo stato a parole");
            StringAssert.Contains(c, "spiega[scelto.stato]",
                "e la scheda dello slot scelto lo deve dire per esteso, sempre");

            foreach (var k in new[] { "Pag_StatoDichiarato", "Pag_StatoDaDichiarare",
                                      "Pag_StatoIgnoto", "Pag_StatoOrfano" })
                StringAssert.Contains(c, k, $"lo stato «{k}» deve venire dal dizionario");
        }

        [TestMethod]
        public void UN_DUAL_MOSTRA_ENTRAMBE_LeRighe() {
            /*  Un dual e' un filtro solo che raccoglie due righe. Mostrarne una sarebbe
                dire meno di quello che il motore ha dichiarato — e su una camera a
                matrice e' proprio la differenza che conta. */
            var c = Codice();
            StringAssert.Contains(c, "bande.join('+')", "le due righe vanno mostrate insieme");
            StringAssert.Contains(c, "vociCatalogo[", "le righe stanno nel catalogo, si cercano per id");
        }

        [TestMethod]
        public void GLI_SLOT_NON_DICHIARATI_SI_VEDONO_LoStesso() {
            /*  Uno slot che sparisce perche' non ha dati e' un'assenza che si scopre
                sotto il cielo. Si disegnano TUTTE le righe, e chi non e' piu' in ruota
                si vede spento invece di essere tolto. */
            var c = Codice();
            StringAssert.Contains(c, "righeRuota.map(", "la fila si costruisce su tutte le righe");
            Assert.IsFalse(Regex.IsMatch(c, @"righeRuota\s*\.\s*filter\("),
                "nessuno slot va tolto dalla fila: uno slot che manca e' un'informazione");
            StringAssert.Contains(Css(), ".slot.orfano", "un orfano si vede spento, non sparisce");
        }

        [TestMethod]
        public void UN_DATO_ASSENTE_SI_VEDE_ASSENTE_NonDiventaUnTrattino() {
            var c = Codice();
            StringAssert.Contains(c, "Pag_NonDisponibile", "un dato che manca si dice a parole");
            StringAssert.Contains(Css(), ".dato .assente", "e si vede diverso da un valore vero");
        }

        [TestMethod]
        public void LA_PAGINA_NON_CALCOLA_Niente() {
            /*  La regola del progetto, tradotta in una guardia. Il ponte consegna e
                mostra; il motore decide. Se qui comparisse un ordinamento per qualita',
                una scelta del filtro migliore o un conto sull'idoneita', sarebbero due
                motori che possono divergere — e nessuno saprebbe quale ha ragione. */
            var c = Codice();
            foreach (var vietato in new[] { ".sort(", "Math.", "adatto =", "adatto=", "fwhm_nm =" })
                Assert.IsFalse(c.Contains(vietato),
                    $"«{vietato}» nella pagina: e' il motore che decide, non lei");
        }

        [TestMethod]
        public void LO_SLOT_SCELTO_SOPRAVVIVE_MaNonEsceDaiBordi() {
            var c = Codice();
            StringAssert.Contains(c, "let slotScelto", "la scelta vive fuori dal ridisegno");
            StringAssert.Contains(c, "slotScelto >= righeRuota.length",
                "se la ruota cambia sotto, lo slot scelto puo' non esistere piu'");
        }

        [TestMethod]
        public void LA_FILA_E_ORIZZONTALE_E_NON_VA_A_CAPO() {
            /*  L'ordine di una ruota e' l'ordine fisico degli slot: se andasse a capo,
                la settima posizione smetterebbe di essere la settima da sinistra in una
                finestra stretta. Scorre invece di avvolgersi. */
            var css = Css();
            StringAssert.Contains(css, ".ruota { display:flex");
            StringAssert.Contains(css, "overflow-x:auto");
            Assert.IsFalse(Regex.IsMatch(css, @"\.ruota\s*\{[^}]*flex-wrap\s*:\s*wrap"),
                "la fila non deve andare a capo: perderebbe l'ordine fisico");
        }

        [TestMethod]
        public void OGNI_BANDA_DEL_CATALOGO_HA_IL_SUO_Colore() {
            /*  Le otto che il motore usa davvero. Se ne comparisse una nona senza colore
                cadrebbe sul neutro — visibile ma muta — e questa prova lo direbbe prima. */
            var css = Css();
            foreach (var b in new[] { "b-ha", "b-oiii", "b-sii", "b-l", "b-r", "b-g", "b-b", "b-dual", "vuota" })
                StringAssert.Contains(css, ".banda." + b, $"manca il colore per «{b}»");
        }
    }
}
