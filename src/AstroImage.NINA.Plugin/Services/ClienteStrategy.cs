using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AstroImage.NINA.Plugin.Models;
using AstroImage.NINA.Plugin.Localization;

#nullable enable

namespace AstroImage.NINA.Plugin.Services {

    /*  IL CORRIERE.
     *
     *  L'interfaccia compone la domanda, questo la porta a Strategy e riporta
     *  indietro la risposta. Non la legge, non la corregge, non la completa.
     *
     *  PERCHE' IL CORPO E' UNA STRINGA e non un tipo. La domanda contiene il sito,
     *  il banco, la ruota, il bersaglio, la notte: cose che il ponte non usa per
     *  niente. Tipizzarle vorrebbe dire ricompilare il ponte ogni volta che il motore
     *  aggiunge un campo, e — molto peggio — mettere sotto mano a qualcuno un oggetto
     *  con dentro il sito e il banco, cioe' la tentazione di deciderci sopra qualcosa.
     *  Cio' che non si sa leggere non si puo' reinterpretare. Il ponte sa leggere una
     *  cosa sola di tutta la conversazione: i modelli di sequenza.
     *
     *  PERCHE' L'INDIRIZZO ARRIVA DA FUORI. Qui dentro non c'e' scritto ne' localhost
     *  ne' http. Oggi il servizio gira sulla stessa macchina in chiaro perche' e' una
     *  prova di trasporto; domani sara' un dominio con TLS e un'identita'. Quel giorno
     *  cambia l'Uri che qualcun altro passa al costruttore, e questo file resta com'e'.
     *  Se un giorno qui comparisse un "127.0.0.1", la prova sarebbe diventata
     *  l'architettura senza che nessuno l'abbia deciso.
     *
     *  E L'INDIRIZZO LO TIENE IL PONTE, non la pagina. La pagina fornisce il corpo,
     *  mai la destinazione: altrimenti chiunque riesca a parlare alla pagina potrebbe
     *  usare il ponte per bussare dove vuole.
     */

    /// <summary>
    /// Parla al servizio Strategy. Un metodo, una domanda, una risposta.
    /// </summary>
    public sealed class ClienteStrategy {

        private readonly HttpClient _http;
        private readonly Uri _prescrizione;
        private readonly Uri _salute;
        private readonly Uri _filtri;
        private readonly Uri _cerca;

        /// <param name="http">Il cliente HTTP. Lo costruisce chi sa quanto deve durare
        /// una connessione e quante ne servono: non e' una decisione di questo file.</param>
        /// <param name="baseUri">La radice del servizio, per esempio
        /// <c>http://127.0.0.1:8791/</c> oggi o <c>https://…/</c> domani.</param>
        public ClienteStrategy(HttpClient http, Uri baseUri) {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            if (baseUri is null) throw new ArgumentNullException(nameof(baseUri));
            _prescrizione = new Uri(baseUri, "v1/prescrizione");
            _salute = new Uri(baseUri, "v1/salute");
            _filtri = new Uri(baseUri, "v1/filtri");
            _cerca = new Uri(baseUri, "v1/cerca");
        }

        /*  IL CATALOGO DEI VETRI, per far dichiarare all'utente che cosa ha in ruota.
         *
         *  Torna null quando non si e' potuto avere, e null non e' un elenco vuoto: un
         *  elenco vuoto direbbe «il motore non conosce nessun vetro», che sarebbe falso.
         *  Chi chiama deve poter distinguere «non l'ho chiesto bene» da «non c'e' niente»,
         *  perche' nel primo caso la pagina dice di accendere il servizio e nel secondo
         *  direbbe una bugia. */
        public async Task<CatalogoDelMotore?> Filtri(CancellationToken ct = default) {
            try {
                using var r = await _http.GetAsync(_filtri, ct).ConfigureAwait(false);
                if (!r.IsSuccessStatusCode) return null;
                var corpo = await r.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonSerializer.Deserialize<CatalogoDelMotore>(corpo, SequenceModel.OpzioniJson);
            } catch (Exception) {
                /*  Servizio spento, indirizzo sbagliato, risposta storta: da qui in poi
                 *  sono tutti lo stesso fatto — non c'e' catalogo — e chi chiama lo
                 *  dice a chi guarda. Un'eccezione qui chiuderebbe il pannello. */
                return null;
            }
        }

        /// <summary>
        /// Chiede una prescrizione. <paramref name="richiestaJson"/> e' il corpo che
        /// l'interfaccia ha composto, spedito come e'.
        /// </summary>
        public async Task<EsitoPrescrizione> Prescrizione(string richiestaJson, CancellationToken ct = default) {
            if (string.IsNullOrWhiteSpace(richiestaJson))
                return EsitoPrescrizione.Fallito(0, "richiesta_vuota", Loc.T("Cliente_CorpoVuoto"));

            HttpResponseMessage risposta;
            string corpo;
            try {
                using var contenuto = new StringContent(richiestaJson, Encoding.UTF8, "application/json");
                risposta = await _http.PostAsync(_prescrizione, contenuto, ct).ConfigureAwait(false);
                corpo = await risposta.Content.ReadAsStringAsync().ConfigureAwait(false);
            } catch (OperationCanceledException) when (ct.IsCancellationRequested) {
                throw;
            } catch (Exception e) {
                /*  Il servizio spento e il servizio che risponde male sono due cose
                 *  diverse, e chi legge deve poterle distinguere: la prima si aggiusta
                 *  accendendolo, la seconda no. */
                return EsitoPrescrizione.Fallito(0, "servizio_irraggiungibile", e.Message);
            }

            var stato = (int)risposta.StatusCode;
            RispostaPrescrizione? busta = null;
            try { busta = RispostaPrescrizione.Leggi(corpo); }
            catch (Exception e) {
                return EsitoPrescrizione.Fallito(stato, "risposta_illeggibile", e.Message, corpo);
            }

            if (busta?.Errore != null)
                /*  I dati vengono appiattiti QUI, una volta sola, e viaggiano gia'
                 *  come testo. La frase pero' NON si scrive qui: si scrive al momento
                 *  di mostrarla, perche' la lingua si puo' cambiare mentre il
                 *  pannello e' aperto e un messaggio congelato al momento della
                 *  richiesta resterebbe nella lingua di prima. */
                return EsitoPrescrizione.Fallito(stato, busta.Errore.Codice, busta.Errore.Messaggio,
                    corpo, MessaggioDelMotore.Appiattisci(busta.Errore.Dati));

            if (!risposta.IsSuccessStatusCode)
                return EsitoPrescrizione.Fallito(stato, "risposta_non_riuscita",
                    Loc.F("Cliente_RispostaSenzaMotivo", stato), corpo);

            /*  Le sequenze senza modello si scartano qui e si dice quante erano. Un
             *  elenco che si accorcia in silenzio e' il modo piu' rapido per far
             *  riprendere a qualcuno una notte in meno senza saperlo. */
            var sequenze = new List<SequenzaDiNotte>();
            var scartate = 0;
            foreach (var s in busta?.Prodotto?.Sequenze ?? new List<SequenzaDiNotte>()) {
                if (s?.Modello is null) { scartate++; continue; }
                sequenze.Add(s);
            }

            return EsitoPrescrizione.Riuscita(stato, corpo, sequenze, scartate,
                busta?.Contratto, busta?.Misura?.Ms);
        }

        /*  I TRE MODI DI RIPRESA, chiesti e non scritti.
         *
         *  RESA, EQUILIBRIO e DINAMICA sono decisioni del MOTORE: e' lui a sapere
         *  quanto lunga viene la posa, quale limite la lega e quanto costa la scelta.
         *  Il ponte non ne conserva niente — nemmeno l'elenco degli identificativi —
         *  perche' un elenco copiato qui sarebbe una seconda verita' accanto a quella
         *  del motore, e le due divergerebbero il giorno in cui ne comparisse un
         *  quarto.
         *
         *  Torna vuoto quando il servizio non risponde o non li dichiara: allora la
         *  pagina non mostra i modi e la richiesta parte senza la chiave, cosi' il
         *  servizio applica il proprio predefinito — che e' sempre stato compito suo.
         */
        public async Task<ModalitaDiRipresa> Modalita(CancellationToken ct = default) {
            try {
                using var r = await _http.GetAsync(_salute, ct).ConfigureAwait(false);
                if (!r.IsSuccessStatusCode) return ModalitaDiRipresa.Vuota;
                var corpo = await r.Content.ReadAsStringAsync().ConfigureAwait(false);
                using var doc = JsonDocument.Parse(corpo);
                if (!doc.RootElement.TryGetProperty("modalita", out var m)) return ModalitaDiRipresa.Vuota;
                var elenco = new List<ModoDiRipresa>();
                if (m.TryGetProperty("elenco", out var el) && el.ValueKind == JsonValueKind.Array)
                    foreach (var x in el.EnumerateArray()) {
                        var id = x.TryGetProperty("id", out var i) ? i.GetString() : null;
                        if (string.IsNullOrWhiteSpace(id)) continue;
                        elenco.Add(new ModoDiRipresa { Id = id!,
                            Etichetta = x.TryGetProperty("etichetta", out var e) ? e.GetString() : null,
                            Spiegazione = x.TryGetProperty("spiegazione", out var s) ? s.GetString() : null });
                    }
                /*  E LE POLITICHE DI SESSIONE, dalla stessa risposta e con la stessa forma: elenco e predefinito li
                 *  dichiara il motore. Assenti su un motore piu' vecchio: la pagina resta senza il secondo
                 *  controllo, e la richiesta parte senza la chiave. */
                var politiche = new List<ModoDiRipresa>();
                string? politicaDiSerie = null;
                if (doc.RootElement.TryGetProperty("politica", out var pol) && pol.ValueKind == JsonValueKind.Object) {
                    if (pol.TryGetProperty("elenco", out var pel) && pel.ValueKind == JsonValueKind.Array)
                        foreach (var y in pel.EnumerateArray()) {
                            var pid = y.TryGetProperty("id", out var pi) ? pi.GetString() : null;
                            if (string.IsNullOrWhiteSpace(pid)) continue;
                            politiche.Add(new ModoDiRipresa { Id = pid!,
                                Etichetta = y.TryGetProperty("etichetta", out var pe) ? pe.GetString() : null,
                                Spiegazione = y.TryGetProperty("spiegazione", out var ps) ? ps.GetString() : null });
                        }
                    politicaDiSerie = pol.TryGetProperty("di_serie", out var pd) ? pd.GetString() : null;
                }
                /*  E I CAMPI DEL BANCO, da `limiti.banco`: quali entrano nel motore, chi li scrive, e i codici con cui il
                 *  motore dice che due sorgenti divergono. Il blocco del banco nella pagina si costruisce da qui, non da
                 *  una lista del Ponte. Un campo senza chiave si salta; un motore che non li pubblica lascia l'elenco
                 *  vuoto, e la pagina lo dice invece di inventarne uno. */
                var campiDelBanco = new List<CampoDelBanco>();
                var divergenzeDelBanco = new List<string>();
                string? Testo(JsonElement el, string nome) =>
                    el.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                if (doc.RootElement.TryGetProperty("limiti", out var lim) && lim.ValueKind == JsonValueKind.Object &&
                    lim.TryGetProperty("banco", out var lb) && lb.ValueKind == JsonValueKind.Object) {
                    if (lb.TryGetProperty("campi", out var lc) && lc.ValueKind == JsonValueKind.Array)
                        foreach (var c in lc.EnumerateArray()) {
                            if (c.ValueKind != JsonValueKind.Object) continue;
                            var chiave = Testo(c, "chiave");
                            if (string.IsNullOrWhiteSpace(chiave)) continue;
                            campiDelBanco.Add(new CampoDelBanco { Chiave = chiave!, Pezzo = Testo(c, "pezzo"),
                                Provenienza = Testo(c, "provenienza"), Unita = Testo(c, "unita") });
                        }
                    if (lb.TryGetProperty("divergenze", out var ld) && ld.ValueKind == JsonValueKind.Array)
                        foreach (var x in ld.EnumerateArray())
                            if (x.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(x.GetString()))
                                divergenzeDelBanco.Add(x.GetString()!);
                }
                return new ModalitaDiRipresa { Elenco = elenco,
                    DiSerie = m.TryGetProperty("di_serie", out var d) ? d.GetString() : null,
                    Politiche = politiche, PoliticaDiSerie = politicaDiSerie,
                    CampiDelBanco = campiDelBanco, DivergenzeDelBanco = divergenzeDelBanco };
            } catch (OperationCanceledException) when (ct.IsCancellationRequested) {
                throw;
            } catch { return ModalitaDiRipresa.Vuota; }
        }

        /*  LA RICERCA MENTRE SI SCRIVE (17 settembre 2026). Il testo del campo va al servizio cosi' com'e', e la risposta
         *  torna come testo: le corrispondenze e il loro ordine sono del motore, la pagina le mostra. Il numero di serie e'
         *  quello della pagina di AIS. Null quando non si e' potuta avere: l'elenco sotto il campo resta com'era. */
        public async Task<string?> Cerca(string q, int n = 12, CancellationToken ct = default) {
            try {
                var indirizzo = new Uri(_cerca.AbsoluteUri + "?q=" + Uri.EscapeDataString(q ?? string.Empty) + "&n=" + n);
                using var r = await _http.GetAsync(indirizzo, ct).ConfigureAwait(false);
                if (!r.IsSuccessStatusCode) return null;
                return await r.Content.ReadAsStringAsync().ConfigureAwait(false);
            } catch (OperationCanceledException) when (ct.IsCancellationRequested) {
                throw;
            } catch { return null; }
        }

        /// <summary>C'e' qualcuno dall'altra parte? Vero o falso, senza spiegazioni:
        /// serve solo a dire all'utente se accendere il servizio.</summary>
        public async Task<bool> Raggiungibile(CancellationToken ct = default) {
            try {
                using var r = await _http.GetAsync(_salute, ct).ConfigureAwait(false);
                return r.IsSuccessStatusCode;
            } catch (OperationCanceledException) when (ct.IsCancellationRequested) {
                throw;
            } catch { return false; }
        }
    }

    /// <summary>Un modo di ripresa come il motore lo dichiara: un identificativo, e
    /// le parole che servono a chi non ha una traduzione per quell'identificativo.</summary>
    public sealed class ModoDiRipresa {
        public string Id { get; set; } = string.Empty;
        public string? Etichetta { get; set; }
        public string? Spiegazione { get; set; }
    }

    /// <summary>L'elenco chiuso dei modi e quale vale quando non se ne sceglie
    /// nessuno. Vuoto quando il servizio non risponde: non si inventa.</summary>
    public sealed class ModalitaDiRipresa {
        public IReadOnlyList<ModoDiRipresa> Elenco { get; set; } = new List<ModoDiRipresa>();
        public string? DiSerie { get; set; }
        /// <summary>Le politiche di sessione, con la stessa forma dei modi. Vuote su un motore che non le dichiara.</summary>
        public IReadOnlyList<ModoDiRipresa> Politiche { get; set; } = new List<ModoDiRipresa>();
        public string? PoliticaDiSerie { get; set; }
        /// <summary>I campi del banco che il motore accetta, da <c>limiti.banco</c>. Vuoti su un motore che non li pubblica.</summary>
        public IReadOnlyList<CampoDelBanco> CampiDelBanco { get; set; } = new List<CampoDelBanco>();
        /// <summary>I codici con cui il motore dice che due sorgenti del banco divergono.</summary>
        public IReadOnlyList<string> DivergenzeDelBanco { get; set; } = new List<string>();
        public static ModalitaDiRipresa Vuota => new ModalitaDiRipresa();
    }

    /// <summary>Un campo del contratto del banco, come il servizio lo pubblica: la chiave, il pezzo, chi scrive il
    /// valore (<c>riconoscimento</c>, <c>dichiarabile</c>, <c>nina</c>, <c>driver</c>, <c>richiesta</c>) e l'unita'.</summary>
    public sealed class CampoDelBanco {
        public string Chiave { get; set; } = "";
        public string? Pezzo { get; set; }
        public string? Provenienza { get; set; }
        public string? Unita { get; set; }
    }

    /// <summary>
    /// Com'e' andata. <see cref="Corpo"/> e' la risposta intera del servizio, da
    /// restituire all'interfaccia parola per parola; <see cref="Sequenze"/> e' l'unica
    /// parte che il ponte ha letto, perche' e' l'unica che deve consegnare.
    /// </summary>
    public sealed class EsitoPrescrizione {

        public bool Riuscito { get; private set; }
        public int Stato { get; private set; }
        public string Corpo { get; private set; } = string.Empty;
        public string? Codice { get; private set; }

        /// <summary>La frase italiana del motore: il ripiego, non il testo da
        /// mostrare. Vedi <see cref="MessaggioDelMotore"/>.</summary>
        public string? Messaggio { get; private set; }

        /// <summary>I valori nudi dell'errore, gia' resi testo. Vuoto quando il
        /// motore non li manda — allora si mostra <see cref="Messaggio"/>.</summary>
        public IReadOnlyDictionary<string, string> Dati { get; private set; }
            = new Dictionary<string, string>();

        /// <summary>La frase da mettere davanti a chi guarda, nella lingua scelta
        /// ADESSO: si compone alla lettura, non alla richiesta, perche' la lingua
        /// puo' cambiare mentre il pannello e' aperto.</summary>
        public string? MessaggioTradotto => MessaggioDelMotore.Rendi(Codice, Dati, Messaggio);
        public string? Contratto { get; private set; }
        public double? MsDelMotore { get; private set; }
        public IReadOnlyList<SequenzaDiNotte> Sequenze { get; private set; } = new List<SequenzaDiNotte>();
        /// <summary>Quante notti sono arrivate senza modello. Zero, di solito.</summary>
        public int Scartate { get; private set; }

        internal static EsitoPrescrizione Riuscita(int stato, string corpo, IReadOnlyList<SequenzaDiNotte> seq,
                int scartate, string? contratto, double? ms) =>
            new EsitoPrescrizione { Riuscito = true, Stato = stato, Corpo = corpo, Sequenze = seq,
                Scartate = scartate, Contratto = contratto, MsDelMotore = ms };

        internal static EsitoPrescrizione Fallito(int stato, string? codice, string? messaggio,
                string corpo = "", IReadOnlyDictionary<string, string>? dati = null) =>
            new EsitoPrescrizione { Riuscito = false, Stato = stato, Codice = codice,
                Messaggio = messaggio, Corpo = corpo,
                Dati = dati ?? new Dictionary<string, string>() };
    }
}
