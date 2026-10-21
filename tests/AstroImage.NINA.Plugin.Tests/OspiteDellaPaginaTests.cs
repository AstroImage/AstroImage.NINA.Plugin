using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using AstroImage.NINA.Plugin.Models;
using AstroImage.NINA.Plugin.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace AstroImage.NINA.Plugin.Tests {

    /*  L'OSPITE DELLA PAGINA VERA (21 ottobre 2026).
     *
     *  Il pannello apre AstroImage-Strategy quando le Opzioni ne scrivono l'indirizzo, e il Ponte le risponde con le
     *  domande e i nomi dell'Advanced API. Qui si prova tutto quello che decide l'ospite, senza N.I.N.A. e senza WebView2:
     *  quale indirizzo si apre, a chi si risponde, che cosa esce dal profilo, e che le vie dichiarate sono proprio quelle
     *  a cui si risponde.
     */
    [TestClass]
    public class OspiteDellaPaginaTests {

        private static readonly Uri Sito = new Uri("https://astroimage.github.io/");
        private const string DalSito = "https://astroimage.github.io/";

        private static ProfiloPerLaPagina Banco() => new ProfiloPerLaPagina {
            Nome = "Profilo di prova",
            Latitudine = 45.95, Longitudine = 10.2, Quota = 1000,
            FileOrizzonte = "Casa .hrz",
            CameraId = "0F3C2A10-0000-4000-8000-00000000C0DE", PixelUm = 3.75, Matrice = "None",
            Telescopio = "RC8", Montatura = "AM5", FocaleMm = 1295, Rapporto = 6.5,
            LarghezzaPx = 6248, AltezzaPx = 4176,
            Filtri = new List<FiltroDelProfilo> {
                new FiltroDelProfilo { Nome = "L", Posizione = 0 }, new FiltroDelProfilo { Nome = "Ha", Posizione = 5 },
            },
            Orizzonte = new OrizzonteDelProfilo {
                Azimut = Enumerable.Range(0, 361).Select(g => (double)g).ToArray(),
                Altezza = Enumerable.Range(0, 361).Select(g => 6.4 + g % 30).ToArray(),
            },
        };

        private static JsonObject Chiedi(string messaggio, Func<ProfiloPerLaPagina?>? profilo = null, string sorgente = DalSito) {
            var t = OspiteDellaPagina.Rispondi(messaggio, sorgente, Sito, profilo ?? Banco, "versione-di-prova", out var nota);
            Assert.IsNotNull(t, "nessuna risposta: " + nota);
            return JsonNode.Parse(t!)!.AsObject();
        }

        private static JsonObject Via(string via, Func<ProfiloPerLaPagina?>? profilo = null, string metodo = "GET") =>
            Chiedi(new JsonObject { ["id"] = "r1", ["azione"] = "nina", ["via"] = via, ["metodo"] = metodo }.ToJsonString(), profilo)
                ["risposta"]!.AsObject();

        // 1 ──────────────────────────────── quale pagina si apre
        [TestMethod]
        public void VuotoELaPaginaDiProva_ENonUnErrore() {
            Assert.IsNull(OspiteDellaPagina.Pagina("  ", out var perche));
            Assert.IsNull(perche, "un campo vuoto e' la pagina di prova, non un indirizzo sbagliato");
            Assert.IsNull(OspiteDellaPagina.Pagina(null, out perche));
            Assert.IsNull(perche);
        }

        [TestMethod]
        public void SiApreHttps_OHttpSoloSuQuestaMacchina() {
            Assert.AreEqual("https://astroimage.github.io/", OspiteDellaPagina.Pagina(" https://astroimage.github.io/ ", out var p)!.AbsoluteUri);
            Assert.IsNull(p);
            Assert.IsNotNull(OspiteDellaPagina.Pagina("http://localhost:8765/una-pagina.html", out p), p);
            Assert.IsNotNull(OspiteDellaPagina.Pagina("http://127.0.0.1:8765/", out p), p);
            foreach (var no in new[] { "http://un-altro-pc:8765/", "file:///C:/pagina.html", "astroimage.github.io",
                                       "https://utente:segreto" + "@astroimage.github.io/", "javascript:alert(1)" }) {
                Assert.IsNull(OspiteDellaPagina.Pagina(no, out p), no + " non si deve aprire");
                StringAssert.Contains(p, "non va bene", no + ": il perche' non si dice");
            }
        }

        // 2 ──────────────────────────────── a chi si risponde
        [TestMethod]
        public void SiRispondeSoloAllaPaginaDelleOpzioni() {
            var m = "{\"id\":\"p1\",\"azione\":\"presentati\"}";
            Assert.IsNotNull(OspiteDellaPagina.Rispondi(m, "https://astroimage.github.io/?x=1", Sito, Banco, "versione-di-prova", out _),
                "controllo positivo: la stessa origine, con un'altra domanda nell'indirizzo, riceve la risposta");
            foreach (var altra in new[] { "https://altro.github.io/", "http://astroimage.github.io/", "https://astroimage.github.io:8443/",
                                          "about:blank", "", null }) {
                Assert.IsNull(OspiteDellaPagina.Rispondi(m, altra, Sito, Banco, "versione-di-prova", out var nota),
                    "«" + altra + "» ha ricevuto una risposta");
                StringAssert.Contains(nota, "ignored");
            }
        }

        [TestMethod]
        public void SenzaIdOSenzaJson_NonSiRisponde_EIlProfiloNonSiLegge() {
            var letto = 0;
            ProfiloPerLaPagina Conta() { letto++; return Banco(); }
            Assert.IsNull(OspiteDellaPagina.Rispondi("non e' JSON", DalSito, Sito, Conta, null, out _));
            Assert.IsNull(OspiteDellaPagina.Rispondi("{\"azione\":\"nina\",\"via\":\"/profile/show?active=true\"}", DalSito, Sito, Conta, null, out _));
            Chiedi("{\"id\":\"p1\",\"azione\":\"presentati\"}", Conta);
            Assert.AreEqual(0, letto, "il profilo si e' letto per una domanda che non lo chiedeva");
            Via("/profile/show?active=true", Conta);
            Assert.AreEqual(1, letto, "controllo positivo: la domanda del profilo lo legge, una volta");
        }

        // 3 ──────────────────────────────── le vie dichiarate sono quelle a cui si risponde
        [TestMethod]
        public void LaPresentazioneDichiaraLeVie_EOgniViaDichiarataRisponde() {
            var r = Chiedi("{\"id\":\"p1\",\"azione\":\"presentati\"}");
            Assert.AreEqual("p1", (string?)r["id"]);
            Assert.AreEqual(OspiteDellaPagina.Nome, (string?)r["ospite"]);
            Assert.AreEqual("versione-di-prova", (string?)r["versione"]);
            var vie = r["vie"]!.AsArray().Select(v => (string)v!).ToArray();
            CollectionAssert.AreEqual(new[] { "/profile/show", "/profile/horizon" }, vie);
            foreach (var v in vie)
                Assert.AreEqual(true, (bool?)Via(v + "?active=true")["Success"], v + " e' dichiarata e non risponde");
            var fuori = Via("/sequence/load");
            Assert.AreEqual(false, (bool?)fuori["Success"], "controllo negativo: una via non dichiarata ha risposto");
            Assert.AreEqual(404, (int?)fuori["StatusCode"]);
            StringAssert.Contains((string?)fuori["Error"], "/profile/show", "il rifiuto non dice a che cosa si risponde");
        }

        // 4 ──────────────────────────────── il profilo, coi nomi dell'Advanced API
        [TestMethod]
        public void IlProfiloEsceCoiNomiDiNina_NellaBustaDellAdvancedApi() {
            var b = Via("/profile/show?active=true");
            Assert.AreEqual(true, (bool?)b["Success"]);
            Assert.AreEqual(200, (int?)b["StatusCode"]);
            Assert.AreEqual("", (string?)b["Error"]);
            Assert.AreEqual("API", (string?)b["Type"]);
            var p = b["Response"]!.AsObject();
            Assert.AreEqual("Profilo di prova", (string?)p["Name"]);
            Assert.AreEqual(45.95, (double?)p["AstrometrySettings"]!["Latitude"]);
            Assert.AreEqual(10.2, (double?)p["AstrometrySettings"]!["Longitude"]);
            Assert.AreEqual(1000, (double?)p["AstrometrySettings"]!["Elevation"]);
            Assert.AreEqual("Casa .hrz", (string?)p["AstrometrySettings"]!["HorizonFilePath"]);
            Assert.AreEqual("0F3C2A10-0000-4000-8000-00000000C0DE", (string?)p["CameraSettings"]!["Id"]);
            Assert.AreEqual(3.75, (double?)p["CameraSettings"]!["PixelSize"]);
            Assert.AreEqual("None", (string?)p["CameraSettings"]!["BayerPattern"]);
            Assert.AreEqual("RC8", (string?)p["TelescopeSettings"]!["Name"]);
            Assert.AreEqual("AM5", (string?)p["TelescopeSettings"]!["MountName"]);
            Assert.AreEqual(1295, (double?)p["TelescopeSettings"]!["FocalLength"]);
            Assert.AreEqual(6.5, (double?)p["TelescopeSettings"]!["FocalRatio"]);
            Assert.AreEqual(6248, (int?)p["FramingAssistantSettings"]!["CameraWidth"]);
            Assert.AreEqual(4176, (int?)p["FramingAssistantSettings"]!["CameraHeight"]);
            var f = p["FilterWheelSettings"]!["FilterWheelFilters"]!.AsArray();
            Assert.AreEqual(2, f.Count);
            Assert.AreEqual("Ha", (string?)f[1]!["Name"]);
            Assert.AreEqual(5, (int?)f[1]!["Position"]);
        }

        [TestMethod]
        public void DelProfiloEsceSoloIlBanco() {
            var p = Via("/profile/show?active=true")["Response"]!.AsObject();
            CollectionAssert.AreEquivalent(
                new[] { "Name", "AstrometrySettings", "CameraSettings", "TelescopeSettings", "FramingAssistantSettings", "FilterWheelSettings" },
                p.Select(x => x.Key).ToArray(), "dal profilo esce un gruppo che non e' del banco");
            Assert.IsFalse(p.ToJsonString().Contains("PlateSolve"), "le impostazioni della risoluzione, con la chiave di astrometry.net, sono uscite");
        }

        [TestMethod]
        public void UnValoreCheNonCeEsceNull_ENonZero() {
            var p = Via("/profile/show?active=true", () => new ProfiloPerLaPagina { Nome = "vuoto" })["Response"]!.AsObject();
            Assert.IsNull(p["AstrometrySettings"]!["Latitude"]);
            Assert.IsNull(p["TelescopeSettings"]!["FocalLength"]);
            Assert.IsNull(p["FramingAssistantSettings"]!["CameraWidth"]);
            Assert.AreEqual(0, p["FilterWheelSettings"]!["FilterWheelFilters"]!.AsArray().Count);
        }

        [TestMethod]
        public void SoloIlProfiloAttivo_SoloLetture_EUnProfiloCheNonCeSiDice() {
            var senza = Via("/profile/show");
            Assert.AreEqual(false, (bool?)senza["Success"], "senza active=true si e' risposto");
            StringAssert.Contains((string?)senza["Error"], "profilo attivo");
            Assert.AreEqual(true, (bool?)Via("/profile/show?ACTIVE=True")["Success"], "maiuscole nella domanda: la domanda e' la stessa");
            var scrivi = Via("/profile/show?active=true", null, "POST");
            Assert.AreEqual(false, (bool?)scrivi["Success"]);
            Assert.AreEqual(405, (int?)scrivi["StatusCode"]);
            var nessuno = Via("/profile/show?active=true", () => null);
            Assert.AreEqual(false, (bool?)nessuno["Success"]);
            StringAssert.Contains((string?)nessuno["Error"], "profilo attivo");
        }

        // 5 ──────────────────────────────── l'orizzonte
        [TestMethod]
        public void LOrizzonteEsceComeLAdvancedApi_361Coppie() {
            var o = Via("/profile/horizon")["Response"]!.AsObject();
            var az = o["Azimuths"]!.AsArray();
            var alt = o["Altitudes"]!.AsArray();
            Assert.AreEqual(361, az.Count);
            Assert.AreEqual(361, alt.Count);
            Assert.AreEqual(0, (double?)az[0]);
            Assert.AreEqual(360, (double?)az[360]);
            Assert.AreEqual(6.4, (double?)alt[0]);
        }

        [TestMethod]
        public void SenzaOrizzonte_SiDice_ENonSeNeInventaUnoPiatto() {
            var b = Banco(); b.Orizzonte = null;
            var r = Via("/profile/horizon", () => b);
            Assert.AreEqual(false, (bool?)r["Success"]);
            StringAssert.Contains((string?)r["Error"], "orizzonte");
        }

        [TestMethod]
        public void UnAzioneCheNonCe_SiDice() {
            var r = Chiedi("{\"id\":\"x\",\"azione\":\"manda\"}");
            StringAssert.Contains((string?)r["errore"], "manda");
            Assert.IsNull(r["risposta"]);
        }

        // 6 ──────────────────────────────── il pannello tiene la sua pagina
        [TestMethod]
        public void NelPannelloRestaSoloLaPagina_IlRestoVaNelBrowser() {
            var locale = new Uri("http://localhost:8765/una-pagina.html");
            Assert.IsTrue(OspiteDellaPagina.RestaNelPannello("https://astroimage.github.io/", Sito));
            Assert.IsTrue(OspiteDellaPagina.RestaNelPannello("https://astroimage.github.io/?tab=2#risultati", Sito));
            Assert.IsTrue(OspiteDellaPagina.RestaNelPannello("http://localhost:8765/una-pagina.html?x=1", locale));
            foreach (var fuori in new[] { "https://astroimage.github.io/guida/AstroImage-Strategy-guida.pdf",
                                          "https://github.com/AstroImage", "http://localhost:8765/altro.html" })
                Assert.IsFalse(OspiteDellaPagina.RestaNelPannello(fuori, fuori.Contains("8765") ? locale : Sito), fuori);
            Assert.IsTrue(OspiteDellaPagina.SiApreNelBrowser("https://github.com/AstroImage"));
            foreach (var no in new[] { "file:///C:/Windows/System32/calc.exe", "ms-settings:privacy", "javascript:alert(1)", "" })
                Assert.IsFalse(OspiteDellaPagina.SiApreNelBrowser(no), no + " si consegnerebbe al sistema");
        }
    }
}
