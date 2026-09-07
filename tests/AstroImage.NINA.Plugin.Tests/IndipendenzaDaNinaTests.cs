using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AstroImage.NINA.Plugin.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace AstroImage.NINA.Plugin.Tests {

    /*  IL CRITERIO DI ACCETTAZIONE, TRASFORMATO IN VERIFICA.
     *
     *  «Il Bridge deve poter ricevere il SequenceModel prodotto da Strategy senza
     *  conoscere nulla del motore scientifico» -- e, dall'altro lato, il modello deve
     *  poter esistere senza N.I.N.A. Detto cosi' e' un'intenzione. Qui diventa una
     *  cosa che fallisce.
     *
     *  La prova piu' netta e' che questi test GIRANO SENZA UNA SOLA DLL DI N.I.N.A.
     *  accanto: il progetto di test non dichiara il pacchetto NINA.Plugin, quindi il
     *  test host non ha da nessuna parte NINA.Core, NINA.Sequencer e compagnia. Se il
     *  modello ne toccasse anche solo un tipo, questi test non partirebbero proprio.
     */
    [TestClass]
    public class IndipendenzaDaNinaTests {

        private const string SPAZIO = "AstroImage.NINA.Plugin.Models";

        /*  DUE INSIEMI, E LA DIFFERENZA E' COSTATA UN BUCO.
         *
         *  `TipiDelContratto` sono i tipi che descrivono la forma del modello, e stanno
         *  nel namespace esatto. `TipiSottoModels` comprende anche i sotto-namespace,
         *  e serve alle guardie sulla purezza: quando e' nato Models/Json/ con il
         *  convertitore degli istanti, il filtro sul namespace ESATTO ha smesso di
         *  vedere l'unico codice eseguibile della cartella. Provato: mettendo dentro
         *  quel tipo un CalcolaQualcosa() e una proprieta' calcolata, tutte e quattro
         *  le guardie restavano verdi.
         *
         *  Il convertitore ha diritto a Read e Write: sono i due metodi che la classe
         *  base gli impone, non logica che qualcuno ha aggiunto. Tutto il resto no. */
        private static Type[] TipiDelContratto() =>
            TipiCaricati().Where(t => t.Namespace == SPAZIO).ToArray();

        private static Type[] TipiSottoModels() =>
            TipiCaricati().Where(t => t.Namespace == SPAZIO ||
                                      (t.Namespace != null && t.Namespace.StartsWith(SPAZIO + ".",
                                                                StringComparison.Ordinal))).ToArray();

        private static bool EUnConvertitore(Type t) {
            for (var b = t.BaseType; b != null; b = b.BaseType)
                if (b.Namespace == "System.Text.Json.Serialization" && b.Name.StartsWith("JsonConverter"))
                    return true;
            return false;
        }

        /*  L'assembly contiene ANCHE la classe del plugin, che da N.I.N.A. dipende
         *  eccome: eredita da PluginBase. Senza le DLL di N.I.N.A. quel tipo non si
         *  carica, e GetTypes() solleva. Non e' un guasto del test: e' la
         *  dimostrazione. Si raccolgono i tipi che si sono caricati lo stesso, e i
         *  nostri devono essere tutti li'. */
        private static Type[] TipiCaricati() {
            var asm = typeof(SequenceModel).Assembly;
            Type?[] tutti;
            try { tutti = asm.GetTypes(); }
            catch (ReflectionTypeLoadException e) {
                /*  E QUI STAVA LA FALLA, che e' il modo classico in cui una guardia
                 *  smette di fare la guardia senza dirlo.
                 *
                 *  Un tipo che nomina N.I.N.A. NON SI CARICA, quindi arriva nullo.
                 *  Buttare via i nulli senza contarli vuol dire che il tipo colpevole
                 *  sparisce prima di essere esaminato: i controlli qui sotto rivedono
                 *  gli stessi tipi innocenti di sempre e restano verdi PROPRIO sulla
                 *  regressione per cui esistono. Provato: aggiungendo a Models una
                 *  classe che eredita da PluginBase, tutti e quattro i test passavano.
                 *
                 *  Un nullo solo e' atteso ed e' la classe del plugin, che da N.I.N.A.
                 *  dipende per mestiere. Il secondo e' la notizia. */
                Assert.AreEqual(1, e.Types.Count(t => t is null),
                    "oltre alla classe del plugin, un altro tipo di questo assembly non si e' " +
                    "caricato senza N.I.N.A.: " +
                    string.Join("; ", e.LoaderExceptions.Select(x => x?.Message)));
                tutti = e.Types;
            }
            return tutti.Where(t => t is not null).Select(t => t!).ToArray();
        }

        [TestMethod]
        public void IlModelloSiCaricaAncheSenzaNina() {
            var tipi = TipiDelContratto();
            /*  Un numero esatto e non un minimo: cosi' il test si accorge sia di un
             *  tipo che sparisce sia di uno che compare senza che nessuno lo abbia
             *  raccontato qui. Aggiungerne uno lecito costa una riga da aggiornare,
             *  ed e' il prezzo giusto per una soglia che non si puo' aggirare. */
            Assert.AreEqual(9, tipi.Length,
                "il modello deve avere esattamente questi tipi: SequenceModel, Quando, Bersaglio, " +
                "Offset, Ottica, Sito, Capacita, Blocco, Dither. Trovati: " +
                string.Join(", ", tipi.Select(t => t.Name)));
            foreach (var atteso in new[] { "SequenceModel", "Quando", "Bersaglio", "Offset",
                                           "Ottica", "Sito", "Capacita", "Blocco", "Dither" }) {
                Assert.IsTrue(tipi.Any(t => t.Name == atteso), $"{atteso} non si e' caricato");
            }
        }

        [TestMethod]
        public void NessunTipoDelModelloNominaNina() {
            var colpevoli = new List<string>();

            foreach (var t in TipiSottoModels()) {
                /*  La classe base di un convertitore VIENE da System.Text.Json e non da
                 *  N.I.N.A., quindi passa: qui si cerca N.I.N.A., non l'ereditarieta'. */
                Ispeziona(t.BaseType, $"{t.Name} (classe base)", colpevoli);
                foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance |
                                                  BindingFlags.Static | BindingFlags.DeclaredOnly))
                    Ispeziona(p.PropertyType, $"{t.Name}.{p.Name}", colpevoli);
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance |
                                              BindingFlags.Static | BindingFlags.DeclaredOnly))
                    Ispeziona(f.FieldType, $"{t.Name}.{f.Name}", colpevoli);
                foreach (var mth in t.GetMethods(BindingFlags.Public | BindingFlags.Instance |
                                                 BindingFlags.Static | BindingFlags.DeclaredOnly)) {
                    Ispeziona(mth.ReturnType, $"{t.Name}.{mth.Name}() (ritorno)", colpevoli);
                    foreach (var par in mth.GetParameters())
                        Ispeziona(par.ParameterType, $"{t.Name}.{mth.Name}({par.Name})", colpevoli);
                }
            }

            Assert.AreEqual(0, colpevoli.Count,
                "il modello ha smesso di essere indipendente da N.I.N.A.:\n  " +
                string.Join("\n  ", colpevoli));
        }

        /// <summary>
        /// Guarda un tipo e, se e' generico, anche cio' che contiene: un
        /// <c>List&lt;QualcosaDiNina&gt;</c> nasconde la dipendenza dentro il
        /// parametro, dove un controllo sul solo nome del tipo non la vedrebbe.
        /// </summary>
        private static void Ispeziona(Type? t, string dove, List<string> colpevoli) {
            if (t is null) return;
            if (t.IsArray) { Ispeziona(t.GetElementType(), dove, colpevoli); return; }
            if (t.IsGenericType)
                foreach (var arg in t.GetGenericArguments()) Ispeziona(arg, dove, colpevoli);

            var asm = t.Assembly.GetName().Name ?? "";
            if (asm.StartsWith("NINA", StringComparison.OrdinalIgnoreCase) ||
                asm.StartsWith("ASCOM", StringComparison.OrdinalIgnoreCase))
                colpevoli.Add($"{dove} -> {t.FullName} (da {asm})");
        }

        [TestMethod]
        public void AccantoAiTestNonC_eNessunaDllDiNina() {
            /*  Il controllo che rende vero tutto il resto. Se un giorno qualcuno
             *  aggiungesse il pacchetto NINA.Plugin a questo progetto di test "per
             *  comodita'", i test sopra continuerebbero a passare ma non
             *  dimostrerebbero piu' niente: girerebbero in un mondo dove N.I.N.A.
             *  c'e'. Questo test si accorge del cambio di mondo. */
            var cartella = Path.GetDirectoryName(typeof(IndipendenzaDaNinaTests).Assembly.Location)!;
            var estranee = Directory.EnumerateFiles(cartella, "*.dll")
                .Select(Path.GetFileName)
                .Where(n => n!.StartsWith("NINA.", StringComparison.OrdinalIgnoreCase))
                .ToList();

            Assert.AreEqual(0, estranee.Count,
                "questi test girano con N.I.N.A. accanto, quindi non provano piu' l'indipendenza: " +
                string.Join(", ", estranee));
        }

        [TestMethod]
        public void IlModelloNonSaFareNiente() {
            /*  «Un modello che sa fare qualcosa non e' un modello» -- sta scritto nel
             *  README della cartella Models, ed e' il confine di questo progetto.
             *  Gli unici metodi ammessi sono quelli della serializzazione, che non
             *  decidono niente: leggono e scrivono la stessa cosa. Se domani comparisse
             *  un CalcolaOre() o un ConvertiInSequenza(), questo test lo ferma prima
             *  che diventi un'abitudine. */
            var ammessi = new HashSet<string> { "Leggi", "Scrivi" };
            var diConvertitore = new HashSet<string> { "Read", "Write", "CanConvert" };
            var trovati = new List<string>();

            foreach (var t in TipiSottoModels()) {
                /*  Un convertitore ha diritto ai metodi che la classe base gli impone,
                 *  e a nessun altro: se ne comparisse un terzo, sarebbe logica messa
                 *  nel posto dove nessuno la cerca. */
                var leciti = EUnConvertitore(t) ? diConvertitore : ammessi;
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance |
                                               BindingFlags.Static | BindingFlags.DeclaredOnly))
                    if (!m.IsSpecialName && !leciti.Contains(m.Name))
                        trovati.Add($"{t.Name}.{m.Name}()");
            }

            Assert.AreEqual(0, trovati.Count,
                "nel modello e' comparso un metodo che non e' serializzazione:\n  " +
                string.Join("\n  ", trovati));
        }

        [TestMethod]
        public void NessunaProprietaCalcolata() {
            /*  LA PORTA DI SERVIZIO del test qui sopra.
             *
             *  Un controllo sui soli metodi non vede `public double OreTotali =>
             *  Blocchi.Sum(b => b.Sec * b.N) / 3600;`: e' una proprieta', non un
             *  metodo, e passerebbe liscia. Sarebbe pero' esattamente cio' che qui
             *  non deve esistere -- un calcolo -- e per giunta un calcolo che il
             *  motore fa gia', quindi due risposte alla stessa domanda.
             *
             *  Le proprieta' di un contratto si leggono E si scrivono, perche' devono
             *  poter tornare indietro. Una senza scrittura e' per forza derivata da
             *  altre: e' la firma del calcolo, e si riconosce senza ambiguita'. */
            var calcolate = TipiSottoModels()
                .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance |
                                                 BindingFlags.DeclaredOnly)
                                  .Where(p => !p.CanWrite)
                                  .Select(p => $"{t.Name}.{p.Name}"))
                .ToList();

            Assert.AreEqual(0, calcolate.Count,
                "una proprieta' senza scrittura nel modello e' un valore derivato, " +
                "cioe' un calcolo travestito:\n  " + string.Join("\n  ", calcolate));
        }
    }
}
