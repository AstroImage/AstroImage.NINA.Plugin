using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AstroImage.NINA.Plugin.Models;

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
        public const int VersioneCorrente = 1;

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
                nota = "La configurazione dei filtri non si e' potuta leggere (" + e.Message +
                       "). Riparto da vuoto: nessun filtro e' stato inventato, e la " +
                       "configurazione salvata non e' stata cancellata.";
                return new RuotaVirtuale();
            }
            if (r is null) {
                nota = "La configurazione dei filtri e' vuota o malformata. Riparto da vuoto.";
                return new RuotaVirtuale();
            }

            /*  Una versione che non conosciamo non si indovina. Meglio nessuna mappa che
             *  una mappa letta con la grammatica sbagliata: l'utente riconfigura in un
             *  minuto, una notte ripresa col vetro sbagliato non si recupera. */
            if (r.Versione > VersioneCorrente) {
                nota = $"La configurazione dei filtri e' in versione {r.Versione}, e questo " +
                       $"ponte ne conosce fino alla {VersioneCorrente}. Non la interpreto: " +
                       "aggiorna il plugin, oppure riconfigura i filtri qui.";
                return new RuotaVirtuale();
            }

            var pulite = new List<VoceRuota>();
            var viste = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var doppie = new List<string>();

            foreach (var v in r.Vetri ?? new List<VoceRuota>()) {
                if (v is null || string.IsNullOrWhiteSpace(v.Nina)) continue;
                var nome = v.Nina!.Trim();
                /*  Due voci per lo stesso nome non sono un dettaglio: il nome e' la
                 *  chiave, e due valori diversi per la stessa chiave vorrebbero dire
                 *  scegliere a caso. Vince la prima, e si dice che e' successo. */
                if (!viste.Add(nome)) { doppie.Add(nome); continue; }
                pulite.Add(new VoceRuota {
                    Nina = nome,
                    Motore = string.IsNullOrWhiteSpace(v.Motore) ? null : v.Motore!.Trim(),
                    Nota = string.IsNullOrWhiteSpace(v.Nota) ? null : v.Nota!.Trim(),
                });
            }

            if (doppie.Count > 0)
                nota = "Nella configurazione c'erano piu' voci per lo stesso filtro (" +
                       string.Join(", ", doppie.Distinct()) + "). Ho tenuto la prima di ognuna.";

            return new RuotaVirtuale { Versione = VersioneCorrente, Vetri = pulite };
        }

        public static string Scrivi(RuotaVirtuale? r) =>
            JsonSerializer.Serialize(r ?? new RuotaVirtuale(), Opzioni);

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
                perCheNo = "La richiesta non dichiara nessun elenco di vetri. Non la interpreto " +
                           "come «togli tutto»: la configurazione precedente resta dov'e'.";
                return null;
            }
            if (vetri is not JsonArray elenco) {
                perCheNo = "L'elenco dei vetri non e' un elenco.";
                return null;
            }

            var r = new RuotaVirtuale { Versione = VersioneCorrente };
            foreach (var v in elenco) {
                var nina = Testo(v?["nina"]);
                if (string.IsNullOrWhiteSpace(nina)) continue;
                r.Vetri.Add(new VoceRuota {
                    Nina = nina!.Trim(),
                    Motore = Testo(v?["id"]),
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
            : r.Vetri.FirstOrDefault(v => string.Equals(v.Nina, nomeNina, StringComparison.OrdinalIgnoreCase))?.Motore;

        /// <summary>Il giro all'indietro: il motore ha scelto QUESTO vetro, come si
        /// chiama sulla tua ruota? Null se non e' dichiarato da nessuna parte.</summary>
        public static string? NomePerId(RuotaVirtuale? r, string? idMotore) =>
            r is null || string.IsNullOrWhiteSpace(idMotore) ? null
            : r.Vetri.FirstOrDefault(v => string.Equals(v.Motore, idMotore, StringComparison.OrdinalIgnoreCase))?.Nina;

        /// <summary>
        /// Gli identificativi da mandare al motore come `ruota`. Solo quelli dichiarati,
        /// senza ripetizioni. Vuoto quando non hai dichiarato niente — e allora la
        /// richiesta non deve portare `ruota` affatto, perche' una ruota vuota e una
        /// ruota non dichiarata sono due cose diverse: la prima direbbe al motore che
        /// non possiedi nessun filtro.
        /// </summary>
        public static IReadOnlyList<string> IdDichiarati(RuotaVirtuale? r) =>
            r is null ? new List<string>()
            : r.Vetri.Where(v => !string.IsNullOrWhiteSpace(v.Motore))
                     .Select(v => v.Motore!)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .ToList();

        /// <summary>Come mostrare un vetro del catalogo: il suo nome, che per quasi tutti
        /// porta gia' dentro il costruttore. Sta qui e non sul modello per la stessa
        /// ragione di tutto il resto.</summary>
        public static string Etichetta(VetroDelMotore? v) =>
            v is null ? "?" : (string.IsNullOrWhiteSpace(v.Nome) ? (v.Id ?? "?") : v.Nome!);
    }
}
