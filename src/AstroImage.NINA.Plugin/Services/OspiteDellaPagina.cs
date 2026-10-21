using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using AstroImage.NINA.Plugin.Localization;
using AstroImage.NINA.Plugin.Models;

#nullable enable

namespace AstroImage.NINA.Plugin.Services {

    /*  L'OSPITE DELLA PAGINA VERA.
     *
     *  Il pannello e' nato per ospitare l'interfaccia di AstroImage-Strategy intera, e fino a oggi ospitava una pagina di
     *  prova. Con l'indirizzo della pagina vera scritto nelle Opzioni, il pannello apre quella, e il Ponte fa da ospite:
     *  la pagina gli chiede quello che altrimenti chiederebbe all'Advanced API, e lui risponde dal N.I.N.A. in cui gira.
     *  Niente porta di rete da aprire, niente plugin in piu' da installare, niente browser che blocca una pagina https
     *  che parla con una macchina in http.
     *
     *  IL PROTOCOLLO, due azioni, testo JSON in tutte e due le direzioni come per la pagina di prova:
     *
     *      presentati   { id, azione: "presentati" }
     *                -> { id, ospite, versione, vie: [ "/profile/show", "/profile/horizon" ] }
     *
     *      nina         { id, azione: "nina", via: "/profile/show?active=true", metodo: "GET" }
     *                -> { id, risposta: { Response, Error, StatusCode, Success, Type } }
     *
     *  LE VIE LE DICHIARA L'OSPITE, e la pagina gli chiede solo quelle: per tutte le altre resta la strada di prima.
     *  Cosi' il Ponte puo' imparare una via alla volta, e una via che non dichiara non la puo' sbagliare. La risposta ha
     *  la forma e i nomi dell'Advanced API apposta: chi la legge non deve sapere da quale delle due strade arriva.
     *
     *  SI RISPONDE SOLO ALLA PAGINA SCRITTA NELLE OPZIONI. Un messaggio che arriva da un'altra origine — un collegamento
     *  seguito, un riquadro incorporato — non riceve niente, nemmeno un rifiuto.
     *
     *  QUI DENTRO N.I.N.A. NON SI NOMINA, come in Loc e in ImpostazioniPonte: il profilo arriva gia' letto, da
     *  ProfiloDaNina, e queste regole si provano nel banco di prova che gira senza le DLL di N.I.N.A.
     */
    public static class OspiteDellaPagina {

        public const string Nome = "AstroImage.NINA.Plugin";

        public const string ViaDelProfilo = "/profile/show";
        public const string ViaDellOrizzonte = "/profile/horizon";

        /// <summary>Le vie a cui l'ospite risponde. La pagina chiede all'ospite queste, e solo queste.</summary>
        public static readonly IReadOnlyList<string> Vie = new[] { ViaDelProfilo, ViaDellOrizzonte };

        /*  L'INDIRIZZO DELLA PAGINA. Vuoto vuol dire la pagina di prova, come prima: chi non lo scrive non vede cambiare
         *  niente. Se c'e', deve essere https — la pagina pubblica — oppure http sulla macchina stessa, che e' come si
         *  prova una pagina nuova prima di pubblicarla. Un http verso un'altra macchina no: quello che il Ponte da' alla
         *  pagina viaggerebbe in chiaro sulla rete di casa, e chi sta in mezzo potrebbe sostituire la pagina. */
        public static Uri? Pagina(string? indirizzo, out string? perCheNo) {
            perCheNo = null;
            var s = (indirizzo ?? "").Trim();
            if (s.Length == 0) return null;
            if (Uri.TryCreate(s, UriKind.Absolute, out var u) && string.IsNullOrEmpty(u.UserInfo) &&
                (u.Scheme == Uri.UriSchemeHttps || (u.Scheme == Uri.UriSchemeHttp && SuQuestaMacchina(u))))
                return u;
            perCheNo = Loc.F("Ospite_IndirizzoNonValido", s);
            return null;
        }

        private static bool SuQuestaMacchina(Uri u) =>
            u.IsLoopback || string.Equals(u.Host, "localhost", StringComparison.OrdinalIgnoreCase);

        /// <summary>Schema, nome e porta: la stessa origine che il browser usa per decidere chi e' chi.</summary>
        public static string Origine(Uri u) => u.GetLeftPart(UriPartial.Authority).ToLowerInvariant();

        public static bool DallaPagina(string? sorgente, Uri pagina) =>
            Uri.TryCreate(sorgente, UriKind.Absolute, out var u) && Origine(u) == Origine(pagina);

        /*  IL PANNELLO NON SI PORTA VIA. Un collegamento della pagina — la guida, una fonte, un catalogo — aperto dentro il
         *  pannello ci resterebbe, e la pagina sparirebbe con le sue risposte a meta'. Dentro resta solo la pagina stessa,
         *  anche ricaricata o con un'altra domanda nell'indirizzo; il resto si apre nel browser di sistema. */
        public static bool RestaNelPannello(string? destinazione, Uri pagina) =>
            Uri.TryCreate(destinazione, UriKind.Absolute, out var u) && Origine(u) == Origine(pagina) &&
            string.Equals(u.AbsolutePath, pagina.AbsolutePath, StringComparison.Ordinal);

        /// <summary>Solo un indirizzo web si consegna al browser di sistema: mai un file, mai un programma.</summary>
        public static bool SiApreNelBrowser(string? destinazione) =>
            Uri.TryCreate(destinazione, UriKind.Absolute, out var u) &&
            (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp);

        /*  LA RISPOSTA A UN MESSAGGIO, tutta qui, e la vista non fa che spedirla.
         *
         *  Torna null quando non si risponde: un messaggio da un'altra origine, un testo che non e' JSON, un messaggio
         *  senza id. In `nota` il perche', per il registro di N.I.N.A. Il profilo si chiede solo quando serve: una
         *  presentazione non legge niente. */
        public static string? Rispondi(string? testo, string? sorgente, Uri pagina, Func<ProfiloPerLaPagina?> profilo,
                                       string? versione, out string? nota) {
            nota = null;
            if (!DallaPagina(sorgente, pagina)) {
                nota = "message from «" + sorgente + "» ignored: the panel page is " + Origine(pagina);
                return null;
            }
            JsonObject? m;
            try { m = JsonNode.Parse(testo ?? "") as JsonObject; } catch (JsonException) { m = null; }
            var id = Testo(m?["id"]);
            if (m is null || id is null) {
                nota = "message without an id ignored";
                return null;
            }
            var azione = Testo(m["azione"]);
            var r = new JsonObject { ["id"] = id };
            if (azione == "presentati") {
                r["ospite"] = Nome;
                r["versione"] = versione;
                r["vie"] = new JsonArray(Vie.Select(v => (JsonNode?)JsonValue.Create(v)).ToArray());
            } else if (azione == "nina") {
                r["risposta"] = Busta(Testo(m["via"]), Testo(m["metodo"]), profilo);
            } else {
                r["errore"] = Loc.F("Ospite_AzioneSconosciuta", azione);
            }
            return r.ToJsonString(new JsonSerializerOptions());
        }

        /*  LA BUSTA DELL'ADVANCED API: Response, Error, StatusCode, Success, Type. La pagina guarda Success e non lo stato,
         *  e lo stesso fa qui: lo stato c'e' perche' c'e' nell'originale. */
        private static JsonObject Busta(string? via, string? metodo, Func<ProfiloPerLaPagina?> profilo) {
            var (percorso, domanda) = Dividi(via);
            if (!Vie.Contains(percorso))
                return No(404, Loc.F("Ospite_ViaSconosciuta", via, string.Join(", ", Vie)));
            if (!string.IsNullOrEmpty(metodo) && !string.Equals(metodo, "GET", StringComparison.OrdinalIgnoreCase))
                return No(405, Loc.T("Ospite_SoloLettura"));
            if (percorso == ViaDelProfilo &&
                !(domanda.TryGetValue("active", out var attivo) && string.Equals(attivo, "true", StringComparison.OrdinalIgnoreCase)))
                return No(400, Loc.T("Ospite_SoloProfiloAttivo"));

            var p = profilo();
            if (p is null) return No(500, Loc.T("Ospite_SenzaProfilo"));
            if (percorso == ViaDelProfilo) return Si(Profilo(p));
            if (p.Orizzonte is null || p.Orizzonte.Azimut.Length == 0) return No(404, Loc.T("Ospite_SenzaOrizzonte"));
            return Si(new JsonObject {
                ["Azimuths"] = Numeri(p.Orizzonte.Azimut),
                ["Altitudes"] = Numeri(p.Orizzonte.Altezza),
            });
        }

        /*  I NOMI DI N.I.N.A., gruppo per gruppo, come li scrive l'Advanced API: il profilo, il sito, la camera, il
         *  telescopio, il Framing Assistant e la ruota. Un valore che il profilo non ha parte null, e non zero. */
        private static JsonObject Profilo(ProfiloPerLaPagina p) => new JsonObject {
            ["Name"] = p.Nome,
            ["AstrometrySettings"] = new JsonObject {
                ["Latitude"] = p.Latitudine, ["Longitude"] = p.Longitudine, ["Elevation"] = p.Quota,
                ["HorizonFilePath"] = p.FileOrizzonte,
            },
            ["CameraSettings"] = new JsonObject {
                ["Id"] = p.CameraId, ["PixelSize"] = p.PixelUm, ["BayerPattern"] = p.Matrice,
            },
            ["TelescopeSettings"] = new JsonObject {
                ["Name"] = p.Telescopio, ["MountName"] = p.Montatura, ["FocalLength"] = p.FocaleMm, ["FocalRatio"] = p.Rapporto,
            },
            ["FramingAssistantSettings"] = new JsonObject {
                ["CameraWidth"] = p.LarghezzaPx, ["CameraHeight"] = p.AltezzaPx,
            },
            ["FilterWheelSettings"] = new JsonObject {
                ["FilterWheelFilters"] = new JsonArray(p.Filtri.Select(f =>
                    (JsonNode?)new JsonObject { ["Name"] = f.Nome, ["Position"] = f.Posizione }).ToArray()),
            },
        };

        private static JsonObject Si(JsonNode dato) =>
            new JsonObject { ["Response"] = dato, ["Error"] = "", ["StatusCode"] = 200, ["Success"] = true, ["Type"] = "API" };

        private static JsonObject No(int stato, string perche) =>
            new JsonObject { ["Response"] = "", ["Error"] = perche, ["StatusCode"] = stato, ["Success"] = false, ["Type"] = "API" };

        private static JsonArray Numeri(double[] v) => new JsonArray(v.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray());

        private static (string Percorso, Dictionary<string, string> Domanda) Dividi(string? via) {
            var s = via ?? "";
            var q = s.IndexOf('?');
            var domanda = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (q < 0) return (s, domanda);
            foreach (var coppia in s.Substring(q + 1).Split('&', StringSplitOptions.RemoveEmptyEntries)) {
                var u = coppia.IndexOf('=');
                var k = Uri.UnescapeDataString(u < 0 ? coppia : coppia.Substring(0, u));
                domanda[k] = u < 0 ? "" : Uri.UnescapeDataString(coppia.Substring(u + 1));
            }
            return (s.Substring(0, q), domanda);
        }

        private static string? Testo(JsonNode? n) {
            try { return n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null; } catch (Exception) { return null; }
        }
    }
}
