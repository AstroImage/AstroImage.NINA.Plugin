using System;
using System.Collections.Generic;
using System.Linq;
using AstroImage.NINA.Plugin.Models;

#nullable enable

namespace AstroImage.NINA.Plugin.Services {

    /*  METTERE INSIEME LE TRE COSE, e far vedere dove non combaciano.
     *
     *  Ci sono tre elenchi, e nessuno dei tre e' padrone degli altri:
     *
     *      la ruota di N.I.N.A.     nomi e slot — stato operativo, cambia quando vuoi
     *      la dichiarazione         nome → identificativo — la scrivi tu, una volta
     *      il catalogo del motore   gli identificativi che esistono davvero
     *
     *  Combaciare non e' garantito, e i modi di non combaciare sono informazioni, non
     *  errori: un filtro rinominato lascia una voce orfana, un vetro non ancora
     *  dichiarato lascia una riga vuota, un catalogo aggiornato puo' far sparire un
     *  identificativo. Questo pezzo non aggiusta niente — mostra.
     */
    public static class RiconciliaRuota {

        public enum Stato {
            /// <summary>Dichiarato, e l'identificativo esiste nel catalogo del motore.</summary>
            Mappato,
            /// <summary>In ruota, ma non hai ancora detto che cos'e'. Il motore non sa di averlo.</summary>
            NonMappato,
            /// <summary>Dichiarato, ma quell'identificativo il motore non lo conosce (piu').</summary>
            Ignoto,
            /// <summary>Dichiarato una volta, ma quel nome in ruota non c'e' piu': rinominato o tolto.</summary>
            Orfano,
        }

        public sealed class Riga {
            public string Nina { get; set; } = string.Empty;
            /// <summary>Lo slot letto ADESSO da N.I.N.A. Null per una voce orfana, che
            /// in ruota non c'e' piu'. Non e' identita': si mostra per farsi riconoscere.</summary>
            public int? Slot { get; set; }
            public string? IdMotore { get; set; }
            public VetroDelMotore? Vetro { get; set; }
            public string? Nota { get; set; }
            public Stato Stato { get; set; }
            /// <summary>Se questo vetro ha senso sulla camera in uso. Null quando non si
            /// sa — o perche' non si conosce la camera, o perche' il catalogo non lo
            /// dichiara. Null NON vuol dire «no».</summary>
            public bool? AdattoAllaCamera { get; set; }
            /// <summary>Lo stesso nome compare piu' volte nella ruota di N.I.N.A.
            /// SwitchFilter risolve per nome e prende il primo: e' ambiguita' loro, ma
            /// va detta.</summary>
            public bool NomeAmbiguo { get; set; }
        }

        /// <param name="inRuota">Nomi e slot come N.I.N.A. li da' adesso.</param>
        /// <param name="cameraAMatrice">
        /// Vero per una camera a colori, falso per una monocromatica, null se non si sa.
        /// Serve solo a segnalare: non nasconde niente e non impedisce di dichiarare.
        /// </param>
        public static IReadOnlyList<Riga> Righe(
                IEnumerable<(string Nome, int? Slot)>? inRuota,
                RuotaVirtuale? dichiarazione,
                CatalogoDelMotore? catalogo,
                bool? cameraAMatrice) {

            var ruota = (inRuota ?? Enumerable.Empty<(string, int?)>())
                .Where(x => !string.IsNullOrWhiteSpace(x.Nome)).ToList();
            var dich = dichiarazione ?? new RuotaVirtuale();
            var vetri = catalogo?.Filtri?.Where(v => !string.IsNullOrWhiteSpace(v.Id)).ToList()
                        ?? new List<VetroDelMotore>();

            var perId = new Dictionary<string, VetroDelMotore>(StringComparer.OrdinalIgnoreCase);
            foreach (var v in vetri) perId[v.Id!] = v;

            var quante = ruota.GroupBy(x => x.Nome, StringComparer.OrdinalIgnoreCase)
                              .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            var righe = new List<Riga>();

            foreach (var (nome, slot) in ruota) {
                var voce = dich.Vetri.FirstOrDefault(v =>
                    string.Equals(v.Nina, nome, StringComparison.OrdinalIgnoreCase));
                var id = voce?.Motore;
                VetroDelMotore? vetro = null;
                if (!string.IsNullOrWhiteSpace(id)) perId.TryGetValue(id!, out vetro);

                righe.Add(new Riga {
                    Nina = nome,
                    Slot = slot,
                    IdMotore = id,
                    Vetro = vetro,
                    Nota = voce?.Nota,
                    Stato = string.IsNullOrWhiteSpace(id) ? Stato.NonMappato
                          : vetro is null ? Stato.Ignoto : Stato.Mappato,
                    AdattoAllaCamera = Adatto(vetro, cameraAMatrice),
                    NomeAmbiguo = quante.TryGetValue(nome, out var n) && n > 1,
                });
            }

            /*  LE ORFANE NON SI CANCELLANO. Un nome sparito dalla ruota quasi sempre e'
             *  stato rinominato, non buttato: tenerla permette di ripuntarla in un clic
             *  invece di ricominciare. Cancellarla in silenzio sarebbe perdere lavoro
             *  dell'utente per fare ordine. */
            var nomiInRuota = new HashSet<string>(ruota.Select(x => x.Nome), StringComparer.OrdinalIgnoreCase);
            foreach (var v in dich.Vetri) {
                if (string.IsNullOrWhiteSpace(v.Nina) || nomiInRuota.Contains(v.Nina!)) continue;
                VetroDelMotore? vetro = null;
                if (!string.IsNullOrWhiteSpace(v.Motore)) perId.TryGetValue(v.Motore!, out vetro);
                righe.Add(new Riga {
                    Nina = v.Nina!, Slot = null, IdMotore = v.Motore, Vetro = vetro,
                    Nota = v.Nota, Stato = Stato.Orfano,
                    AdattoAllaCamera = Adatto(vetro, cameraAMatrice),
                });
            }

            return righe;
        }

        /*  Tre stati anche qui, e per la stessa ragione di la': nel catalogo del motore
         *  `per_mono` e `per_cfa` sono dichiarati veri o non dichiarati affatto — un
         *  `false` non esiste. Quindi «non dichiarato» resta «non si sa», e chi guarda
         *  non si vede sconsigliare un vetro sulla base di un dato che nessuno ha dato. */
        private static bool? Adatto(VetroDelMotore? v, bool? cameraAMatrice) {
            if (v is null || cameraAMatrice is null) return null;
            var b = cameraAMatrice == true ? v.PerCfa : v.PerMono;
            return b == true ? true : (bool?)null;
        }
    }
}
