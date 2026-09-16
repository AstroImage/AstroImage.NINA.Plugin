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
         *  I valori sono quelli del riquadro dei modi di AIS.   */
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
            /*  Dal ritiro generalizzato il ramo chiama la funzione che toglie la prescrizione, la stessa dei salvataggi: le
             *  tre proprieta' si leggono li'. */
            StringAssert.Contains(ramo, "ritiraDalloSchermo('profilo')", "il cambio di profilo non ritira dallo schermo");
            var f = js.IndexOf("function ritiraDalloSchermo(", StringComparison.Ordinal);
            Assert.IsTrue(f >= 0, "la funzione che toglie la prescrizione non c'e'");
            var corpo = js.Substring(f, js.IndexOf("\n  }", f, StringComparison.Ordinal) - f);
            StringAssert.Contains(corpo, "$('uscita').innerHTML", "la prescrizione dell'altro profilo resta a schermo");
            StringAssert.Contains(corpo, "stradaScelta = null", "la strada scelta sull'altro banco resta scelta");
            StringAssert.Contains(js, "profilo: 'Pag_ProfiloCambiato'", "la pagina toglie la prescrizione senza dire perche'");
        }

        /*  LA FEDELTA' VISIVA SUL MINIX (16 settembre 2026): cinque difetti visti a schermo, cinque guardie strutturali —
         *  leggono la pagina, non la eseguono; la prova vera e' lo schermo di N.I.N.A. dopo l'installazione. */
        private static string PaginaSenzaCommenti() =>
            System.Text.RegularExpressions.Regex.Replace(Risorsa("prova.js"), @"/\*.*?\*/", "",
                System.Text.RegularExpressions.RegexOptions.Singleline);

        private static string Tratto(string js, string da, string a) {
            var i = js.IndexOf(da, StringComparison.Ordinal);
            Assert.IsTrue(i >= 0, "nella pagina non c'e' «" + da + "»");
            var fine = js.IndexOf(a, i + da.Length, StringComparison.Ordinal);
            Assert.IsTrue(fine > i, "dopo «" + da + "» non c'e' «" + a + "»");
            return js.Substring(i, fine - i);
        }

        /*  «Ore utili 21.95 h» accanto a «notte chiesta 2026-09-16» sembrava una notte da ventidue ore: erano tre notti. */
        [TestMethod]
        public void LE_ORE_UTILI_DICONO_IN_QUANTE_NOTTI_SONO_SOMMATE() {
            var riga = Tratto(PaginaSenzaCommenti(), "T('Pag_RigaOreUtili')", "</tr>");
            StringAssert.Contains(riga, "p.notte.nottiDisponibili", "le ore utili non dicono in quante notti");
            StringAssert.Contains(riga, "'Pag_OreUtiliInNotti'");
            StringAssert.Contains(riga, "'Pag_OreUtiliInUnaNotte'");
            foreach (var lingua in new[] { "it", "en" }) {
                StringAssert.Contains(Tutte(lingua)["Pag_OreUtiliInNotti"], "{1}", lingua + ": le notti non entrano nella frase");
                StringAssert.Contains(Tutte(lingua)["Pag_OreUtiliInUnaNotte"], "{0}", lingua);
            }
        }

        /*  L'ORIZZONTE DEL PROFILO ARRIVA ALLA RICHIESTA (prova in N.I.N.A. sul MiniX, 16 settembre 2026). Il profilo di
         *  Borno ha il suo Orizzonte Personalizzato, `SitoDelProfilo` lo legge, e il motore diceva «orizzonte non
         *  dichiarato nel profilo: vale il limite operativo, 15°»: la vista rispondeva alla pagina con sette campi del
         *  sito, senza il profilo, e la pagina rimanda quel sito com'e'. OrizzonteRealeTests provava l'unione e il filo del
         *  modello, non questo passaggio. Qui si legge la vista: il sito che da' alla pagina porta l'orizzonte, e la pagina
         *  dice da quale file viene, o che non si e' letto. */
        [TestMethod]
        public void L_ORIZZONTE_DEL_PROFILO_ARRIVA_ALLA_RICHIESTA_E_SI_VEDE() {
            string? radice = null;
            var su = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
            for (var i = 0; i < 8 && su is not null; i++, su = su.Parent)
                if (System.IO.Directory.Exists(System.IO.Path.Combine(su.FullName, "src", "AstroImage.NINA.Plugin"))) {
                    radice = System.IO.Path.Combine(su.FullName, "src"); break;
                }
            Assert.IsNotNull(radice, "sorgenti non trovati accanto ai test");
            var vista = System.IO.File.ReadAllText(System.IO.Path.Combine(radice!, "AstroImage.NINA.Plugin", "Views",
                "PannelloStrategyView.xaml.cs"));
            var i0 = vista.IndexOf("private void Sito(string id)", StringComparison.Ordinal);
            Assert.IsTrue(i0 >= 0, "la risposta del sito non si trova");
            var metodo = vista.Substring(i0, vista.IndexOf("\n        }", i0, StringComparison.Ordinal) - i0);
            var sito = metodo.Substring(metodo.IndexOf("[\"sito\"] = new JsonObject", StringComparison.Ordinal));
            sito = sito.Substring(0, sito.IndexOf("},", StringComparison.Ordinal));
            StringAssert.Contains(sito, "[\"orizzonte\"]", "il sito che la pagina rimanda non porta l'orizzonte del profilo");
            StringAssert.Contains(metodo, "[\"orizzonteFile\"]", "la pagina non sa da quale file viene l'orizzonte");
            var js = PaginaSenzaCommenti();
            Assert.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(js, @"sito:\s+sito \|\| \{\}"),
                "la richiesta non rimanda il sito ricevuto");
            var riga = Tratto(js, "function rigaOrizzonte(", "\n  }");
            foreach (var k in new[] { "'Pag_Orizzonte'", "'Pag_OrizzonteDalProfilo'", "'Pag_OrizzonteNonLetto'" })
                StringAssert.Contains(riga, k);
            foreach (var lingua in new[] { "it", "en" })
                StringAssert.Contains(Tutte(lingua)["Pag_OrizzonteDalProfilo"], "{1}", lingua);
        }

        /*  I NUMERI NELLA LINGUA DI CHI GUARDA (regia, 16 settembre 2026): virgola in italiano, punto in inglese, lo spazio
         *  fine delle migliaia da cinque cifre, e il rapporto focale a due decimali — la pagina non distingueva 5,17 da
         *  5,20. Una funzione sola, `cifra`, e il separatore e' una parola del dizionario. */
        [TestMethod]
        public void I_NUMERI_SI_SCRIVONO_NELLA_LINGUA_DI_CHI_GUARDA() {
            var js = PaginaSenzaCommenti();
            var cifra = Tratto(js, "const cifra = (", "\n  };");
            StringAssert.Contains(cifra, "T('Pag_SeparatoreDecimale')", "il separatore decimale non viene dalla lingua");
            StringAssert.Contains(cifra, @"\u202f", "le migliaia non hanno lo spazio fine");
            Assert.AreEqual(",", Tutte("it")["Pag_SeparatoreDecimale"]);
            Assert.AreEqual(".", Tutte("en")["Pag_SeparatoreDecimale"]);
            StringAssert.Contains(js, "cifra(v, c.unita === 'f/' ? 2 : undefined)", "il rapporto focale non ha due decimali");
            var toFixed = System.Text.RegularExpressions.Regex.Matches(js, @"\.toFixed\(").Count;
            Assert.IsTrue(toFixed <= 2, "fuori da cifra un numero a schermo si scrive col punto: " + toFixed + " toFixed");
        }

        /*  IL GUADAGNO SI VEDE (regia, 16 settembre 2026): il motore sceglie il modo, il pannello lo mostra, la sequenza
         *  lo imposta. Nella tabella delle notti ogni blocco dice il suo guadagno, il modo e chi l'ha deciso; un blocco
         *  senza guadagno dice che resta quello della camera. */
        [TestMethod]
        public void LA_TABELLA_DELLE_NOTTI_DICE_IL_GUADAGNO_DI_OGNI_BLOCCO() {
            var js = PaginaSenzaCommenti();
            var righe = Tratto(js, "for (const s of p.sequenze) {", "$('uscita').innerHTML");
            StringAssert.Contains(righe, "guadagnoDelBlocco(", "la riga della notte non dice il guadagno dei blocchi");
            var f = Tratto(js, "function guadagnoDelBlocco(", "\n  }");
            StringAssert.Contains(f, "b.gainFonte", "il guadagno non dice chi l'ha deciso");
            foreach (var k in new[] { "'Pag_GuadagnoMotore'", "'Pag_GuadagnoDichiarato'", "'Pag_GuadagnoDellaCamera'" })
                StringAssert.Contains(f, k);
            StringAssert.Contains(js, "T('Pag_ColGuadagno')", "la tabella non ha la colonna del guadagno");
            foreach (var lingua in new[] { "it", "en" }) {
                StringAssert.Contains(Tutte(lingua)["Pag_GuadagnoMotore"], "{1}", lingua + ": il modo non entra nella frase");
                Assert.IsTrue(Tutte(lingua).ContainsKey("Pag_GuadagnoDellaCamera"), lingua);
            }
        }

        /*  IL VERDETTO CON LE SUE NOTTI (regia, 16 settembre 2026): quanto del progetto coprono le notti chieste, e quante
         *  ne servono per il minimo. I numeri li manda Strategy (`prescrizione.verdetto`); qui si mettono le parole. */
        [TestMethod]
        public void IL_VERDETTO_DICE_LA_COPERTURA_E_LE_NOTTI_PER_IL_MINIMO() {
            var js = PaginaSenzaCommenti();
            var riga = Tratto(js, "T('Pag_RigaVerdetto')", "T('Pag_RigaCalcolataSu')");
            foreach (var pezzo in new[] { "p.prescrizione.verdetto", "coperturaPercento", "perIlMinimo",
                                          "'Pag_Verdetto_Minimo'", "'Pag_Verdetto_MinimoOltre'", "'Pag_Verdetto_Coperto'" })
                StringAssert.Contains(riga, pezzo, "la riga del verdetto non usa " + pezzo);
            foreach (var lingua in new[] { "it", "en" }) {
                StringAssert.Contains(Tutte(lingua)["Pag_Verdetto_Minimo"], "{2}", lingua);
                StringAssert.Contains(Tutte(lingua)["Pag_Verdetto_MinimoOltre"], "{2}", lingua);
                StringAssert.Contains(Tutte(lingua)["Pag_Verdetto_Coperto"], "{1}", lingua);
            }
        }

        /*  UN'IMMAGINE DIVERSA SI DICE DIVERSA (decisione del 16 settembre 2026): la HOO consegnata perche' manca il SII non
         *  e' una SHO piu' economica. Strategy lo dichiara sulla bloccata (`immagineDiversa`). */
        [TestMethod]
        public void LA_HOO_AL_POSTO_DELLA_SHO_SI_DICE_IMMAGINE_DIVERSA() {
            var menu = Tratto(PaginaSenzaCommenti(), "function disegnaMenu(pr) {", "\n  }");
            StringAssert.Contains(menu, "immagineDiversa", "il menu non legge la dichiarazione dell'immagine diversa");
            StringAssert.Contains(menu, "'Pag_Men_ImmagineDiversa'");
            StringAssert.Contains(Tutte("it")["Pag_Men_ImmagineDiversa"], "non una {1} più economica");
            StringAssert.Contains(Tutte("en")["Pag_Men_ImmagineDiversa"], "{1}");
        }

        /*  IL SENSORE PRIMA DELLA RUOTA (decisione del 16 settembre 2026): un filtro che sulla camera non va si dice cosi', col
         *  filtro compatibile che Strategy suggerisce. */
        [TestMethod]
        public void IL_MOTIVO_DEL_SENSORE_SUGGERISCE_IL_FILTRO_COMPATIBILE() {
            var motivo = Tratto(PaginaSenzaCommenti(), "function testoDelMotivo(x) {", "\n  }");
            StringAssert.Contains(motivo, "'Pag_Men_Motivo_sensore_incompatibile_suggerito'",
                "il motivo del sensore non dice quale filtro serve");
            foreach (var lingua in new[] { "it", "en" })
                StringAssert.Contains(Tutte(lingua)["Pag_Men_Motivo_sensore_incompatibile_suggerito"], "{3}", lingua);
        }

        /*  «mm proposto dal catalogo» senza il numero: la voce riconosciuta propone un valore, e il valore non si vedeva. */
        [TestMethod]
        public void LA_PROPOSTA_DEL_CATALOGO_SI_VEDE_COL_SUO_NUMERO() {
            var fonte = Tratto(PaginaSenzaCommenti(), "const fonte = x =>", "const riga = c =>");
            StringAssert.Contains(fonte, "x.fonte === 'catalogo'", "la proposta del catalogo non ha un ramo suo");
            StringAssert.Contains(fonte, "'Pag_Banco_CatalogoPropone'");
            StringAssert.Contains(fonte, "x.valore", "la proposta del catalogo si scrive senza il suo numero");
            foreach (var lingua in new[] { "it", "en" })
                StringAssert.Contains(Tutte(lingua)["Pag_Banco_CatalogoPropone"], "{0}", lingua);
        }

        /*  «in attesa» in verde con una prescrizione a schermo: il cambio lingua riscriveva il testo iniziale dello stato e
         *  lasciava il colore. Uno stato scritto e' il resoconto di una cosa avvenuta: non torna indietro. */
        [TestMethod]
        public void LO_STATO_SCRITTO_NON_TORNA_IN_ATTESA() {
            var stato = Tratto(PaginaSenzaCommenti(), "const stato = (t, c) =>", "};");
            StringAssert.Contains(stato, "removeAttribute('data-loc')",
                "il cambio lingua riscrive lo stato col testo iniziale e lascia il colore di prima");
        }

        /*  Il blocco del sito diceva «non disponibile» accanto a seeing, altezza minima e notti serene, e il riquadro giallo
         *  sotto diceva il valore assunto: la stessa assenza in due frasi. Il motore nomina il campo (`campo` in `parziale`),
         *  e la pagina scrive l'assunzione accanto al campo; ritirata la prescrizione, l'assunzione se ne va con lei. */
        [TestMethod]
        public void IL_SITO_DICE_ACCANTO_AL_CAMPO_IL_VALORE_ASSUNTO() {
            var js = PaginaSenzaCommenti();
            var prov = Tratto(js, "function provenienzaDelSito(", "\n  }");
            StringAssert.Contains(prov, "parzialeUsato", "il sito non guarda che cosa il motore ha assunto");
            StringAssert.Contains(prov, ".campo === campo", "l'assunzione non si lega al suo campo");
            StringAssert.Contains(prov, "'Pag_Prov_assunto'");
            StringAssert.Contains(js, "parzialeUsato = p.parziale", "la pagina non tiene l'elenco dei dati assunti");
            StringAssert.Contains(Tratto(js, "function ritiraDalloSchermo(", "\n  }"), "parzialeUsato = null",
                "ritirata la prescrizione, le sue assunzioni restano accanto al sito");
            foreach (var lingua in new[] { "it", "en" })
                StringAssert.Contains(Tutte(lingua)["Pag_Prov_assunto"], "{0}", lingua);
        }

        /*  «Qui dentro non c'e' nessun indirizzo», «e' il ripiego»: l'italiano che si legge ha gli accenti. L'apostrofo
         *  resta per le elisioni e per «po'», che e' un troncamento. */
        [TestMethod]
        public void LE_VOCI_ITALIANE_SCRIVONO_GLI_ACCENTI() {
            var accento = new System.Text.RegularExpressions.Regex(
                @"(?:^|[^\p{L}])(\p{L}*[aeiouAEIOU])'(?=$|[\s.,;:!?)»—-])");
            var sbagliate = new System.Collections.Generic.List<string>();
            foreach (var v in Tutte("it"))
                foreach (System.Text.RegularExpressions.Match m in accento.Matches(v.Value))
                    if (!string.Equals(m.Groups[1].Value, "po", StringComparison.OrdinalIgnoreCase))
                        sbagliate.Add(v.Key + ": " + m.Groups[1].Value + "'");
            Assert.AreEqual(0, sbagliate.Count, sbagliate.Count + " accenti scritti con l'apostrofo: " +
                string.Join(" · ", sbagliate.GetRange(0, Math.Min(12, sbagliate.Count))));
        }

        /*  E LA CAMERA (regia, 16 settembre 2026): collegata o scollegata, ritira la prescrizione in mano; l'ospite lo dice
         *  alla pagina come per il profilo, e la pagina la toglie con la frase della camera. */
        [TestMethod]
        public void SUL_CAMBIO_DELLA_CAMERA_LA_PRESCRIZIONE_SI_TOGLIE_DALLO_SCHERMO() {
            var js = PaginaSenzaCommenti();
            StringAssert.Contains(Tratto(js, "r.evento === 'camera'", "return;"), "ritiraDalloSchermo('camera')",
                "la pagina non ascolta il cambio della camera");
            StringAssert.Contains(js, "camera: 'Pag_RitirataPerCamera'", "il ritiro della camera non ha la sua frase");
            foreach (var lingua in new[] { "it", "en" })
                Assert.IsTrue(Tutte(lingua).ContainsKey("Pag_RitirataPerCamera"), lingua);
        }

        /*  IL PONTE CONSEGNA, NON AMMINISTRA (decisione del 16 settembre 2026). Un secondo «Manda» sulla stessa notte, o il
         *  ventesimo, produce un altro contenitore: il Sequenziatore e' di chi riprende. Nessun codice di rifiuto per una
         *  notte gia' consegnata, nella vista, nei servizi o nel dizionario; e la risposta dice ogni volta che cosa ha
         *  consegnato, come al primo invio. Guardia strutturale: se il divieto tornasse, cade. */
        [TestMethod]
        public void IL_PONTE_NON_RIFIUTA_UNA_NOTTE_GIA_CONSEGNATA() {
            string? radice = null;
            var su = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
            for (var i = 0; i < 8 && su is not null; i++, su = su.Parent)
                if (System.IO.Directory.Exists(System.IO.Path.Combine(su.FullName, "src", "AstroImage.NINA.Plugin"))) {
                    radice = System.IO.Path.Combine(su.FullName, "src", "AstroImage.NINA.Plugin"); break;
                }
            Assert.IsNotNull(radice, "sorgenti non trovati accanto ai test");
            var trovati = System.IO.Directory.GetFiles(radice!, "*.cs", System.IO.SearchOption.AllDirectories)
                .Where(f => f.IndexOf(System.IO.Path.DirectorySeparatorChar + "obj" + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal) < 0)
                .Where(f => System.Text.RegularExpressions.Regex.IsMatch(System.IO.File.ReadAllText(f),
                    @"gia_consegnata|SegnaConsegnata|Consegnate\b"))
                .Select(System.IO.Path.GetFileName).ToList();
            Assert.AreEqual(0, trovati.Count, "il divieto della consegna ripetuta e' tornato: " + string.Join(", ", trovati));
            foreach (var lingua in new[] { "it", "en" })
                Assert.IsFalse(Tutte(lingua).Keys.Any(k => k.IndexOf("GiaConsegnata", StringComparison.Ordinal) >= 0),
                    lingua + ": il dizionario ha di nuovo la frase del rifiuto");
            StringAssert.Contains(Tratto(PaginaSenzaCommenti(), "async function manda(tasto)", "\n  }"), "'Pag_NotteAggiunta'",
                "la risposta non dice che cosa ha consegnato");
        }

        /*  LA LUNA TIENE FUORI, E SI DICE (regia, 16 settembre 2026). Sotto la soglia di Luna di un filtro il canale esce
         *  dalla notte, non dal progetto; Strategy manda notte per notte i canali fuori coi numeri (`prodotto.luna.fuori`),
         *  e la pagina li scrive in giallo: la notte, il canale, il filtro col nome della ruota, la fase, la distanza e la
         *  soglia — e, per un filtro che porta piu' righe nello stesso frame, il perche' della soglia piu' severa.
         *  Guardia strutturale: il Ponte non calcola la soglia, la scrive. */
        [TestMethod]
        public void LA_LUNA_CHE_TIENE_FUORI_UN_CANALE_SI_SCRIVE_COI_SUOI_NUMERI() {
            var js = PaginaSenzaCommenti();
            var riquadro = Tratto(js, "function lunaFuori(l) {", "\n  }");
            foreach (var pezzo in new[] { "l.fuori", "f.notte", "f.id", "vetroNellaRuota(f.filtro)", "f.fasePercento",
                                          "f.distanza", "f.soglia", "f.congiunto", "'Pag_LunaFuori'", "'Pag_LunaFuoriCongiunto'",
                                          "'Pag_LunaFuoriTitolo'" })
                StringAssert.Contains(riquadro, pezzo, "il riquadro della Luna non usa " + pezzo);
            StringAssert.Contains(js, "lunaFuori(p.luna)", "il riquadro della Luna non entra nella risposta");
            foreach (var lingua in new[] { "it", "en" }) {
                StringAssert.Contains(Tutte(lingua)["Pag_LunaFuori"], "{5}", lingua);
                StringAssert.Contains(Tutte(lingua)["Pag_LunaFuoriCongiunto"], "{5}", lingua);
                Assert.IsTrue(Tutte(lingua).ContainsKey("Pag_LunaFuoriTitolo"), "Pag_LunaFuoriTitolo manca in " + lingua);
            }
            StringAssert.Contains(Tutte("it")["Pag_LunaFuoriCongiunto"], "stesso frame");
        }

        /*  E IL VERDETTO NON DICE UNA COPERTURA CHE LE NOTTI NON DANNO: quando la Luna tiene fuori un canale da tutte le notti
         *  chieste, Strategy lo segna (`verdetto.lunaFuori`) e la riga dice il perche' con le notti per il minimo. */
        [TestMethod]
        public void IL_VERDETTO_DICE_QUANDO_LA_LUNA_TIENE_FUORI_TUTTE_LE_NOTTI() {
            var riga = Tratto(PaginaSenzaCommenti(), "T('Pag_RigaVerdetto')", "T('Pag_RigaCalcolataSu')");
            foreach (var pezzo in new[] { "v.lunaFuori", "'Pag_Verdetto_LunaFuori'", "'Pag_Verdetto_LunaFuoriOltre'" })
                StringAssert.Contains(riga, pezzo, "la riga del verdetto non usa " + pezzo);
            foreach (var lingua in new[] { "it", "en" }) {
                StringAssert.Contains(Tutte(lingua)["Pag_Verdetto_LunaFuori"], "{1}", lingua);
                StringAssert.Contains(Tutte(lingua)["Pag_Verdetto_LunaFuoriOltre"], "{1}", lingua);
            }
        }

        /*  LA DISTANZA DI RIFERIMENTO DELLA LUNA SI DICHIARA NEL BANCO (regia, 16 settembre 2026): e' quanto slavato accetta
         *  chi riprende. Il blocco del banco mostra il pezzo `luna` con la sua parola e la sua nota; la chiave e la
         *  provenienza vengono dalla lista che Strategy pubblica, come per gli altri campi dichiarabili. */
        [TestMethod]
        public void IL_BANCO_MOSTRA_LA_DISTANZA_DI_RIFERIMENTO_DELLA_LUNA() {
            var js = PaginaSenzaCommenti();
            StringAssert.Contains(Tratto(js, "const PEZZI_DEL_BLOCCO", ";"), "'luna'", "il blocco del banco non mostra la Luna");
            StringAssert.Contains(Tratto(js, "const PAROLA_CAMPO_BANCO", "};"), "'luna.riferimento_deg': 'Pag_Banco_luna_riferimento_deg'");
            StringAssert.Contains(Tratto(js, "const NOTA_CAMPO_BANCO", "};"), "'luna.riferimento_deg': 'Pag_Banco_LunaNota'");
            foreach (var lingua in new[] { "it", "en" })
                foreach (var chiave in new[] { "Pag_Banco_luna_riferimento_deg", "Pag_Banco_LunaNota" })
                    Assert.IsTrue(Tutte(lingua).ContainsKey(chiave), chiave + " manca in " + lingua);
        }

        /*  IL RITIRO GENERALIZZATO (regia, 16 settembre 2026): salvare una ruota, un banco o un sito diversi ritira la
         *  prescrizione in mano (CambioDiProfiloTests), e la risposta del salvataggio lo dice. La pagina la toglie dallo
         *  schermo come sul cambio di profilo, con la frase del suo perche' nelle due lingue. Guardia strutturale. */
        [TestMethod]
        public void UN_SALVATAGGIO_CHE_RITIRA_TOGLIE_LA_PRESCRIZIONE_DALLO_SCHERMO() {
            var js = System.Text.RegularExpressions.Regex.Replace(Risorsa("prova.js"), @"/\*.*?\*/", "",
                System.Text.RegularExpressions.RegexOptions.Singleline);
            foreach (var azione in new[] { "salvaFiltri", "salvaBanco", "salvaSito" }) {
                var i = js.IndexOf("chiedi('" + azione + "'", StringComparison.Ordinal);
                Assert.IsTrue(i >= 0, "la pagina non salva piu' con " + azione);
                var fine = js.IndexOf("});", i, StringComparison.Ordinal);
                Assert.IsTrue(fine > i, "la risposta di " + azione + " non si chiude");
                StringAssert.Contains(js.Substring(i, fine - i), "ritiraDalloSchermo(r2.ritirata)",
                    azione + ": la prescrizione ritirata resta a schermo");
            }
            foreach (var chiave in new[] { "Pag_RitirataPerRuota", "Pag_RitirataPerBanco", "Pag_RitirataPerSito" })
                foreach (var lingua in new[] { "it", "en" })
                    Assert.IsTrue(Tutte(lingua).ContainsKey(chiave), chiave + " manca in " + lingua);
        }

        /*  IL PONTE NON RICONOSCE I SENSORI, E NON DEVE COMINCIARE.
         *
         *  Della camera collegata legge quello che il driver dichiara — passo del
         *  pixel, dimensioni, matrice, bit, elettroni per ADU, guadagno — e lo passa.
         *  Quale silicio ci sia sotto, e in quale modo lo stia leggendo, lo decide
         *  Strategy, e lo dice nel campo del riconoscimento.
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
