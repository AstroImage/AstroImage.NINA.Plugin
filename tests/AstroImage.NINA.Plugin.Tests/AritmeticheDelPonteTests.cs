using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace AstroImage.NINA.Plugin.Tests {

    /*  IL PONTE NON FA CONTI SUI NUMERI CHE MOSTRA — la regola generalizzata (regia, 16
     *  settembre 2026).
     *
     *  Ogni numero a schermo arriva gia' fatto dal servizio: il Ponte non moltiplica, non divide e non combina
     *  due campi per ottenerlo. Contati il 16 settembre, i casi veri erano tre — le pose e le ore della notte nella pagina, le
     *  ore del blocco scartato nel montatore — piu' uno di confine. Due si tollerano, alla terza si generalizza: questa
     *  guardia cerca, nel codice della pagina e del C# del Ponte, ogni moltiplicazione, divisione, `reduce` e `Sum`.
     *  Commenti, testi ed espressioni regolari si tolgono prima di cercare; i testi interpolati del C# si tolgono interi,
     *  e un'operazione scritta dentro un testo non la vede — lo si dichiara.
     *
     *  LE ECCEZIONI SONO NOMINATE UNA PER UNA, col perche'. Un'eccezione senza motivo sarebbe un buco con un'etichetta.
     */
    [TestClass]
    public class AritmeticheDelPonteTests {

        /*  Il codice senza commenti, testi ed espressioni regolari; i caratteri tolti diventano spazi, e gli a capo
         *  restano, cosi' i numeri di riga sono quelli del file. */
        private static string Spoglia(string s, bool js) {
            var o = new StringBuilder(s.Length);
            void Vuota(int da, int a) { for (var k = da; k < a && k < s.Length; k++) o.Append(s[k] == '\n' ? '\n' : ' '); }
            var i = 0;
            var ultimo = '\0';
            while (i < s.Length) {
                var c = s[i];
                var n = i + 1 < s.Length ? s[i + 1] : '\0';
                if (c == '/' && n == '*') {
                    var f = s.IndexOf("*/", i + 2, StringComparison.Ordinal); f = f < 0 ? s.Length : f + 2;
                    Vuota(i, f); i = f; continue;
                }
                if (c == '/' && n == '/') {
                    var f = s.IndexOf('\n', i); f = f < 0 ? s.Length : f;
                    Vuota(i, f); i = f; continue;
                }
                if (c == '"' || c == '\'' || (js && c == '`')) {
                    var verbatim = !js && c == '"' && i > 0 && (s[i - 1] == '@' || (i > 1 && s[i - 1] == '$' && s[i - 2] == '@'));
                    var j = i + 1;
                    while (j < s.Length) {
                        if (!verbatim && s[j] == '\\') { j += 2; continue; }
                        if (s[j] == c) {
                            if (verbatim && j + 1 < s.Length && s[j + 1] == '"') { j += 2; continue; }
                            break;
                        }
                        if (s[j] == '\n' && !verbatim && c != '`') break;
                        j++;
                    }
                    var f = Math.Min(j + 1, s.Length);
                    Vuota(i, f); i = f; ultimo = 'x'; continue;
                }
                if (js && c == '/' && "(,=:!&|?{};".IndexOf(ultimo) >= 0) {
                    var j = i + 1; var classe = false;
                    while (j < s.Length && s[j] != '\n') {
                        if (s[j] == '\\') { j += 2; continue; }
                        if (s[j] == '[') classe = true;
                        else if (s[j] == ']') classe = false;
                        else if (s[j] == '/' && !classe) break;
                        j++;
                    }
                    j++;
                    while (j < s.Length && char.IsLetter(s[j])) j++;
                    Vuota(i, j); i = j; ultimo = 'x'; continue;
                }
                o.Append(c);
                if (!char.IsWhiteSpace(c)) ultimo = c;
                i++;
            }
            return o.ToString();
        }

        private static readonly Regex Operazione = new Regex(@"[\w\)\]]\s*[*/]\s*[\w\(]|\breduce\s*\(|\.Sum\s*\(");

        private static IEnumerable<string> Trovate(string nome, string codice, IEnumerable<(string Frammento, string Perche)> eccezioni) {
            foreach (var (frammento, _) in eccezioni) codice = codice.Replace(frammento, new string(' ', frammento.Length));
            foreach (Match m in Operazione.Matches(codice)) {
                var riga = codice.Take(m.Index).Count(ch => ch == '\n') + 1;
                var testo = codice.Split('\n')[riga - 1].Trim();
                yield return nome + ":" + riga + " «" + (testo.Length > 90 ? testo.Substring(0, 90) + "…" : testo) + "»";
            }
        }

        private static string Pagina() {
            var asm = typeof(global::AstroImage.NINA.Plugin.Services.DichiarazioneBanco).Assembly;
            using var s = asm.GetManifestResourceStream("AstroImage.NINA.Plugin.Views.Pagina.prova.js");
            Assert.IsNotNull(s, "prova.js non e' incorporata");
            using var r = new StreamReader(s, Encoding.UTF8);
            return r.ReadToEnd();
        }

        private static string CartellaDelPonte() {
            var d = new DirectoryInfo(Path.GetDirectoryName(typeof(AritmeticheDelPonteTests).Assembly.Location)!);
            while (d is not null && !Directory.Exists(Path.Combine(d.FullName, "src", "AstroImage.NINA.Plugin"))) d = d.Parent;
            Assert.IsNotNull(d, "non trovo src/AstroImage.NINA.Plugin sopra la cartella delle prove");
            return Path.Combine(d!.FullName, "src", "AstroImage.NINA.Plugin");
        }

        [TestMethod]
        public void LA_PAGINA_NON_RICAVA_UN_NUMERO_DA_CAMPI_DELLA_PRESCRIZIONE() {
            var eccezioni = new[] {
                ("r.corpo.length / 1024", "la dimensione della risposta in KB: un fatto del trasporto, non un campo della prescrizione"),
            };
            var trovate = Trovate("prova.js", Spoglia(Pagina(), js: true), eccezioni).ToList();
            Assert.AreEqual(0, trovate.Count,
                "il numero che la pagina mostra lo manda il motore: " + string.Join(" · ", trovate));
        }

        [TestMethod]
        public void IL_CSHARP_DEL_PONTE_NON_RICAVA_UN_NUMERO_DA_CAMPI_DELLA_PRESCRIZIONE() {
            var eccezioni = new Dictionary<string, (string, string)[]> {
                [Path.Combine("Services", "SitoDelProfilo.cs")] = new[] {
                    ("rmsPx * scala", "la guida che il guider di N.I.N.A. misura, da pixel a secondi d'arco: un campo che si manda al motore, non un numero della prescrizione") },
                [Path.Combine("Views", "PannelloStrategyView.xaml.cs")] = new[] {
                    ("ricetta.Blocchi.Sum(b => b.Pose)", "le pose consegnate, sommate su quello che il Ponte ha costruito: riportato alla regia, in attesa") },
            };
            var radice = CartellaDelPonte();
            var trovate = new List<string>();
            foreach (var f in Directory.GetFiles(radice, "*.cs", SearchOption.AllDirectories)) {
                var relativo = Path.GetRelativePath(radice, f);
                if (relativo.StartsWith("bin" + Path.DirectorySeparatorChar) || relativo.StartsWith("obj" + Path.DirectorySeparatorChar)) continue;
                var ecc = eccezioni.TryGetValue(relativo, out var e) ? e : Array.Empty<(string, string)>();
                trovate.AddRange(Trovate(relativo, Spoglia(File.ReadAllText(f), js: false), ecc));
            }
            Assert.AreEqual(0, trovate.Count,
                "il Ponte non fa conti sui numeri che mostra: " + string.Join(" · ", trovate));
        }

        /*  IL CONTROLLO DELLA GUARDIA: le tre forme che cerca si vedono davvero, e quelle tolte non si vedono. Senza, uno
         *  spogliatore che togliesse troppo renderebbe verdi le due prove sopra senza guardare niente. */
        [TestMethod]
        public void Controllo_LeOperazioniSiVedono_ECommentiTestiERegexNo() {
            const string js = "const ore = m.blocchi.reduce((a, b) => a + b.sec * b.n, 0) / 3600;\n" +
                              "// a * b\nconst t = 'a * b / c'; const re = /x\\/y/g; /* p / q */\n";
            var trovate = Trovate("prova", Spoglia(js, js: true), Array.Empty<(string, string)>()).ToList();
            Assert.AreEqual(3, trovate.Count, string.Join(" · ", trovate));
            Assert.IsTrue(trovate.All(x => x.StartsWith("prova:1 ")), "tutte sulla prima riga: " + string.Join(" · ", trovate));
            const string cs = "var ore = pose * secondi / 3600.0; var s = \"a * b\"; var t = @\"x / y\"; // p * q\nvar n = r.Blocchi.Sum(b => b.Pose);";
            Assert.AreEqual(3, Trovate("cs", Spoglia(cs, js: false), Array.Empty<(string, string)>()).Count());
        }
    }
}
