using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
