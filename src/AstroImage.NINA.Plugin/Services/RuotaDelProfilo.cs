using System;
using System.Collections.Generic;
using System.Linq;
using NINA.Profile.Interfaces;
using AstroImage.NINA.Plugin.Localization;

#nullable enable

namespace AstroImage.NINA.Plugin.Services {

    /*  LA RUOTA COM'E' ADESSO, letta dal profilo attivo.
     *
     *  L'ELENCO VIENE DAL PROFILO, NON DAL DISPOSITIVO, ed e' la distinzione che conta:
     *  il driver sa soltanto quale slot e' montato in questo momento, mentre l'elenco
     *  dei vetri che possiedi esiste anche a ruota scollegata e a telescopio spento.
     *  Configurare i filtri di pomeriggio, con niente acceso, dev'essere possibile.
     *
     *  SI CHIEDE OGNI VOLTA. I profili si cambiano mentre N.I.N.A. e' aperto, e con
     *  loro cambia la ruota: una fotografia scattata all'avvio sarebbe falsa prima o
     *  poi. E' lo stesso motivo per cui FonteDaModello non cattura i modelli una volta
     *  per tutte, ed e' un difetto che in questo progetto abbiamo gia' pagato.
     *
     *  Vuoto e nullo restano due cose diverse: vuoto vuol dire ruota configurata senza
     *  filtri — che e' normale, non un guasto — e nullo vuol dire che non si e' potuto
     *  nemmeno chiedere.
     */
    public sealed class RuotaDelProfilo {

        private readonly IProfileService? profilo;

        public RuotaDelProfilo(IProfileService? profilo) => this.profilo = profilo;

        /// <summary>Nome e slot di ogni vetro dichiarato in ruota, nell'ordine del profilo.
        /// Lista vuota se non c'e' niente o non si e' potuto leggere: <see cref="PerCheNo"/>
        /// dice quale dei due.</summary>
        public IReadOnlyList<(string Nome, int? Slot)> Vetri(out string? perCheNo) {
            perCheNo = null;
            try {
                var cfg = profilo?.ActiveProfile?.FilterWheelSettings;
                if (cfg is null) {
                    perCheNo = Loc.T("Ruota_ProfiloSenzaRuota");
                    return new List<(string, int?)>();
                }
                var filtri = cfg.FilterWheelFilters;
                if (filtri is null) {
                    perCheNo = Loc.T("Ruota_ElencoNonRiuscito");
                    return new List<(string, int?)>();
                }
                var fuori = filtri.Where(f => f is not null && !string.IsNullOrWhiteSpace(f.Name))
                                  .Select(f => (Nome: f.Name.Trim(), Slot: (int?)f.Position))
                                  .ToList();
                if (fuori.Count == 0)
                    perCheNo = Loc.T("Ruota_ProfiloSenzaFiltri");
                return fuori;
            } catch (Exception e) {
                perCheNo = Loc.F("Ruota_LetturaFallita", e.Message);
                return new List<(string, int?)>();
            }
        }

        /// <summary>Solo i nomi, per chi non ha bisogno degli slot.</summary>
        public IReadOnlyList<string> Nomi() => Vetri(out _).Select(x => x.Nome).ToList();

        /// <summary>
        /// Se la camera del profilo attivo e' a matrice (colore) oppure monocromatica.
        /// Null quando non si sa, ed e' un caso normale: il profilo non lo dichiara
        /// sempre, e null non deve mai diventare «no». Serve solo a segnalare in
        /// pagina quali vetri hanno senso: non nasconde niente e non impedisce nulla.
        /// </summary>
        public bool? CameraAMatrice() {
            try {
                var c = profilo?.ActiveProfile?.CameraSettings;
                if (c is null) return null;

                /*  `BayerPatternEnum` non ammette il nullo, quindi il «non impostato»
                 *  si legge nel valore: `Auto` vuol dire «lo dica la camera», ed e' il
                 *  valore di partenza — cioe' quasi sempre il profilo non lo sa. In quel
                 *  caso si torna null, che e' la verita', invece di scommettere.
                 *
                 *  `None` e' una dichiarazione esplicita di monocromatica; qualunque
                 *  schema di Bayer e' una dichiarazione esplicita di camera a colori.
                 *
                 *  Non si guarda il NOME della camera. Sarebbe la stessa scommessa sui
                 *  nomi che questa intera funzionalita' esiste per togliere. */
                var t = c.BayerPattern.ToString();
                if (string.Equals(t, "Auto", StringComparison.OrdinalIgnoreCase)) return null;
                return !string.Equals(t, "None", StringComparison.OrdinalIgnoreCase);
            } catch (Exception) { return null; }
        }
    }
}
