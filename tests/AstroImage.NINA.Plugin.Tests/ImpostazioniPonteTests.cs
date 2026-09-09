using System;
using System.Globalization;
using System.IO;
using AstroImage.NINA.Plugin.Localization;
using AstroImage.NINA.Plugin.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace AstroImage.NINA.Plugin.Tests {

    /*  LA LINGUA E' TUA, NON DELLA POSTAZIONE.
     *
     *  Tutto il resto di quello che il ponte ricorda — la ruota, il sito — sta nel
     *  profilo di N.I.N.A., perche' cambia davvero fra Borno e Milano. La lingua no: a
     *  leggere sei sempre tu. Un valore per profilo si presenterebbe come un mistero —
     *  cambi postazione e il pannello cambia lingua — quindi qui si verifica anche che
     *  del profilo non ci sia traccia.
     *
     *  OGNI PROVA RIMETTE L'ITALIANO PRIMA DI USCIRE. Loc e' un oggetto solo per tutta
     *  la suite, e le altre duecento asserzioni guardano dentro il testo italiano: una
     *  prova che se ne andasse lasciando l'inglese le farebbe cadere tutte, e il guasto
     *  sembrerebbe di chi non c'entra niente.
     */
    [TestClass]
    public class ImpostazioniPonteTests {

        private string _cartella = "";
        private string Percorso => Path.Combine(_cartella, "impostazioni.json");

        [TestInitialize]
        public void Prima() {
            _cartella = Path.Combine(Path.GetTempPath(), "ponte-prove-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_cartella);
        }

        [TestCleanup]
        public void Dopo() {
            Loc.Instance.ForzaLingua("it");
            try { Directory.Delete(_cartella, true); } catch (IOException) { }
        }

        // 1 ─────────────────────────────────────────────── si salva
        [TestMethod]
        public void SceglierLaLinguaScriveIlFile() {
            var i = ImpostazioniPonte.Carica(Percorso);
            Assert.IsFalse(File.Exists(Percorso), "senza sceglier niente non si scrive niente");

            i.Lingua = "it";

            Assert.IsTrue(File.Exists(Percorso), "la scelta deve sopravvivere alla chiusura di N.I.N.A.");
            StringAssert.Contains(File.ReadAllText(Percorso), "\"lingua\"");
            StringAssert.Contains(File.ReadAllText(Percorso), "it");
        }

        // 2 ─────────────────────────────────────────────── si rilegge
        [TestMethod]
        public void UnaNuovaIstanzaRitrovaLaScelta() {
            ImpostazioniPonte.Carica(Percorso).Lingua = "it";

            var dopo = ImpostazioniPonte.Carica(Percorso);

            Assert.AreEqual("it", dopo.Lingua);
            Assert.IsNull(dopo.Nota, "una rilettura riuscita non ha niente da dire");
        }

        // 3 ─────────────────────────────────────────────── si applica all'avvio
        [TestMethod]
        public void CaricareAPPLICA_LaLingua_NonSoloLaLegge() {
            ImpostazioniPonte.Carica(Percorso).Lingua = "en";
            Loc.Instance.ForzaLingua("it");
            Assert.AreEqual("it", Loc.Instance.LinguaInUso, "punto di partenza");

            ImpostazioniPonte.Carica(Percorso);

            Assert.AreEqual("en", Loc.Instance.LinguaInUso,
                "leggere le impostazioni all'avvio DEVE gia' aver cambiato la lingua: " +
                "altrimenti il pannello nasce in una lingua e la cambia sotto gli occhi");
            Assert.AreEqual("Retry", Loc.T("Pannello_Riprova"));
        }

        [TestMethod]
        public void SenzaFile_SiParteInINGLESE_ENonSeguendoNina() {
            /*  La prima installazione parla inglese: il catalogo lo apre gente in mezzo
                mondo. Chi vuole l'italiano lo sceglie, ed e' a un clic. */
            var prima = CultureInfo.CurrentUICulture;
            try {
                CultureInfo.CurrentUICulture = new CultureInfo("it-IT");
                var i = ImpostazioniPonte.Carica(Percorso);
                Assert.AreEqual("en", i.Lingua);
                Assert.AreEqual("en", Loc.Instance.LinguaInUso,
                    "anche con N.I.N.A. in italiano, chi installa la prima volta trova l'inglese");
                Assert.IsNull(i.Nota, "un file che non c'e' non e' un guasto: e' la prima volta");
            } finally { CultureInfo.CurrentUICulture = prima; }
        }

        [TestMethod]
        public void SceglierSeguiNina_RidaIlComandoAllaLinguaDiNina() {
            var prima = CultureInfo.CurrentUICulture;
            try {
                var i = ImpostazioniPonte.Carica(Percorso);
                i.Lingua = ImpostazioniPonte.SegueNina;

                CultureInfo.CurrentUICulture = new CultureInfo("it-IT");
                Assert.AreEqual("it", Loc.Instance.LinguaInUso);
                CultureInfo.CurrentUICulture = new CultureInfo("en-GB");
                Assert.AreEqual("en", Loc.Instance.LinguaInUso);

                Assert.AreEqual("", ImpostazioniPonte.Carica(Percorso).Lingua,
                    "«segui N.I.N.A.» e' una scelta come le altre e si salva");
            } finally { CultureInfo.CurrentUICulture = prima; }
        }

        // 4 ─────────────────────────────────────────────── cambio a caldo
        [TestMethod]
        public void IlCambioSiVedeSubito_SenzaRiavviare() {
            var i = ImpostazioniPonte.Carica(Percorso);
            var avvisi = 0;
            Loc.Instance.PropertyChanged += (_, e) => { if (e.PropertyName == "Item[]") avvisi++; };

            i.Lingua = "it";
            Assert.AreEqual("Riprova", Loc.T("Pannello_Riprova"));
            i.Lingua = "en";
            Assert.AreEqual("Retry", Loc.T("Pannello_Riprova"));

            Assert.IsTrue(avvisi >= 2,
                "senza l'avviso su «Item[]» la pagina resterebbe scritta nella lingua vecchia " +
                "finche' non la si chiude e riapre");
        }

        [TestMethod]
        public void CambiareLingua_AvvisaAncheChiGuardaLeImpostazioni() {
            var i = ImpostazioniPonte.Carica(Percorso);
            string? cambiata = null;
            i.PropertyChanged += (_, e) => cambiata = e.PropertyName;
            i.Lingua = "it";
            Assert.AreEqual("Lingua", cambiata, "la tendina deve seguire anche se cambia da altrove");
        }

        // 5 ─────────────────────────────────────────────── niente profilo
        [TestMethod]
        public void IlFileSTA_FUORI_DaiProfiliEDallaCartellaDelPlugin() {
            var p = ImpostazioniPonte.PercorsoDiSerie;

            StringAssert.Contains(p, Path.Combine("NINA", "Plugins"));
            StringAssert.Contains(p, "AstroImage.NINA.Plugin");
            /*  «3.0.0» e' la cartella dove N.I.N.A. INSTALLA i plugin, e un aggiornamento
                puo' riscriverla. Le impostazioni stanno un piano sopra e sopravvivono. */
            Assert.IsFalse(p.Contains("3.0.0"),
                "sotto la cartella di installazione un aggiornamento del plugin porterebbe via la scelta");
            StringAssert.StartsWith(p, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        }

        [TestMethod]
        public void LeImpostazioniNonSANNO_CheCosaSiaUnProfilo() {
            /*  La guardia contro il ripensamento piu' probabile: qualcuno che, per
                comodita', fa passare la lingua dal profilo come la ruota e il sito. */
            var t = typeof(ImpostazioniPonte);
            foreach (var m in t.GetMembers())
                Assert.IsFalse(m.Name.Contains("Profil", StringComparison.OrdinalIgnoreCase),
                    "la lingua non e' del profilo: e' della persona che legge, ed e' la stessa a Borno e a Milano");
            Assert.IsFalse(t.Assembly.GetName().Name!.Length == 0);
        }

        // 6 ─────────────────────────────────────────────── file rotto
        [TestMethod]
        public void UnFileROTTO_NonImpedisceAlPluginDiPartire_ENonSiRiscriveDaSolo() {
            File.WriteAllText(Percorso, "{ questo non e' json");

            var i = ImpostazioniPonte.Carica(Percorso);

            Assert.AreEqual("en", i.Lingua, "si riparte da quelle di serie");
            Assert.IsNotNull(i.Nota, "e si dice perche': un ripiego silenzioso e' il difetto, non la cura");
            StringAssert.Contains(File.ReadAllText(Percorso), "questo non e' json",
                "il file di chi l'ha scritto a mano non si cancella: potrebbe volerlo aggiustare");
        }

        [TestMethod]
        public void UnFileVUOTO_ValeComeNonAverloScritto() {
            File.WriteAllText(Percorso, "null");
            var i = ImpostazioniPonte.Carica(Percorso);
            Assert.AreEqual("en", i.Lingua);
            Assert.IsNotNull(i.Nota);
        }

        // 7 ─────────────────────────────────────────────── valore sconosciuto
        [TestMethod]
        public void UnaLinguaCheNonAbbiamo_ValeComeNonAverlaDetta() {
            File.WriteAllText(Percorso, "{\"lingua\":\"de\"}");

            var i = ImpostazioniPonte.Carica(Percorso);

            /*  «de» finisce sull'inglese e NON su «segui N.I.N.A.». E' voluto: file
                assente, file rotto e valore sconosciuto sono tre modi di non aver
                scelto, e devono finire nello stesso posto. Farli finire in due posti
                diversi vorrebbe dire spiegare, un domani, perche' un file corrotto
                segue N.I.N.A. e un'installazione nuova no. */
            Assert.AreEqual("en", i.Lingua);
            Assert.IsNotNull(i.Nota, "e si dice che quella lingua non c'era");
            StringAssert.Contains(i.Nota!, "de");
        }

        [TestMethod]
        public void AncheAssegnandola_UnaLinguaSconosciutaNonAttecchisce() {
            var i = ImpostazioniPonte.Carica(Percorso);
            i.Lingua = "it";
            i.Lingua = "klingon";
            Assert.AreEqual("en", i.Lingua);
        }

        [TestMethod]
        public void MaiuscoleESpazi_NonFannoUnaLinguaDiversa() {
            var i = ImpostazioniPonte.Carica(Percorso);
            i.Lingua = "  IT  ";
            Assert.AreEqual("it", i.Lingua);
        }

        [TestMethod]
        public void UnPercorsoImpossibile_NonFaCadereNiente() {
            /*  Un disco pieno o una cartella senza permessi non devono far cadere
                N.I.N.A. all'avvio: si torna false e si dice perche'. */
            var i = ImpostazioniPonte.Carica(Path.Combine(_cartella, "\0", "x.json"));
            Assert.IsFalse(i.Salva());
            Assert.IsNotNull(i.Nota);
        }
    }
}
