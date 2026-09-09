using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

#nullable enable

namespace AstroImage.NINA.Plugin.Models {

    /*  LA RUOTA VIRTUALE: che cosa sono, fisicamente, i vetri che hai in ruota.
     *
     *  N.I.N.A. sa come hai CHIAMATO i tuoi filtri e in quale slot stanno. Non sa che
     *  cosa sono: quei nomi li scrivi tu a mano e non provano niente — «HA» puo' stare
     *  davanti a un L-Ultimate, e su una ruota vera si leggono cose come «ULTIMATE»,
     *  «LPS_P2 IDAS», «V4 IDAS». Il motore, dall'altra parte, conosce i vetri per
     *  identificativo e ne conosce la fisica.
     *
     *  Fra le due cose non c'e' regola che tenga: quale duale serva per Ha+OIII lo sa
     *  solo chi l'ha comprato. Quindi si DICHIARA, una volta per profilo:
     *
     *      nome in N.I.N.A.  →  identificativo del motore
     *
     *  E basta. Niente nanometri, niente bande, niente costruttore: quelli il motore
     *  li ha gia', piu' ricchi, e ridigitarli qui creerebbe una seconda verita'
     *  spettrale destinata a divergere dalla prima.
     *
     *  QUI DENTRO NON C'E' UN METODO, ed e' una regola del progetto sorvegliata da
     *  IndipendenzaDaNinaTests: un modello e' dato muto. Leggere, scrivere e cercare
     *  stanno in `Services/DichiarazioneRuota`.
     */
    public sealed class RuotaVirtuale {

        /// <summary>La forma del documento. Serve perche' un JSON dentro una stringa
        /// non si migra da solo: il giorno che la forma cambia, questo dice da dove.</summary>
        [JsonPropertyName("versione")] public int Versione { get; set; } = 1;

        [JsonPropertyName("vetri")] public List<VoceRuota> Vetri { get; set; } = new List<VoceRuota>();

        [JsonExtensionData] public Dictionary<string, JsonElement>? Altro { get; set; }
    }

    /// <summary>
    /// Una riga della dichiarazione.
    /// <para/>
    /// LO SLOT NON C'E', per progetto. Lo slot e' stato operativo: se domani sposti
    /// ULTIMATE dal 3 al 5, una configurazione basata sullo slot resterebbe valida e
    /// SBAGLIATA — direbbe ancora «lult» per lo slot 3, dove ora c'e' un altro vetro, e
    /// nessuno se ne accorgerebbe. Con la chiave sul nome, rinominare un filtro rompe
    /// il legame in modo VISIBILE: la voce diventa orfana e si ripunta in un clic. Fra
    /// un guasto che si vede e uno che non si vede vince sempre quello che si vede.
    /// </summary>
    public sealed class VoceRuota {

        /// <summary>Il nome esatto scritto nella ruota di N.I.N.A. E' la chiave.</summary>
        [JsonPropertyName("nina")] public string? Nina { get; set; }

        /// <summary>L'identificativo del vetro nel catalogo del motore, oppure null:
        /// null vuol dire «non dichiarato», e un filtro non dichiarato il motore non
        /// sa di averlo.</summary>
        [JsonPropertyName("motore")] public string? Motore { get; set; }

        /// <summary>Testo libero di chi configura. Dichiaratamente NON usato per
        /// calcolare niente: serve a ricordarsi perche', non a decidere.</summary>
        [JsonPropertyName("nota")] public string? Nota { get; set; }
    }
}
