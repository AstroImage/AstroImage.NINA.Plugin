using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

#nullable enable

namespace AstroImage.NINA.Plugin.Models {

    /*  I VETRI CHE IL MOTORE CONOSCE — l'elenco fra cui l'utente dichiara.
     *
     *  Arriva da `GET /v1/filtri`. Non e' la fisica del filtro: e' quel poco che
     *  serve a SCEGLIERNE uno in un elenco. La fisica — finestre, trasmissione,
     *  blocking, provenienza — resta di la', dove qualcuno ci calcola sopra.
     *
     *  Il ponte non ne fa niente di scientifico: mostra e confronta identificativi.
     */
    public sealed class CatalogoDelMotore {

        [JsonPropertyName("contratto")] public string? Contratto { get; set; }

        [JsonPropertyName("filtri")] public List<VetroDelMotore>? Filtri { get; set; }

        /// <summary>
        /// Su quali vetri il motore calcola quando la richiesta non porta `ruota`.
        /// Serve a dirlo a chi guarda: finche' la ruota non e' configurata, la
        /// prescrizione e' stata calcolata su QUESTI, non sui tuoi.
        /// </summary>
        [JsonPropertyName("di_serie")] public List<string>? DiSerie { get; set; }

        [JsonExtensionData] public Dictionary<string, JsonElement>? Altro { get; set; }
    }

    /// <summary>
    /// Un vetro del catalogo del motore. <see cref="Id"/> e' l'identita': e' quello
    /// che viaggia nelle richieste e nelle risposte, mai il nome.
    /// </summary>
    public sealed class VetroDelMotore {

        [JsonPropertyName("id")] public string? Id { get; set; }

        /// <summary>Da mostrare. Per quasi tutti porta gia' dentro il costruttore —
        /// «Ha 7 nm (Lumicon / ZWO)», «IDAS LPS P2» — che infatti e' dichiarato a parte
        /// solo per quattro vetri su trentacinque.</summary>
        [JsonPropertyName("nome")] public string? Nome { get; set; }

        [JsonPropertyName("banda")] public string? Banda { get; set; }
        [JsonPropertyName("lambda_nm")] public double? LambdaNm { get; set; }

        /// <summary>La larghezza distingue dove il nome non basta: sei filtri Ha che
        /// differiscono solo per questo.</summary>
        [JsonPropertyName("fwhm_nm")] public double? FwhmNm { get; set; }

        /// <summary>Quali bande copre. Serve sui duali, dove `dual` da solo non basta:
        /// dieci fanno Ha+OIII e l'Askar D2 fa SII+OIII.</summary>
        [JsonPropertyName("bande")] public List<string>? Bande { get; set; }

        [JsonPropertyName("dual")] public bool Dual { get; set; }

        /*  TRE STATI E NON DUE. Nel catalogo del motore non esiste un solo `false`:
         *  o e' dichiarato vero, o non e' dichiarato affatto. `null` vuol dire «non
         *  si sa», e non va arrotondato a «no» — arrotondarlo nasconderebbe meta'
         *  catalogo a chi filtra per camera. */
        [JsonPropertyName("per_mono")] public bool? PerMono { get; set; }
        [JsonPropertyName("per_cfa")] public bool? PerCfa { get; set; }

        [JsonPropertyName("costruttore")] public string? Costruttore { get; set; }

        [JsonExtensionData] public Dictionary<string, JsonElement>? Altro { get; set; }

    }
}
