using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using AstroImage.NINA.Plugin.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace AstroImage.NINA.Plugin.Tests {

    /*  I PROGETTI APERTI (regia, 19 settembre 2026).
     *
     *  L'unita' non e' la notte, e' il progetto: il motore propone un profilo, la prima consegna lo congela, e da li' ogni
     *  domanda per quel bersaglio con quel banco lo porta in `opzioni.profilo`. «Apri un progetto nuovo» lo toglie. La meta'
     *  del motore — il profilo onorato, le premesse cambiate, il filtro che manca — non si prova qui: si prova dove il
     *  motore si esegue.
     */
    [TestClass]
    public class ProgettiTests {

        private static JsonObject Proposto(string bersaglio = "ngc6888") => new JsonObject {
            ["versione"] = 1, ["stato"] = "proposto",
            ["chiave"] = new JsonObject { ["bersaglio"] = bersaglio, ["tel"] = "rc8", ["red"] = 0.8, ["cam"] = "asi2600mm" },
            ["strada"] = "sho", ["strategia"] = "equilibrio",
            ["modi"] = new JsonObject { ["nb"] = new JsonObject { ["modo"] = "HCG", ["guadagno"] = 100, ["offset"] = 50 } },
            ["canali"] = new JsonObject { ["Ha"] = new JsonObject { ["posa"] = 600, ["filtro"] = "ha3", ["classe"] = "nb" } },
            ["premesse"] = new JsonObject { ["sito"] = new JsonObject { ["sqm"] = 20.8 } },
            ["pareggio"] = new JsonArray(),
        };
        private static JsonObject Banco(double red = 0.8) => new JsonObject {
            ["tel"] = new JsonObject { ["id"] = "rc8" }, ["red"] = red, ["cam"] = new JsonObject { ["id"] = "asi2600mm" },
        };
        private static readonly string BancoRc8 = ProgettiDelProfilo.ChiaveDelBanco(Banco());

        [TestMethod]
        public void LaPrimaConsegnaApre_ESoloUnProfiloProposto_ECongelaTogliendoLoStato() {
            var progetti = new List<ProgettiDelProfilo.Progetto>();

            Assert.IsTrue(ProgettiDelProfilo.Apri(progetti, "NGC 6888", BancoRc8, Proposto(), new DateTime(2026, 9, 19)));
            var p = progetti[0];
            Assert.AreEqual("2026-09-19", p.ApertoIl);
            Assert.IsFalse(p.Profilo.ContainsKey("stato"), "il profilo congelato porta ancora «proposto»");
            Assert.IsFalse(p.Profilo.ContainsKey("pareggio"), "il pareggio e' della proposta, non del progetto");
            Assert.IsTrue(p.Profilo.ContainsKey("premesse"), "senza le premesse dell'apertura il motore non sa che cosa e' cambiato");

            /*  la seconda consegna non riapre: il progetto resta quello */
            var altro = Proposto(); altro["strada"] = "hoo";
            Assert.IsFalse(ProgettiDelProfilo.Apri(progetti, "NGC 6888", BancoRc8, altro, new DateTime(2026, 9, 20)));
            Assert.AreEqual(1, progetti.Count);
            Assert.AreEqual("sho", progetti[0].Profilo["strada"]!.ToString());

            /*  un profilo gia' congelato non apre niente */
            var congelato = Proposto("m31"); congelato["stato"] = "congelato";
            Assert.IsFalse(ProgettiDelProfilo.Apri(progetti, "M31", BancoRc8, congelato, DateTime.Now));
        }

        [TestMethod]
        public void UnProgettoEIlBersaglioColSuoBanco_IlBersaglioSiConfrontaNormalizzato() {
            var progetti = new List<ProgettiDelProfilo.Progetto>();
            ProgettiDelProfilo.Apri(progetti, "NGC 6888", BancoRc8, Proposto(), DateTime.Now);

            Assert.IsNotNull(ProgettiDelProfilo.Trova(progetti, "ngc6888", BancoRc8), "«NGC 6888» e «ngc6888» sono lo stesso bersaglio");
            Assert.IsNull(ProgettiDelProfilo.Trova(progetti, "NGC 6888", ProgettiDelProfilo.ChiaveDelBanco(Banco(1.0))),
                "col riduttore diverso e' un altro progetto");
            Assert.IsNull(ProgettiDelProfilo.Trova(progetti, "NGC 7000", BancoRc8), "un altro bersaglio e' un altro progetto");
        }

        [TestMethod]
        public void LaChiaveDelBanco_TelescopioRiduttoreCamera_EIlDriverSenzaVoceColSuoNome() {
            Assert.AreEqual("rc8|0.8|asi2600mm", ProgettiDelProfilo.ChiaveDelBanco(Banco()));
            var driver = new JsonObject { ["tel"] = new JsonObject { ["id"] = "rc8" }, ["red"] = 1,
                ["cam"] = new JsonObject { ["nome"] = "ZWO ASI2600MM Pro", ["pixel_um"] = 3.76 } };
            Assert.AreEqual("rc8|1|nome:ZWO ASI2600MM Pro", ProgettiDelProfilo.ChiaveDelBanco(driver));
        }

        [TestMethod]
        public void IlProfiloPartiConLaDomanda_SenzaScavalcareQuelloDellaPagina() {
            var progetti = new List<ProgettiDelProfilo.Progetto>();
            ProgettiDelProfilo.Apri(progetti, "NGC 6888", BancoRc8, Proposto(), DateTime.Now);

            var domanda = new JsonObject { ["bersaglio"] = new JsonObject { ["id"] = "NGC 6888" }, ["banco"] = Banco(),
                                           ["opzioni"] = new JsonObject { ["copertura"] = "full" } };
            Assert.IsNotNull(ProgettiDelProfilo.AggiungiAllaDomanda(domanda, progetti));
            Assert.AreEqual("sho", domanda["opzioni"]!["profilo"]!["strada"]!.ToString(), "il profilo del progetto non parte");

            var sua = new JsonObject { ["bersaglio"] = new JsonObject { ["id"] = "NGC 6888" }, ["banco"] = Banco(),
                                       ["opzioni"] = new JsonObject { ["profilo"] = new JsonObject { ["strada"] = "della pagina" } } };
            ProgettiDelProfilo.AggiungiAllaDomanda(sua, progetti);
            Assert.AreEqual("della pagina", sua["opzioni"]!["profilo"]!["strada"]!.ToString(), "il Ponte ha scavalcato la pagina");

            var senza = new JsonObject { ["bersaglio"] = new JsonObject { ["id"] = "M31" }, ["banco"] = Banco() };
            Assert.IsNull(ProgettiDelProfilo.AggiungiAllaDomanda(senza, progetti));
            Assert.IsFalse(senza.ContainsKey("opzioni"), "senza progetto la domanda non cambia");
        }

        [TestMethod]
        public void IlDocumentoSiLeggeESiScrive_EUnaVersioneDiversaSiDice() {
            var progetti = new List<ProgettiDelProfilo.Progetto>();
            ProgettiDelProfilo.Apri(progetti, "NGC 6888", BancoRc8, Proposto(), new DateTime(2026, 9, 19));
            var letti = ProgettiDelProfilo.Leggi(ProgettiDelProfilo.Scrivi(progetti), out var nota);
            Assert.IsNull(nota);
            Assert.AreEqual(1, letti.Count);
            Assert.AreEqual("NGC 6888", letti[0].Bersaglio);
            Assert.AreEqual(BancoRc8, letti[0].Banco);
            Assert.AreEqual(600, letti[0].Profilo["canali"]!["Ha"]!["posa"]!.GetValue<int>());

            ProgettiDelProfilo.Leggi("{\"versione\":9,\"progetti\":[]}", out var notaV);
            Assert.AreEqual("progetti_versione", notaV);
            ProgettiDelProfilo.Leggi("non json", out var notaJ);
            Assert.AreEqual("progetti_illeggibili", notaJ);
            Assert.AreEqual(0, ProgettiDelProfilo.Leggi(null, out _).Count);
        }

        [TestMethod]
        public void ApriUnProgettoNuovo_ToglieQuelloDiQuestoBersaglioConQuestoBanco() {
            var progetti = new List<ProgettiDelProfilo.Progetto>();
            ProgettiDelProfilo.Apri(progetti, "NGC 6888", BancoRc8, Proposto(), DateTime.Now);
            ProgettiDelProfilo.Apri(progetti, "M31", BancoRc8, Proposto("m31"), DateTime.Now);

            Assert.IsTrue(ProgettiDelProfilo.Chiudi(progetti, "NGC 6888", BancoRc8));
            Assert.IsNull(ProgettiDelProfilo.Trova(progetti, "NGC 6888", BancoRc8));
            Assert.IsNotNull(ProgettiDelProfilo.Trova(progetti, "M31", BancoRc8), "ha chiuso anche un altro progetto");
            Assert.IsFalse(ProgettiDelProfilo.Chiudi(progetti, "NGC 6888", BancoRc8), "non c'era piu' niente da chiudere");
        }
    }
}
