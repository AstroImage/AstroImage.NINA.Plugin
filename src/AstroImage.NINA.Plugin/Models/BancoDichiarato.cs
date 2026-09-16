using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

#nullable enable

namespace AstroImage.NINA.Plugin.Models {

    /*  IL BANCO DICHIARATO: quello che di un banco non sanno ne' N.I.N.A. ne' il riconoscimento del catalogo, e che
     *  dichiara chi riprende, una volta per profilo.
     *
     *  LE CHIAVI NON SONO SCRITTE QUI. Sono quelle che il servizio pubblica in `/v1/salute`, `limiti.banco` —
     *  `tel.id`, `tel.apertura_mm`, `mnt.rms_caratteristico_arcsec` — e il Ponte ne tiene i valori cosi' come chi
     *  riprende li ha scritti. Una lista di campi scritta nel Ponte sarebbe una seconda verita' sul contratto del banco,
     *  destinata a divergere dalla prima. Se un valore vada bene lo decide il servizio, che rifiuta una chiave che non
     *  conosce e un valore fuori dominio, e lo dice.
     *
     *  QUI NON C'E' LA FOCALE, e non per dimenticanza: la focale la tiene N.I.N.A., che ci fa il plate solve, e si
     *  rilegge ogni volta invece di copiarla. Come negli altri modelli, qui dentro non c'e' un metodo: leggere e
     *  scrivere stanno in `Services/DichiarazioneBanco`. */
    public sealed class BancoDichiarato {

        /// <summary>La forma del documento: un JSON dentro una stringa non si migra da solo.</summary>
        [JsonPropertyName("versione")] public int Versione { get; set; } = 1;

        /// <summary>I valori dichiarati, per chiave del contratto del banco: un numero, o un identificativo di catalogo.</summary>
        [JsonPropertyName("valori")] public Dictionary<string, JsonElement> Valori { get; set; } = new Dictionary<string, JsonElement>();

        [JsonExtensionData] public Dictionary<string, JsonElement>? Altro { get; set; }
    }
}
