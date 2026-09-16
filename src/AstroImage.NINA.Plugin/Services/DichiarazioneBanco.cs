using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AstroImage.NINA.Plugin.Localization;
using AstroImage.NINA.Plugin.Models;

#nullable enable

namespace AstroImage.NINA.Plugin.Services {

    /*  LEGGERE E SCRIVERE IL BANCO DICHIARATO.
     *
     *  Il Ponte non sa quali chiavi esistano — le pubblica il servizio — ma sa riconoscere una chiave da un testo
     *  qualunque, e sa che un valore dichiarato e' un numero o un identificativo. Tutto il resto lo giudica il motore,
     *  quando la dichiarazione parte: una chiave che non conosce si rifiuta nominandola, e un valore fuori dominio anche.
     *  Qui non si nomina N.I.N.A.: si prova senza.
     */
    public static class DichiarazioneBanco {

        /*  La forma di una chiave del contratto: un pezzo, e un campo dentro il pezzo. */
        private static readonly Regex FormaChiave = new Regex(@"^[a-z]+(\.[a-z_]+)?$", RegexOptions.CultureInvariant);

        /// <summary>Il banco salvato nel profilo. Un documento assente e' un banco non ancora dichiarato; uno illeggibile
        /// si dice, e non impedisce al pannello di aprirsi.</summary>
        public static BancoDichiarato Leggi(string? json, out string? nota) {
            nota = null;
            if (string.IsNullOrWhiteSpace(json)) return new BancoDichiarato();
            try {
                var b = JsonSerializer.Deserialize<BancoDichiarato>(json!);
                if (b is null) { nota = Loc.T("Banco_DichiarazioneVuota"); return new BancoDichiarato(); }
                if (b.Versione != 1) nota = Loc.F("Banco_VersioneSconosciuta", b.Versione);
                b.Valori ??= new Dictionary<string, JsonElement>();
                return b;
            } catch (Exception e) {
                nota = Loc.F("Banco_DichiarazioneIlleggibile", e.Message);
                return new BancoDichiarato();
            }
        }

        public static string Scrivi(BancoDichiarato? b) => JsonSerializer.Serialize(b ?? new BancoDichiarato());

        /// <summary>
        /// Dal messaggio della pagina: <c>{ banco: { "tel.id": "askar71f", "tel.apertura_mm": 80 } }</c>. Un valore vuoto
        /// toglie la chiave. Una chiave che non ha la forma di una chiave, o un valore che non e' ne' un numero ne' un
        /// testo, si rifiuta e si dice: salvarlo vorrebbe dire scoprirlo solo alla prossima prescrizione.
        /// </summary>
        public static BancoDichiarato? DalMessaggio(JsonNode? messaggio, out string? perCheNo) {
            perCheNo = null;
            if (!(messaggio?["banco"] is JsonObject o)) { perCheNo = Loc.T("Banco_MessaggioSenzaBanco"); return null; }
            var b = new BancoDichiarato();
            foreach (var kv in o) {
                if (!FormaChiave.IsMatch(kv.Key)) { perCheNo = Loc.F("Banco_ChiaveMalformata", kv.Key); return null; }
                if (kv.Value is null) continue;
                var el = JsonSerializer.SerializeToElement(kv.Value);
                if (el.ValueKind == JsonValueKind.Number) { b.Valori[kv.Key] = el; continue; }
                if (el.ValueKind == JsonValueKind.String) {
                    var s = el.GetString();
                    if (string.IsNullOrWhiteSpace(s)) continue;
                    b.Valori[kv.Key] = JsonSerializer.SerializeToElement(s!.Trim());
                    continue;
                }
                perCheNo = Loc.F("Banco_ValoreMalformato", kv.Key);
                return null;
            }
            return b;
        }

        /// <summary>I valori dichiarati come oggetto, per la pagina: la chiave e il valore cosi' come sono.</summary>
        public static JsonObject PerLaPagina(BancoDichiarato? b) {
            var o = new JsonObject();
            if (b?.Valori is null) return o;
            foreach (var kv in b.Valori) o[kv.Key] = JsonNode.Parse(kv.Value.GetRawText());
            return o;
        }
    }
}
