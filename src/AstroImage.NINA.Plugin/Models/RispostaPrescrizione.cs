using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

#nullable enable

namespace AstroImage.NINA.Plugin.Models {

    /*  LA BUSTA DEL SERVIZIO, e non un centimetro piu' di quanto serva.
     *
     *  Strategy risponde con tutta la superficie in un colpo: valutazione,
     *  prescrizione, posa, piano, e il modello di sequenza per ogni notte. Di tutto
     *  questo il ponte capisce UNA cosa sola — le sequenze — perche' e' l'unica che
     *  deve consegnare al Sequenziatore Avanzato. Il resto serve all'interfaccia, e
     *  il ponte lo trasporta senza guardarci dentro.
     *
     *  Per questo `valutazione`, `prescrizione`, `posa` e `piano` non hanno una
     *  classe qui: finiscono in JsonExtensionData e riescono come sono entrati. Non
     *  e' pigrizia, e' il confine. Se avessero un tipo, ogni volta che il motore
     *  aggiunge un campo il ponte andrebbe ricompilato — e prima o poi qualcuno,
     *  avendo il tipo sotto mano, ci leggerebbe dentro per prendere una decisione.
     *  Cio' che non si sa leggere non si puo' reinterpretare.
     */

    /// <summary>
    /// Quello che torna da <c>POST /v1/prescrizione</c>. O c'e' <see cref="Prodotto"/>,
    /// o c'e' <see cref="Errore"/>: mai tutti e due, mai nessuno dei due.
    /// </summary>
    public sealed class RispostaPrescrizione {

        /// <summary>Versione del contratto. Il ponte la legge e non la interpreta:
        /// serve a chi guarda i log quando due parti non vanno piu' d'accordo.</summary>
        [JsonPropertyName("contratto")] public string? Contratto { get; set; }

        [JsonPropertyName("prodotto")] public ProdottoPrescrizione? Prodotto { get; set; }

        [JsonPropertyName("errore")] public ErroreServizio? Errore { get; set; }

        [JsonPropertyName("misura")] public MisuraServizio? Misura { get; set; }

        [JsonExtensionData] public IDictionary<string, JsonElement>? Altro { get; set; }

        /// <summary>Legge la busta dal JSON del servizio.</summary>
        public static RispostaPrescrizione? Leggi(string json) =>
            JsonSerializer.Deserialize<RispostaPrescrizione>(json, SequenceModel.OpzioniJson);
    }

    /// <summary>
    /// La superficie. Il ponte tipizza solo <see cref="Sequenze"/>; tutto il resto
    /// passa per <see cref="Altro"/> e arriva all'interfaccia come e' partito.
    /// </summary>
    public sealed class ProdottoPrescrizione {

        [JsonPropertyName("bersaglio")] public BersaglioRisolto? Bersaglio { get; set; }

        [JsonPropertyName("sequenze")] public List<SequenzaDiNotte>? Sequenze { get; set; }

        /// <summary>Qui dentro finiscono valutazione, prescrizione, posa, piano e la
        /// notte: roba dell'interfaccia, che il ponte trasporta senza leggerla.</summary>
        [JsonExtensionData] public IDictionary<string, JsonElement>? Altro { get; set; }
    }

    /// <summary>Quale oggetto il motore ha capito, e da dove l'ha preso.</summary>
    public sealed class BersaglioRisolto {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("nomi")] public List<string>? Nomi { get; set; }
        [JsonPropertyName("via")] public string? Via { get; set; }
        [JsonExtensionData] public IDictionary<string, JsonElement>? Altro { get; set; }
    }

    /// <summary>
    /// Una notte del piano, con il suo modello di sequenza.
    /// <see cref="Notte"/> e' <c>int?</c> e non <c>int</c> per la ragione di sempre in
    /// questo progetto: una chiave assente non e' la notte numero zero.
    /// </summary>
    public sealed class SequenzaDiNotte {
        [JsonPropertyName("notte")] public int? Notte { get; set; }
        [JsonPropertyName("modello")] public SequenceModel? Modello { get; set; }
        [JsonExtensionData] public IDictionary<string, JsonElement>? Altro { get; set; }
    }

    /// <summary>
    /// Perche' il servizio non ha potuto rispondere. <see cref="Codice"/> e' per il
    /// programma, <see cref="Messaggio"/> per la persona: un motivo che nessuno puo'
    /// leggere non e' un motivo.
    /// </summary>
    public sealed class ErroreServizio {
        [JsonPropertyName("codice")] public string? Codice { get; set; }
        [JsonPropertyName("messaggio")] public string? Messaggio { get; set; }
        [JsonExtensionData] public IDictionary<string, JsonElement>? Altro { get; set; }
    }

    /// <summary>Quanto ci ha messo il motore a pensare, in millisecondi.</summary>
    public sealed class MisuraServizio {
        [JsonPropertyName("ms")] public double? Ms { get; set; }
        [JsonExtensionData] public IDictionary<string, JsonElement>? Altro { get; set; }
    }
}
