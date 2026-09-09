using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AstroImage.NINA.Plugin.Models;

#nullable enable

namespace AstroImage.NINA.Plugin.Services {

    /*  METTERE INSIEME IL SITO: quello che N.I.N.A. sa, e quello che dichiari tu.
     *
     *  Le due fonti non hanno lo stesso peso e non vanno confuse. Un SQM letto da un
     *  misuratore e un SQM scritto a mano valgono lo stesso per il calcolo, ma non per
     *  chi guarda: il primo e' un dato, il secondo una responsabilita'. La provenienza
     *  viaggia con ogni numero, e la pagina la mostra.
     *
     *  E NON C'E' UN SITO DI RIPIEGO. Se un valore non arriva ne' da N.I.N.A. ne' dalla
     *  dichiarazione, resta nullo — non diventa il numero di Borno, non diventa una
     *  media, non diventa niente. Il servizio poi si comporta come gia' fa con un sito
     *  incompleto, e la pagina lo dice prima di chiedere.
     */
    public static class DichiarazioneSito {

        public const int VersioneCorrente = 1;

        /// <summary>Da N.I.N.A., sempre disponibile.</summary>
        public const string DaProfilo = "profilo N.I.N.A.";
        /// <summary>Da uno strumento collegato: e' una misura.</summary>
        public const string Misurato = "misurato dallo strumento";
        /// <summary>Scritto da chi riprende: e' una sua responsabilita'.</summary>
        public const string Dichiarato = "dichiarato da te";
        /// <summary>Non lo sa nessuno.</summary>
        public const string Assente = "non disponibile";

        private static readonly JsonSerializerOptions Opzioni = new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        /*  Come per la ruota: non solleva mai, e una versione che non conosciamo non si
         *  indovina. Meglio nessun parametro dichiarato che parametri letti con la
         *  grammatica sbagliata — l'utente riscrive cinque numeri in un minuto, una
         *  notte pianificata sul cielo di un altro non si recupera. */
        public static SitoDichiarato Leggi(string? json, out string? nota) {
            nota = null;
            if (string.IsNullOrWhiteSpace(json)) return new SitoDichiarato();
            SitoDichiarato? d;
            try { d = JsonSerializer.Deserialize<SitoDichiarato>(json!, Opzioni); }
            catch (Exception e) {
                nota = "I parametri del sito non si sono potuti leggere (" + e.Message +
                       "). Riparto da vuoto: nessun valore e' stato inventato.";
                return new SitoDichiarato();
            }
            if (d is null) { nota = "I parametri del sito sono vuoti o malformati."; return new SitoDichiarato(); }
            if (d.Versione > VersioneCorrente) {
                nota = $"I parametri del sito sono in versione {d.Versione}, e questo ponte " +
                       $"ne conosce fino alla {VersioneCorrente}. Non li interpreto.";
                return new SitoDichiarato();
            }
            d.Versione = VersioneCorrente;
            return d;
        }

        public static string Scrivi(SitoDichiarato? d) =>
            JsonSerializer.Serialize(d ?? new SitoDichiarato(), Opzioni);

        /*  Stessa guardia del salvataggio della ruota, e per lo stesso difetto: una
         *  chiave assente non e' «azzera tutto». */
        public static SitoDichiarato? DalMessaggio(JsonNode? messaggio, out string? perCheNo) {
            perCheNo = null;
            var corpo = messaggio?["corpo"];
            if (corpo?["sito"] is not JsonObject s) {
                perCheNo = "La richiesta non dichiara nessun sito. Non la interpreto come " +
                           "«azzera tutto»: i parametri precedenti restano dove sono.";
                return null;
            }
            return new SitoDichiarato {
                Versione = VersioneCorrente,
                Sqm = Numero(s["sqm"]),
                Seeing = Numero(s["seeing"]),
                Rms = Numero(s["rms"]),
                HorizonMin = Numero(s["horizonMin"]),
                ClearFrac = Numero(s["clearFrac"]),
            };
        }

        /// <summary>
        /// Il sito da spedire al motore: la geometria da N.I.N.A., il resto da N.I.N.A.
        /// se uno strumento lo misura e dalla dichiarazione altrimenti. Ogni campo
        /// porta la propria provenienza; i campi che nessuno conosce restano nulli.
        /// </summary>
        /// <param name="letto">Quello che N.I.N.A. sa adesso.</param>
        /// <param name="dichiarato">Quello che ha scritto chi riprende.</param>
        public static SitoDiRipresa Unisci(SitoDiRipresa? letto, SitoDichiarato? dichiarato) {
            var p = new Dictionary<string, string>();
            var s = new SitoDiRipresa { Provenienza = p, Nome = letto?.Nome };

            /*  LA GEOMETRIA VIENE SOLO DA N.I.N.A. e non si dichiara: e' l'unico dato
             *  che ogni utente ha per forza inserito, ed e' anche l'unico su cui una
             *  dichiarazione sarebbe un doppione destinato a divergere dal profilo. */
            s.Lat = letto?.Lat; p["lat"] = s.Lat is null ? Assente : DaProfilo;
            s.Lon = letto?.Lon; p["lon"] = s.Lon is null ? Assente : DaProfilo;

            /*  Per gli altri vince la MISURA sulla dichiarazione, sempre: se un
             *  misuratore c'e', il numero scritto a mano l'anno scorso non deve
             *  sovrascriverlo. */
            s.Sqm = Scegli(letto?.Sqm, dichiarato?.Sqm, "sqm", p);
            s.Seeing = Scegli(letto?.Seeing, dichiarato?.Seeing, "seeing", p);
            s.Rms = Scegli(letto?.Rms, dichiarato?.Rms, "rms", p);

            /*  ALTEZZA MINIMA E NOTTI SERENE: solo dichiarate.
             *
             *  N.I.N.A. ha un orizzonte per AZIMUT, molto piu' ricco del numero solo
             *  che il motore accetta. Ridurlo a un numero e' una derivazione con
             *  perdita, e quale numero? Il massimo nasconde meta' cielo, il minimo fa
             *  riprendere dentro la casa. Finche' il motore non accetta un profilo di
             *  orizzonte, questa resta una dichiarazione — dirlo e' meglio che
             *  scegliere di nascosto quale meta' dell'orizzonte buttare via.
             *
             *  Le notti serene non le sa nessuno: nessuno strumento le misura. */
            s.HorizonMin = dichiarato?.HorizonMin;
            p["horizonMin"] = s.HorizonMin is null ? Assente : Dichiarato;
            s.ClearFrac = dichiarato?.ClearFrac;
            p["clearFrac"] = s.ClearFrac is null ? Assente : Dichiarato;

            return s;
        }

        /// <summary>
        /// Che cosa manca perche' il motore possa produrre una prescrizione completa, o
        /// null se non manca niente. Serve a dirlo PRIMA di chiedere: il servizio, con
        /// un sito incompleto, risponde con le ore e nessuna sequenza — e senza questa
        /// riga chi guarda vedrebbe un risultato vuoto senza sapere perche'.
        /// </summary>
        public static string? CheCosaManca(SitoDiRipresa? s) {
            var mancano = new List<string>();
            if (s?.Lat is null || s?.Lon is null)
                mancano.Add("le coordinate del sito, che vengono dal profilo di N.I.N.A.");
            if (s?.Sqm is null)
                mancano.Add("la qualita' del cielo (SQM): senza, il motore non sa quanto " +
                            "fondo cielo stai raccogliendo e non puo' decidere la posa");
            return mancano.Count == 0 ? null : "Manca " + string.Join("; ", mancano) + ".";
        }

        private static double? Scegli(double? misurato, double? dichiarato,
                                      string campo, Dictionary<string, string> p) {
            if (misurato is not null) { p[campo] = Misurato; return misurato; }
            if (dichiarato is not null) { p[campo] = Dichiarato; return dichiarato; }
            p[campo] = Assente;
            return null;
        }

        private static double? Numero(JsonNode? n) {
            if (n is null) return null;
            try {
                var v = n.GetValue<double>();
                return double.IsFinite(v) ? v : (double?)null;
            } catch (Exception) { return null; }
        }
    }
}
