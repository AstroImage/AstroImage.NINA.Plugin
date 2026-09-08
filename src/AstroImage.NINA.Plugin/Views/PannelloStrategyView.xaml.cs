using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
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
     *  IL PROTOCOLLO, tre campi e nessuna cerimonia:
     *      pagina  ->  ospite    { id, azione: "prescrizione", corpo: { … } }
     *      ospite  ->  pagina    { id, ok: true,  corpo: "<il JSON del servizio>", notti, ms }
     *                            { id, ok: false, codice, messaggio }
     *  `corpo` torna come TESTO e non come oggetto, di proposito: e' la risposta del
     *  servizio parola per parola, e passarla come stringa e' l'unico modo di essere
     *  certi che nessuno l'abbia rimaneggiata attraversando il confine.
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

                if (azione != "prescrizione") {
                    Rispondi(id, false, null, "azione_sconosciuta", "Azione: " + azione); return;
                }

                var corpo = messaggio["corpo"]?.ToJsonString() ?? "{}";
                var esito = await cliente.Prescrizione(corpo);
                if (!esito.Riuscito) { Rispondi(id, false, null, esito.Codice, esito.Messaggio); return; }

                Rispondi(id, true, esito.Corpo, null, null, esito.Sequenze.Count, esito.MsDelMotore);
            } catch (Exception ex) {
                Rispondi(id, false, null, "ponte_in_errore", ex.Message);
            }
        }

        private void Rispondi(string id, bool ok, string corpo, string codice, string messaggio,
                              int notti = 0, double? ms = null) {
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
