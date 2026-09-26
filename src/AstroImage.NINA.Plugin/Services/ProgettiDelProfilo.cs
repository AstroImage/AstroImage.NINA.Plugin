using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

#nullable enable

namespace AstroImage.NINA.Plugin.Services {

    /*  I PROGETTI APERTI, PER PROFILO DI N.I.N.A. (regia, 19 settembre 2026).
     *
     *  L'unita' non e' la notte, e' il progetto. Il motore propone un profilo — modo e guadagno, posa e filtro per canale,
     *  serie corta, temperatura, strada, strategia, seeing, convenzioni della posa — e il Ponte lo congela alla prima
     *  consegna di una sequenza per quel bersaglio con quel banco: guardare una prescrizione e' gratis, consegnare e'
     *  l'impegno. Da li' in poi ogni richiesta per quel progetto porta il profilo in `opzioni.profilo`, e il motore lo
     *  onora: ricalcola solo la notte. «Apri un progetto nuovo» toglie il profilo, e la consegna dopo ne apre un altro.
     *
     *  UN PROGETTO E' IL BERSAGLIO COL SUO BANCO — telescopio, riduttore, camera — e i filtri li porta il profilo. Il sito
     *  non entra: e' una premessa, e se cambia lo dice il motore. Si cerca col bersaglio come la pagina lo chiede e col
     *  banco come la pagina lo manda; il motore rifiuta un profilo il cui banco, riconosciuto, non coincide.
     *
     *  Il documento vive nella memoria per profilo, come la ruota e il sito, ed e' versionato: e' il seme della memoria di
     *  cio' che e' stato ripreso, e ci cresceranno dentro le ore. Questa classe non nomina N.I.N.A.: si prova senza.
     */
    public static class ProgettiDelProfilo {

        /// <summary>La chiave del documento dei progetti nella memoria per profilo.</summary>
        public const string Chiave = "progetti";

        /// <summary>La versione del documento.</summary>
        public const int Versione = 1;

        /// <summary>Un progetto aperto: il bersaglio e il banco come li manda la pagina, quando si e' aperto, il profilo.</summary>
        public sealed class Progetto {
            public string Bersaglio { get; set; } = "";
            public string Banco { get; set; } = "";
            public string ApertoIl { get; set; } = "";
            public JsonObject Profilo { get; set; } = new JsonObject();
        }

        /*  Il bersaglio si confronta normalizzato: «NGC 7000», «ngc7000» e «NGC7000» sono lo stesso, come per il motore. */
        public static string Norma(string? s) =>
            new string((s ?? "").ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

        /// <summary>La chiave del banco come la pagina lo manda: telescopio, riduttore, camera. Il resto non e' identita'.</summary>
        public static string ChiaveDelBanco(JsonObject? banco) {
            if (banco is null) return "";
            string Pezzo(string k) {
                var v = banco[k];
                if (v is JsonObject o) {
                    var id = o["id"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(id)) return id!.Trim();
                    var nome = o["nome"]?.ToString() ?? o["name"]?.ToString();
                    return string.IsNullOrWhiteSpace(nome) ? "" : "nome:" + nome!.Trim();
                }
                return v?.ToString()?.Trim() ?? "";
            }
            var red = banco["red"];
            var r = red is JsonValue jv && jv.TryGetValue<double>(out var d) ? d.ToString("0.###", CultureInfo.InvariantCulture)
                                                                             : red?.ToString() ?? "";
            return Pezzo("tel") + "|" + r + "|" + Pezzo("cam");
        }

        /// <summary>I progetti del documento salvato; quello che non si legge si dice in `nota` e si scarta.</summary>
        public static List<Progetto> Leggi(string? json, out string? nota) {
            nota = null;
            var out_ = new List<Progetto>();
            if (string.IsNullOrWhiteSpace(json)) return out_;
            JsonNode? radice;
            try { radice = JsonNode.Parse(json!); }
            catch (JsonException) { nota = "progetti_illeggibili"; return out_; }
            if (radice is not JsonObject doc || (doc["versione"]?.GetValue<int>() ?? 0) != Versione) {
                nota = "progetti_versione"; return out_;
            }
            foreach (var n in doc["progetti"] as JsonArray ?? new JsonArray()) {
                if (n is not JsonObject p || p["profilo"] is not JsonObject prof) continue;
                out_.Add(new Progetto {
                    Bersaglio = p["bersaglio"]?.ToString() ?? "", Banco = p["banco"]?.ToString() ?? "",
                    ApertoIl = p["apertoIl"]?.ToString() ?? "", Profilo = (JsonObject)prof.DeepClone(),
                });
            }
            return out_;
        }

        /// <summary>Il documento da salvare.</summary>
        public static string Scrivi(IEnumerable<Progetto> progetti) {
            var arr = new JsonArray();
            foreach (var p in progetti) arr.Add(new JsonObject {
                ["bersaglio"] = p.Bersaglio, ["banco"] = p.Banco, ["apertoIl"] = p.ApertoIl, ["profilo"] = p.Profilo.DeepClone(),
            });
            return new JsonObject { ["versione"] = Versione, ["progetti"] = arr }.ToJsonString();
        }

        /// <summary>Il progetto aperto per questo bersaglio e questo banco, o null.</summary>
        public static Progetto? Trova(IEnumerable<Progetto> progetti, string? bersaglio, string? banco) {
            var b = Norma(bersaglio);
            return progetti.FirstOrDefault(p => (Norma(p.Bersaglio) == b ||
                                                 Norma(p.Profilo["chiave"]?["bersaglio"]?.ToString()) == b)
                                                && string.Equals(p.Banco, banco ?? "", StringComparison.Ordinal));
        }

        /// <summary>
        /// Apre il progetto se per quel bersaglio e quel banco non ce n'e' uno: il profilo proposto dal motore si congela.
        /// Vero se lo ha aperto; falso se c'era gia' o se il profilo non e' uno proposto.
        /// </summary>
        public static bool Apri(List<Progetto> progetti, string? bersaglio, string? banco, JsonObject? profilo, DateTime quando) {
            if (profilo is null || profilo["stato"]?.ToString() != "proposto") return false;
            if (Trova(progetti, bersaglio, banco) != null) return false;
            var congelato = (JsonObject)profilo.DeepClone();
            congelato.Remove("stato");
            congelato.Remove("pareggio");
            progetti.Add(new Progetto { Bersaglio = bersaglio ?? "", Banco = banco ?? "",
                ApertoIl = quando.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Profilo = congelato });
            return true;
        }

        /// <summary>Il bersaglio come la pagina lo chiede nella domanda: l'identificativo, o il nome.</summary>
        public static string? BersaglioDellaDomanda(JsonObject? corpo) {
            var b = corpo?["bersaglio"] as JsonObject;
            var s = b?["id"]?.ToString() ?? b?["nome"]?.ToString();
            return string.IsNullOrWhiteSpace(s) ? null : s!.Trim();
        }

        /// <summary>
        /// Se per il bersaglio e il banco della domanda c'e' un progetto aperto, il suo profilo parte in `opzioni.profilo` —
        /// a meno che la domanda ne porti gia' uno: chi la compone resta padrone. Restituisce il progetto, o null.
        /// </summary>
        public static Progetto? AggiungiAllaDomanda(JsonObject? corpo, IEnumerable<Progetto> progetti) {
            if (corpo is null) return null;
            var p = Trova(progetti, BersaglioDellaDomanda(corpo), ChiaveDelBanco(corpo["banco"] as JsonObject));
            if (p is null) return null;
            if (corpo["opzioni"] is not JsonObject opz) { opz = new JsonObject(); corpo["opzioni"] = opz; }
            if (!opz.ContainsKey("profilo")) opz["profilo"] = p.Profilo.DeepClone();
            return p;
        }

        /// <summary>Toglie il progetto di questo bersaglio e questo banco: la consegna dopo ne apre uno nuovo.</summary>
        public static bool Chiudi(List<Progetto> progetti, string? bersaglio, string? banco) {
            var p = Trova(progetti, bersaglio, banco);
            return p != null && progetti.Remove(p);
        }

        /*  I PROGETTI APERTI SI VEDONO (regia, 26 settembre 2026). Stavano nella memoria del profilo e comparivano solo
         *  chiedendo quell'oggetto: sul MiniX M82 dava la L a 60 s e M81 a 180 s, e non c'era modo di sapere che M82 aveva un
         *  progetto aperto la notte prima, con la posa congelata. La pagina li riceve tutti, come sono salvati — il profilo
         *  intero, perche' le pose le scrive lei, con la stessa funzione del riquadro del progetto —, i piu' recenti prima. */
        /// <summary>I progetti aperti per la pagina: bersaglio, banco, data d'apertura e profilo, i piu' recenti prima.</summary>
        public static JsonArray PerLaPagina(IEnumerable<Progetto> progetti) {
            var arr = new JsonArray();
            foreach (var p in progetti.OrderByDescending(x => x.ApertoIl, StringComparer.Ordinal)
                                      .ThenBy(x => x.Bersaglio, StringComparer.OrdinalIgnoreCase))
                arr.Add(new JsonObject {
                    ["bersaglio"] = p.Bersaglio, ["banco"] = p.Banco, ["apertoIl"] = p.ApertoIl, ["profilo"] = p.Profilo.DeepClone(),
                });
            return arr;
        }
    }
}
