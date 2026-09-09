using System.Text.Json.Nodes;
using AstroImage.NINA.Plugin.Models;
using AstroImage.NINA.Plugin.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace AstroImage.NINA.Plugin.Tests {

    /*  DOVE STAI RIPRENDENDO, E DA DOVE LO SAPPIAMO.
     *
     *  Il difetto: la pagina spediva sette numeri scritti dentro — 45.9, 10.2, sqm 20.8,
     *  seeing 1.6, rms 0.6, altezza 20, notti serene 0.35. Sono Borno, l'altopiano di chi
     *  ha scritto il motore. Su quella macchina non si vedeva; ovunque altro il ponte
     *  pianificava la notte di qualcun altro spacciandola per la tua, e senza un errore:
     *  cambiare l'SQM da 20.8 a 19.0 porta la posa da 600 s x 29 a 240 s x 60.
     *
     *  Qui si prova che nessun valore si inventa, che la provenienza viaggia con il
     *  numero, e — la cosa che conta di piu' — che quando un dato manca resta mancante.
     */
    [TestClass]
    public class SitoTests {

        private static SitoDiRipresa DaNina(double? lat = 45.95, double? lon = 10.2,
                                            double? sqm = null, double? seeing = null, double? rms = null) =>
            new SitoDiRipresa { Lat = lat, Lon = lon, Sqm = sqm, Seeing = seeing, Rms = rms };

        // ─────────────────────────────────────── la geometria viene solo dal profilo

        [TestMethod]
        public void LaGeometriaVieneDalProfilo_ENonSiDichiara() {
            var s = DichiarazioneSito.Unisci(DaNina(), new SitoDichiarato { Sqm = 20.8 });

            Assert.AreEqual(45.95, s.Lat);
            Assert.AreEqual(10.2, s.Lon);
            Assert.AreEqual(DichiarazioneSito.DaProfilo, s.Provenienza!["lat"]);
            Assert.AreEqual(DichiarazioneSito.DaProfilo, s.Provenienza["lon"]);
        }

        [TestMethod]
        public void SenzaCoordinate_NonSiINVENTANO() {
            /*  Il caso peggiore possibile: un ripiego silenzioso rimetterebbe Borno
                addosso a chi riprende da un'altra parte del mondo. */
            var s = DichiarazioneSito.Unisci(DaNina(lat: null, lon: null), new SitoDichiarato());

            Assert.IsNull(s.Lat);
            Assert.IsNull(s.Lon);
            Assert.AreEqual(DichiarazioneSito.Assente, s.Provenienza!["lat"]);
        }

        // ──────────────────────── misura contro dichiarazione: vince sempre la misura

        [TestMethod]
        public void UnaMISURA_BatteUnaDichiarazione() {
            /*  Se una stazione col misuratore c'e', il numero scritto a mano l'anno
                scorso non deve sovrascriverla. */
            var s = DichiarazioneSito.Unisci(DaNina(sqm: 21.4), new SitoDichiarato { Sqm = 20.8 });

            Assert.AreEqual(21.4, s.Sqm);
            Assert.AreEqual(DichiarazioneSito.Misurato, s.Provenienza!["sqm"]);
        }

        [TestMethod]
        public void SenzaMisura_ValeLaDichiarazione_EsiDICE() {
            var s = DichiarazioneSito.Unisci(DaNina(), new SitoDichiarato { Sqm = 20.8, Seeing = 1.6 });

            Assert.AreEqual(20.8, s.Sqm);
            Assert.AreEqual(DichiarazioneSito.Dichiarato, s.Provenienza!["sqm"],
                "un SQM scritto a mano e uno misurato non devono somigliarsi");
            Assert.AreEqual(DichiarazioneSito.Dichiarato, s.Provenienza["seeing"]);
        }

        [TestMethod]
        public void SenzaNIENTE_IlValoreRESTA_MANCANTE() {
            /*  E' la regola che chiude il difetto: nessun valore di serie nascosto. */
            var s = DichiarazioneSito.Unisci(DaNina(), new SitoDichiarato());

            Assert.IsNull(s.Sqm);
            Assert.IsNull(s.Seeing);
            Assert.IsNull(s.Rms);
            Assert.IsNull(s.HorizonMin);
            Assert.IsNull(s.ClearFrac);
            foreach (var campo in new[] { "sqm", "seeing", "rms", "horizonMin", "clearFrac" })
                Assert.AreEqual(DichiarazioneSito.Assente, s.Provenienza![campo], campo);
        }

        [TestMethod]
        public void NessunValoreDiBORNO_SopravviveDaNessunaParte() {
            /*  La prova esplicita contro il difetto: nessuno dei sette numeri deve
                comparire da solo. Se un domani qualcuno rimettesse un ripiego, questo
                test lo prende — ed e' la ragione per cui elenca proprio quei numeri. */
            var s = DichiarazioneSito.Unisci(new SitoDiRipresa(), new SitoDichiarato());

            foreach (var v in new double?[] { s.Lat, s.Lon, s.Sqm, s.Seeing, s.Rms,
                                              s.HorizonMin, s.ClearFrac })
                Assert.IsNull(v, "un sito senza dati deve restare senza dati");
        }

        // ──────────────────────────── l'orizzonte e le notti serene: solo dichiarati

        [TestMethod]
        public void AltezzaMinimaENottiSerene_RestanoDichiarate() {
            /*  N.I.N.A. ha un orizzonte per AZIMUT, il motore un numero solo: ridurre
                l'uno all'altro sceglierebbe di nascosto quale meta' del cielo buttare
                via. E le notti serene non le misura nessuno strumento. */
            var s = DichiarazioneSito.Unisci(DaNina(), new SitoDichiarato { HorizonMin = 25, ClearFrac = 0.33 });

            Assert.AreEqual(25, s.HorizonMin);
            Assert.AreEqual(DichiarazioneSito.Dichiarato, s.Provenienza!["horizonMin"]);
            Assert.AreEqual(0.33, s.ClearFrac);
            Assert.AreEqual(DichiarazioneSito.Dichiarato, s.Provenienza["clearFrac"]);
        }

        // ─────────────────────────────── si dice PRIMA che manca qualcosa di decisivo

        [TestMethod]
        public void SenzaSQM_SiDICE_PrimaDiChiedere() {
            /*  Il servizio, con un sito senza SQM, risponde con le ore e NESSUNA
                sequenza — misurato. Senza questo avviso chi guarda vedrebbe un
                risultato vuoto senza sapere perche'. */
            var manca = DichiarazioneSito.CheCosaManca(
                DichiarazioneSito.Unisci(DaNina(), new SitoDichiarato()));

            Assert.IsNotNull(manca);
            StringAssert.Contains(manca!, "SQM");
        }

        [TestMethod]
        public void SenzaCoordinate_SiDiceANCHE_Quello() {
            var manca = DichiarazioneSito.CheCosaManca(
                DichiarazioneSito.Unisci(DaNina(lat: null, lon: null), new SitoDichiarato { Sqm = 20.8 }));

            Assert.IsNotNull(manca);
            StringAssert.Contains(manca!, "coordinate");
        }

        [TestMethod]
        public void ConCoordinateESQM_NonMancaNiente() {
            var manca = DichiarazioneSito.CheCosaManca(
                DichiarazioneSito.Unisci(DaNina(), new SitoDichiarato { Sqm = 20.8 }));

            Assert.IsNull(manca, "seeing, RMS e notti serene hanno un comportamento di serie nel motore");
        }

        // ─────────────────────────────────────────── salvataggio, come per la ruota

        [TestMethod]
        public void Dichiarazione_AndataERitorno() {
            var d = new SitoDichiarato { Sqm = 20.8, Seeing = 1.6, Rms = 0.6, HorizonMin = 20, ClearFrac = 0.33 };
            var r = DichiarazioneSito.Leggi(DichiarazioneSito.Scrivi(d), out var nota);

            Assert.IsNull(nota);
            Assert.AreEqual(20.8, r.Sqm);
            Assert.AreEqual(0.33, r.ClearFrac);
            Assert.AreEqual(DichiarazioneSito.VersioneCorrente, r.Versione);
        }

        [TestMethod]
        public void ConfigurazioneCorrotta_NonSolleva_ENonInventa() {
            foreach (var brutta in new[] { "{ non e' json", "[]", "null", "{\"sqm\":\"molto\"}" }) {
                var r = DichiarazioneSito.Leggi(brutta, out _);
                Assert.IsNotNull(r, brutta);
                Assert.IsNull(r.Sqm, "da una configurazione illeggibile non esce un numero: " + brutta);
            }
        }

        [TestMethod]
        public void UnaVersioneFutura_NonSiInterpreta() {
            var r = DichiarazioneSito.Leggi("{\"versione\":99,\"sqm\":20.8}", out var nota);
            Assert.IsNull(r.Sqm);
            StringAssert.Contains(nota ?? "", "99");
        }

        // ───────────────────────────── il salvataggio legge dove la pagina scrive

        [TestMethod]
        public void IlSalvataggio_LeggeDoveLaPaginaScrive() {
            var m = JsonNode.Parse("{\"id\":\"r1\",\"azione\":\"salvaSito\",\"corpo\":{\"sito\":" +
                                   "{\"sqm\":20.8,\"seeing\":1.6,\"rms\":null}}}");
            var d = DichiarazioneSito.DalMessaggio(m, out var perCheNo);

            Assert.IsNotNull(d, perCheNo);
            Assert.AreEqual(20.8, d!.Sqm);
            Assert.AreEqual(1.6, d.Seeing);
            Assert.IsNull(d.Rms, "un campo lasciato vuoto resta vuoto, non diventa zero");
        }

        [TestMethod]
        public void UnaRichiestaSenzaSito_NonAZZERA_Niente() {
            /*  Stessa guardia della ruota, per lo stesso difetto trovato sul campo:
                una chiave assente non e' «azzera tutto». */
            foreach (var m in new[] { JsonNode.Parse("{\"corpo\":{}}"),
                                      JsonNode.Parse("{\"sito\":{\"sqm\":20}}"),
                                      JsonNode.Parse("{}") }) {
                var d = DichiarazioneSito.DalMessaggio(m, out var perCheNo);
                Assert.IsNull(d, "va rifiutata, non eseguita");
                Assert.IsFalse(string.IsNullOrWhiteSpace(perCheNo));
            }
        }

        [TestMethod]
        public void IlSitoNonNominaNina() {
            /*  Come il resto del contratto: deve girare in un banco di prova senza
                N.I.N.A. installata. La lettura dal profilo sta in SitoDelProfilo, che
                e' l'unico pezzo che la nomina. */
            foreach (var t in new[] { typeof(SitoDiRipresa), typeof(SitoDichiarato),
                                      typeof(DichiarazioneSito) }) {
                foreach (var p in t.GetProperties())
                    Assert.IsFalse((p.PropertyType.Namespace ?? "").StartsWith("NINA"),
                        t.Name + "." + p.Name + " nomina N.I.N.A.");
            }
        }
    }
}
