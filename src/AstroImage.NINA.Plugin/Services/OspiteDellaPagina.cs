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
     *                -> { id, ospite, versione, vie: [ "/profile/show", "/profile/horizon", ... ] }
     *
     *      nina         { id, azione: "nina", via: "/profile/show?active=true", metodo: "GET", corpo: "<testo>" }
     *                -> { id, risposta: { Response, Error, StatusCode, Success, Type } }
     *
     *  LE VIE LE DICHIARA L'OSPITE, e la pagina gli chiede solo quelle: per tutte le altre resta la strada di prima.
     *  Cosi' il Ponte puo' imparare una via alla volta, e una via che non dichiara non la puo' sbagliare. La risposta ha
     *  la forma e i nomi dell'Advanced API apposta: chi la legge non deve sapere da quale delle due strade arriva.
     *
     *  DUE VIE SONO DEL PONTE, e si vede dal nome (passo 3, 21 ottobre 2026). L'Advanced API carica un file di sequenza
     *  e prende il posto di quella aperta; il Ponte fa un'altra cosa, la sua: costruisce il bersaglio coi pezzi dei
     *  modelli di N.I.N.A., lo aggiunge accanto agli altri o lo mette al posto di uno, e rilegge ogni valore che scrive.
     *  Per questo non finge di essere l'Advanced API:
     *
     *      /ponte/bersagli   GET    i bersagli del Sequenziatore Avanzato, [{ indice, nome, stato }], lo stato coi nomi
     *                               di N.I.N.A. (CREATED, RUNNING, FINISHED, FAILED, SKIPPED, DISABLED)
     *      /ponte/consegna   POST   corpo { modello, sostituisci: null | { indice, nome } }: il modello della sequenza
     *                               — lo stesso contratto che il servizio consegnava —, come bersaglio nuovo o al
     *                               posto di quello che ha quell'indice E quel nome
     *
     *  Una via si dichiara solo se c'e' chi la serve (ServiziDellOspite): le vie dichiarate e quelle servite sono lo
     *  stesso elenco, per costruzione e per prova.
     *
     *  SI RISPONDE SOLO ALLA PAGINA SCRITTA NELLE OPZIONI. Un messaggio che arriva da un'altra origine — un collegamento
     *  seguito, un riquadro incorporato — non riceve niente, nemmeno un rifiuto.
     *
     *  QUI DENTRO N.I.N.A. NON SI NOMINA, come in Loc e in ImpostazioniPonte: il profilo arriva gia' letto, da
     *  ProfiloDaNina, la consegna la fa ConsegnaDallaPagina, e queste regole si provano nel banco di prova che gira senza
     *  le DLL di N.I.N.A.
     */
    public static class OspiteDellaPagina {

        public const string Nome = "AstroImage.NINA.Plugin";

        public const string ViaDelProfilo = "/profile/show";
        public const string ViaDellOrizzonte = "/profile/horizon";
        public const string ViaDeiBersagli = "/ponte/bersagli";
        public const string ViaDellaConsegna = "/ponte/consegna";

        /// <summary>Le vie del profilo, che l'ospite da' sempre: la lettura non ha bisogno di nessun servizio.</summary>
        public static readonly IReadOnlyList<string> Vie = new[] { ViaDelProfilo, ViaDellOrizzonte };

        /// <summary>Le vie a cui l'ospite risponde con questi servizi. La pagina chiede all'ospite queste, e solo queste.</summary>
        public static IReadOnlyList<string> VieDi(ServiziDellOspite servizi) {
            var v = new List<string>(Vie);
            if (servizi.Bersagli is not null) v.Add(ViaDeiBersagli);
            if (servizi.Consegna is not null) v.Add(ViaDellaConsegna);
            return v;
        }

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

        /// <summary>Solo il profilo, come nei passi 1 e 2: nessun bersaglio e nessuna consegna da dichiarare.</summary>
        public static string? Rispondi(string? testo, string? sorgente, Uri pagina, Func<ProfiloPerLaPagina?> profilo,
                                       string? versione, out string? nota) =>
            Rispondi(testo, sorgente, pagina, new ServiziDellOspite { Profilo = profilo }, versione, out nota);

        /*  LA RISPOSTA A UN MESSAGGIO, tutta qui, e la vista non fa che spedirla.
         *
         *  Torna null quando non si risponde: un messaggio da un'altra origine, un testo che non e' JSON, un messaggio
         *  senza id. In `nota` il perche', per il registro di N.I.N.A. Il profilo si chiede solo quando serve: una
         *  presentazione non legge niente. */
        public static string? Rispondi(string? testo, string? sorgente, Uri pagina, ServiziDellOspite servizi,
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
                r["vie"] = new JsonArray(VieDi(servizi).Select(v => (JsonNode?)JsonValue.Create(v)).ToArray());
            } else if (azione == "nina") {
                r["risposta"] = Busta(Testo(m["via"]), Testo(m["metodo"]), Testo(m["corpo"]), servizi);
            } else {
                r["errore"] = Loc.F("Ospite_AzioneSconosciuta", azione);
            }
            return r.ToJsonString(new JsonSerializerOptions());
        }

        /*  LA BUSTA DELL'ADVANCED API: Response, Error, StatusCode, Success, Type. La pagina guarda Success e non lo stato,
         *  e lo stesso fa qui: lo stato c'e' perche' c'e' nell'originale. */
        private static JsonObject Busta(string? via, string? metodo, string? corpo, ServiziDellOspite servizi) {
            var (percorso, domanda) = Dividi(via);
            var vie = VieDi(servizi);
            if (!vie.Contains(percorso))
                return No(404, Loc.F("Ospite_ViaSconosciuta", via, string.Join(", ", vie)));
            var post = string.Equals(metodo, "POST", StringComparison.OrdinalIgnoreCase);
            if (percorso == ViaDellaConsegna) {
                if (!post) return No(405, Loc.T("Ospite_ConsegnaConPost"));
                return Consegna(corpo, servizi.Consegna!);
            }
            if (!string.IsNullOrEmpty(metodo) && !string.Equals(metodo, "GET", StringComparison.OrdinalIgnoreCase))
                return No(405, Loc.T("Ospite_SoloLettura"));
            if (percorso == ViaDeiBersagli) {
                IReadOnlyList<BersaglioDelSequenziatore> presenti;
                try { presenti = servizi.Bersagli!(); } catch (Exception e) { return No(500, e.Message); }
                return Si(new JsonArray(presenti.Select((b, i) =>
                    (JsonNode?)new JsonObject { ["indice"] = i, ["nome"] = b.Nome, ["stato"] = b.Stato }).ToArray()));
            }
            if (percorso == ViaDelProfilo &&
                !(domanda.TryGetValue("active", out var attivo) && string.Equals(attivo, "true", StringComparison.OrdinalIgnoreCase)))
                return No(400, Loc.T("Ospite_SoloProfiloAttivo"));

            var p = servizi.Profilo();
            if (p is null) return No(500, Loc.T("Ospite_SenzaProfilo"));
            if (percorso == ViaDelProfilo) return Si(Profilo(p));
            if (p.Orizzonte is null || p.Orizzonte.Azimut.Length == 0) return No(404, Loc.T("Ospite_SenzaOrizzonte"));
            return Si(new JsonObject {
                ["Azimuths"] = Numeri(p.Orizzonte.Azimut),
                ["Altitudes"] = Numeri(p.Orizzonte.Altezza),
            });
        }

        /*  LA CONSEGNA. Il modello si legge col lettore del contratto (SequenceModel.Leggi), lo stesso che leggeva la
         *  risposta del servizio: la pagina e il servizio lo scrivono con la stessa funzione, e il Ponte non ha due
         *  letture. Una sostituzione porta indice E nome: chi consegna ricontrolla che quell'indice indichi ancora quel
         *  nome, perche' fra la lettura dell'elenco e il clic la sequenza puo' essere cambiata. Il rifiuto torna con le sue
         *  ragioni accanto — gli scarti e le note —, non solo con una frase. */
        private static JsonObject Consegna(string? corpo, Func<SequenceModel, int?, string?, EsitoDellaConsegna> consegna) {
            JsonObject? c;
            try { c = JsonNode.Parse(corpo ?? "") as JsonObject; } catch (JsonException) { c = null; }
            SequenceModel? modello = null;
            if (c?["modello"] is JsonObject mo) {
                try { modello = SequenceModel.Leggi(mo.ToJsonString()); } catch (JsonException) { modello = null; }
            }
            if (modello is null) return No(400, Loc.T("Ospite_ConsegnaSenzaModello"));

            int? indice = null; string? nome = null;
            if (c!["sostituisci"] is JsonObject s) {
                int i = -1;
                try { i = s["indice"]?.GetValue<int>() ?? -1; } catch (Exception) { i = -1; }
                nome = Testo(s["nome"]);
                if (i < 0 || string.IsNullOrWhiteSpace(nome)) return No(400, Loc.T("Ospite_SostituzioneMalformata"));
                indice = i;
            } else if (c["sostituisci"] is not null) {
                return No(400, Loc.T("Ospite_SostituzioneMalformata"));
            }

            EsitoDellaConsegna e;
            try { e = consegna(modello, indice, nome); } catch (Exception ex) { return No(500, ex.Message); }
            var dettagli = new JsonObject {
                ["nome"] = e.Nome, ["bersaglio"] = e.Bersaglio, ["blocchi"] = e.Blocchi,
                ["pose"] = new JsonArray(e.PosePerBlocco.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()),
                ["note"] = Testi(e.Note), ["scartati"] = Testi(e.Scartati), ["sostituito"] = e.Sostituito,
            };
            if (e.Fatta) return Si(dettagli);
            var no = No(409, e.PerCheNo ?? string.Join(" ", e.Scartati));
            no["Response"] = dettagli;
            return no;
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

        private static JsonArray Testi(IEnumerable<string>? v) =>
            new JsonArray((v ?? Enumerable.Empty<string>()).Select(x => (JsonNode?)JsonValue.Create(x)).ToArray());

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

    /*  CHI SERVE LE VIE. Il profilo c'e' sempre; i bersagli e la consegna solo se la vista li da', e una via senza chi
     *  la serve non si dichiara. Sono funzioni e non oggetti di N.I.N.A., cosi' l'ospite si prova senza N.I.N.A. */
    public sealed class ServiziDellOspite {
        public Func<ProfiloPerLaPagina?> Profilo { get; set; } = () => null;
        public Func<IReadOnlyList<BersaglioDelSequenziatore>>? Bersagli { get; set; }
        public Func<SequenceModel, int?, string?, EsitoDellaConsegna>? Consegna { get; set; }
    }

    /// <summary>Un bersaglio del Sequenziatore Avanzato: il nome con cui lo vede chi riprende, e lo stato di N.I.N.A.</summary>
    public sealed class BersaglioDelSequenziatore {
        public string Nome { get; set; } = "";
        public string? Stato { get; set; }
    }

    /// <summary>Com'e' andata una consegna: fatta o no, il perche', e quello che si e' costruito, scartato e notato.</summary>
    public sealed class EsitoDellaConsegna {
        public bool Fatta { get; set; }
        public string? PerCheNo { get; set; }
        public string? Nome { get; set; }
        public string? Bersaglio { get; set; }
        public int Blocchi { get; set; }
        /// <summary>Le pose scritte, blocco per blocco e nell'ordine del modello. Il totale lo fa chi le ha chieste.</summary>
        public List<int> PosePerBlocco { get; set; } = new List<int>();
        public List<string> Note { get; set; } = new List<string>();
        public List<string> Scartati { get; set; } = new List<string>();
        public string? Sostituito { get; set; }
    }
}
