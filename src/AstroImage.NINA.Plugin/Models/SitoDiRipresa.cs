using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

#nullable enable

namespace AstroImage.NINA.Plugin.Models {

    /*  DA DOVE STAI RIPRENDENDO — e da dove lo sappiamo.
     *
     *  IL DIFETTO CHE CHIUDE. La pagina di prova spediva sette numeri scritti a mano:
     *  45.9, 10.2, sqm 20.8, seeing 1.6, rms 0.6, altezza minima 20, notti serene 0.35.
     *  Sono Borno, l'altopiano dove riprende chi ha scritto il motore. Su quella
     *  macchina non si vedeva; su qualunque altra, il ponte avrebbe pianificato la
     *  notte di qualcun altro spacciandola per la tua.
     *
     *  E IL MOTORE NON SE NE SAREBBE ACCORTO, perche' quei numeri sono plausibili.
     *  Cambiare l'SQM da 20.8 a 19.0 porta la posa da 600 s x 29 a 240 s x 60: una
     *  notte completamente diversa, senza un solo messaggio d'errore. E' la stessa
     *  famiglia del filtro sostituito in silenzio, spostata di un livello.
     *
     *  DUE COSE DIVERSE, E NON SI SOSTITUISCONO A VICENDA:
     *
     *      la GEOMETRIA — latitudine e longitudine — N.I.N.A. le ha SEMPRE, perche'
     *      ogni utente le inserisce per forza. Si prendono e basta.
     *
     *      la QUALITA' DEL CIELO — l'SQM — e' una MISURA, non una posizione. Non si
     *      ricava dalle coordinate: fra il balcone e l'altopiano, a venti chilometri
     *      di distanza, ci sono due magnitudini. N.I.N.A. la sa solo se hai un
     *      misuratore collegato; altrimenti la dichiari, come dichiari i filtri.
     *
     *  Ogni valore porta quindi la propria provenienza, e la pagina la mostra: un
     *  numero misurato e uno scritto a mano non devono somigliarsi.
     */
    public sealed class SitoDiRipresa {

        [JsonPropertyName("lat")] public double? Lat { get; set; }
        [JsonPropertyName("lon")] public double? Lon { get; set; }
        [JsonPropertyName("sqm")] public double? Sqm { get; set; }
        [JsonPropertyName("seeing")] public double? Seeing { get; set; }
        [JsonPropertyName("rms")] public double? Rms { get; set; }
        [JsonPropertyName("horizonMin")] public double? HorizonMin { get; set; }
        [JsonPropertyName("clearFrac")] public double? ClearFrac { get; set; }

        /// <summary>Per ogni campo, da dove viene quel numero. Chiave uguale al campo.</summary>
        [JsonPropertyName("provenienza")]
        public Dictionary<string, string>? Provenienza { get; set; }

        /// <summary>Il nome del sito nel profilo di N.I.N.A., se ne ha uno. Da mostrare
        /// soltanto: il motore non lo usa e non deve riceverlo.</summary>
        [JsonPropertyName("nome")] public string? Nome { get; set; }

        [JsonExtensionData] public Dictionary<string, JsonElement>? Altro { get; set; }
    }

    /*  I PARAMETRI CHE N.I.N.A. NON SA, dichiarati da chi riprende e salvati nel suo
     *  profilo — accanto alla ruota, per la stessa ragione: due profili sono due
     *  postazioni, e la postazione in montagna non ha il cielo di quella in citta'.
     *
     *  Tutti nulli di partenza, e nulli restano finche' qualcuno non li scrive. Non
     *  c'e' nessun valore di serie nascosto qui dentro: se manca, manca. */
    public sealed class SitoDichiarato {

        [JsonPropertyName("versione")] public int Versione { get; set; } = 1;

        [JsonPropertyName("sqm")] public double? Sqm { get; set; }
        [JsonPropertyName("seeing")] public double? Seeing { get; set; }
        [JsonPropertyName("rms")] public double? Rms { get; set; }
        [JsonPropertyName("horizonMin")] public double? HorizonMin { get; set; }
        [JsonPropertyName("clearFrac")] public double? ClearFrac { get; set; }

        [JsonExtensionData] public Dictionary<string, JsonElement>? Altro { get; set; }
    }
}
