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

        // 7 ──────────────────────────────── le due vie del Ponte: i bersagli e la consegna (passo 3)
        private static string Fixture(string nome) => System.IO.File.ReadAllText(System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(typeof(OspiteDellaPaginaTests).Assembly.Location)!, "Fixtures", nome + ".json"));

        private sealed class Registro {
            public SequenceModel? Modello; public int? Indice; public string? Nome; public int Chiamate;
        }

        private static ServiziDellOspite Servizi(Registro r, Func<EsitoDellaConsegna>? esito = null) => new ServiziDellOspite {
            Profilo = Banco,
            Bersagli = () => new[] { new BersaglioDelSequenziatore { Nome = "M 31", Stato = "RUNNING" },
                                     new BersaglioDelSequenziatore { Nome = "NGC 7000", Stato = "CREATED" } },
            Consegna = (m, i, n) => { r.Modello = m; r.Indice = i; r.Nome = n; r.Chiamate++;
                return esito?.Invoke() ?? new EsitoDellaConsegna { Fatta = true, Nome = m.Nome, Bersaglio = m.Bersaglio?.Nome,
                    Blocchi = m.Blocchi?.Count ?? 0, PosePerBlocco = new List<int> { 10, 12, 20 }, Note = new List<string> { "una nota" }, Sostituito = n }; },
        };

        private static JsonObject ViaCon(ServiziDellOspite servizi, string via, string metodo = "GET", string? corpo = null) {
            var m = new JsonObject { ["id"] = "c1", ["azione"] = "nina", ["via"] = via, ["metodo"] = metodo, ["corpo"] = corpo };
            var t = OspiteDellaPagina.Rispondi(m.ToJsonString(), DalSito, Sito, servizi, "versione-di-prova", out var nota);
            Assert.IsNotNull(t, nota);
            return JsonNode.Parse(t!)!["risposta"]!.AsObject();
        }

        private static string Corpo(string fixture, JsonObject? sostituisci = null) => new JsonObject {
            ["modello"] = JsonNode.Parse(Fixture(fixture)), ["sostituisci"] = sostituisci,
        }.ToJsonString();

        [TestMethod]
        public void LeVieDelPonte_SiDichiaranoSoloSeCeChiLeServe() {
            var r = new Registro();
            var con = JsonNode.Parse(OspiteDellaPagina.Rispondi("{\"id\":\"p\",\"azione\":\"presentati\"}", DalSito, Sito, Servizi(r),
                null, out _)!)!["vie"]!.AsArray().Select(v => (string)v!).ToArray();
            CollectionAssert.AreEqual(new[] { "/profile/show", "/profile/horizon", "/ponte/bersagli", "/ponte/consegna" }, con);
            var senza = OspiteDellaPagina.VieDi(new ServiziDellOspite { Profilo = Banco });
            CollectionAssert.AreEqual(new[] { "/profile/show", "/profile/horizon" }, senza.ToArray(),
                "senza chi consegna, la consegna non si dichiara");
            var rifiutata = Via("/ponte/consegna", null, "POST");
            Assert.AreEqual(404, (int?)rifiutata["StatusCode"], "una via non dichiarata ha risposto");
        }

        [TestMethod]
        public void IBersagli_ConIndiceENome_NellOrdineDiNina() {
            var b = ViaCon(Servizi(new Registro()), "/ponte/bersagli");
            Assert.AreEqual(true, (bool?)b["Success"]);
            var l = b["Response"]!.AsArray();
            Assert.AreEqual(2, l.Count);
            Assert.AreEqual(1, (int?)l[1]!["indice"]);
            Assert.AreEqual("NGC 7000", (string?)l[1]!["nome"]);
            Assert.AreEqual("RUNNING", (string?)l[0]!["stato"], "lo stato di N.I.N.A. non arriva alla pagina, che non saprebbe che cosa non toccare");
            Assert.AreEqual(405, (int?)ViaCon(Servizi(new Registro()), "/ponte/bersagli", "POST")["StatusCode"], "i bersagli si leggono, non si scrivono");
        }

        [TestMethod]
        public void LaConsegna_PortaIlModelloDelContratto_ComeLoLeggevaIlServizio() {
            var r = new Registro();
            var b = ViaCon(Servizi(r), "/ponte/consegna", "POST", Corpo("mono"));
            Assert.AreEqual(true, (bool?)b["Success"], (string?)b["Error"]);
            Assert.AreEqual(1, r.Chiamate);
            var atteso = SequenceModel.Leggi(Fixture("mono"))!;
            Assert.IsNotNull(r.Modello);
            Assert.AreEqual(atteso.Scrivi(), r.Modello!.Scrivi(), "il modello arrivato non e' quello del contratto, letto come il servizio");
            Assert.IsNull(r.Indice, "senza sostituzione e' un bersaglio nuovo");
            var d = b["Response"]!.AsObject();
            Assert.AreEqual(5, (int?)d["blocchi"]);
            CollectionAssert.AreEqual(new[] { 10, 12, 20 }, d["pose"]!.AsArray().Select(x => (int)x!).ToArray(), "le pose blocco per blocco, non un totale");
            Assert.AreEqual("una nota", (string?)d["note"]![0]);
            Assert.AreEqual(0, d["scartati"]!.AsArray().Count);
        }

        [TestMethod]
        public void LaSostituzione_PortaIndiceENome_OppureNonParte() {
            var r = new Registro();
            var b = ViaCon(Servizi(r), "/ponte/consegna", "POST", Corpo("mono", new JsonObject { ["indice"] = 1, ["nome"] = "NGC 7000" }));
            Assert.AreEqual(true, (bool?)b["Success"]);
            Assert.AreEqual(1, r.Indice);
            Assert.AreEqual("NGC 7000", r.Nome);
            Assert.AreEqual("NGC 7000", (string?)b["Response"]!["sostituito"]);
            foreach (var storta in new[] { new JsonObject { ["indice"] = 1 }, new JsonObject { ["nome"] = "M 31" },
                                           new JsonObject { ["indice"] = -1, ["nome"] = "M 31" }, new JsonObject { ["indice"] = "uno", ["nome"] = "M 31" } }) {
                var n = r.Chiamate;
                var x = ViaCon(Servizi(r), "/ponte/consegna", "POST", Corpo("mono", storta));
                Assert.AreEqual(400, (int?)x["StatusCode"], storta.ToJsonString());
                Assert.AreEqual(n, r.Chiamate, "una sostituzione storta ha raggiunto chi consegna: " + storta.ToJsonString());
            }
        }

        [TestMethod]
        public void SenzaModello_OConUnaLettura_NonSiConsegnaNiente() {
            var r = new Registro();
            Assert.AreEqual(405, (int?)ViaCon(Servizi(r), "/ponte/consegna", "GET", Corpo("mono"))["StatusCode"]);
            Assert.AreEqual(400, (int?)ViaCon(Servizi(r), "/ponte/consegna", "POST", "{}")["StatusCode"]);
            Assert.AreEqual(400, (int?)ViaCon(Servizi(r), "/ponte/consegna", "POST", "non e' JSON")["StatusCode"]);
            Assert.AreEqual(400, (int?)ViaCon(Servizi(r), "/ponte/consegna", "POST", null)["StatusCode"]);
            Assert.AreEqual(0, r.Chiamate, "una richiesta senza modello ha raggiunto chi consegna");
        }

        [TestMethod]
        public void UnRifiuto_TornaColPerche_EConGliScarti() {
            var r = new Registro();
            var no = ViaCon(Servizi(r, () => new EsitoDellaConsegna {
                PerCheNo = "il filtro «O» non e' nella ruota", Scartati = new List<string> { "il filtro «O» non e' nella ruota", "NON è stato consegnato" },
            }), "/ponte/consegna", "POST", Corpo("mono"));
            Assert.AreEqual(false, (bool?)no["Success"]);
            Assert.AreEqual(409, (int?)no["StatusCode"]);
            StringAssert.Contains((string?)no["Error"], "«O»");
            Assert.AreEqual(2, no["Response"]!["scartati"]!.AsArray().Count, "gli scarti non viaggiano col rifiuto");
            var rotto = ViaCon(new ServiziDellOspite { Profilo = Banco, Consegna = (m, i, n) => throw new InvalidOperationException("il Sequenziatore e' sparito") },
                "/ponte/consegna", "POST", Corpo("mono"));
            Assert.AreEqual(500, (int?)rotto["StatusCode"]);
            StringAssert.Contains((string?)rotto["Error"], "sparito");
        }

        // 8 ──────────────────────────────── le dichiarazioni della pagina nel profilo attivo (passo 4)
        private sealed class Memoria {
            public string Id = "11111111-1111-1111-1111-111111111111", Nome = "Profilo di casa";
            public string? Documento; public int Scritture; public string? Guasto;
            public DichiarazioniDellaPagina Servizio() => new DichiarazioniDellaPagina {
                Profilo = () => (Id, Nome), Leggi = () => Documento,
                Scrivi = d => { if (Guasto is not null) return Guasto; Documento = d; Scritture++; return null; },
            };
        }

        private static JsonObject Dichiarazioni(Memoria m, string metodo = "GET", string? corpo = null) =>
            ViaCon(new ServiziDellOspite { Profilo = Banco, Dichiarazioni = m.Servizio() }, "/ponte/dichiarazioni", metodo, corpo);

        private static string DaSalvare(string id, JsonNode? dati) => new JsonObject { ["id"] = id, ["dati"] = dati }.ToJsonString();

        [TestMethod]
        public void LeDichiarazioni_SiDichiaranoSoloSeCeLaMemoria() {
            CollectionAssert.Contains(OspiteDellaPagina.VieDi(new ServiziDellOspite { Dichiarazioni = new Memoria().Servizio() }).ToArray(), "/ponte/dichiarazioni");
            CollectionAssert.DoesNotContain(OspiteDellaPagina.VieDi(new ServiziDellOspite()).ToArray(), "/ponte/dichiarazioni");
        }

        [TestMethod]
        public void UnProfiloSenzaDichiarazioni_DaNull_ColNomeEConLIdentita() {
            var r = Dichiarazioni(new Memoria());
            Assert.AreEqual(true, (bool?)r["Success"]);
            Assert.IsNull(r["Response"]!["dati"]);
            Assert.AreEqual("Profilo di casa", (string?)r["Response"]!["nome"]);
            Assert.AreEqual("11111111-1111-1111-1111-111111111111", (string?)r["Response"]!["id"]);
        }

        [TestMethod]
        public void SalvateERilette_SonoLeStesse_DentroUnaBustaConLaVersione() {
            var m = new Memoria();
            var dati = JsonNode.Parse("{\"versione\":1,\"chiavi\":{\"ap_cfg\":\"{\\\"tel\\\":\\\"rc8\\\"}\",\"ap_site\":\"borno\"}}")!;
            var s1 = Dichiarazioni(m, "POST", DaSalvare(m.Id, dati));
            Assert.AreEqual(true, (bool?)s1["Success"], (string?)s1["Error"]);
            Assert.AreEqual(1, m.Scritture);
            var busta = JsonNode.Parse(m.Documento!)!;
            Assert.AreEqual(1, (int?)busta["versione"], "la busta non porta la sua versione");
            Assert.IsTrue(JsonNode.DeepEquals(dati, busta["dati"]), "nella busta non c'e' quello che la pagina ha mandato");
            var letto = Dichiarazioni(m)["Response"]!["dati"];
            Assert.IsTrue(JsonNode.DeepEquals(dati, letto), "riletto, non e' quello salvato: " + letto?.ToJsonString());
        }

        [TestMethod]
        public void UnaBustaCheNonSiLegge_ValeNessuna_ENonSiCancella() {
            foreach (var rotta in new[] { "non e' JSON", "{\"versione\":2,\"dati\":{}}", "{\"dati\":{}}", "{\"versione\":1,\"dati\":\"testo\"}" }) {
                var m = new Memoria { Documento = rotta };
                var r = Dichiarazioni(m)["Response"]!;
                Assert.IsNull(r["dati"], rotta);
                StringAssert.Contains((string?)r["nota"], "non si leggono", rotta);
                Assert.AreEqual(rotta, m.Documento, "una lettura ha toccato la memoria");
            }
        }

        [TestMethod]
        public void SiSalvaSoloNelProfiloDaCuiSonoStateLette() {
            var m = new Memoria { Documento = "{\"versione\":1,\"dati\":{\"chiavi\":{}}}" };
            var r = Dichiarazioni(m, "POST", DaSalvare("22222222-2222-2222-2222-222222222222", JsonNode.Parse("{\"chiavi\":{\"ap_site\":\"milano\"}}")));
            Assert.AreEqual(false, (bool?)r["Success"]);
            Assert.AreEqual(409, (int?)r["StatusCode"]);
            StringAssert.Contains((string?)r["Error"], "Profilo di casa", "il rifiuto non dice qual e' il profilo attivo");
            Assert.AreEqual(0, m.Scritture, "le dichiarazioni di un altro profilo sono state scritte in questo");
            var ok = Dichiarazioni(m, "POST", DaSalvare(m.Id.ToUpperInvariant(), JsonNode.Parse("{\"chiavi\":{}}")));
            Assert.AreEqual(true, (bool?)ok["Success"], "controllo positivo: lo stesso profilo, scritto in maiuscolo, e' lo stesso profilo");
        }

        [TestMethod]
        public void SenzaDati_TroppoGrandi_OConLaMemoriaGuasta_NonSiSalva() {
            var m = new Memoria();
            Assert.AreEqual(400, (int?)Dichiarazioni(m, "POST", "{\"id\":\"" + m.Id + "\"}")["StatusCode"]);
            Assert.AreEqual(400, (int?)Dichiarazioni(m, "POST", DaSalvare(m.Id, JsonValue.Create("testo")))["StatusCode"]);
            var grande = new JsonObject { ["chiavi"] = new JsonObject { ["ap_sites"] = new string('x', 300 * 1024) } };
            Assert.AreEqual(413, (int?)Dichiarazioni(m, "POST", DaSalvare(m.Id, grande))["StatusCode"]);
            Assert.AreEqual(0, m.Scritture);
            m.Guasto = "il disco e' pieno";
            var g = Dichiarazioni(m, "POST", DaSalvare(m.Id, JsonNode.Parse("{\"chiavi\":{}}")));
            Assert.AreEqual(500, (int?)g["StatusCode"]);
            StringAssert.Contains((string?)g["Error"], "disco");
        }
    }
}
