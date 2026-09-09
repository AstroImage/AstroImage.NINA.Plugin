using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using AstroImage.NINA.Plugin.Models;
using AstroImage.NINA.Plugin.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace AstroImage.NINA.Plugin.Tests {

    /*  LA RUOTA VIRTUALE: che cosa sono, fisicamente, i vetri che hai in ruota.
     *
     *  Il difetto che questa roba corregge non era nel ponte: era in cio' che il ponte
     *  NON mandava. La richiesta portava solo `opzioni.filterNames`, che rietichetta i
     *  canali in uscita, e mai `ruota`. Il servizio in quel caso fa
     *  `M.ruota(DB.default_filters)`: il motore calcolava sui suoi dieci vetri di serie
     *  e poi rinominava il risultato con i nomi della ruota di chi guardava. Una
     *  prescrizione che SEMBRAVA fatta sul proprio equipaggiamento e non lo era.
     *
     *  Qui si prova il nucleo, che non nomina N.I.N.A. e infatti gira senza.
     */
    [TestClass]
    public class RuotaVirtualeTests {

        private static string Fixture(string nome) =>
            File.ReadAllText(Path.Combine(
                Path.GetDirectoryName(typeof(RuotaVirtualeTests).Assembly.Location)!,
                "Fixtures", "servizio", nome + ".json"));

        private static RuotaVirtuale Dichiarata(params (string nina, string? id)[] voci) =>
            new RuotaVirtuale {
                Vetri = voci.Select(v => new VoceRuota { Nina = v.nina, Motore = v.id }).ToList()
            };

        private static CatalogoDelMotore Catalogo(params (string id, string nome, string banda)[] v) =>
            new CatalogoDelMotore {
                Contratto = "1",
                Filtri = v.Select(x => new VetroDelMotore { Id = x.id, Nome = x.nome, Banda = x.banda }).ToList(),
                DiSerie = new List<string> { "ha3", "lum" },
            };

        // ─────────────────────────────────────────── 1 · il catalogo del motore

        [TestMethod]
        public void Catalogo_SiLeggeComeIlMotoreLoManda() {
            const string json = @"{""contratto"":""1"",""filtri"":[
                {""id"":""lult"",""nome"":""Optolong L-Ultimate 3 nm"",""banda"":""dual"",
                 ""lambda_nm"":578,""fwhm_nm"":3,""bande"":[""Ha"",""OIII""],""dual"":true,
                 ""per_mono"":null,""per_cfa"":true,""costruttore"":""Optolong""},
                {""id"":""ha3"",""nome"":""Ha 3 nm"",""banda"":""Ha"",""lambda_nm"":656.3,
                 ""fwhm_nm"":3,""dual"":false,""per_mono"":true,""per_cfa"":null}],
              ""di_serie"":[""ha3"",""lum""]}";

            var c = JsonSerializer.Deserialize<CatalogoDelMotore>(json, SequenceModel.OpzioniJson)!;

            Assert.AreEqual("1", c.Contratto);
            Assert.AreEqual(2, c.Filtri!.Count);
            var lult = c.Filtri.First(x => x.Id == "lult");
            Assert.IsTrue(lult.Dual, "il duale si dichiara duale");
            CollectionAssert.AreEqual(new[] { "Ha", "OIII" }, lult.Bande!.ToArray(),
                "le bande servono: dieci duali fanno Ha+OIII e l'Askar D2 fa SII+OIII");
            Assert.AreEqual(true, lult.PerCfa);
            /*  TRE STATI E NON DUE. Nel catalogo del motore `per_mono` e' dichiarato
                vero oppure non e' dichiarato affatto — un `false` non esiste. Se
                arrotondassimo il nullo a «no», meta' catalogo sparirebbe dalle tendine
                di chi ha una monocromatica. */
            Assert.IsNull(lult.PerMono, "«non dichiarato» deve restare nullo, non diventare «no»");
            Assert.IsNull(c.Filtri.First(x => x.Id == "ha3").PerCfa);
            CollectionAssert.AreEqual(new[] { "ha3", "lum" }, c.DiSerie!.ToArray());
        }

        // ─────────────────────────────────── 2 · la dichiarazione, avanti e indietro

        [TestMethod]
        public void Dichiarazione_AndataERitorno() {
            var r = Dichiarata(("ULTIMATE", "lult"), ("LPS_P2 IDAS", "idas"));
            var riletta = DichiarazioneRuota.Leggi(DichiarazioneRuota.Scrivi(r), out var nota);

            Assert.IsNull(nota);
            Assert.AreEqual(DichiarazioneRuota.VersioneCorrente, riletta.Versione);
            Assert.AreEqual("lult", DichiarazioneRuota.IdPerNome(riletta, "ULTIMATE"));
            /*  Maiuscole e spazi ai bordi non fanno un vetro diverso, per chiunque
                tranne che per un confronto di stringhe. */
            Assert.AreEqual("lult", DichiarazioneRuota.IdPerNome(riletta, "ultimate"));
            Assert.AreEqual("ULTIMATE", DichiarazioneRuota.NomePerId(riletta, "lult"),
                "il giro all'indietro e' quello che serve a SwitchFilter");
        }

        [TestMethod]
        public void UnNomeNonDichiarato_NonProduceUnIndovinello() {
            var r = Dichiarata(("ULTIMATE", "lult"), ("V4 IDAS", null));

            Assert.IsNull(DichiarazioneRuota.IdPerNome(r, "V4 IDAS"),
                "dichiarato a null vuol dire «non lo so», e non autorizza a scegliere");
            Assert.IsNull(DichiarazioneRuota.IdPerNome(r, "HA 7NM"), "e un nome mai visto nemmeno");
            Assert.IsNull(DichiarazioneRuota.NomePerId(r, "lenh"),
                "un vetro che il motore sceglie ma che non hai dichiarato non ha un nome sulla tua ruota");
        }

        // ───────────────────────────────── 3 e 4 · che cosa parte come `ruota`

        [TestMethod]
        public void RuotaNonConfigurata_NonMandaUnElencoVuoto() {
            /*  Vuoto e non dichiarato sono due cose diverse: un `ruota: []` direbbe al
                motore «non possiedo nessun filtro», che e' una bugia diversa da «non
                te l'ho detto». Con zero dichiarati la richiesta non deve portare
                `ruota` affatto — e il servizio allora usa i suoi vetri di serie, cosa
                che la pagina deve dire forte a chi guarda. */
            Assert.AreEqual(0, DichiarazioneRuota.IdDichiarati(new RuotaVirtuale()).Count);
            Assert.AreEqual(0, DichiarazioneRuota.IdDichiarati(null).Count);
            Assert.AreEqual(0, DichiarazioneRuota.IdDichiarati(Dichiarata(("V4 IDAS", null))).Count,
                "un filtro in ruota ma non dichiarato non entra nell'inventario");
        }

        [TestMethod]
        public void RuotaConfigurata_MandaGliIdentificativi_NonINomi() {
            var r = Dichiarata(("ULTIMATE", "lult"), ("LPS_P2 IDAS", "idas"), ("V4 IDAS", null));
            var ruota = DichiarazioneRuota.IdDichiarati(r);

            CollectionAssert.AreEquivalent(new[] { "lult", "idas" }, ruota.ToArray());
            CollectionAssert.DoesNotContain(ruota.ToArray(), "ULTIMATE",
                "al motore vanno gli identificativi del suo catalogo, mai i nomi della ruota");
        }

        [TestMethod]
        public void PiuVetriPerLaStessaBanda_ViaggianoTuttiEDue() {
            /*  E' il caso che rende utile mandare l'inventario invece della mappa
                canale→vetro: se possiedi sia un L-eNhance sia un L-Ultimate, chi sceglie
                dev'essere il motore in base al cielo, non una tabella scritta a mano. */
            var r = Dichiarata(("ENHA", "lenh"), ("ULTIMATE", "lult"));
            CollectionAssert.AreEquivalent(new[] { "lenh", "lult" },
                DichiarazioneRuota.IdDichiarati(r).ToArray());
        }

        [TestMethod]
        public void LoStessoIdentificativoDueVolte_NonSiRipete() {
            var r = Dichiarata(("HO A", "lult"), ("HO B", "lult"));
            Assert.AreEqual(1, DichiarazioneRuota.IdDichiarati(r).Count);
        }

        // ────────────────────────────────────────── 7 · configurazione malandata

        [TestMethod]
        public void ConfigurazioneCorrotta_NonSolleva_ENonInventa() {
            foreach (var brutta in new[] { "{ non e' json", "[]", "{\"vetri\":\"pippo\"}", "null" }) {
                var r = DichiarazioneRuota.Leggi(brutta, out var nota);
                Assert.IsNotNull(r, "deve tornare sempre qualcosa: " + brutta);
                Assert.AreEqual(0, r.Vetri.Count, "e non deve inventarsi vetri: " + brutta);
                Assert.IsFalse(string.IsNullOrWhiteSpace(nota),
                    "e deve DIRE che qualcosa non andava: " + brutta);
            }
        }

        [TestMethod]
        public void ConfigurazioneAssente_NonEUnErrore() {
            var r = DichiarazioneRuota.Leggi(null, out var nota);
            Assert.AreEqual(0, r.Vetri.Count);
            Assert.IsNull(nota, "non aver mai configurato non e' un guasto da segnalare");
        }

        [TestMethod]
        public void UnaVersioneFutura_NonSiInterpretaAMemoria() {
            var json = "{\"versione\":99,\"vetri\":[{\"nina\":\"ULTIMATE\",\"motore\":\"lult\"}]}";
            var r = DichiarazioneRuota.Leggi(json, out var nota);

            Assert.AreEqual(0, r.Vetri.Count,
                "meglio nessuna mappa che una mappa letta con la grammatica sbagliata");
            StringAssert.Contains(nota ?? "", "99");
        }

        [TestMethod]
        public void DueVociPerLoStessoNome_VinceLaPrimaESiDice() {
            var json = "{\"versione\":1,\"vetri\":[{\"nina\":\"HA\",\"motore\":\"ha3\"}," +
                       "{\"nina\":\"ha\",\"motore\":\"ha7\"}]}";
            var r = DichiarazioneRuota.Leggi(json, out var nota);

            Assert.AreEqual(1, r.Vetri.Count);
            Assert.AreEqual("ha3", DichiarazioneRuota.IdPerNome(r, "HA"));
            Assert.IsFalse(string.IsNullOrWhiteSpace(nota), "una scelta arbitraria va dichiarata");
        }

        // ──────────────────────────── 8 · un filtro rinominato in N.I.N.A.

        [TestMethod]
        public void FiltroRinominato_LaVecchiaVoceRestaVISIBILE_NonSparisce() {
            /*  E' la ragione per cui la chiave e' il NOME e non lo slot. Con lo slot,
                spostare un vetro lascerebbe una configurazione valida e SBAGLIATA, in
                silenzio. Col nome, rinominarlo la rompe in modo visibile: la voce
                diventa orfana, il nome nuovo compare non mappato, e si ripunta.        */
            var righe = RiconciliaRuota.Righe(
                new[] { ("L-ULT", (int?)3) },                      // rinominato in N.I.N.A.
                Dichiarata(("ULTIMATE", "lult")),                  // dichiarato col nome vecchio
                Catalogo(("lult", "Optolong L-Ultimate 3 nm", "dual")),
                cameraAMatrice: null);

            var nuova = righe.Single(r => r.Nina == "L-ULT");
            Assert.AreEqual(RiconciliaRuota.Stato.NonMappato, nuova.Stato);
            Assert.AreEqual(3, nuova.Slot);

            var vecchia = righe.Single(r => r.Nina == "ULTIMATE");
            Assert.AreEqual(RiconciliaRuota.Stato.Orfano, vecchia.Stato,
                "la vecchia si vede, cosi' si ripunta invece di ricominciare");
            Assert.IsNull(vecchia.Slot, "e non ha piu' uno slot, perche' in ruota non c'e'");
        }

        [TestMethod]
        public void UnIdentificativoCheIlMotoreNonConoscePiu_SiDichiaraIgnoto() {
            var righe = RiconciliaRuota.Righe(
                new[] { ("ULTIMATE", (int?)3) },
                Dichiarata(("ULTIMATE", "vetro_sparito")),
                Catalogo(("lult", "Optolong L-Ultimate 3 nm", "dual")),
                cameraAMatrice: null);

            Assert.AreEqual(RiconciliaRuota.Stato.Ignoto, righe.Single().Stato,
                "un catalogo che cambia non deve trasformarsi in un mapping silenziosamente rotto");
        }

        [TestMethod]
        public void DueSlotConLoStessoNome_SiSegnALA() {
            /*  SwitchFilter risolve per nome e prende il primo: l'ambiguita' e' di
                N.I.N.A., non nostra, ma va detta a chi guarda. */
            var righe = RiconciliaRuota.Righe(
                new[] { ("Ha", (int?)1), ("Ha", (int?)5) },
                Dichiarata(("Ha", "ha3")),
                Catalogo(("ha3", "Ha 3 nm", "Ha")),
                cameraAMatrice: null);

            Assert.AreEqual(2, righe.Count);
            Assert.IsTrue(righe.All(r => r.NomeAmbiguo));
        }

        // ────────────────────────────── 11 · monocromatica contro camera a matrice

        [TestMethod]
        public void LAvvisoSullaCamera_NonSiInventaUnNo() {
            var cat = new CatalogoDelMotore {
                Filtri = new List<VetroDelMotore> {
                    new VetroDelMotore { Id = "lult", Nome = "L-Ultimate", Banda = "dual", PerCfa = true },
                    new VetroDelMotore { Id = "ha3", Nome = "Ha 3 nm", Banda = "Ha", PerMono = true },
                }
            };
            var dich = Dichiarata(("ULTIMATE", "lult"), ("HA", "ha3"));
            var inRuota = new[] { ("ULTIMATE", (int?)1), ("HA", (int?)2) };

            var aColori = RiconciliaRuota.Righe(inRuota, dich, cat, cameraAMatrice: true);
            Assert.AreEqual(true, aColori.Single(r => r.Nina == "ULTIMATE").AdattoAllaCamera);
            Assert.IsNull(aColori.Single(r => r.Nina == "HA").AdattoAllaCamera,
                "il catalogo non dichiara `per_cfa` sull'Ha stretto: «non si sa» non e' «no»");

            var senzaSapere = RiconciliaRuota.Righe(inRuota, dich, cat, cameraAMatrice: null);
            Assert.IsTrue(senzaSapere.All(r => r.AdattoAllaCamera is null),
                "se non si sa che camera c'e', non si consiglia niente");
        }

        [TestMethod]
        public void SenzaCatalogo_LaRuotaSiVEDE_LoSTESSO() {
            /*  Il servizio spento non deve rendere invisibile la propria ruota: si
                configurera' dopo, ma intanto si vede che cosa c'e'. */
            var righe = RiconciliaRuota.Righe(
                new[] { ("ULTIMATE", (int?)3) }, Dichiarata(("ULTIMATE", "lult")),
                catalogo: null, cameraAMatrice: null);

            Assert.AreEqual(1, righe.Count);
            Assert.AreEqual(RiconciliaRuota.Stato.Ignoto, righe.Single().Stato,
                "senza catalogo non si puo' confermare l'identificativo, e non lo si finge");
        }

        // ─────────── 5 e 6 · quale vetro il motore dice di aver usato, sul dato vero

        [TestMethod]
        public void DallaRispostaVERA_SiLeggeIlVetroPerOgniCanale() {
            var perCanale = VetriDellaPrescrizione.PerCanale(Fixture("prescrizione-ok"));

            Assert.AreEqual("lult", perCanale["Ha+OIII"],
                "il duale su cui il motore ha davvero fatto il conto");
            Assert.AreEqual("idas", perCanale["R"]);
            Assert.AreEqual("idas", perCanale["G"]);
            Assert.AreEqual("idas", perCanale["B"]);
            Assert.IsFalse(perCanale.Keys.Any(k => k.StartsWith("__")),
                "le chiavi di servizio del motore non sono canali");
        }

        [TestMethod]
        public void UnBloccoSuPiuCanali_HaUnVetroSOLO() {
            /*  Su una camera a matrice il blocco «R+G+B» e' un'unica ripresa dietro un
                unico filtro, e infatti i tre canali rispondono lo stesso identificativo. */
            var perCanale = VetriDellaPrescrizione.PerCanale(Fixture("prescrizione-ok"));
            var id = VetriDellaPrescrizione.DelBlocco(new[] { "R", "G", "B" }, perCanale, out var perche);

            Assert.AreEqual("idas", id);
            Assert.IsNull(perche);
        }

        [TestMethod]
        public void CanaliCheDichiaranoVetriDIVERSI_NonSiRisolvonoACaso() {
            var perCanale = new Dictionary<string, string> { ["R"] = "idas", ["G"] = "lpro" };
            var id = VetriDellaPrescrizione.DelBlocco(new[] { "R", "G" }, perCanale, out var perche);

            Assert.IsNull(id, "sceglierne uno vorrebbe dire riprendere col vetro sbagliato senza dirlo");
            Assert.IsFalse(string.IsNullOrWhiteSpace(perche));
        }

        [TestMethod]
        public void UnaRispostaSenzaPosa_NonSolleva_ETornaVuota() {
            foreach (var corpo in new[] { null, "", "{}", "non json", "{\"prodotto\":{}}" }) {
                var m = VetriDellaPrescrizione.PerCanale(corpo);
                Assert.AreEqual(0, m.Count, "corpo: " + (corpo ?? "(null)"));
            }
        }

        // ───────────────────────────────────────────────── il nucleo resta puro

        [TestMethod]
        public void LaRuotaVirtualeNonNominaNina() {
            /*  Stessa regola del resto del contratto: questa roba deve poter girare in
                un banco di prova senza N.I.N.A. installata, ed e' il motivo per cui la
                memoria e' dietro un'interfaccia di due metodi. */
            foreach (var t in new[] { typeof(RuotaVirtuale), typeof(VoceRuota),
                                      typeof(CatalogoDelMotore), typeof(VetroDelMotore),
                                      typeof(DichiarazioneRuota), typeof(RiconciliaRuota),
                                      typeof(VetriDellaPrescrizione) }) {
                foreach (var r in t.Assembly.GetReferencedAssemblies()) { }
                var nomi = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                        BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                            .SelectMany(m => m.GetParameters().Select(p => p.ParameterType)
                                              .Concat(new[] { m.ReturnType }))
                            .Concat(t.GetProperties().Select(p => p.PropertyType))
                            .Select(x => x.Namespace ?? "")
                            .Where(n => n.StartsWith("NINA", StringComparison.Ordinal))
                            .Distinct().ToList();
                Assert.AreEqual(0, nomi.Count,
                    t.Name + " nomina N.I.N.A.: " + string.Join(", ", nomi));
            }
        }
    }
}
