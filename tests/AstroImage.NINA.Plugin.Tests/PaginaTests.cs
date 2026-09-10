using System;
using System.Linq;
using System.Reflection;
using AstroImage.NINA.Plugin.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace AstroImage.NINA.Plugin.Tests {

    /*  LA PAGINA E' TRE FILE, E DEVONO ESSERE DENTRO IL BINARIO.
     *
     *  Da quando prova.html, prova.css e prova.js sono file veri invece di una stringa
     *  dentro un .cs, si e' aperta una porta che prima non esisteva: si puo' rinominare
     *  un file, o spostare la cartella, e il .csproj resta indietro. Il compilatore non
     *  dice niente — sono risorse, non codice — e il guasto si presenta a pannello
     *  aperto, bianco, senza un errore da nessuna parte.
     *
     *  Lo stesso vale per i due segnaposto dentro prova.html: WebView2 riceve la pagina
     *  con NavigateToString e non ha una cartella da cui risolvere gli indirizzi, quindi
     *  un <link href="prova.css"> rimasto li' non caricherebbe niente E NON DIREBBE
     *  NIENTE. Il pannello si aprirebbe senza stile, e uno penserebbe di aver sbagliato
     *  il CSS.
     *
     *  Tutte cose che costano mezz'ora a scoprirle e mezzo secondo a verificarle.
     */
    [TestClass]
    public class PaginaTests {

        private static Assembly Ponte => typeof(SequenceModel).Assembly;

        private static string Pagina() {
            var t = Ponte.GetType("AstroImage.NINA.Plugin.Views.Pagina");
            Assert.IsNotNull(t, "la pagina non si trova piu': e' stata rinominata?");
            var p = t!.GetProperty("Prova", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(p, "Prova non e' piu' una proprieta' statica");
            return (string)p!.GetValue(null)!;
        }

        [TestMethod]
        public void ITreFileSonoDENTRO_LaDll() {
            var presenti = Ponte.GetManifestResourceNames();
            foreach (var nome in new[] { "prova.html", "prova.css", "prova.js" }) {
                var atteso = "AstroImage.NINA.Plugin.Views.Pagina." + nome;
                Assert.IsTrue(presenti.Contains(atteso),
                    $"«{atteso}» non e' incorporata. Nel .csproj serve una riga EmbeddedResource. " +
                    "Ci sono: " + string.Join(", ", presenti));
            }
        }

        [TestMethod]
        public void SiRicompongonoInUnDocumentoSOLO() {
            var p = Pagina();

            StringAssert.StartsWith(p, "<!doctype html>");
            StringAssert.Contains(p, "<style>", "il foglio non e' entrato");
            StringAssert.Contains(p, "<script>", "lo script non e' entrato");
            /*  Ci si ancora al CONTENUTO, non alla sua punteggiatura: la prima versione
                cercava «:root { color-scheme: dark; }» con la graffa di chiusura, e si e'
                rotta il giorno in cui al blocco `:root` sono state aggiunte le variabili
                di colore. Cio' che la prova difende e' che il foglio sia entrato, non
                come sia scritto dentro. */
            StringAssert.Contains(p, "color-scheme: dark", "il contenuto del foglio non c'e'");
            StringAssert.Contains(p, ".goalcard", "il foglio e' entrato a meta'");
            StringAssert.Contains(p, "window.chrome.webview.postMessage", "il contenuto dello script non c'e'");
        }

        [TestMethod]
        public void NESSUN_RiferimentoESTERNO_RestaNellaPagina() {
            /*  Se ne restasse uno, non darebbe errore: darebbe un pannello senza stile o
                senza comportamento, e nessuno saprebbe perche'. */
            var p = Pagina();
            Assert.IsFalse(p.Contains("href=\"prova.css\""), "il collegamento al foglio non e' stato sostituito");
            Assert.IsFalse(p.Contains("src=\"prova.js\""), "il collegamento allo script non e' stato sostituito");
            Assert.IsFalse(p.Contains("<link "), "in una pagina caricata da stringa un <link> non carica niente");
        }

        [TestMethod]
        public void LaPaginaNonCONOSCE_NessunIndirizzo() {
            /*  La regola piu' vecchia del ponte, e l'unica che protegge un segreto: la
                pagina manda un messaggio all'ospite, e l'indirizzo del motore vive nel
                C#, dove chi apre gli strumenti di sviluppo non lo trova. Adesso che la
                pagina e' un file modificabile da chiunque, vale la pena tenerla sotto
                guardia invece che sotto parola. */
            var p = Pagina();
            foreach (var spia in new[] { "http://", "https://", "localhost", "127.0.0.1", "8791" })
                Assert.IsFalse(p.Contains(spia, StringComparison.OrdinalIgnoreCase),
                    $"la pagina contiene «{spia}»: gli indirizzi stanno nel C#, non qui");
        }

        // ── 0b: le parole non stanno nella pagina ────────────────────────────────

        private static string Risorsa(string nome) {
            using (var f = Ponte.GetManifestResourceStream("AstroImage.NINA.Plugin.Views.Pagina." + nome)) {
                Assert.IsNotNull(f, nome + " non e' incorporata");
                using (var l = new System.IO.StreamReader(f!)) { return l.ReadToEnd(); }
            }
        }

        [TestMethod]
        public void IlDizionarioENTRA_NellaPagina() {
            var p = Pagina();
            StringAssert.Contains(p, "window.__LOC__=", "senza dizionario la pagina mostrerebbe i nomi delle chiavi");
            Assert.IsFalse(p.Contains("<!--voci-->"), "il segnaposto delle voci non e' stato sostituito");
            StringAssert.Contains(p, "\"Pag_Intestazione\"", "una voce che dovrebbe esserci non c'e'");
        }

        [TestMethod]
        public void ALLA_PAGINA_VANNO_SOLO_LE_SUE_Parole() {
            /*  Il prefisso non e' comodita': chi apre gli strumenti di sviluppo sulla
                pagina non deve trovarsi in mano il vocabolario intero del plugin. I
                messaggi del montaggio, le ragioni dei rifiuti e i testi delle Opzioni
                restano nel C#, dove nascono. */
            var p = Pagina();
            foreach (var altrui in new[] { "Montaggio_", "Garanzia_", "Opzioni_", "Sito_", "Ruota_" })
                Assert.IsFalse(p.Contains("\"" + altrui), $"nella pagina e' finita una chiave «{altrui}…»");
        }

        [TestMethod]
        public void NEL_MARKUP_NON_E_RIMASTA_Prosa() {
            /*  IL VINCOLO CHE VALE PER TUTTO QUELLO CHE VERRA' DOPO.
                Testo e struttura devono restare separabili: nel markup ci sono le chiavi,
                le parole stanno nei due file delle lingue. Una frase scritta a mano qui
                non darebbe nessun errore — darebbe una pagina meta' tradotta, e nessuno
                se ne accorgerebbe finche' qualcuno non la apre in inglese. */
            var html = Risorsa("prova.html");
            var soloTesto = System.Text.RegularExpressions.Regex.Replace(html, "<!--.*?-->", "",
                System.Text.RegularExpressions.RegexOptions.Singleline);
            soloTesto = string.Join(" ",
                System.Text.RegularExpressions.Regex.Split(soloTesto, "<[^>]*>"));

            foreach (var parola in new[] { "prescrizione", "attesa", "ospite", "indirizzo", "ponte" })
                Assert.IsFalse(soloTesto.Contains(parola, StringComparison.OrdinalIgnoreCase),
                    $"nel markup c'e' ancora la parola «{parola}»: il testo va nel dizionario");
        }

        [TestMethod]
        public void NELLO_SCRIPT_NON_E_RIMASTA_Prosa() {
            var js = Risorsa("prova.js");
            /*  Via i commenti: quelli restano italiani per scelta, non li legge nessun
                utente. Quello che conta e' cio' che finisce sullo schermo. */
            var senza = System.Text.RegularExpressions.Regex.Replace(js, @"/\*.*?\*/", "",
                System.Text.RegularExpressions.RegexOptions.Singleline);

            foreach (var frase in new[] { "sto chiedendo", "risposta ricevuta", "Manda a N.I.N.A.",
                                          "Dove stai riprendendo", "Salva configurazione",
                                          "non salvato", "Nessun filtro dichiarato",
                                          "Configurazione dei filtri", "servizio raggiungibile" })
                Assert.IsFalse(senza.Contains(frase, StringComparison.OrdinalIgnoreCase),
                    $"«{frase}» e' ancora scritta nello script invece che nel dizionario");
        }

        [TestMethod]
        public void LEnfasiSiScriveCosi_ENonConIlMarkup() {
            /*  Nei valori delle lingue non entra markup: l'unica marcatura ammessa e'
                *cosi'* per l'enfasi, e a renderla e' la pagina. Se qualcuno ci mettesse
                un <b>, il testo smetterebbe di essere separabile dalla struttura — e
                sarebbe l'inizio dei blocchi «testo fuso con markup» che in Strategy sono
                gia' centottanta. */
            foreach (var lingua in new[] { "it", "en" })
                foreach (var v in Tutte(lingua).Where(x => x.Key.StartsWith("Pag_", StringComparison.Ordinal)))
                    foreach (var tag in new[] { "<b>", "<span", "<div", "<strong", "<i>", "<br" })
                        Assert.IsFalse(v.Value.Contains(tag, StringComparison.OrdinalIgnoreCase),
                            $"{lingua}: «{v.Key}» contiene markup ({tag}). L'enfasi si scrive *cosi'*");
        }

        private static System.Collections.Generic.Dictionary<string, string> Tutte(string lingua) {
            var rm = new System.Resources.ResourceManager(
                "AstroImage.NINA.Plugin.Localization.Strings_" + lingua, Ponte);
            var set = rm.GetResourceSet(System.Globalization.CultureInfo.InvariantCulture, true, true);
            var d = new System.Collections.Generic.Dictionary<string, string>();
            foreach (System.Collections.DictionaryEntry e in set!) d[(string)e.Key] = (string)(e.Value ?? "");
            return d;
        }

        /*  IL RIQUADRO DEI MODI HA LE MISURE DI AIS, E NON "PRESSAPPOCO".
         *
         *  Questa prova nasce da un errore vero: il riquadro era stato copiato dalle
         *  `.stratcard` di AIS — che sono le card dei RISULTATI, quelle col numero
         *  grande e il costo — invece che dal fieldset «Come riprendere», che e'
         *  `.goalbox` / `.goalcard` / `.gc`. A schermo sembrava giusto. Erano diversi
         *  otto valori su otto: griglia 8 invece di 10, colonna 190 invece di 210,
         *  imbottitura 10/12 invece di 12/14, raggio 10 invece di 12, fondo della
         *  scelta `--surface-2` invece dell'accento al 9%, icona 15 invece di 19,
         *  spunta aggiunta e tolta invece che in dissolvenza, descrizione 10.5/1.35
         *  su `--dim2` invece di 11.5/1.5 su `--dim`.
         *
         *  Nessuno di questi si vede senza misurarli. Percio' li misura una prova:
         *  se un giorno AIS cambia, questa diventa rossa e dice dove sono andati a
         *  divergere, invece di lasciarli divergere in silenzio.
         *
         *  I valori vengono da index.html di AstroImage-Strategy, righe 810-827.   */
        [TestMethod]
        public void IL_RIQUADRO_DEI_MODI_HaLeMisureDiAIS() {
            var css = Risorsa("prova.css");
            var attesi = new[] {
                "gap:10px",                                       // .goalgrid
                "repeat(auto-fit,minmax(210px,1fr))",             // .goalgrid
                "border-radius:12px",                             // .gc
                "padding:12px 14px",                              // .gc
                "flex-direction:column",                          // .gc
                "background:rgba(var(--acc-rgb),.09)",            // .gc scelta
                "border-color:var(--acc)",                        // .gc scelta
                "width:19px; height:19px",                        // .gc-h svg
                "font-size:11.5px; line-height:1.5",              // .gc-d
                "letter-spacing:.09em",                           // .hc-k
            };
            foreach (var v in attesi)
                StringAssert.Contains(css, v,
                    $"il riquadro dei modi non ha piu' la misura di AIS: «{v}»");

            /*  E le classi dei RISULTATI non devono ricomparire: se tornano, vuol dire
                che qualcuno ha ricopiato dal posto sbagliato una seconda volta. */
            foreach (var v in new[] { ".stratcard {", ".stratgrid {", ".sc-h {" })
                Assert.IsFalse(css.Contains(v),
                    $"«{v}» sono le card dei risultati di AIS, non il riquadro dei modi");
        }

        /*  L'ELENCO DEI MODI NON STA NEL PONTE.
         *
         *  Se ci stesse, il giorno in cui il motore ne dichiarasse un quarto ci
         *  sarebbero due verita' — e quella sbagliata sarebbe questa. La pagina
         *  chiede, riceve, mostra; le parole tradotte le mette sopra quando conosce
         *  l'identificativo, e quando non lo conosce usa quelle che arrivano.       */
        [TestMethod]
        public void L_ELENCO_DEI_MODI_ArrivaDalServizio() {
            var js = Risorsa("prova.js");
            StringAssert.Contains(js, "chiedi('modalita')",
                "la pagina non chiede piu' l'elenco dei modi");
            StringAssert.Contains(js, "modi = r.modalita",
                "l'elenco non arriva piu' dalla risposta");
            Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(js, @"modi\s*=\s*\[\s*\{"),
                "l'elenco dei modi e' stato scritto dentro la pagina: adesso ce ne sono due");
        }

        /*  E LA PAGINA NON SI INVENTA UN MODO.
         *
         *  Ogni valore che finisce in `modoScelto` deve venire da fuori: il
         *  predefinito che dichiara il servizio, il primo dell'elenco che manda il
         *  servizio, o il bottone che ha premuto chi guarda. Un identificativo
         *  scritto qui dentro partirebbe verso il motore senza che il motore lo
         *  conosca, e tornerebbe indietro come `modalita_sconosciuta` — che e'
         *  esattamente il guasto che quel codice esiste per rendere visibile.       */
        [TestMethod]
        public void LA_PAGINA_NON_SI_INVENTA_UnModo() {
            var js = Risorsa("prova.js");
            var fonti = System.Text.RegularExpressions.Regex.Matches(js, @"modoScelto\s*=\s*([^;]+);")
                .Cast<System.Text.RegularExpressions.Match>()
                .Select(m => m.Groups[1].Value.Trim())
                .ToArray();
            Assert.IsTrue(fonti.Length > 0, "nessuna assegnazione a modoScelto: la scelta non parte piu'");
            foreach (var f in fonti)
                Assert.IsTrue(f == "null" || f.Contains("r.diSerie") || f.Contains("modi[0].id")
                              || f.Contains("i.value"),
                    $"modoScelto prende un valore che non viene dal motore ne' dalla scelta: «{f}»");
        }

        [TestMethod]
        public void ComporLaPaginaDueVolte_DaLoStessoDocumento() {
            /*  Si compone una volta e si tiene. Se un giorno qualcuno la rendesse
                dipendente da qualcosa che cambia, due pannelli aperti insieme
                mostrerebbero due pagine diverse. */
            Assert.AreEqual(Pagina(), Pagina());
            Assert.IsTrue(Pagina().Length > 20000, "la pagina e' molto piu' corta del previsto");
        }
    }
}
