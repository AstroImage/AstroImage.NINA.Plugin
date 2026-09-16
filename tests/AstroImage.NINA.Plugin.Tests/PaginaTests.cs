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

        /*  IL PANNELLO NON RESTA MUTO QUANDO STRATEGY NON RISPONDE.
         *
         *  Il difetto: `ClienteStrategy.Modalita` restituisce `Vuota` su ogni
         *  fallimento, il gestore rispondeva `ok: true` con zero modi, e la pagina
         *  faceva `return`. I tre riquadri non comparivano e NIENTE diceva perche':
         *  chi guarda vede un plugin rotto, e il plugin sta benissimo. E' costato un
         *  pomeriggio, due volte.
         *
         *  Quattro affermazioni, una per ogni modo di ricaderci.                   */
        [TestMethod]
        public void IL_PANNELLO_NON_RESTA_MUTO_SeStrategyNonRisponde() {
            /*  Si RISALE finche' non si trovano i sorgenti, invece di contare i `..`:
                la profondita' della cartella di uscita cambia fra Debug e Release e fra
                una versione di .NET e l'altra, e un conteggio cablato non trova niente
                — cioe' diventa una prova che non prova. Stesso idioma della prova dei
                sensori, che per questo era nata gia' cosi'. */
            string? radice = null;
            var su = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
            for (var i = 0; i < 8 && su is not null; i++, su = su.Parent)
                if (System.IO.Directory.Exists(System.IO.Path.Combine(su.FullName, "src",
                        "AstroImage.NINA.Plugin"))) { radice = System.IO.Path.Combine(su.FullName, "src"); break; }
            if (radice is null) { Assert.Inconclusive("sorgenti non trovati accanto ai test"); return; }

            var vista = System.IO.File.ReadAllText(System.IO.Path.Combine(
                radice, "AstroImage.NINA.Plugin", "Views", "PannelloStrategyView.xaml.cs"));

            //  1 · un elenco vuoto non e' una risposta: il gestore lo rifiuta
            var gestore = vista.Substring(vista.IndexOf("Task Modalita(string id)", StringComparison.Ordinal));
            gestore = gestore.Substring(0, gestore.IndexOf("\n        }", StringComparison.Ordinal));
            Assert.IsTrue(gestore.Contains("Elenco.Count == 0", StringComparison.Ordinal),
                "il gestore `modalita` non rifiuta l'elenco vuoto: rispondera' «e' andata " +
                "bene, zero modi», e la pagina non avra' niente da dire");

            //  2 · e lo dice NOMINANDO l'indirizzo, che e' l'unica cosa utile
            Assert.IsTrue(gestore.Contains("Pannello_NessunoRisponde", StringComparison.Ordinal),
                "il rifiuto non usa la frase che nomina l'indirizzo configurato: " +
                "«non risponde» senza dire DOVE non dice dove andare a guardare");

            //  3 · la pagina non torna indietro in silenzio su quel ramo
            var pagina = Risorsa("prova.js");
            var ramo = pagina.Substring(pagina.IndexOf("chiedi('modalita')", StringComparison.Ordinal));
            ramo = ramo.Substring(0, Math.Min(600, ramo.Length));
            Assert.IsTrue(ramo.Contains("motoreGiu(", StringComparison.Ordinal),
                "il ramo di fallimento di `modalita` non avvisa nessuno: era `return` e basta");
            Assert.IsTrue(pagina.Contains("function motoreGiu", StringComparison.Ordinal),
                "manca l'avviso stesso");
            //  e l'avviso porta il messaggio dell'ospite, che e' quello con l'indirizzo
            var avviso = pagina.Substring(pagina.IndexOf("function motoreGiu", StringComparison.Ordinal));
            avviso = avviso.Substring(0, Math.Min(800, avviso.Length));
            Assert.IsTrue(avviso.Contains("r.messaggio", StringComparison.Ordinal),
                "l'avviso non mostra il messaggio dell'ospite: perderebbe l'indirizzo");

            //  4 · le due frasi esistono in tutte e due le lingue, o una resta muta
            foreach (var lingua in new[] { "Strings_it.resx", "Strings_en.resx" }) {
                var dizionario = System.IO.File.ReadAllText(System.IO.Path.Combine(
                    radice, "AstroImage.NINA.Plugin", "Localization", lingua));
                foreach (var chiave in new[] { "Pag_StrategyNonRisponde", "Pag_PercioNienteModi",
                                               "Pannello_NessunoRisponde" })
                    Assert.IsTrue(dizionario.Contains("name=\"" + chiave + "\"", StringComparison.Ordinal),
                        lingua + " non ha «" + chiave + "»: in quella lingua il pannello resta muto");
            }
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
        /*  OGNI CHIAVE CHE LA PAGINA CHIEDE ESISTE, E HA IL PREFISSO CHE LA FA ARRIVARE. Il contrario delle voci
         *  orfane: una chiave chiesta e assente, o scritta senza Pag_, a schermo e' un'etichetta che manca senza un
         *  errore — Pagina.cs passa alla pagina solo le voci Pag_. */
        [TestMethod]
        public void OGNI_CHIAVE_CHIESTA_DALLA_PAGINA_ESISTE_ECHA_IL_PREFISSO() {
            var js = System.Text.RegularExpressions.Regex.Replace(Risorsa("prova.js"), @"/\*.*?\*/", "",
                System.Text.RegularExpressions.RegexOptions.Singleline);
            var senzaPrefisso = System.Text.RegularExpressions.Regex.Matches(js, @"\b(?:T|MF)\('(?!Pag_)([A-Za-z_]+)'")
                .Cast<System.Text.RegularExpressions.Match>().Select(m => m.Groups[1].Value).Distinct().ToArray();
            Assert.AreEqual(0, senzaPrefisso.Length, "chiavi senza il prefisso Pag_: " + string.Join(", ", senzaPrefisso));
            var p = Pagina();
            var assenti = System.Text.RegularExpressions.Regex.Matches(js, @"'(Pag_[A-Za-z_]+)'")
                .Cast<System.Text.RegularExpressions.Match>().Select(m => m.Groups[1].Value).Distinct()
                .Where(k => !p.Contains("\"" + k + "\"")).ToArray();
            Assert.AreEqual(0, assenti.Length, "chiavi chieste dalla pagina e assenti dal dizionario: " + string.Join(", ", assenti));
        }

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

        /*  LE POLITICHE ARRIVANO DAL SERVIZIO, e la pagina non ne sceglie una di suo: il valore che parte viene
         *  dal predefinito che il servizio dichiara, dal primo dell'elenco che manda, o dal bottone premuto. */
        [TestMethod]
        public void LE_POLITICHE_ArrivanoDalServizio_ELaPaginaNonNeInventaUna() {
            var js = Risorsa("prova.js");
            StringAssert.Contains(js, "politiche = r.politiche", "l'elenco delle politiche non arriva piu' dalla risposta");
            Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(js, @"politiche\s*=\s*\[\s*\{"),
                "l'elenco delle politiche e' stato scritto dentro la pagina: adesso ce ne sono due");
            var fonti = System.Text.RegularExpressions.Regex.Matches(js, @"politicaScelta\s*=\s*([^;]+);")
                .Cast<System.Text.RegularExpressions.Match>().Select(m => m.Groups[1].Value.Trim()).ToArray();
            Assert.IsTrue(fonti.Length > 0, "nessuna assegnazione a politicaScelta: la scelta non parte");
            foreach (var f in fonti)
                Assert.IsTrue(f == "null" || f.Contains("r.politicaDiSerie") || f.Contains("i.value"),
                    $"politicaScelta prende un valore che non viene dal motore ne' dalla scelta: «{f}»");
            StringAssert.Contains(js, "politica: politicaScelta", "la politica scelta non parte nella richiesta");
        }

        /*  IL MENU LEGGE LE STRADE E NON NE DECIDE NESSUNA. Il nome di una strada e' quello del motore; il prezzo e'
         *  quello di progetto, e la pagina non lo moltiplica per i riquadri — sarebbe una regola di scala del motore
         *  scritta qui; le chiavi delle parole sono letterali, perche' una chiave composta la prova delle voci orfane
         *  non la vede; e il clic rifa' la domanda con la strada, invece di rimescolare le carte. */
        [TestMethod]
        public void IL_MENU_LEGGE_LE_STRADE_E_NON_NE_DECIDE_NESSUNA() {
            var js = System.Text.RegularExpressions.Regex.Replace(Risorsa("prova.js"), @"/\*.*?\*/", "",
                System.Text.RegularExpressions.RegexOptions.Singleline);
            Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(js, @"'Pag_[A-Za-z_]*'\s*\+"),
                "una chiave composta col codice: la prova delle voci orfane non la vede");
            Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(js, @"\*\s*(pr\.)?panels|panels\s*\*"),
                "la pagina moltiplica per i riquadri: e' una regola di scala del motore");
            StringAssert.Contains(js, "c.progetto && c.progetto.ideal", "il prezzo non e' piu' quello di progetto");
            StringAssert.Contains(js, "esc(c.name)", "il nome della strada non e' piu' quello del motore");
            StringAssert.Contains(js, "strada: stradaScelta", "la strada scelta non parte nella richiesta");
            foreach (var letto in new[] { "roadChoices", "raccomandataPerche", "raccomandataAssente", "blocked",
                                          "stessaRipresa", "stradeEscluse", "nonPrezzabili", "roadRequestedSostituita",
                                          "roadRequestedStessaRipresa", "roadAutoRisolta", "limitiDellaStrada" })
                StringAssert.Contains(js, letto, $"il menu non legge piu' «{letto}»");
        }

        /*  IL BANCO NON E' PIU' SCRITTO NELLA PAGINA (16 settembre 2026). La fascia gialla che lo diceva e' uscita con lui,
         *  come la prova di prima prometteva. Adesso il banco si compone dalla lista dei campi che il servizio pubblica
         *  in /v1/salute: quello che N.I.N.A. tiene, quello che dichiara chi riprende, la camera del driver. Nessun pezzo
         *  scritto qui, e la pagina legge dal prodotto il banco usato e le sue divergenze. */
        [TestMethod]
        public void IL_BANCO_NON_E_SCRITTO_NELLA_PAGINA_E_SI_COSTRUISCE_DALLA_LISTA_DEL_SERVIZIO() {
            var js = System.Text.RegularExpressions.Regex.Replace(Risorsa("prova.js"), @"/\*.*?\*/", "",
                System.Text.RegularExpressions.RegexOptions.Singleline);
            foreach (var vietato in new[] { "'askar71f'", "'am5'", "'asi2600mc'", "red:", "bancoMandato" })
                Assert.IsFalse(js.Contains(vietato), $"«{vietato}»: un pezzo del banco scritto nella pagina");
            Assert.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(js, @"banco:\s*bancoDaMandare\(\)"),
                "la richiesta non compone il banco dalla lista");
            foreach (var letto in new[] { "r.campiDelBanco", "r.divergenzeDelBanco", "chiedi('banco')", "chiedi('salvaBanco'",
                                          "p.banco", "pb.divergenze", "c.provenienza === 'nina'", "c.provenienza === 'dichiarabile'" })
                StringAssert.Contains(js, letto, $"la pagina non legge piu' «{letto}»");
        }

        /*  IL CAMPO SQM DICE CHE COSA VUOLE (regia, 16 settembre 2026): il carattere del sito, non la trasparenza di
         *  stanotte, e nel dubbio il valore piu' chiaro dell'intervallo, perche' un cielo dichiarato troppo scuro
         *  prescrive meno ore di quelle che servono. La frase sta nel blocco del sito, accanto a seeing e guida, e
         *  nelle due lingue dice le due cose. Guardia strutturale: legge la pagina, non la esegue. */
        [TestMethod]
        public void IL_CAMPO_SQM_DICE_CHE_COSA_VUOLE() {
            var js = System.Text.RegularExpressions.Regex.Replace(Risorsa("prova.js"), @"/\*.*?\*/", "",
                System.Text.RegularExpressions.RegexOptions.Singleline);
            var i = js.IndexOf("$('sito').innerHTML", StringComparison.Ordinal);
            Assert.IsTrue(i >= 0, "la pagina non disegna piu' il blocco del sito");
            var fine = js.IndexOf("Array.prototype.forEach", i, StringComparison.Ordinal);
            Assert.IsTrue(fine > i, "il blocco del sito non si chiude dove ci si aspetta");
            StringAssert.Contains(js.Substring(i, fine - i), "MF('Pag_SqmNota')",
                "il campo del cielo non dice che cosa vuole");
            var it = Tutte("it")["Pag_SqmNota"];
            StringAssert.Contains(it, "carattere del sito");
            StringAssert.Contains(it, "più chiaro");
            var en = Tutte("en")["Pag_SqmNota"];
            StringAssert.Contains(en, "character of the site");
            StringAssert.Contains(en, "brightest");
        }

        /*  SUL CAMBIO DI PROFILO LA PRESCRIZIONE ESCE DALLO SCHERMO. Il pannello la ritira dalle mani del Ponte
         *  (CambioDiProfiloTests); la pagina la deve togliere da davanti agli occhi, dire perche', e dimenticare la strada
         *  scelta, che era di una scheda calcolata sull'altro banco. */
        [TestMethod]
        public void SUL_CAMBIO_DI_PROFILO_LA_PRESCRIZIONE_SI_TOGLIE_DALLO_SCHERMO_E_SI_DICE_PERCHE() {
            var js = System.Text.RegularExpressions.Regex.Replace(Risorsa("prova.js"), @"/\*.*?\*/", "",
                System.Text.RegularExpressions.RegexOptions.Singleline);
            var i = js.IndexOf("r.evento === 'profilo'", StringComparison.Ordinal);
            Assert.IsTrue(i >= 0, "la pagina non ascolta il cambio di profilo");
            var fine = js.IndexOf("return;", i, StringComparison.Ordinal);
            Assert.IsTrue(fine > i, "il ramo del cambio di profilo non si chiude");
            var ramo = js.Substring(i, fine - i);
            StringAssert.Contains(ramo, "$('uscita').innerHTML", "la prescrizione dell'altro profilo resta a schermo");
            StringAssert.Contains(ramo, "Pag_ProfiloCambiato", "la pagina toglie la prescrizione senza dire perche'");
            StringAssert.Contains(ramo, "stradaScelta = null", "la strada scelta sull'altro banco resta scelta");
        }

        /*  IL PONTE NON RICONOSCE I SENSORI, E NON DEVE COMINCIARE.
         *
         *  Della camera collegata legge quello che il driver dichiara — passo del
         *  pixel, dimensioni, matrice, bit, elettroni per ADU, guadagno — e lo passa.
         *  Quale silicio ci sia sotto, e in quale modo lo stia leggendo, lo decide
         *  `resolveSensor` dentro Strategy, dalla GEOMETRIA: e' cosi' che distingue un
         *  IMX294 venduto binnato dallo stesso sensore letto intero, che i costruttori
         *  chiamano IMX492.
         *
         *  Se qui comparisse una tabella di alias — «ASI2600 e' un IMX571» — ci
         *  sarebbero due verita', e il giorno in cui il catalogo dei sensori cambia
         *  quella sbagliata sarebbe la copia. Lo stesso vale per il pozzetto, il
         *  rumore di lettura e la QE: sono caratterizzazione, e stanno nel catalogo.
         *
         *  La prova cerca i nomi delle famiglie di sensori, che sono il segno piu'
         *  riconoscibile di una tabella che sta nascendo.                            */
        [TestMethod]
        public void IL_PONTE_NON_CONOSCE_I_Sensori() {
            /*  Si risale finche' non si trova, come fa la prova sulle chiavi orfane:
                contare i «..» dipende dalla forma della cartella di uscita, che cambia
                fra Debug e Release e fra una versione di .NET e l'altra. */
            string? radice = null;
            var d = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
            for (var i = 0; i < 8 && d is not null; i++, d = d.Parent) {
                var c = System.IO.Path.Combine(d.FullName, "src", "AstroImage.NINA.Plugin");
                if (System.IO.Directory.Exists(c)) { radice = c; break; }
            }
            if (radice is null) { Assert.Inconclusive("sorgenti non trovati accanto ai test"); return; }

            var testo = string.Join("\n", new[] { "*.cs", "*.js" }
                .SelectMany(p => System.IO.Directory.EnumerateFiles(radice, p, System.IO.SearchOption.AllDirectories))
                .Where(f => !f.Contains(System.IO.Path.DirectorySeparatorChar + "obj" + System.IO.Path.DirectorySeparatorChar)
                         && !f.Contains(System.IO.Path.DirectorySeparatorChar + "bin" + System.IO.Path.DirectorySeparatorChar))
                .Select(System.IO.File.ReadAllText));

            /*  I commenti restano: uno che SPIEGA perche' il ponte non riconosce i
                sensori nomina per forza un IMX. E' il codice a non doverli nominare. */
            var codice = System.Text.RegularExpressions.Regex.Replace(testo, @"/\*.*?\*/", " ",
                System.Text.RegularExpressions.RegexOptions.Singleline);
            codice = System.Text.RegularExpressions.Regex.Replace(codice, @"(?m)^\s*//.*$", " ");

            var trovati = new[] { "IMX", "ICX", "MN34", "KAF", "sensor_id", "saturazione_e" }
                .Where(t => codice.Contains(t, StringComparison.OrdinalIgnoreCase)).ToArray();
            Assert.AreEqual(0, trovati.Length,
                "nel codice del ponte compaiono nomi di sensori o campi del catalogo: "
                + string.Join(", ", trovati) + ". Il riconoscimento sta in Strategy.");
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
