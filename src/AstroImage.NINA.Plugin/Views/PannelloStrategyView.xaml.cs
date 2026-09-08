using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using AstroImage.NINA.Plugin.Services;
using AstroImage.NINA.Plugin.ViewModels;
using Microsoft.Web.WebView2.Core;

namespace AstroImage.NINA.Plugin.Views {

    /*  IL PONTE FRA LA PAGINA E IL C#.
     *
     *  La pagina non conosce nessun indirizzo e non fa nessuna chiamata di rete: manda
     *  un messaggio al suo ospite, e l'ospite va a chiedere. E' l'unico disegno che
     *  regge quando il motore sara' remoto e ci sara' un'identita' di mezzo — il
     *  segreto vive nel C#, dove chi apre gli strumenti di sviluppo sulla pagina non
     *  lo trova.
     *
     *  IL PROTOCOLLO, tre azioni e nessuna cerimonia:
     *
     *      salute        { id, azione: "salute" }
     *                 -> { id, ok }
     *
     *      prescrizione  { id, azione: "prescrizione", corpo: { … } }
     *                 -> { id, ok, corpo: "<il JSON del servizio>", notti, ms,
     *                      prescrizione: "<identificativo>", consegnabile, perche }
     *
     *      manda         { id, azione: "manda", prescrizione: "<identificativo>", notte: n }
     *                 -> { id, ok, nome, bersaglio, blocchi, pose, note[], scartati[] }
     *
     *      in errore  -> { id, ok: false, codice, messaggio }
     *
     *  `corpo` torna come TESTO e non come oggetto, di proposito: e' la risposta del
     *  servizio parola per parola, e passarla come stringa e' l'unico modo di essere
     *  certi che nessuno l'abbia rimaneggiata attraversando il confine.
     *
     *  IL MODELLO NON TORNA INDIETRO DALLA PAGINA. `manda` porta un numero di notte e
     *  l'identificativo della prescrizione, non la sequenza: cio' che finisce nel
     *  Sequenziatore e' cio' che il motore ha deciso, non cio' che la pagina dice.
     *  L'identificativo e' la guardia contro il gesto piu' normale che ci sia —
     *  chiedere un secondo oggetto e poi premere «manda» sulla riga del primo, rimasta
     *  sullo schermo. Vedi PrescrizioneCorrente.
     */
    public partial class PannelloStrategyView : UserControl {

        private bool _pronto;

        public PannelloStrategyView() {
            InitializeComponent();
            Loaded += AlCaricamento;
        }

        private async void AlCaricamento(object mittente, RoutedEventArgs e) {
            if (_pronto) { return; }
            try {
                /*  UNA CARTELLA DATI TUTTA NOSTRA, e non e' pignoleria: due istanze di
                 *  WebView2 nello stesso processo che condividono la cartella fanno
                 *  fallire la navigazione della seconda IN SILENZIO — nessuna
                 *  eccezione, solo IsSuccess falso. N.I.N.A. ne ha gia' una sua, e i
                 *  plugin che la usano sono piu' di uno. */
                var cartella = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "NINA", "WebView2", "AstroImageStrategyBridge");

                var ambiente = await CoreWebView2Environment.CreateAsync(
                    browserExecutableFolder: null, userDataFolder: cartella);
                await Vetro.EnsureCoreWebView2Async(ambiente);

                var nucleo = Vetro.CoreWebView2;
                /*  La pagina parla solo per messaggi. Niente oggetti dell'ospite
                 *  esposti al JavaScript: una superficie in meno da sorvegliare. */
                nucleo.Settings.AreHostObjectsAllowed = false;
                nucleo.Settings.IsWebMessageEnabled = true;
                nucleo.WebMessageReceived += AlMessaggio;
                nucleo.NavigationCompleted += AlNavigazione;

                nucleo.NavigateToString(Pagina.Prova);
                _pronto = true;
            } catch (Exception ex) {
                MostraRipiego("WebView2 non e' partito", ex.Message);
            }
        }

        private void AlNavigazione(object mittente, CoreWebView2NavigationCompletedEventArgs e) {
            if (e.IsSuccess) { MostraVetro(); }
            else { MostraRipiego("La pagina non si e' caricata", "WebErrorStatus: " + e.WebErrorStatus); }
        }

        private async void AlMessaggio(object mittente, CoreWebView2WebMessageReceivedEventArgs e) {
            string testo;
            try { testo = e.TryGetWebMessageAsString(); }
            catch { return; }
            if (string.IsNullOrWhiteSpace(testo)) { return; }

            string id = null;
            try {
                var messaggio = JsonNode.Parse(testo)?.AsObject();
                if (messaggio is null) { return; }
                id = messaggio["id"]?.GetValue<string>();
                var azione = messaggio["azione"]?.GetValue<string>();

                var cliente = (DataContext as PannelloStrategyVM)?.Cliente;
                if (cliente is null) { Rispondi(id, false, null, "senza_cliente",
                    "Il pannello non ha un corriere: manca il ViewModel."); return; }

                if (azione == "salute") {
                    var su = await cliente.Raggiungibile();
                    Rispondi(id, su, null, su ? null : "servizio_irraggiungibile",
                             su ? null : "Nessuno risponde a " + (DataContext as PannelloStrategyVM)?.Radice);
                    return;
                }

                if (azione == "manda") { Manda(id, messaggio); return; }

                if (azione != "prescrizione") {
                    Rispondi(id, false, null, "azione_sconosciuta", "Azione: " + azione); return;
                }

                var corpo = messaggio["corpo"]?.ToJsonString() ?? "{}";
                var esito = await cliente.Prescrizione(corpo);
                if (!esito.Riuscito) { Rispondi(id, false, null, esito.Codice, esito.Messaggio); return; }

                /*  La risposta resta in mano al ponte e riceve un identificativo, che
                 *  torna alla pagina. Quando la pagina chiedera' di consegnare, dovra'
                 *  rimandarlo: e' cio' che impedisce di mandare la riga di una
                 *  prescrizione precedente rimasta sullo schermo. */
                var vm = DataContext as PannelloStrategyVM;
                var idPrescrizione = vm?.InMano.Prendi(esito);

                Rispondi(id, true, esito.Corpo, null, null, esito.Sequenze.Count, esito.MsDelMotore,
                         new JsonObject {
                             ["prescrizione"] = idPrescrizione,
                             ["consegnabile"] = vm?.PerCheNonConsegna is null,
                             ["perche"] = vm?.PerCheNonConsegna,
                         });
            } catch (Exception ex) {
                Rispondi(id, false, null, "ponte_in_errore", ex.Message);
            }
        }

        /*  CONSEGNARE, cioe' l'unico gesto di questo ponte che tocca N.I.N.A.
         *
         *  Aggiunge un bersaglio al Sequenziatore Avanzato e si ferma li'. Non avvia
         *  niente: a premere il tasto e' chi riprende, e SequenceBuilder non nomina
         *  nemmeno StartAdvancedSequence — c'e' un test che lo verifica leggendo i
         *  riferimenti dentro il DLL.
         *
         *  Niente await qui dentro, di proposito: il messaggio arriva sul thread
         *  dell'interfaccia e la costruzione tocca oggetti WPF del Sequenziatore, che
         *  su un altro thread non si toccano. Restando sincrono non c'e' modo di
         *  finire altrove.
         */
        private void Manda(string id, JsonObject messaggio) {
            var vm = DataContext as PannelloStrategyVM;
            if (vm is null) {
                Rispondi(id, false, null, "senza_cliente", "Il pannello non ha un ViewModel."); return;
            }
            if (vm.PerCheNonConsegna != null) {
                Rispondi(id, false, null, "consegna_non_disponibile", vm.PerCheNonConsegna); return;
            }

            var idPrescrizione = messaggio["prescrizione"]?.GetValue<string>();
            var notte = messaggio["notte"]?.GetValue<int>() ?? 0;

            var scelta = vm.InMano.Notte(idPrescrizione, notte, out var codice, out var motivo);
            if (scelta is null) { Rispondi(id, false, null, codice, motivo); return; }

            var contenitore = vm.Costruttore.Costruisci(scelta.Modello, out var ricetta);
            if (contenitore is null) {
                Rispondi(id, false, null, "niente_da_costruire",
                    "Dal modello non e' uscito niente di riprendibile" +
                    (ricetta.Scartati.Count > 0 ? ": " + string.Join("; ", ricetta.Scartati) : "."));
                return;
            }

            SequenceBuilder.Consegna(vm.Mediatore, contenitore);

            /*  Le cose scartate e le note NON si nascondono dietro un «fatto». Un
             *  blocco che non si e' costruito deve vedersi, o chi riprende scoprira'
             *  sotto il cielo che una banda non c'era. */
            Rispondi(id, true, null, null, null, 0, null, new JsonObject {
                ["nome"] = ricetta.Nome,
                ["bersaglio"] = ricetta.NomeBersaglio,
                ["blocchi"] = ricetta.Blocchi.Count,
                ["pose"] = ricetta.Blocchi.Sum(b => b.Pose),
                ["note"] = new JsonArray(ricetta.Note.Select(x => (JsonNode)x!).ToArray()),
                ["scartati"] = new JsonArray(ricetta.Scartati.Select(x => (JsonNode)x!).ToArray()),
            });
        }

        private void Rispondi(string id, bool ok, string corpo, string codice, string messaggio,
                              int notti = 0, double? ms = null, JsonObject extra = null) {
            var nucleo = Vetro?.CoreWebView2;
            if (nucleo is null) { return; }
            var o = new JsonObject {
                ["id"] = id, ["ok"] = ok, ["notti"] = notti,
                ["ms"] = ms is null ? null : JsonValue.Create(ms.Value),
                ["codice"] = codice, ["messaggio"] = messaggio,
                /*  La risposta del servizio viaggia come testo: e' quella che e'
                 *  arrivata, non una sua riscrittura. */
                ["corpo"] = corpo,
            };
            if (extra != null) {
                /*  I nodi si spostano, non si copiano: un JsonNode appartiene a un
                 *  genitore solo, e riusarlo altrove solleva. */
                foreach (var k in extra.Select(x => x.Key).ToArray()) {
                    var v = extra[k];
                    extra.Remove(k);
                    o[k] = v;
                }
            }
            nucleo.PostWebMessageAsString(o.ToJsonString(new JsonSerializerOptions()));
        }

        private void AlRiprova(object mittente, RoutedEventArgs e) {
            if (Vetro?.CoreWebView2 != null) { Vetro.CoreWebView2.NavigateToString(Pagina.Prova); }
            else { AlCaricamento(mittente, e); }
        }

        private void MostraVetro() {
            Ripiego.Visibility = Visibility.Collapsed;
            Vetro.Visibility = Visibility.Visible;
        }

        private void MostraRipiego(string titolo, string motivo) {
            Vetro.Visibility = Visibility.Collapsed;
            Ripiego.Visibility = Visibility.Visible;
            RipiegoTitolo.Text = titolo;
            RipiegoMotivo.Text = motivo;
        }
    }
}
