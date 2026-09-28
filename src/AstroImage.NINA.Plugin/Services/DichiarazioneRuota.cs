using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AstroImage.NINA.Plugin.Models;
using AstroImage.NINA.Plugin.Localization;

#nullable enable

namespace AstroImage.NINA.Plugin.Services {

    /*  LEGGERE, SCRIVERE E CERCARE dentro la dichiarazione dei vetri.
     *
     *  Sta qui e non nel modello perche' in questo progetto un modello e' dato muto —
     *  niente metodi, niente proprieta' calcolate — e c'e' un test che lo verifica per
     *  riflessione. La regola non e' formale: un modello che sa fare qualcosa e' un
     *  modello dentro cui, prima o poi, qualcuno mette una decisione.
     */
    public static class DichiarazioneRuota {

        /// <summary>La forma che questo codice sa scrivere.</summary>
        public const int VersioneCorrente = 2;

        /*  LE DUE CAMERE (regia, 28 settembre 2026): sullo stesso nome della ruota di N.I.N.A. il vetro dichiarato con la
         *  camera monocromatica e quello con la camera a colori. Le parole sono quelle con cui il servizio dice la matrice
         *  della camera riconosciuta (`dati.matrice`), cosi' la pagina le passa com'e'. */
        public const string CameraMono = "mono";
        public const string CameraColore = "colore";

        /// <summary>La camera come parola della dichiarazione: quella detta, se e' una delle due; altrimenti quella che
        /// il profilo di N.I.N.A. dichiara con lo schema di Bayer; altrimenti null, che vuol dire «non si sa».</summary>
        public static string? Camera(string? detta, bool? cameraAMatrice) {
            var d = (detta ?? "").Trim().ToLowerInvariant();
            if (d == CameraMono || d == CameraColore) return d;
            return cameraAMatrice is null ? null : cameraAMatrice == true ? CameraColore : CameraMono;
        }

        /// <summary>
        /// LA VISTA PER UNA CAMERA: la dichiarazione con un vetro per nome, quello di <paramref name="camera"/>, in
        /// <c>Motore</c>. Tutto cio' che lavora su un vetro per nome — la domanda, la consegna, la riconciliazione — lavora
        /// su questa. Una camera che non e' una delle due da' la vista vuota: nessun vetro, invece di uno indovinato.
        /// </summary>
        public static RuotaVirtuale PerCamera(RuotaVirtuale? r, string? camera) {
            var vista = new RuotaVirtuale { Versione = VersioneCorrente };
            if (r is null || (camera != CameraMono && camera != CameraColore)) return vista;
            foreach (var v in r.Vetri) {
                if (v is null || string.IsNullOrWhiteSpace(v.Nina)) continue;
                var id = camera == CameraMono ? (v.Mono ?? v.Motore) : (v.Colore ?? v.Motore);
                vista.Vetri.Add(new VoceRuota { Nina = v.Nina, Motore = id, Nota = v.Nota });
            }
            return vista;
        }

        /// <summary>Se con le due camere ogni nome ha lo stesso vetro: allora la camera non serve a scegliere.</summary>
        public static bool UgualePerLeDueCamere(RuotaVirtuale? r) =>
            r is null || r.Vetri.All(v => v is null || string.Equals(v.Mono ?? v.Motore, v.Colore ?? v.Motore, StringComparison.OrdinalIgnoreCase));

        private static readonly JsonSerializerOptions Opzioni = new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        /*  NON SOLLEVA MAI, e non e' pigrizia. Una configurazione illeggibile non deve
         *  impedirti di aprire il pannello: deve farti dire che c'e' e che non si legge.
         *  Il caso peggiore che questo metodo produce e' una ruota vuota con un motivo
         *  scritto — mai un mapping inventato, mai un'eccezione in faccia a N.I.N.A. */
        public static RuotaVirtuale Leggi(string? json, out string? nota) {
            nota = null;
            if (string.IsNullOrWhiteSpace(json)) return new RuotaVirtuale();

            RuotaVirtuale? r;
            try { r = JsonSerializer.Deserialize<RuotaVirtuale>(json!, Opzioni); }
            catch (Exception e) {
                nota = Loc.F("Ruota_ConfigNonLetta", e.Message);
                return new RuotaVirtuale();
            }
            if (r is null) {
                nota = Loc.T("Ruota_ConfigVuota");
                return new RuotaVirtuale();
            }

            /*  Una versione che non conosciamo non si indovina. Meglio nessuna mappa che
             *  una mappa letta con la grammatica sbagliata: l'utente riconfigura in un
             *  minuto, una notte ripresa col vetro sbagliato non si recupera. */
            if (r.Versione > VersioneCorrente) {
                nota = Loc.F("Ruota_VersioneAvanti", r.Versione, VersioneCorrente);
                return new RuotaVirtuale();
            }

            var pulite = new List<VoceRuota>();
            var viste = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var doppie = new List<string>();
            var diPrima = false;
            string? Pulito(string? s) => string.IsNullOrWhiteSpace(s) ? null : s!.Trim();

            foreach (var v in r.Vetri ?? new List<VoceRuota>()) {
                if (v is null || string.IsNullOrWhiteSpace(v.Nina)) continue;
                var nome = v.Nina!.Trim();
                /*  Due voci per lo stesso nome non sono un dettaglio: il nome e' la
                 *  chiave, e due valori diversi per la stessa chiave vorrebbero dire
                 *  scegliere a caso. Vince la prima, e si dice che e' successo. */
                if (!viste.Add(nome)) { doppie.Add(nome); continue; }
                /*  LA FORMA DI PRIMA (versione 1): un vetro per nome, dichiarato quando le camere non si distinguevano. Vale
                 *  per tutt'e due finche' chi riprende non le separa — la prescrizione resta quella di ieri —, e si dice. */
                var mono = Pulito(v.Mono);
                var colore = Pulito(v.Colore);
                var unico = Pulito(v.Motore);
                if (mono is null && colore is null && unico != null) { mono = unico; colore = unico; diPrima = true; }
                pulite.Add(new VoceRuota { Nina = nome, Mono = mono, Colore = colore, Nota = Pulito(v.Nota) });
            }

            var note = new List<string>();
            if (doppie.Count > 0) note.Add(Loc.F("Ruota_VociDoppie", string.Join(", ", doppie.Distinct())));
            if (diPrima) note.Add(Loc.T("Ruota_DichiarazioneDiPrima"));
            if (note.Count > 0) nota = string.Join(" ", note);

            return new RuotaVirtuale { Versione = VersioneCorrente, Vetri = pulite };
        }

        /*  SI SCRIVE SEMPRE LA FORMA DELLE DUE CAMERE: una voce con un vetro solo — la forma di prima, o una vista per una
         *  camera — vale per tutt'e due, e si scrive cosi'. Rileggendola non sembra una dichiarazione di prima. */
        public static string Scrivi(RuotaVirtuale? r) =>
            JsonSerializer.Serialize(new RuotaVirtuale {
                Versione = VersioneCorrente,
                Vetri = (r?.Vetri ?? new List<VoceRuota>()).Where(v => v != null).Select(v => new VoceRuota {
                    Nina = v.Nina,
                    Mono = v.Mono ?? (v.Colore is null ? v.Motore : null),
                    Colore = v.Colore ?? (v.Mono is null ? v.Motore : null),
                    Nota = v.Nota,
                }).ToList(),
                Altro = r?.Altro,
            }, Opzioni);

        /*  LA DICHIARAZIONE COM'E' ARRIVATA DALLA PAGINA, e la guardia che impedisce a
         *  una chiave mancante di cancellare il lavoro di qualcuno.
         *
         *  Questo metodo esiste per un difetto vero, trovato al primo uso sul campo. La
         *  pagina spedisce con `chiedi(azione, corpo)`, che impacchetta il carico sotto
         *  `corpo`; chi leggeva cercava `vetri` alla radice, non lo trovava, e costruiva
         *  una dichiarazione VUOTA. Poi la salvava, e il salvataggio RIUSCIVA: il
         *  pannello rispondeva «fatto» e ridisegnava fedelmente il nulla che aveva
         *  appena scritto. Quattro vetri appena dichiarati, spariti, con un esito verde.
         *
         *  Da qui due regole, non una:
         *
         *  1. Il percorso si legge dove la pagina scrive davvero. Sta scritto qui, in un
         *     posto solo, e si prova.
         *
         *  2. `vetri` ASSENTE non e' «dichiara niente»: e' una richiesta malformata, e
         *     si rifiuta. Un elenco vuoto invece e' una richiesta legittima — «togli
         *     tutto» — e si esegue. Sono due cose diverse, e confonderle e' precisamente
         *     il modo in cui si cancella la configurazione di qualcuno senza dirglielo.
         */
        public static RuotaVirtuale? DalMessaggio(JsonNode? messaggio, out string? perCheNo) {
            perCheNo = null;
            var carico = messaggio?["corpo"];
            var vetri = carico?["vetri"];

            if (vetri is null) {
                perCheNo = Loc.T("Ruota_SenzaElenco");
                return null;
            }
            if (vetri is not JsonArray elenco) {
                perCheNo = Loc.T("Ruota_ElencoNonElenco");
                return null;
            }

            var r = new RuotaVirtuale { Versione = VersioneCorrente };
            foreach (var v in elenco) {
                var nina = Testo(v?["nina"]);
                if (string.IsNullOrWhiteSpace(nina)) continue;
                /*  la pagina manda il vetro con la mono e quello con la colore; un `id` solo — la forma di prima — vale per
                 *  tutt'e due, come quando si legge una dichiarazione della versione 1 */
                var unico = Testo(v?["id"]);
                var conMono = v is JsonObject o1 && o1.ContainsKey("mono");
                var conColore = v is JsonObject o2 && o2.ContainsKey("colore");
                r.Vetri.Add(new VoceRuota {
                    Nina = nina!.Trim(),
                    Mono = conMono ? Testo(v?["mono"]) : unico,
                    Colore = conColore ? Testo(v?["colore"]) : unico,
                    Nota = Testo(v?["nota"]),
                });
            }
            return r;
        }

        /*  Un valore JSON puo' essere assente, nullo, o di un tipo che non ci
         *  aspettiamo. Tutti e tre vogliono dire la stessa cosa qui — «non dichiarato» —
         *  e nessuno dei tre deve sollevare mentre qualcuno sta salvando. */
        private static string? Testo(JsonNode? n) {
            if (n is null) return null;
            try {
                var s = n.GetValue<string>();
                return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
            } catch (Exception) { return null; }
        }

        /// <summary>Che identificativo del motore e' stato dichiarato per questo nome di
        /// N.I.N.A. Null se non e' stato dichiarato — e null non e' un invito a indovinare.</summary>
        public static string? IdPerNome(RuotaVirtuale? r, string? nomeNina) =>
            r is null || string.IsNullOrWhiteSpace(nomeNina) ? null
            : Unico(r.Vetri.FirstOrDefault(v => string.Equals(v.Nina, nomeNina, StringComparison.OrdinalIgnoreCase)));

        /// <summary>Il giro all'indietro: il motore ha scelto QUESTO vetro, come si
        /// chiama sulla tua ruota? Null se non e' dichiarato da nessuna parte.</summary>
        public static string? NomePerId(RuotaVirtuale? r, string? idMotore) =>
            r is null || string.IsNullOrWhiteSpace(idMotore) ? null
            : r.Vetri.FirstOrDefault(v => string.Equals(Unico(v), idMotore, StringComparison.OrdinalIgnoreCase))?.Nina;

        /*  IL VETRO DI UN NOME, per le funzioni che ne vogliono uno: quello della vista per una camera; su una dichiarazione
         *  salvata, quello che vale per tutt'e due le camere, e null dove le due camere dicono vetri diversi — li' serve la
         *  camera, e senza non si sceglie. */
        private static string? Unico(VoceRuota? v) =>
            v is null ? null
            : !string.IsNullOrWhiteSpace(v.Motore) ? v.Motore
            : string.Equals(v.Mono, v.Colore, StringComparison.OrdinalIgnoreCase) ? v.Mono : null;

        /// <summary>
        /// Gli identificativi da mandare al motore come `ruota`. Solo quelli dichiarati,
        /// senza ripetizioni. Vuoto quando non hai dichiarato niente — e allora la
        /// richiesta non deve portare `ruota` affatto, perche' una ruota vuota e una
        /// ruota non dichiarata sono due cose diverse: la prima direbbe al motore che
        /// non possiedi nessun filtro.
        /// </summary>
        /// <remarks>Su una dichiarazione salvata, con le due camere, conta i vetri dell'una e dell'altra.</remarks>
        public static IReadOnlyList<string> IdDichiarati(RuotaVirtuale? r) =>
            r is null ? new List<string>()
            : r.Vetri.SelectMany(v => new[] { v.Motore, v.Mono, v.Colore })
                     .Where(id => !string.IsNullOrWhiteSpace(id))
                     .Select(id => id!)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .ToList();

        /// <summary>
        /// Gli identificativi che partono come `ruota` quando la domanda parte dal pannello, e le voci
        /// dichiarate che nella ruota di N.I.N.A. adesso non ci sono.
        /// </summary>
        /// <remarks>
        /// LE ORFANE NON RENDONO PERCORRIBILE UNA STRADA (regia, 16 settembre 2026). Parte l'identificativo di ogni
        /// voce il cui nome e' nella ruota adesso; una voce il cui nome non c'e' piu' e' orfana, e non parte — a meno
        /// che lo stesso identificativo sia dichiarato anche sotto un nome che c'e'. Una voce senza identificativo
        /// non e' mai entrata, quindi non e' un'orfana. Il confronto dei nomi e' quello di `SequenceBuilder`: senza
        /// maiuscole e senza spazi ai bordi.
        /// </remarks>
        public static IReadOnlyList<string> PerLaRichiesta(RuotaVirtuale? r, IEnumerable<string>? nomiInRuota,
                                                           out List<VoceRuota> orfane) {
            var nomi = new HashSet<string>(
                (nomiInRuota ?? Enumerable.Empty<string>()).Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()),
                StringComparer.OrdinalIgnoreCase);
            var dichiarate = r?.Vetri.Where(v => !string.IsNullOrWhiteSpace(Unico(v)) && !string.IsNullOrWhiteSpace(v.Nina))
                                      .ToList() ?? new List<VoceRuota>();
            orfane = dichiarate.Where(v => !nomi.Contains(v.Nina!.Trim())).ToList();
            return dichiarate.Where(v => nomi.Contains(v.Nina!.Trim()))
                             .Select(v => Unico(v)!)
                             .Distinct(StringComparer.OrdinalIgnoreCase)
                             .ToList();
        }

        /// <summary>Come mostrare un vetro del catalogo: il suo nome, che per quasi tutti
        /// porta gia' dentro il costruttore. Sta qui e non sul modello per la stessa
        /// ragione di tutto il resto.</summary>
        public static string Etichetta(VetroDelMotore? v) =>
            v is null ? "?" : (string.IsNullOrWhiteSpace(v.Nome) ? (v.Id ?? "?") : v.Nome!);
    }
}
