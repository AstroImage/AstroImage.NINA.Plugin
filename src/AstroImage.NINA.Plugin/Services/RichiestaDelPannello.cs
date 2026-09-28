using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using AstroImage.NINA.Plugin.Localization;
using AstroImage.NINA.Plugin.Models;

#nullable enable

namespace AstroImage.NINA.Plugin.Services {

    /*  LA DOMANDA CHE PARTE DAL PANNELLO, composta fuori dalla vista.
     *
     *  Il corriere non tocca cio' che trasporta, con un'eccezione dichiarata: `ruota`, che la pagina non puo'
     *  conoscere perche' vive nel profilo di N.I.N.A. e nelle impostazioni del plugin. Qui si decide che cosa
     *  parte al suo posto. Sta qui e non nella vista perche' si prova senza N.I.N.A. e senza una pagina.
     */
    public static class RichiestaDelPannello {

        public sealed class Esito {
            /// <summary>Il corpo da mandare al servizio. Null quando la domanda non parte.</summary>
            public string? Corpo { get; set; }
            /// <summary>Gli identificativi che il Ponte ha aggiunto come `ruota`. Null quando non ha aggiunto
            /// niente: il client l'aveva gia' dichiarata, oppure non c'e' niente di dichiarato.</summary>
            public List<string>? RuotaAggiunta { get; set; }
            /// <summary>Le voci dichiarate che nella ruota di N.I.N.A. adesso non ci sono.</summary>
            public List<VoceRuota> Orfane { get; } = new List<VoceRuota>();
            /// <summary>Perche' la domanda non parte, nella lingua di chi guarda. Null quando parte.</summary>
            public string? Rifiuto { get; set; }
            /// <summary>Il codice del rifiuto: `ruota_solo_orfane`, o `camera_non_nota` quando per scegliere i vetri serve
            /// la camera e non si sa. Null quando parte.</summary>
            public string? Codice { get; set; }
            /// <summary>La camera della dichiarazione usata, `mono` o `colore`; null quando le due camere dicono gli stessi
            /// vetri, o quando la ruota l'ha mandata la pagina.</summary>
            public string? Camera { get; set; }
        }

        /// <param name="corpo">Il corpo come la pagina l'ha scritto.</param>
        /// <param name="dichiarazione">La ruota dichiarata del profilo attivo.</param>
        /// <param name="nomiInRuota">I nomi dei filtri nella ruota del profilo attivo, adesso.</param>
        public static Esito Componi(JsonObject? corpo, RuotaVirtuale? dichiarazione, IEnumerable<string>? nomiInRuota) =>
            Componi(corpo, dichiarazione, nomiInRuota, null);

        /// <param name="camera">La camera del banco, `mono` o `colore` (<see cref="DichiarazioneRuota.Camera"/>), o null
        /// se non si sa.</param>
        /// <remarks>LE DUE CAMERE (regia, 28 settembre 2026): parte la dichiarazione della camera del banco. Senza la camera,
        /// parte la dichiarazione se le due camere dicono gli stessi vetri; se no la domanda non parte, e si dice perche':
        /// scegliere a caso vorrebbe dire calcolare sui vetri dell'altra camera, il difetto dell'RGB puro su una mono.</remarks>
        public static Esito Componi(JsonObject? corpo, RuotaVirtuale? dichiarazione, IEnumerable<string>? nomiInRuota, string? camera) {
            var e = new Esito();
            var c = corpo ?? new JsonObject();
            if (c["ruota"] is null) {
                if (camera is null && !DichiarazioneRuota.UgualePerLeDueCamere(dichiarazione)) {
                    e.Codice = "camera_non_nota";
                    e.Rifiuto = Loc.T("Ruota_CameraNonNota");
                    return e;
                }
                e.Camera = camera;
                var vista = DichiarazioneRuota.PerCamera(dichiarazione, camera ?? DichiarazioneRuota.CameraMono);
                var ids = DichiarazioneRuota.PerLaRichiesta(vista, nomiInRuota, out var orfane);
                e.Orfane.AddRange(orfane);
                /*  SOLO ORFANE, E LA DOMANDA NON PARTE. Mandare una ruota vuota vorrebbe dire i filtri di serie del
                 *  motore — il servizio non distingue vuota da non dichiarata —, e non mandarla sarebbe lo stesso:
                 *  una prescrizione calcolata su filtri che non sono i tuoi, dopo che li avevi dichiarati. */
                if (ids.Count == 0 && orfane.Count > 0) {
                    e.Codice = "ruota_solo_orfane";
                    e.Rifiuto = Loc.F("Ruota_SoloOrfane",
                        string.Join(", ", orfane.Select(o => "«" + o.Nina + "» (" + o.Motore + ")")));
                    return e;
                }
                if (ids.Count > 0) {
                    e.RuotaAggiunta = ids.ToList();
                    var a = new JsonArray();
                    foreach (var id in ids) a.Add(id);
                    c["ruota"] = a;
                }
            }
            e.Corpo = c.ToJsonString();
            return e;
        }
    }
}
