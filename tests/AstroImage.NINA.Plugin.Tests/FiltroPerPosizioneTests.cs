using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AstroImage.NINA.Plugin.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace AstroImage.NINA.Plugin.Tests {

    /*  IL FILTRO CHE SCATTA E' QUELLO DELLA POSIZIONE, NON QUELLO DELLA TENDINA (21 settembre 2026).
     *
     *  Nella 3.3 `SwitchFilter` sceglie il filtro rivalutando un'espressione: la definizione e' il nome del filtro
     *  ripulito, e il nome si risolve nella tabella dei simboli di N.I.N.A. Il ponte rileggeva il nome della
     *  tendina, che e' sempre giusto; la posizione che scatta puo' non esserlo.
     *
     *  Qui non c'e' N.I.N.A.: ci sono sosia con la FORMA della 3.3 — lo stesso nome delle proprieta', la stessa
     *  tabella con i simboli di ogni origine, la stessa regola con cui i nomi si ripuliscono (ricopiata da
     *  `SymbolBroker.SanitizeIdentifier` della 3.3.0.1058, e dichiarata qui come sosia, non come verita': nel
     *  plugin si chiama la funzione vera). La ruota e' quella mono di chi riprende, coi nomi veri e le posizioni
     *  vere lette dal profilo con l'Advanced API: L R G B S H O, da 0 a 6.
     */
    [TestClass]
    public class FiltroPerPosizioneTests {

        private static readonly (string Nome, int Posizione)[] RuotaMono =
            { ("L", 0), ("R", 1), ("G", 2), ("B", 3), ("S", 4), ("H", 5), ("O", 6) };

        /* ── i sosia della 3.3 ─────────────────────────────────────────────────────────────────────── */

        public sealed class Simbolo {
            public string Key { get; set; } = "";
            public object? Value { get; set; }
            public string Category { get; set; } = "";
        }
        public sealed class SimboloAmbiguo {
            public string Key { get; set; } = "";
            public Simbolo[] Symbols { get; set; } = Array.Empty<Simbolo>();
        }

        /// <summary>La tabella dei simboli: un nome, uno o piu' simboli di origini diverse.</summary>
        public sealed class Tabella {
            public readonly Dictionary<string, List<Simbolo>> Dati = new Dictionary<string, List<Simbolo>>();

            /// <summary>Sosia di SymbolBroker.SanitizeIdentifier della 3.3.</summary>
            public static string SanitizeIdentifier(string str) {
                if (str == null) return "__Null__";
                if (str.Length == 0) return "__Empty__";
                var sb = new StringBuilder();
                for (int i = 0; i < str.Length; i++) {
                    char c = str[i];
                    bool lettera = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
                    bool cifra = c >= '0' && c <= '9';
                    if (i == 0) { if (lettera || c == '_') sb.Append(c); else if (cifra) { sb.Append('_'); sb.Append(c); } else sb.Append('_'); }
                    else if (lettera || cifra || c == '_') sb.Append(c); else sb.Append('_');
                }
                return sb.ToString();
            }

            public void Aggiungi(string origine, string nome, object? valore) {
                if (!Dati.TryGetValue(nome, out var l)) Dati[nome] = l = new List<Simbolo>();
                l.RemoveAll(s => s.Category == origine);
                l.Add(new Simbolo { Key = nome, Value = valore, Category = origine });
            }

            /// <summary>Come la 3.3 mette i filtri del profilo: ripuliti, fusi senza distinguere maiuscole, l'ultimo vince.</summary>
            public void Filtri(IEnumerable<(string Nome, int Posizione)> profilo, bool collegata) {
                foreach (var g in profilo.GroupBy(f => SanitizeIdentifier(f.Nome), StringComparer.OrdinalIgnoreCase))
                    Aggiungi(FiltroPerPosizione.OrigineDeiFiltri, SanitizeIdentifier(g.Last().Nome), collegata ? (object?)g.Last().Posizione : null);
            }

            public bool TryGetSymbol(string key, out object? symbol) {
                symbol = null;
                if (!Dati.TryGetValue(key, out var l) || l.Count == 0) return false;
                if (l.Count == 1) { symbol = l[0]; return true; }
                symbol = new SimboloAmbiguo { Key = key, Symbols = l.ToArray() };
                return false;
            }
        }

        /// <summary>L'espressione del filtro: si rivaluta sulla tabella, e le variabili della sequenza vincono.</summary>
        public sealed class Espressione {
            public Tabella? SymbolBroker { get; set; }
            public string Definition { get; set; } = "";
            public string? Error { get; private set; }
            public double Value { get; private set; }
            public Dictionary<string, object?> Resolved { get; } = new Dictionary<string, object?>();
            public Dictionary<string, double> Variabili { get; } = new Dictionary<string, double>();

            public void Evaluate(bool ignoreRoot) {
                Error = null; Resolved.Clear();
                if (Variabili.TryGetValue(Definition, out var v)) { Resolved[Definition] = v; Value = v; return; }
                if (SymbolBroker is null) { Error = "Undefined: " + Definition; Value = 0; return; }
                if (SymbolBroker.TryGetSymbol(Definition, out var s)) { Value = s is Simbolo x && x.Value is int p ? p : 0; return; }
                Error = s is SimboloAmbiguo ? "'" + Definition + "' is ambiguous" : "Undefined: " + Definition;
                Value = 0;
            }
        }

        /// <summary>Il «cambia filtro» della 3.3: impostare il filtro scrive nella definizione il nome ripulito.</summary>
        public sealed class CambiaFiltro33 {
            public Espressione XfilterExpression { get; } = new Espressione();
            public string XfilterDefinition { get => XfilterExpression.Definition; set => XfilterExpression.Definition = value; }
            public Tabella? SymbolBroker { get; set; }
            public string? Filter { get; private set; }
            public void Imposta(string nome) { Filter = nome; XfilterDefinition = Tabella.SanitizeIdentifier(nome); }
        }

        /// <summary>Il «cambia filtro» della 3.2: il filtro e basta.</summary>
        public sealed class CambiaFiltro32 {
            public string? Filter { get; set; }
        }

        /* ── la scena ──────────────────────────────────────────────────────────────────────────────── */

        private static CambiaFiltro33 Scena(string nome, bool collegata, (string Nome, int Posizione)[]? profilo = null,
                                            Action<Tabella>? altro = null, Action<Espressione>? variabili = null) {
            var tabella = new Tabella();
            tabella.Filtri(profilo ?? RuotaMono, collegata);
            altro?.Invoke(tabella);
            var cf = new CambiaFiltro33 { SymbolBroker = tabella };
            cf.XfilterExpression.SymbolBroker = tabella;
            variabili?.Invoke(cf.XfilterExpression);
            cf.Imposta(nome);
            return cf;
        }

        private static bool Consegna(string nome, CambiaFiltro33 cf, out string? perCheNo, (string Nome, int Posizione)[]? profilo = null) {
            var p = profilo ?? RuotaMono;
            var lettura = FiltroPerPosizione.Leggi(cf, nome);
            return FiltroPerPosizione.Verifica(nome, p.First(f => f.Nome == nome).Posizione, p, lettura, out perCheNo);
        }

        /* ── cio' che deve passare ─────────────────────────────────────────────────────────────────── */

        [TestMethod]
        public void LA_RUOTA_MONO_VERA_SI_RISOLVE_OGNI_NOME_UNA_VOLTA_SOLA_E_SI_CONSEGNA() {
            foreach (var collegata in new[] { false, true })
                foreach (var (nome, _) in RuotaMono) {
                    Assert.IsTrue(Consegna(nome, Scena(nome, collegata), out var perche),
                        $"{nome}, ruota {(collegata ? "collegata" : "scollegata")}: rifiutato — {perche}");
                    Assert.IsNull(perche);
                }
        }

        /*  Il caso del primo screenshot: ruota scollegata, la definizione «O», l'espressione che vale 0 e il Sequencer
         *  che mostra «{Filtro_L}». Non e' un rifiuto: al momento di scattare la ruota e' collegata e l'espressione si
         *  rivaluta. Un rifiuto falso insegna a ignorare i rifiuti. */
        [TestMethod]
        public void IL_FILTRO_L_DELLA_RUOTA_SCOLLEGATA_NON_E_UN_RIFIUTO() {
            var cf = Scena("O", collegata: false);
            var lettura = FiltroPerPosizione.Leggi(cf, "O");
            Assert.AreEqual("O", lettura.Definizione);
            Assert.IsNull(lettura.PosizioneDelSimbolo, "a ruota scollegata il simbolo del filtro non ha valore");
            Assert.IsNull(lettura.PosizioneCalcolata, "e la posizione calcolata (0) non si legge come una posizione");
            Assert.IsTrue(FiltroPerPosizione.Verifica("O", 6, RuotaMono, lettura, out var perche), perche);
        }

        [TestMethod]
        public void SULLA_3_2_NON_C_E_IL_LIVELLO_ESPRESSIONE_E_IL_NOME_BASTA() {
            var lettura = FiltroPerPosizione.Leggi(new CambiaFiltro32 { Filter = "H" }, "H");
            Assert.IsFalse(lettura.LivelloEspressione);
            Assert.IsTrue(FiltroPerPosizione.Verifica("H", 5, RuotaMono, lettura, out var perche), perche);
        }

        /* ── cio' che deve cadere, coi nomi veri della ruota mono ──────────────────────────────────── */

        [TestMethod]
        public void UN_INTERRUTTORE_CHIAMATO_H_RENDE_AMBIGUO_IL_FILTRO_H_E_NON_SI_CONSEGNA() {
            var cf = Scena("H", collegata: true, altro: t => t.Aggiungi("Switch", "H", 1.0));
            Assert.IsFalse(Consegna("H", cf, out var perche), "il filtro H, ambiguo con un interruttore, e' passato");
            StringAssert.Contains(perche, "Switch");
            StringAssert.Contains(perche, "«H»");
        }

        [TestMethod]
        public void UNA_VARIABILE_DELLA_SEQUENZA_CHIAMATA_S_VINCE_SUL_FILTRO_S_E_NON_SI_CONSEGNA() {
            var cf = Scena("S", collegata: true, variabili: e => e.Variabili["S"] = 2);
            Assert.IsFalse(Consegna("S", cf, out var perche), "una variabile S avrebbe mandato la ruota in posizione 2");
            StringAssert.Contains(perche, "variabile");
        }

        [TestMethod]
        public void UN_NOME_CHE_N_I_N_A_NON_CONOSCE_NON_SI_CONSEGNA() {
            var cf = Scena("O", collegata: true, profilo: RuotaMono.Where(f => f.Nome != "O").ToArray());
            Assert.IsFalse(Consegna("O", cf, out var perche), "O non e' nella tabella, eppure e' passato");
            StringAssert.Contains(perche, "«O»");
        }

        [TestMethod]
        public void UN_NOME_CHE_PER_N_I_N_A_NON_E_UN_FILTRO_NON_SI_CONSEGNA() {
            var cf = Scena("G", collegata: true, profilo: RuotaMono.Where(f => f.Nome != "G").ToArray(), altro: t => t.Aggiungi("Weather", "G", 9.81));
            Assert.IsFalse(Consegna("G", cf, out var perche), "G e' un simbolo del meteo, eppure e' passato come filtro");
            StringAssert.Contains(perche, "Weather");
        }

        /*  A ruota collegata si rilegge anche la posizione che l'espressione calcola: deve essere quella del filtro
         *  scelto. Qui la tabella dice 0 per O — il caso in cui la ruota riprenderebbe dalla L. */
        [TestMethod]
        public void A_RUOTA_COLLEGATA_LA_POSIZIONE_CALCOLATA_DEVE_ESSERE_QUELLA_DEL_FILTRO() {
            var cf = Scena("O", collegata: true, altro: t => t.Aggiungi(FiltroPerPosizione.OrigineDeiFiltri, "O", 0));
            Assert.IsFalse(Consegna("O", cf, out var perche), "la ruota sarebbe andata in posizione 0, eppure e' passato");
            StringAssert.Contains(perche, "0");
            StringAssert.Contains(perche, "6");
        }

        /*  Due filtri che ripuliti danno lo stesso nome — senza distinguere maiuscole — la 3.3 li fonde in silenzio e
         *  tiene l'ultimo: per il primo la posizione sarebbe sbagliata anche a ruota collegata. */
        [TestMethod]
        public void DUE_FILTRI_DEL_PROFILO_CHE_DIVENTANO_LO_STESSO_NOME_NON_SI_CONSEGNANO() {
            var profilo = RuotaMono.Take(5).Concat(new[] { ("Ha", 5), ("HA", 7) }).ToArray();
            var cf = Scena("Ha", collegata: false, profilo: profilo);
            Assert.IsFalse(Consegna("Ha", cf, out var perche, profilo), "Ha e HA diventano un nome solo, eppure e' passato");
            StringAssert.Contains(perche, "«Ha»");
            StringAssert.Contains(perche, "«HA»");
        }

        [TestMethod]
        public void LA_DEFINIZIONE_SCRITTA_DA_N_I_N_A_DEVE_ESSERE_IL_NOME_RIPULITO() {
            var cf = Scena("O", collegata: true);
            cf.XfilterDefinition = "L";
            Assert.IsFalse(Consegna("O", cf, out var perche), "la definizione dice L, eppure e' passato");
            StringAssert.Contains(perche, "«L»");
        }

        [TestMethod]
        public void SENZA_LA_TABELLA_DEI_SIMBOLI_NON_SI_SA_E_NON_SI_CONSEGNA() {
            var cf = Scena("O", collegata: true);
            cf.SymbolBroker = null; cf.XfilterExpression.SymbolBroker = null;
            var lettura = FiltroPerPosizione.Leggi(cf, "O");
            Assert.IsFalse(lettura.TabellaLetta);
            Assert.IsFalse(FiltroPerPosizione.Verifica("O", 6, RuotaMono, lettura, out var perche), "senza tabella e' passato");
            Assert.IsNotNull(perche);
        }
    }
}
