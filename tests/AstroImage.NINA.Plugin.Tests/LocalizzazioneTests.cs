using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Text.RegularExpressions;
using AstroImage.NINA.Plugin.Localization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace AstroImage.NINA.Plugin.Tests {

    /*  LE DUE LINGUE DEVONO RESTARE UNA COPIA L'UNA DELL'ALTRA.
     *
     *  Il difetto che questi test aspettano non e' una traduzione brutta: e' una chiave
     *  aggiunta all'italiano e dimenticata nell'inglese. Non da' nessun errore, non
     *  rompe la compilazione, e si presenta a un utente straniero come una frase in
     *  italiano in mezzo alla pagina — oppure, se manca in tutte e due, come il nome
     *  della chiave. Con 81 voci per lingua, «lo ricontrollo a mano» dura un mese.
     *
     *  E I SEGNAPOSTO CONTANO QUANTO LE PAROLE: un {2} che nell'inglese non c'e' fa
     *  sparire un numero dal messaggio, e un {3} in piu' fa saltare string.Format. Il
     *  ripiego di Loc.F evita che la consegna cada, ma il messaggio uscirebbe con i
     *  segnaposto scritti in chiaro. Meglio accorgersene qui.
     */
    [TestClass]
    public class LocalizzazioneTests {

        /*  L'ITALIANO SI RIMETTE SEMPRE, E NON SOLO DOVE SERVE.
         *
         *  Loc e' un oggetto solo per tutta la suite, e quasi duecento asserzioni
         *  guardano dentro il testo italiano. Una prova che se ne andasse lasciando
         *  acceso l'inglese le farebbe cadere tutte — e il rosso comparirebbe su prove
         *  che non c'entrano niente, in un ordine che MSTest non garantisce nemmeno.
         *  Qui e' stato vero per un paio di giri: verde per fortuna, non per costruzione.
         *  Una pulizia che vale per ogni metodo toglie il problema alla radice. */
        [TestCleanup]
        public void Dopo() => Loc.Instance.ForzaLingua("it");

        private static ResourceManager Res(string lingua) =>
            new ResourceManager("AstroImage.NINA.Plugin.Localization.Strings_" + lingua,
                                typeof(Loc).Assembly);

        private static Dictionary<string, string> Tutte(string lingua) {
            var rm = Res(lingua);
            var set = rm.GetResourceSet(CultureInfo.InvariantCulture, true, true);
            Assert.IsNotNull(set, "le risorse " + lingua + " non sono dentro la DLL");
            var d = new Dictionary<string, string>();
            foreach (System.Collections.DictionaryEntry e in set!)
                d[(string)e.Key] = (string)(e.Value ?? "");
            return d;
        }

        [TestMethod]
        public void LeDueLingueHannoLeSTESSEChiavi() {
            var it = Tutte("it");
            var en = Tutte("en");
            Assert.AreNotEqual(0, it.Count, "il file italiano e' vuoto");

            var soloIt = it.Keys.Except(en.Keys).OrderBy(x => x).ToList();
            var soloEn = en.Keys.Except(it.Keys).OrderBy(x => x).ToList();
            Assert.AreEqual(0, soloIt.Count, "chiavi che esistono solo in italiano: " + string.Join(", ", soloIt));
            Assert.AreEqual(0, soloEn.Count, "chiavi che esistono solo in inglese: " + string.Join(", ", soloEn));
            Assert.AreEqual(it.Count, en.Count);
        }

        [TestMethod]
        public void NessunaVoceVuota() {
            foreach (var lingua in new[] { "it", "en" })
                foreach (var v in Tutte(lingua))
                    Assert.IsFalse(string.IsNullOrWhiteSpace(v.Value),
                        $"{lingua}: la chiave «{v.Key}» non ha testo, e in pagina si vedrebbe un buco");
        }

        [TestMethod]
        public void ISegnapostoCoincidonoFraLeDueLingue() {
            var it = Tutte("it");
            var en = Tutte("en");
            foreach (var chiave in it.Keys) {
                CollectionAssert.AreEquivalent(
                    Segnaposto(it[chiave]).ToList(), Segnaposto(en[chiave]).ToList(),
                    $"«{chiave}»: i segnaposto delle due lingue non coincidono.\n" +
                    $"  it: {it[chiave]}\n  en: {en[chiave]}");
            }
        }

        private static IEnumerable<string> Segnaposto(string testo) =>
            Regex.Matches(testo, @"\{(\d+)").Cast<Match>().Select(m => m.Groups[1].Value).Distinct();

        [TestMethod]
        public void LInglesseEIlRipiego_QuandoLItalianoNonHaLaChiave() {
            /*  Non si puo' togliere una chiave dal resx durante un test, quindi si prova
                la proprieta' che conta: una chiave che non esiste in nessuna delle due
                torna se stessa, e mai una stringa vuota. Se un giorno il ripiego venisse
                tolto, questo diventerebbe null e la pagina mostrerebbe un buco. */
            Loc.Instance.ForzaLingua("it");
            Assert.AreEqual("Chiave_Che_Non_Esiste", Loc.T("Chiave_Che_Non_Esiste"));
            Loc.Instance.ForzaLingua("en");
            Assert.AreEqual("Chiave_Che_Non_Esiste", Loc.T("Chiave_Che_Non_Esiste"));
            Assert.AreEqual("", Loc.T(""));
        }

        [TestMethod]
        public void LaLinguaSiPuoImporreEIlTestoCambiaDavvero() {
            Loc.Instance.ForzaLingua("it");
            Assert.AreEqual("it", Loc.Instance.LinguaInUso);
            var italiano = Loc.T("Pannello_Riprova");

            Loc.Instance.ForzaLingua("en");
            Assert.AreEqual("en", Loc.Instance.LinguaInUso);
            var inglese = Loc.T("Pannello_Riprova");

            Assert.AreEqual("Riprova", italiano);
            Assert.AreEqual("Retry", inglese);
        }

        [TestMethod]
        public void SenzaImposizione_SiSegueLaLinguaDiNina() {
            var prima = CultureInfo.CurrentUICulture;
            try {
                Loc.Instance.ForzaLingua("");
                CultureInfo.CurrentUICulture = new CultureInfo("de-DE");
                Assert.AreEqual("en", Loc.Instance.LinguaInUso, "una lingua che non abbiamo cade sull'inglese");
                CultureInfo.CurrentUICulture = new CultureInfo("it-IT");
                Assert.AreEqual("it", Loc.Instance.LinguaInUso);
            } finally {
                CultureInfo.CurrentUICulture = prima;
                Loc.Instance.ForzaLingua("it");
            }
        }

        [TestMethod]
        public void IValoriDentroLaFraseSeguonoChiLegge() {
            /*  «4,83 h» per chi ha N.I.N.A. in italiano e «4.83 h» per gli altri: i numeri
                seguono la cultura di chi guarda lo schermo, non quella del file di
                risorse. E' il motivo per cui Loc.F formatta con CurrentCulture. */
            var prima = CultureInfo.CurrentCulture;
            try {
                Loc.Instance.ForzaLingua("en");
                CultureInfo.CurrentCulture = new CultureInfo("en-US");
                StringAssert.Contains(Loc.F("Montaggio_FiltroNonInRuota", "b", "Ha", 29, 600.0, 4.8333), "4.83");
                CultureInfo.CurrentCulture = new CultureInfo("it-IT");
                StringAssert.Contains(Loc.F("Montaggio_FiltroNonInRuota", "b", "Ha", 29, 600.0, 4.8333), "4,83");
            } finally {
                CultureInfo.CurrentCulture = prima;
                Loc.Instance.ForzaLingua("it");
            }
        }

        [TestMethod]
        public void UnaFraseConTroppiPochiValori_NonFaCadereNiente() {
            /*  Un resx modificato a mano non deve poter far cadere una consegna: si
                restituisce il modello invece di sollevare. */
            Loc.Instance.ForzaLingua("it");
            var fuori = Loc.F("Montaggio_InRuotaCiSono");
            Assert.IsNotNull(fuori);
            Assert.IsTrue(fuori.Length > 0);
        }

        [TestMethod]
        public void NessunaChiaveOrfana_OgniVoceDelResxEUsataDalCodice() {
            /*  Il contrario del test sulle chiavi mancanti: una voce che nessuno chiede
                piu' resta li' per sempre e va tradotta a ogni giro. Si guarda il codice
                sorgente, perche' e' l'unico posto dove le chiavi compaiono per nome.
                Se la cartella dei sorgenti non si trova (pacchetto senza sorgenti), il
                test si astiene invece di dare un falso allarme. */
            var radice = RadiceDeiSorgenti();
            if (radice is null) { Assert.Inconclusive("sorgenti non trovati accanto ai test"); return; }

            var testo = string.Join("\n", System.IO.Directory
                .EnumerateFiles(radice, "*.cs", System.IO.SearchOption.AllDirectories)
                .Concat(System.IO.Directory.EnumerateFiles(radice, "*.xaml", System.IO.SearchOption.AllDirectories))
                .Where(f => !f.Contains(System.IO.Path.DirectorySeparatorChar + "obj" + System.IO.Path.DirectorySeparatorChar)
                         && !f.Contains(System.IO.Path.DirectorySeparatorChar + "bin" + System.IO.Path.DirectorySeparatorChar))
                .Select(System.IO.File.ReadAllText));

            var orfane = Tutte("it").Keys.Where(kk => !testo.Contains("\"" + kk + "\"")
                                                   && !testo.Contains("[" + kk + "]"))
                                         .OrderBy(x => x).ToList();
            Assert.AreEqual(0, orfane.Count,
                "voci nei resx che il codice non chiede piu': " + string.Join(", ", orfane));
        }

        private static string? RadiceDeiSorgenti() {
            var d = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
            for (var i = 0; i < 8 && d is not null; i++, d = d.Parent) {
                var c = System.IO.Path.Combine(d.FullName, "src", "AstroImage.NINA.Plugin");
                if (System.IO.Directory.Exists(c)) return c;
            }
            return null;
        }
    }
}
