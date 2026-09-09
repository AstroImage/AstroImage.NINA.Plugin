using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Threading.Tasks;
using AstroImage.NINA.Plugin.Models;
using AstroImage.NINA.Plugin.Services;
using AstroImage.NINA.Plugin.ViewModels;
using Microsoft.Web.WebView2.Core;
using NINA.Core.Utility;
using AstroImage.NINA.Plugin.Localization;

namespace AstroImage.NINA.Plugin.Views {

    /*  IL PONTE FRA LA PAGINA E IL C#.
     *
     *  La pagina non conosce nessun indirizzo e non fa nessuna chiamata di rete: manda
     *  un messaggio al suo ospite, e l'ospite va a chiedere. E' l'unico disegno che
     *  regge quando il motore sara' remoto e ci sara' un'identita' di mezzo — il
     *  segreto vive nel C#, dove chi apre gli strumenti di sviluppo sulla pagina non
     *  lo trova.
     *
     *  IL PROTOCOLLO, cinque azioni e nessuna cerimonia:
     *
     *      salute        { id, azione: "salute" }
     *                 -> { id, ok }
     *
     *      filtri        { id, azione: "filtri" }
     *                 -> { id, ok, righe[], catalogo[], catalogoDisponibile, diSerie[],
     *                      cameraAMatrice, ruotaVuota, nota, dichiarati }
     *
     *      salvaFiltri   { id, azione: "salvaFiltri", vetri: [{ nina, id, nota }] }
     *                 -> { id, ok, dichiarati }
     *
     *      prescrizione  { id, azione: "prescrizione", corpo: { … } }
     *                 -> { id, ok, corpo: "<il JSON del servizio>", notti, ms,
     *                      prescrizione: "<identificativo>", consegnabile, perche,
     *                      ruotaAggiunta }
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
            Loaded += AlRitorno;
            Unloaded += AlCongedo;
        }

        /*  CI SI RIATTACCA A OGNI RITORNO, e la prima versione non lo faceva.
         *
         *  Il difetto era questo, ed e' istruttivo: il selettore della lingua vive nelle
         *  OPZIONI, e aprire le Opzioni scarica il pannello. Agganciarsi una volta sola
         *  nel costruttore e staccarsi su Unloaded voleva dire non ascoltare proprio nel
         *  momento in cui la lingua cambia — il meccanismo funzionava, il percorso che
         *  una persona fa davvero no.
         *
         *  E non basta riattaccarsi: mentre eravamo via la lingua PUO' essere gia'
         *  cambiata, e nessuno ce l'ha detto. Quindi tornando si ridisegna comunque.
         *  Costa due letture e toglie l'unico caso in cui il pannello resterebbe scritto
         *  nella lingua di prima.
         */
        private void AlRitorno(object mittente, RoutedEventArgs e) {
            /*  Prima si toglie e poi si mette: Loaded puo' scattare piu' volte, e due
             *  iscrizioni vorrebbero dire due ricariche per ogni cambio. */
            Loc.Instance.PropertyChanged -= AlCambioLingua;
            Loc.Instance.PropertyChanged += AlCambioLingua;
            /*  Alla PRIMA apertura _pronto e' ancora falso — AlCaricamento sta aspettando
             *  WebView2 — e la pagina nascera' gia' nella lingua giusta da se'. */
            if (_pronto) { ChiediRidisegno(); }
        }

        /*  Loc e' un oggetto solo che vive quanto N.I.N.A.: un suo evento agganciato a
         *  questa vista la terrebbe viva per sempre. Un pannello aperto e chiuso dieci
         *  volte lascerebbe dieci viste, ognuna con dentro un WebView2. */
        private void AlCongedo(object mittente, RoutedEventArgs e) =>
            Loc.Instance.PropertyChanged -= AlCambioLingua;

        /*  IL CAMBIO LINGUA ARRIVA DALLE OPZIONI, che sono un'altra pagina.
         *
         *  Chi gira l'interruttore si aspetta di vedere il pannello cambiare, non di
         *  doverlo chiudere e riaprire. Qui non si traduce niente: si dice alla pagina
         *  di richiedere quello che il C# le aveva scritto, e il C# lo riscrive nella
         *  lingua nuova.
         */
        private void AlCambioLingua(object mittente, System.ComponentModel.PropertyChangedEventArgs e) {
            if (e.PropertyName != "Item[]") { return; }
            ChiediRidisegno();
        }

        /// <summary>
        /// Dice alla pagina di richiedere cio' che il C# le aveva scritto. Non traduce
        /// niente: le frasi rinascono di la', nella lingua che vale adesso.
        /// </summary>
        private void ChiediRidisegno() {
            if (!_pronto) { return; }
            /*  L'evento puo' arrivare da un thread qualunque; toccare WebView2 fuori dal
             *  suo thread e' un guasto che compare a caso. */
            Dispatcher.BeginInvoke(new Action(() => {
                try { Vetro?.CoreWebView2?.PostWebMessageAsString("{\"evento\":\"lingua\"}"); }
                catch (Exception) {
                    /*  La pagina puo' non esserci ancora, o essere gia' andata via. Non
                     *  si perde niente: alla prossima apertura nasce nella lingua giusta. */
                }
            }));
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
                MostraRipiego(Loc.T("Pannello_WebViewNonPartito"), ex.Message);
            }
        }

        private void AlNavigazione(object mittente, CoreWebView2NavigationCompletedEventArgs e) {
            if (e.IsSuccess) { MostraVetro(); }
            else { MostraRipiego(Loc.T("Pannello_PaginaNonCaricata"), "WebErrorStatus: " + e.WebErrorStatus); }
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
                if (cliente is null) { Rispondi(id, false, null, "senza_cliente", Loc.T("Pannello_SenzaCorriere")); return; }

                if (azione == "salute") {
                    var su = await cliente.Raggiungibile();
                    Rispondi(id, su, null, su ? null : "servizio_irraggiungibile",
                             su ? null : Loc.F("Pannello_NessunoRisponde", (DataContext as PannelloStrategyVM)?.Radice));
                    return;
                }

                if (azione == "manda") { Manda(id, messaggio); return; }

                /*  LA PAGINA CHIEDE LA CONFIGURAZIONE, e la compone lei.
                 *
                 *  Il corriere non tocca cio' che trasporta: c'e' un test che verifica
                 *  che il corpo parta come e' stato scritto, e quella proprieta' vale
                 *  piu' di venti righe risparmiate — il giorno in cui il ponte comincia
                 *  a «migliorare» le richieste, nessuno sa piu' che cosa abbia chiesto
                 *  davvero il client.
                 *
                 *  L'UNICA ECCEZIONE E' `ruota`, ed e' dichiarata: la pagina non puo'
                 *  conoscerla, perche' vive nel profilo di N.I.N.A. e nelle impostazioni
                 *  del plugin. Si aggiunge solo se manca, e si dice sempre nella risposta
                 *  che cosa e' partito. Vedi il commento sull'azione `prescrizione`. */
                if (azione == "filtri") { await Filtri(id); return; }

                if (azione == "salvaFiltri") { SalvaFiltri(id, messaggio); return; }

                if (azione == "sito") { Sito(id); return; }

                if (azione == "salvaSito") { SalvaSito(id, messaggio); return; }

                if (azione != "prescrizione") {
                    Rispondi(id, false, null, "azione_sconosciuta", Loc.F("Pannello_AzioneSconosciuta", azione)); return;
                }

                /*  LA RUOTA ENTRA NELLA RICHIESTA, e questa e' la correzione di un
                 *  difetto vero, non una comodita'.
                 *
                 *  Fino a ieri il ponte mandava solo `opzioni.filterNames`, che
                 *  RIETICHETTA i canali in uscita. Non mandava `ruota`, e il servizio in
                 *  quel caso fa `M.ruota(DB.default_filters)`: il motore calcolava sui
                 *  suoi dieci vetri di serie e poi rinominava il risultato con i nomi
                 *  della tua ruota. Una prescrizione che SEMBRAVA fatta sul tuo
                 *  equipaggiamento, e non lo era. E `OWNED` non e' cosmetico: decide
                 *  perfino se una banda sia ottenibile.
                 *
                 *  QUI IL PONTE TOCCA LA RICHIESTA, e altrove ha scritto che non lo fa.
                 *  Lo scostamento e' voluto e limitato: la ruota la pagina non puo'
                 *  conoscerla — vive nel profilo di N.I.N.A. e nelle impostazioni del
                 *  plugin — e la correttezza di una prescrizione non puo' dipendere dal
                 *  fatto che un client si ricordi di dichiararla. Quindi si aggiunge
                 *  SOLO SE MANCA: un client che la dichiara resta padrone della propria
                 *  domanda. E si dice sempre nella risposta che cosa e' partito, perche'
                 *  un'aggiunta silenziosa sarebbe esattamente il difetto che stiamo
                 *  togliendo, girato dall'altra parte. */
                var vmR = DataContext as PannelloStrategyVM;
                var corpoJson = messaggio["corpo"]?.AsObject() ?? new JsonObject();
                var dichiarati = DichiarazioneRuota.IdDichiarati(vmR?.Dichiarazione) ?? new List<string>();
                JsonArray ruotaInviata = null;
                if (corpoJson["ruota"] is null && dichiarati.Count > 0) {
                    ruotaInviata = new JsonArray();
                    foreach (var v in dichiarati) ruotaInviata.Add(v);
                    corpoJson["ruota"] = ruotaInviata.DeepClone();
                }
                var corpo = corpoJson.ToJsonString();
                var esito = await cliente.Prescrizione(corpo);
                if (!esito.Riuscito) { Rispondi(id, false, null, esito.Codice, esito.Messaggio); return; }

                /*  La risposta resta in mano al ponte e riceve un identificativo, che
                 *  torna alla pagina. Quando la pagina chiedera' di consegnare, dovra'
                 *  rimandarlo: e' cio' che impedisce di mandare la riga di una
                 *  prescrizione precedente rimasta sullo schermo. */
                var vm = DataContext as PannelloStrategyVM;
                var idPrescrizione = vm?.InMano.Prendi(esito);
                /*  IL DATO CHE MANCAVA. Alla prima integrazione il pannello disse «non
                 *  disponibile» e per sapere PERCHE' bisognava passarci sopra il mouse.
                 *  Adesso il motivo finisce nel log, e la domanda «e' stato chiamato
                 *  AddAdvancedTarget?» ha una risposta scritta invece che dedotta. */
                var perche = vm?.PerCheNonConsegna;
                Logger.Info("[AstroImage] prescription: " + esito.Sequenze.Count + " nights, " +
                            (perche is null ? "deliverable" : "NOT deliverable — " + perche) +
                            ", declared wheel: " + (dichiarati.Count > 0
                                ? string.Join("+", dichiarati) : "NONE (the engine uses its stock filters)"));

                Rispondi(id, true, esito.Corpo, null, null, esito.Sequenze.Count, esito.MsDelMotore,
                         new JsonObject {
                             ["prescrizione"] = idPrescrizione,
                             ["consegnabile"] = perche is null,
                             ["perche"] = perche,
                             /*  Che cosa e' partito davvero. Null quando il ponte non ha
                                aggiunto niente — o perche' il client aveva gia' dichiarato
                                la sua ruota, o perche' non ce n'e' una configurata: e in
                                quel secondo caso la pagina deve dirlo forte, perche' la
                                prescrizione che si sta guardando e' stata calcolata sui
                                vetri di serie del motore e non sui propri. */
                             ["ruotaAggiunta"] = ruotaInviata?.DeepClone(),
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
            /*  OGNI PASSO SI SCRIVE NEL LOG DI N.I.N.A., e non e' zelo.
             *
             *  La prima volta che questo percorso non ha funzionato, la domanda giusta —
             *  «AddAdvancedTarget e' stato chiamato o no?» — non aveva una risposta:
             *  c'era una schermata e quattro ipotesi. Un percorso che tocca un
             *  programma altrui e finisce in un'interfaccia che non e' la nostra deve
             *  lasciare una traccia leggibile dove la si va a cercare, cioe' nel log di
             *  N.I.N.A. insieme a tutto il resto della serata. */
            const string IO = "[AstroImage] ";
            var vm = DataContext as PannelloStrategyVM;
            if (vm is null) {
                Logger.Error(IO + "send: the panel has no ViewModel");
                Rispondi(id, false, null, "senza_cliente", Loc.T("Pannello_SenzaViewModel")); return;
            }

            var idPrescrizione = messaggio["prescrizione"]?.GetValue<string>();
            var notte = messaggio["notte"]?.GetValue<int>() ?? 0;
            Logger.Info(IO + $"send: night {notte}, prescription {idPrescrizione ?? "(none)"}");

            var perche = vm.PerCheNonConsegna;
            if (perche != null) {
                Logger.Warning(IO + "send: cannot deliver — " + perche);
                Rispondi(id, false, null, "consegna_non_disponibile", perche); return;
            }

            var scelta = vm.InMano.Notte(idPrescrizione, notte, out var codice, out var motivo);
            if (scelta is null) {
                Logger.Warning(IO + $"send: night not obtained — {codice}: {motivo}");
                Rispondi(id, false, null, codice, motivo); return;
            }

            /*  IL VETRO LO DICE IL MOTORE, NON LA NOSTRA MAPPA.
             *
             *  `blocco.filtro` arriva dal motore per RIETICHETTATURA del canale: e' il
             *  nome che gli abbiamo passato noi accanto a quel canale, e ci torna
             *  indietro identico. Circolare, e per ora innocuo — ma il giorno in cui il
             *  motore sceglie fra un L-eNhance e un L-Ultimate in base al cielo, quel
             *  nome direbbe l'uno mentre le ore sono state calcolate sull'altro.
             *
             *  La risposta vera e' in `posa.<canale>.ex.spec.filter.id`, dove il motore
             *  dichiara il vetro su cui ha fatto il conto. Si risolve quello nella
             *  dichiarazione dell'utente e si ottiene il nome operativo giusto.
             *
             *  E se un identificativo non e' dichiarato NON si ripiega sul nome vecchio:
             *  il motore ha calcolato ore su un vetro che, per quanto ne sappiamo, in
             *  ruota non c'e'. Consegnare comunque vorrebbe dire riprendere col vetro
             *  sbagliato — l'asimmetria di sempre: un rifiuto si corregge in un clic,
             *  cinque ore no. */
            var perCanale = vm.InMano.VetriPerCanale();
            var nonDichiarati = new List<string>();
            var discordi = new List<string>();
            foreach (var b in scelta.Modello?.Blocchi ?? new List<Blocco>()) {
                var idVetro = VetriDellaPrescrizione.DelBlocco(b.Canali, perCanale, out var perCheNoVetro);
                if (perCheNoVetro != null && idVetro is null && b.Canali != null && b.Canali.Count > 0
                        && perCanale.Count > 0) { discordi.Add(perCheNoVetro); continue; }
                if (idVetro is null) continue;   // il motore non l'ha detto: resta il nome di prima
                var nome = DichiarazioneRuota.NomePerId(vm.Dichiarazione, idVetro);
                if (string.IsNullOrWhiteSpace(nome)) {
                    var etichetta = string.Join("+", b.Canali ?? new List<string>());
                    nonDichiarati.Add(idVetro == VetriDellaPrescrizione.NessunFiltro
                        /*  «__none» non e' un vetro che ti manca: e' il motore che dice
                         *  «davanti non ci va niente, il colore lo fa la matrice». Ma
                         *  con cinque vetri in ruota qualcosa davanti c'e' per forza, e
                         *  riprendere con quello montato sarebbe la sostituzione
                         *  silenziosa di sempre. Quindi si chiede di dichiararlo. */
                        ? Loc.F("Manda_NessunFiltroDaDichiarare", etichetta)
                        : Loc.F("Manda_VetroNonDichiarato", etichetta, idVetro));
                    continue;
                }
                b.Filtro = nome;
            }
            if (discordi.Count > 0 || nonDichiarati.Count > 0) {
                var motivoVetro = Loc.F("Manda_FiltroNonRisolto",
                    string.Join("; ", discordi.Concat(nonDichiarati)));
                Logger.Warning(IO + "send: filter could not be resolved — " + motivoVetro);
                Rispondi(id, false, null, "vetro_non_dichiarato", motivoVetro);
                return;
            }

            var contenitore = vm.Costruttore.Costruisci(scelta.Modello, out var ricetta);
            if (contenitore is null) {
                Logger.Warning(IO + "send: nothing to build — " +
                    (ricetta.Scartati.Count > 0 ? string.Join("; ", ricetta.Scartati) : "nessun motivo dichiarato"));
                Rispondi(id, false, null, "niente_da_costruire",
                    Loc.T("Pannello_NienteDaCostruire") +
                    (ricetta.Scartati.Count > 0 ? ": " + string.Join("; ", ricetta.Scartati) : "."));
                return;
            }

            Logger.Info(IO + $"send: built «{ricetta.NomeBersaglio}», {ricetta.Blocchi.Count} blocks, " +
                             $"{ricetta.Blocchi.Sum(b => b.Pose)} exposures; dropped {ricetta.Scartati.Count}");
            try {
                SequenceBuilder.Consegna(vm.Mediatore, contenitore);
                Logger.Info(IO + "send: AddAdvancedTarget called, no exception");
            } catch (Exception ex) {
                Logger.Error(IO + "send: AddAdvancedTarget threw", ex);
                Rispondi(id, false, null, "consegna_fallita", ex.Message);
                return;
            }

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

        /*  LA CONFIGURAZIONE DEI VETRI, messa insieme dalle tre cose che la compongono:
         *  la ruota com'e' adesso nel profilo, la dichiarazione che l'utente ha gia'
         *  fatto, e il catalogo del motore. Nessuna delle tre e' padrona delle altre, e
         *  i modi in cui non combaciano sono informazioni — un nome rinominato lascia
         *  una voce orfana, un vetro nuovo una riga vuota. */
        private async Task Filtri(string id) {
            var vm = DataContext as PannelloStrategyVM;
            if (vm is null) { Rispondi(id, false, null, "senza_cliente", Loc.T("Pannello_SenzaViewModel")); return; }

            /*  Il catalogo si chiede una volta e si tiene: cambia solo quando cambia il
             *  motore, e un giro di rete a ogni apertura della pagina sarebbe speso per
             *  niente. Se il servizio e' spento resta null — e null non e' un elenco
             *  vuoto: la pagina deve poter dire «accendi il motore», non «non conosce
             *  nessun vetro». */
            if (vm.Catalogo is null) vm.Catalogo = await vm.Cliente.Filtri();

            var vetri = vm.Ruota.Vetri(out var perCheNo);
            var righe = RiconciliaRuota.Righe(vetri, vm.Dichiarazione, vm.Catalogo, vm.Ruota.CameraAMatrice());

            var elenco = new JsonArray();
            foreach (var r in righe) {
                elenco.Add(new JsonObject {
                    ["nina"] = r.Nina,
                    ["slot"] = r.Slot,
                    ["id"] = r.IdMotore,
                    ["vetro"] = DichiarazioneRuota.Etichetta(r.Vetro),
                    ["banda"] = r.Vetro?.Banda,
                    ["stato"] = r.Stato.ToString().ToLowerInvariant(),
                    ["adatto"] = r.AdattoAllaCamera,
                    ["ambiguo"] = r.NomeAmbiguo,
                    ["nota"] = r.Nota,
                });
            }

            var catalogo = new JsonArray();
            foreach (var v in vm.Catalogo?.Filtri ?? new List<VetroDelMotore>()) {
                if (string.IsNullOrWhiteSpace(v.Id)) continue;
                catalogo.Add(new JsonObject {
                    ["id"] = v.Id, ["nome"] = DichiarazioneRuota.Etichetta(v), ["banda"] = v.Banda,
                    ["fwhm_nm"] = v.FwhmNm, ["dual"] = v.Dual,
                    ["bande"] = v.Bande is null ? null : new JsonArray(v.Bande.Select(x => (JsonNode)x).ToArray()),
                    ["per_mono"] = v.PerMono, ["per_cfa"] = v.PerCfa,
                });
            }

            var diSerie = new JsonArray();
            foreach (var s in vm.Catalogo?.DiSerie ?? new List<string>()) diSerie.Add(s);

            Rispondi(id, true, null, null, null, 0, null, new JsonObject {
                ["righe"] = elenco,
                ["catalogo"] = catalogo,
                ["catalogoDisponibile"] = vm.Catalogo != null,
                ["diSerie"] = diSerie,
                ["cameraAMatrice"] = vm.Ruota.CameraAMatrice(),
                ["ruotaVuota"] = perCheNo,
                ["nota"] = vm.NotaDichiarazione,
                ["dichiarati"] = DichiarazioneRuota.IdDichiarati(vm.Dichiarazione).Count,
            });
        }

        /*  SALVA LA DICHIARAZIONE. Arriva intera e sostituisce quella di prima: un
         *  salvataggio parziale lascerebbe due verita' in giro, e a quel punto quale
         *  vale? La pagina manda tutto quello che ha in tavola. */
        private void SalvaFiltri(string id, JsonObject messaggio) {
            var vm = DataContext as PannelloStrategyVM;
            if (vm is null) { Rispondi(id, false, null, "senza_cliente", Loc.T("Pannello_SenzaViewModel")); return; }

            var nuova = DichiarazioneRuota.DalMessaggio(messaggio, out var perCheMalformata);
            if (nuova is null) {
                Logger.Warning("[AstroImage] filter save refused — " + perCheMalformata);
                Rispondi(id, false, null, "richiesta_malformata", perCheMalformata);
                return;
            }

            var ok = vm.SalvaDichiarazione(nuova, out var perCheNo);
            Logger.Info("[AstroImage] virtual wheel saved: " + DichiarazioneRuota.IdDichiarati(nuova).Count +
                        " vetri dichiarati" + (ok ? "" : " — NON SCRITTA: " + perCheNo));
            Rispondi(id, ok, null, ok ? null : "salvataggio_fallito", perCheNo, 0, null,
                     new JsonObject { ["dichiarati"] = DichiarazioneRuota.IdDichiarati(nuova).Count });
        }

        /*  DOVE SEI, e da dove lo sappiamo.
         *
         *  Fino a ieri la pagina spediva sette numeri scritti dentro: Borno. Adesso la
         *  geometria arriva dal profilo di N.I.N.A. e il resto da uno strumento se c'e',
         *  dalla dichiarazione se no — con la provenienza accanto a ogni valore, perche'
         *  un SQM misurato e uno scritto a mano non devono somigliarsi. */
        private void Sito(string id) {
            var vm = DataContext as PannelloStrategyVM;
            if (vm is null) { Rispondi(id, false, null, "senza_cliente", Loc.T("Pannello_SenzaViewModel")); return; }

            var letto = vm.Sito.Leggi(out var perCheNo);
            var unito = DichiarazioneSito.Unisci(letto, vm.SitoScritto);

            var prov = new JsonObject();
            foreach (var kv in unito.Provenienza ?? new Dictionary<string, string>())
                prov[kv.Key] = kv.Value;

            Rispondi(id, true, null, null, null, 0, null, new JsonObject {
                ["sito"] = new JsonObject {
                    ["lat"] = unito.Lat, ["lon"] = unito.Lon, ["sqm"] = unito.Sqm,
                    ["seeing"] = unito.Seeing, ["rms"] = unito.Rms,
                    ["horizonMin"] = unito.HorizonMin, ["clearFrac"] = unito.ClearFrac,
                },
                /*  Solo i campi che l'utente puo' scrivere: la geometria non si dichiara,
                 *  viene dal profilo e un doppione qui divergerebbe da quello. */
                ["dichiarato"] = new JsonObject {
                    ["sqm"] = vm.SitoScritto?.Sqm, ["seeing"] = vm.SitoScritto?.Seeing,
                    ["rms"] = vm.SitoScritto?.Rms, ["horizonMin"] = vm.SitoScritto?.HorizonMin,
                    ["clearFrac"] = vm.SitoScritto?.ClearFrac,
                },
                ["provenienza"] = prov,
                ["nome"] = unito.Nome,
                ["perCheNo"] = perCheNo,
                ["nota"] = vm.NotaSito,
                /*  Che cosa manca perche' il motore possa produrre una prescrizione
                 *  COMPLETA. Si dice PRIMA di chiedere: il servizio con un sito
                 *  incompleto risponde con le ore e nessuna sequenza, e senza questa
                 *  riga chi guarda vedrebbe un risultato vuoto senza sapere perche'. */
                ["manca"] = DichiarazioneSito.CheCosaManca(unito),
            });
        }

        private void SalvaSito(string id, JsonObject messaggio) {
            var vm = DataContext as PannelloStrategyVM;
            if (vm is null) { Rispondi(id, false, null, "senza_cliente", Loc.T("Pannello_SenzaViewModel")); return; }

            var nuovo = DichiarazioneSito.DalMessaggio(messaggio, out var perCheMalformata);
            if (nuovo is null) {
                Logger.Warning("[AstroImage] site save refused — " + perCheMalformata);
                Rispondi(id, false, null, "richiesta_malformata", perCheMalformata);
                return;
            }
            var ok = vm.SalvaSito(nuovo, out var perCheNo);
            Logger.Info("[AstroImage] declared site saved" + (ok ? "" : " — NOT WRITTEN: " + perCheNo));
            Rispondi(id, ok, null, ok ? null : "salvataggio_fallito", perCheNo);
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
