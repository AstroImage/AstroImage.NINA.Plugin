using System;
using System.Collections.Generic;
using System.Text.Json;

#nullable enable

namespace AstroImage.NINA.Plugin.Services {

    /*  QUALE VETRO IL MOTORE DICE DI AVER USATO, canale per canale.
     *
     *  Fino a ieri il nome del filtro tornava per una strada circolare: il ponte
     *  mandava `filterNames` — canale → nome della tua ruota — e il motore lo
     *  rimandava indietro. Funzionava, ma nascondeva una cosa: quel nome NON diceva
     *  quale vetro il motore avesse davvero messo nel conto. Diceva solo che cosa
     *  avevamo scritto noi accanto a quel canale.
     *
     *  Il giorno in cui il motore sceglie fra un L-eNhance e un L-Ultimate in base al
     *  cielo, i due valori divergono: la sequenza nominerebbe l'uno mentre le ore sono
     *  state calcolate sull'altro. E' la stessa forma di difetto del filtro mancante e
     *  del dither ereditato — qualcosa che SEMBRA la prescrizione e non lo e'.
     *
     *  La risposta vera sta in `prodotto.posa.<canale>.ex.spec.filter.id`: li' il
     *  motore dichiara il vetro su cui ha fatto il conto.
     *
     *  PERCHE' SI LEGGE DAL CORPO GREZZO E NON DAL DTO. `RispostaPrescrizione` lascia
     *  di proposito `posa` dentro JsonExtensionData, e la ragione e' scritta li':
     *  «cio' che non si sa leggere non si puo' reinterpretare». Tipizzarla vorrebbe
     *  dire mettere in mano al ponte tutta la superficie del motore, e prima o poi
     *  qualcuno ci prenderebbe una decisione scientifica. Qui invece si estrae UNA
     *  sola cosa, per un percorso dichiarato, e nient'altro: non e' interpretare la
     *  fisica, e' leggere un identificativo che qualcun altro ha scelto.
     *
     *  La casa definitiva di questo dato e' il modello di sequenza — un campo accanto
     *  a `filtro`, dalla parte del motore. Finche' non c'e', si legge di qua.
     */
    public static class VetriDellaPrescrizione {

        /*  «NESSUN FILTRO» NON E' UN FILTRO MANCANTE, ed e' una distinzione che il campo
         *  ci ha fatto pagare subito.
         *
         *  Il motore usa il sentinella `__none` per dire «davanti non ci va niente: il
         *  colore lo fa gia' la matrice di Bayer» — sta scritto nella sua stessa
         *  sorgente, «`__none` non e' un'assenza». Il ponte lo trattava come un
         *  identificativo di catalogo non dichiarato e rifiutava la consegna dicendo
         *  «il vetro __none non e' dichiarato nella tua ruota», che e' una frase senza
         *  senso: quel vetro non esiste, ed e' proprio il punto.
         *
         *  Resta comunque una cosa da NON indovinare. Se il motore dice «niente davanti»
         *  e la tua ruota ha cinque vetri, riprendere con quello che c'e' montato sarebbe
         *  la sostituzione silenziosa che questo progetto rifiuta da sempre. Quindi si
         *  dichiara: chi ha uno slot vuoto o un vetro trasparente lo mappa su questo
         *  identificativo, e allora la consegna sa quale slot chiedere. */
        public const string NessunFiltro = "__none";

        /// <summary>
        /// Canale → identificativo del vetro che il motore ha usato. Vuota se la
        /// risposta non lo dice: assente non e' un errore, e non autorizza a indovinare.
        /// Non solleva mai — una risposta storta non deve far cadere una consegna.
        /// </summary>
        public static IReadOnlyDictionary<string, string> PerCanale(string? corpo) {
            var fuori = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(corpo)) return fuori;

            try {
                using var doc = JsonDocument.Parse(corpo!);
                if (!doc.RootElement.TryGetProperty("prodotto", out var prodotto)) return fuori;
                if (!prodotto.TryGetProperty("posa", out var posa)) return fuori;
                if (posa.ValueKind != JsonValueKind.Object) return fuori;

                foreach (var canale in posa.EnumerateObject()) {
                    /*  Le chiavi che cominciano per «__» sono servizio del motore
                     *  (`__hdr` e simili), non canali. */
                    if (canale.Name.StartsWith("__", StringComparison.Ordinal)) continue;
                    if (canale.Value.ValueKind != JsonValueKind.Object) continue;
                    if (!canale.Value.TryGetProperty("ex", out var ex)) continue;
                    if (!ex.TryGetProperty("spec", out var spec)) continue;
                    if (!spec.TryGetProperty("filter", out var filtro)) continue;
                    if (!filtro.TryGetProperty("id", out var id)) continue;
                    if (id.ValueKind != JsonValueKind.String) continue;
                    var v = id.GetString();
                    if (!string.IsNullOrWhiteSpace(v)) fuori[canale.Name] = v!;
                }
            } catch (Exception) {
                /*  Un corpo illeggibile qui non e' una novita': il cliente l'ha gia'
                 *  letto una volta per arrivare fin qui. Se succede lo stesso, si torna
                 *  vuoti e chi chiama decide — che e' meglio di far cadere la consegna. */
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            return fuori;
        }

        /// <summary>
        /// Il vetro di un blocco, che puo' coprire piu' canali: su una camera a matrice
        /// il blocco «R+G+B» e' un'unica ripresa dietro un unico filtro, e infatti i tre
        /// canali rispondono lo stesso identificativo.
        /// <para/>
        /// Se i canali di uno stesso blocco dichiarassero vetri DIVERSI non ci sarebbe un
        /// vetro giusto da scegliere: si torna null con il motivo, e chi chiama rifiuta.
        /// Sceglierne uno a caso sarebbe esattamente il difetto che stiamo togliendo.
        /// </summary>
        public static string? DelBlocco(IEnumerable<string>? canali,
                                        IReadOnlyDictionary<string, string> perCanale,
                                        out string? perche) {
            perche = null;
            if (canali is null) return null;

            string? scelto = null;
            var discordi = new List<string>();
            var senza = new List<string>();

            foreach (var c in canali) {
                if (string.IsNullOrWhiteSpace(c)) continue;
                if (!perCanale.TryGetValue(c, out var id)) { senza.Add(c); continue; }
                if (scelto is null) scelto = id;
                else if (!string.Equals(scelto, id, StringComparison.OrdinalIgnoreCase)) discordi.Add(c);
            }

            if (discordi.Count > 0) {
                perche = "I canali di questo blocco dichiarano vetri diversi (" +
                         string.Join(", ", discordi) + "): non c'e' un filtro solo che li " +
                         "copra, e sceglierne uno a caso vorrebbe dire riprendere col vetro " +
                         "sbagliato senza dirlo.";
                return null;
            }
            if (scelto is null && senza.Count > 0)
                perche = "La risposta non dice quale vetro sia stato usato per " +
                         string.Join(", ", senza) + ".";
            return scelto;
        }
    }
}
