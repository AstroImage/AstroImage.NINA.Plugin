using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using AstroImage.NINA.Plugin.Localization;

#nullable enable

namespace AstroImage.NINA.Plugin.Services {

    /*  IL FILTRO, IN N.I.N.A. 3.3, SI SCEGLIE PER POSIZIONE (21 settembre 2026).
     *
     *  Il ponte imposta il filtro scegliendolo dalla ruota del profilo, e fino a oggi lo
     *  rileggeva per NOME. Ma nella 3.3 `SwitchFilter.Execute` non usa il filtro della
     *  tendina: a ruota collegata rivaluta l'espressione del filtro e prende il filtro
     *  alla POSIZIONE che l'espressione da'. Impostare la tendina scrive nella definizione
     *  il nome del filtro ripulito (lettere, cifre e trattino basso), e quel nome si
     *  risolve nella tabella dei simboli di N.I.N.A.:
     *
     *    - prima le variabili della sequenza, che vincono su tutto;
     *    - poi i simboli di qualunque origine — filtri, interruttori, meteo, montatura,
     *      altri plugin — e se il nome e' di piu' d'uno e' AMBIGUO e non si risolve;
     *    - i filtri stanno nella tabella con la loro posizione solo a ruota collegata;
     *      a ruota scollegata c'e' il nome, senza valore, e l'espressione vale 0;
     *    - due filtri del profilo che ripuliti danno lo stesso nome, senza distinguere
     *      maiuscole, diventano un simbolo solo: vince l'ultimo, in silenzio.
     *
     *  Misurato sul minix: a ruota collegata ULTIMATE si risolve in 2 e LPS_P2 IDAS in 0,
     *  entrambi giusti; a ruota scollegata il pannello del Sequencer mostrava «{Filtro_L}»
     *  accanto a «O», cioe' la posizione 0. Il rischio vero e' la ruota mono: nomi di una
     *  lettera, O, H, S, L, R, G, B, che un interruttore, un sensore o una variabile
     *  possono portare anche loro. Allora la sequenza passerebbe la rilettura per nome e
     *  riprenderebbe dalla posizione sbagliata, senza che nessuno se ne accorga.
     *
     *  QUINDI SI CHIEDE A N.I.N.A., non a una copia delle sue regole. La lettura prende
     *  per riflessione, dall'oggetto vero, la definizione, l'espressione rivalutata, la
     *  tabella dei simboli e la funzione con cui N.I.N.A. ripulisce i nomi; la decisione
     *  sta in una funzione a parte, che le prove esercitano con sosia della stessa forma.
     *  Sulla 3.2 il livello espressione non c'e': li' il filtro della tendina e' quello che
     *  scatta, e la rilettura per nome basta.
     */

    /// <summary>Che cosa N.I.N.A. dice del filtro appena impostato: una lettura, nessuna decisione.</summary>
    public sealed class LetturaDelFiltro {
        /// <summary>False sulla 3.2: il filtro non ha il livello espressione.</summary>
        public bool LivelloEspressione { get; init; }
        /// <summary>La definizione che N.I.N.A. ha scritto per il filtro.</summary>
        public string? Definizione { get; init; }
        /// <summary>Il nome del filtro ripulito da N.I.N.A.: la chiave con cui lo cerca.</summary>
        public string? Chiave { get; init; }
        /// <summary>La funzione con cui N.I.N.A. ripulisce un nome, per confrontare tutto il profilo.</summary>
        public Func<string, string?>? Ripulisci { get; init; }
        /// <summary>La tabella dei simboli si e' potuta interrogare.</summary>
        public bool TabellaLetta { get; init; }
        /// <summary>Le origini dei simboli che portano la chiave: nessuna, una sola, o piu' d'una (ambiguo).</summary>
        public IReadOnlyList<string> Origini { get; init; } = Array.Empty<string>();
        /// <summary>Il valore del simbolo del filtro: la posizione a ruota collegata, null a ruota scollegata.</summary>
        public double? PosizioneDelSimbolo { get; init; }
        /// <summary>Il valore dell'espressione rivalutata, solo quando il simbolo ha un valore.</summary>
        public double? PosizioneCalcolata { get; init; }
        /// <summary>Una variabile della sequenza porta la chiave, e vincerebbe sul filtro.</summary>
        public bool Variabile { get; init; }
        /// <summary>L'errore dell'espressione, se N.I.N.A. ne segnala uno: si scrive nel registro, non decide.</summary>
        public string? Errore { get; init; }

        public static readonly LetturaDelFiltro SenzaEspressione = new LetturaDelFiltro { LivelloEspressione = false };

        public override string ToString() =>
            !LivelloEspressione ? "livello espressione assente" :
            $"definizione={Definizione ?? "null"}, chiave={Chiave ?? "null"}, tabella={(TabellaLetta ? "letta" : "non letta")}, " +
            $"origini=[{string.Join(",", Origini)}], posizione del simbolo={Num(PosizioneDelSimbolo)}, " +
            $"posizione calcolata={Num(PosizioneCalcolata)}, variabile={Variabile}, errore={Errore ?? "nessuno"}";

        private static string Num(double? d) => d is null ? "nessuna" : d.Value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    public static class FiltroPerPosizione {

        public const string OrigineDeiFiltri = "Filter";

        /// <summary>
        /// Legge dall'oggetto «cambia filtro» che cosa N.I.N.A. risolvera' al momento di scattare. Per riflessione,
        /// sul tipo che l'oggetto ha davvero: un plugin compilato sulla 3.2 legge cosi' le proprieta' della 3.3.
        /// </summary>
        public static LetturaDelFiltro Leggi(object? cambioFiltro, string nome) {
            if (cambioFiltro is null) return LetturaDelFiltro.SenzaEspressione;
            var t = cambioFiltro.GetType();
            var pDefinizione = t.GetProperty("XfilterDefinition", BindingFlags.Public | BindingFlags.Instance);
            var pEspressione = t.GetProperty("XfilterExpression", BindingFlags.Public | BindingFlags.Instance);
            if (pDefinizione is null || pEspressione is null) return LetturaDelFiltro.SenzaEspressione;

            var definizione = Prova(() => pDefinizione.GetValue(cambioFiltro) as string);
            var espressione = Prova(() => pEspressione.GetValue(cambioFiltro));
            var tabella = Prova(() => Garanzia.Leggi(cambioFiltro, "SymbolBroker")) ?? Prova(() => Garanzia.Leggi(espressione, "SymbolBroker"));

            /*  La funzione con cui N.I.N.A. ripulisce i nomi: quella vera, nel suo assembly; se non si trova (i sosia
                delle prove), quella del tipo della tabella. Mai una copia della regola scritta qui. */
            var ripulitore = t.Assembly.GetType("NINA.Sequencer.Logic.SymbolBroker")?.GetMethod("SanitizeIdentifier", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null)
                          ?? tabella?.GetType().GetMethod("SanitizeIdentifier", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
            Func<string, string?>? ripulisci = ripulitore is null ? null : (s => Prova(() => ripulitore.Invoke(null, new object?[] { s }) as string));
            var chiave = ripulisci?.Invoke(nome);

            // l'espressione si rivaluta come fa Execute, prima di leggerla
            Prova(() => { espressione?.GetType().GetMethod("Evaluate", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(bool) }, null)?.Invoke(espressione, new object[] { true }); return (object?)null; });
            var errore = Prova(() => Garanzia.Leggi(espressione, "Error") as string);
            double? valore = Prova(() => Numero(Garanzia.Leggi(espressione, "Value")));
            var variabile = chiave is not null && Prova(() => Garanzia.Leggi(espressione, "Resolved") is IDictionary d && d.Contains(chiave) && d[chiave] is not null);

            // la tabella dei simboli: chi porta quel nome, e con quale valore
            var origini = new List<string>();
            double? posizioneDelSimbolo = null;
            var cerca = tabella?.GetType().GetMethod("TryGetSymbol", BindingFlags.Public | BindingFlags.Instance);
            var letta = false;
            if (cerca is not null && chiave is not null) {
                try {
                    var argomenti = new object?[] { chiave, null };
                    var trovato = cerca.Invoke(tabella, argomenti) is bool b && b;
                    var simbolo = argomenti[1];
                    letta = true;
                    if (Garanzia.Leggi(simbolo, "Symbols") is IEnumerable piu) {
                        foreach (var s in piu) origini.Add(Garanzia.Leggi(s, "Category") as string ?? "?");
                    } else if (trovato && simbolo is not null) {
                        origini.Add(Garanzia.Leggi(simbolo, "Category") as string ?? "?");
                        posizioneDelSimbolo = Numero(Garanzia.Leggi(simbolo, "Value"));
                    }
                } catch (Exception) { letta = false; }
            }

            return new LetturaDelFiltro {
                LivelloEspressione = true, Definizione = definizione, Chiave = chiave, Ripulisci = ripulisci,
                TabellaLetta = letta, Origini = origini, PosizioneDelSimbolo = posizioneDelSimbolo,
                PosizioneCalcolata = posizioneDelSimbolo is null ? null : valore, Variabile = variabile, Errore = errore,
            };
        }

        /// <summary>
        /// La decisione: il filtro scelto sara' quello che scatta? Se no, <paramref name="perCheNo"/> dice perche' e
        /// il blocco non si consegna. Tutto quello che si controlla vale anche a ruota scollegata — i nomi stanno nel
        /// profilo e nella tabella —; a ruota collegata si rilegge in piu' la posizione.
        /// </summary>
        public static bool Verifica(string nome, int posizione, IReadOnlyList<(string Nome, int Posizione)> profilo,
                                    LetturaDelFiltro lettura, out string? perCheNo) {
            perCheNo = null;
            // la 3.2: il filtro della tendina e' quello che scatta, e il nome e' gia' stato riletto
            if (!lettura.LivelloEspressione) return true;

            if (lettura.Chiave is null || lettura.Ripulisci is null) {
                perCheNo = Loc.F("Filtro33_SenzaChiave", nome);
                return false;
            }

            // due filtri del profilo che per N.I.N.A. diventano lo stesso nome: ne terrebbe uno solo, l'ultimo
            var omonimi = profilo.Where(f => string.Equals(lettura.Ripulisci(f.Nome), lettura.Chiave, StringComparison.OrdinalIgnoreCase))
                                 .Select(f => "«" + f.Nome + "»").ToList();
            if (omonimi.Count > 1) {
                perCheNo = Loc.F("Filtro33_Doppio", nome, string.Join(", ", omonimi), lettura.Chiave);
                return false;
            }

            if (!string.Equals(lettura.Definizione, lettura.Chiave, StringComparison.Ordinal)) {
                perCheNo = Loc.F("Filtro33_Definizione", nome, lettura.Definizione ?? "", lettura.Chiave);
                return false;
            }

            if (lettura.Variabile) {
                perCheNo = Loc.F("Filtro33_Variabile", nome, lettura.Chiave);
                return false;
            }

            if (!lettura.TabellaLetta) {
                perCheNo = Loc.F("Filtro33_SenzaTabella", nome);
                return false;
            }
            if (lettura.Origini.Count == 0) {
                perCheNo = Loc.F("Filtro33_NonRisolto", nome, lettura.Chiave);
                return false;
            }
            if (lettura.Origini.Count > 1) {
                perCheNo = Loc.F("Filtro33_Ambiguo", nome, lettura.Chiave,
                                 string.Join(", ", lettura.Origini.Where(o => o != OrigineDeiFiltri).DefaultIfEmpty(OrigineDeiFiltri)));
                return false;
            }
            if (lettura.Origini[0] != OrigineDeiFiltri) {
                perCheNo = Loc.F("Filtro33_NonUnFiltro", nome, lettura.Chiave, lettura.Origini[0]);
                return false;
            }

            // a ruota collegata: la posizione del simbolo, e quella che l'espressione rivalutata calcola
            foreach (var letta in new[] { lettura.PosizioneDelSimbolo, lettura.PosizioneCalcolata }) {
                if (letta is double p && Math.Abs(p - posizione) > 1e-9) {
                    perCheNo = Loc.F("Filtro33_Posizione", nome, p.ToString("0", CultureInfo.InvariantCulture), posizione);
                    return false;
                }
            }
            return true;
        }

        private static double? Numero(object? v) {
            if (v is null) return null;
            try {
                var d = Convert.ToDouble(v, CultureInfo.InvariantCulture);
                return double.IsNaN(d) ? null : d;
            } catch (Exception) { return null; }
        }

        private static T? Prova<T>(Func<T?> f) {
            try { return f(); } catch (Exception) { return default; }
        }
    }
}
