using System;
using System.IO;
using AstroImage.NINA.Plugin.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace AstroImage.NINA.Plugin.Tests {

    /*  DOVE STA IL MOTORE, E CHI LO DICE (17 settembre 2026).
     *
     *  Finche' il ponte ha girato su due macchine di casa, l'indirizzo stava in un file accanto al DLL: si apriva il
     *  blocco note e si scriveva. Chi installa il plugin per la prima volta la cartella dei plugin non sa nemmeno dove
     *  sia, e un pannello che non dice dove sta guardando sembra rotto. Adesso l'indirizzo e' un'impostazione, il file
     *  resta come ripiego per i PC gia' configurati, e la scelta si vede: ogni strada dice da dove viene, e quello che
     *  non e' un indirizzo http non vince in silenzio.
     */
    [TestClass]
    public class IndirizzoDelMotoreTests {

        // 1 ──────────────────────────────── l'ordine delle tre strade
        [TestMethod]
        public void LOpzioneVinceSulFile_EIlFileSulDiSerie() {
            var r = IndirizzoDelMotore.Scegli("http://il-computer-del-motore:8791", "http://127.0.0.1:9/", out var da);
            Assert.AreEqual("http://il-computer-del-motore:8791/", r, "l'opzione non vince, o non le si aggiunge la barra finale");
            StringAssert.Contains(da, "options", "non si dice che l'indirizzo viene dalle opzioni");

            Assert.AreEqual("http://127.0.0.1:9/", IndirizzoDelMotore.Scegli("   ", "http://127.0.0.1:9/", out da),
                "senza opzione non vale il file");
            StringAssert.Contains(da, IndirizzoDelMotore.FileIndirizzo);

            Assert.AreEqual(IndirizzoDelMotore.DiSerie, IndirizzoDelMotore.Scegli(null, null, out da),
                "senza niente non vale l'indirizzo di serie");
            StringAssert.Contains(da, "default");
        }

        // 2 ──────────────────────────────── quello che non e' un indirizzo non vince in silenzio
        [TestMethod]
        public void UnIndirizzoCheNonEUnIndirizzoSiDice_ENonVince() {
            var r = IndirizzoDelMotore.Scegli("il mio motore", "http://127.0.0.1:9/", out var da);
            Assert.AreEqual("http://127.0.0.1:9/", r, "un'opzione storta ha vinto lo stesso");
            StringAssert.Contains(da, "il mio motore", "non si dice che cosa era scritto");
            StringAssert.Contains(da, "ignored");

            Assert.AreEqual(IndirizzoDelMotore.DiSerie, IndirizzoDelMotore.Scegli("ftp://casa/", null, out da),
                "un indirizzo che non e' http ha vinto");
            Assert.AreEqual(IndirizzoDelMotore.DiSerie, IndirizzoDelMotore.Scegli(null, "casa:8791", out da),
                "un file storto ha vinto");
            StringAssert.Contains(da, IndirizzoDelMotore.FileIndirizzo);
        }

        // 3 ──────────────────────────────── il tunnel di chi prova il plugin da casa sua
        [TestMethod]
        public void UnIndirizzoHttpsConUnNomeLungoVaBene() {
            var r = IndirizzoDelMotore.Scegli("https://qualcosa-di-lungo.trycloudflare.com", null, out var da);
            Assert.AreEqual("https://qualcosa-di-lungo.trycloudflare.com/", r);
            StringAssert.Contains(da, "options");
        }

        // 4 ──────────────────────────────── si scrive, e sopravvive alla chiusura
        [TestMethod]
        public void LIndirizzoScrittoNelleOpzioniSiRilegge() {
            var cartella = Path.Combine(Path.GetTempPath(), "ponte-prove-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(cartella);
            var percorso = Path.Combine(cartella, "impostazioni.json");
            try {
                var i = ImpostazioniPonte.Carica(percorso);
                Assert.AreEqual("", i.Indirizzo, "di serie non c'e' nessun indirizzo: valgono il file e poi quello di serie");

                i.Indirizzo = "  https://prova.trycloudflare.com  ";

                StringAssert.Contains(File.ReadAllText(percorso), "\"indirizzo\"");
                var riletta = ImpostazioniPonte.Carica(percorso);
                Assert.AreEqual("https://prova.trycloudflare.com", riletta.Indirizzo, "l'indirizzo non sopravvive alla chiusura");
                Assert.AreEqual(i.Lingua, riletta.Lingua, "scrivendo l'indirizzo si e' persa la lingua");
            } finally {
                Localization.Loc.Instance.ForzaLingua("it");
                try { Directory.Delete(cartella, true); } catch (IOException) { }
            }
        }

        // 5 ──────────────────────────────── la pagina delle opzioni lo chiede, e il pannello lo usa
        [TestMethod]
        public void LE_OPZIONI_CHIEDONO_L_INDIRIZZO_E_IL_PANNELLO_LO_SEGUE() {
            var xaml = Sorgente(Path.Combine("Options", "BridgeOptionsView.xaml"));
            StringAssert.Contains(xaml, "{Binding Indirizzo", "nelle Opzioni non c'e' il campo dell'indirizzo del motore");
            foreach (var k in new[] { "Opzioni_Indirizzo", "Opzioni_IndirizzoAiuto" })
                StringAssert.Contains(xaml, k, "nelle Opzioni manca la parola " + k);

            var vm = Sorgente(Path.Combine("ViewModels", "PannelloStrategyVM.cs"));
            StringAssert.Contains(vm, "IndirizzoDelMotore.Scegli", "il pannello non sceglie l'indirizzo dalle tre strade");
            StringAssert.Contains(vm, "PropertyChanged +=", "il pannello non si accorge se l'indirizzo cambia nelle Opzioni");

            foreach (var lingua in new[] { "it", "en" }) {
                var resx = Sorgente(Path.Combine("Localization", "Strings_" + lingua + ".resx"));
                foreach (var k in new[] { "Opzioni_Indirizzo", "Opzioni_IndirizzoAiuto" })
                    StringAssert.Contains(resx, "name=\"" + k + "\"", lingua + ": manca " + k);
            }
        }

        /*  I sorgenti stanno accanto alle prove, non in una cartella di questa macchina: si risale finche' non si
         *  trova src\AstroImage.NINA.Plugin, come fa la prova della pagina per la vista. */
        private static string Sorgente(string dentroIlProgetto) {
            var su = new DirectoryInfo(AppContext.BaseDirectory);
            for (var n = 0; n < 8 && su is not null; n++, su = su.Parent) {
                var radice = Path.Combine(su.FullName, "src", "AstroImage.NINA.Plugin");
                if (Directory.Exists(radice)) { return File.ReadAllText(Path.Combine(radice, dentroIlProgetto)); }
            }
            Assert.Fail("sorgenti non trovati accanto alle prove");
            return "";
        }
    }
}
