using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text.Json.Nodes;
using AstroImage.NINA.Plugin.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace AstroImage.NINA.Plugin.Tests {

    /*  IL CAPITOLO DELLE ORE, E I SUOI TESTIMONI (25 settembre 2026).
     *  ═══════════════════════════════════════════════════════════════════════════════════════════════════════
     *  Tre sezioni nuove del pannello: le frasi sotto il piano (dove si fermano colore e luminanza, come si spartiscono
     *  le ore quando i tetti sono raggiunti, l'aggiunta, la tavolozza a righe, le ore d'avanzo), l'origine delle ore riga
     *  per riga, e nella profondita' l'elemento con μ_lim. Vale la regola di CampiAScermoTests: per ogni cifra e per ogni
     *  codice mostrati c'e' una relazione che si romperebbe se quel valore, da solo, fosse un altro.
     *
     *  LE FIXTURE sono le otto `servizio/ore-*.json`, risposte vere del servizio ridotte ai pezzi del capitolo. Le
     *  relazioni non portano la fisica di chi manda: dicono come i pezzi stanno fra loro — le ore della soglia sono il
     *  pavimento del canale, le percentuali vengono dai rapporti che la stessa frase dichiara, le ore spese sono la somma
     *  dei gruppi —, e valgono su qualunque prescrizione.
     *
     *  RESTANO SENZA TESTIMONE, dichiarati: quante immagini o regioni stanno dietro un rapporto (`campione`,
     *  `campioneSHO`, `campioneHOO`, `regioni`), il minimo e il massimo di quel rapporto (`rapportoMinimo`,
     *  `rapportoMassimo`), il nome di `dentro` e i due numeri della definizione di μ_lim (`sigma`, `lato`). Contano,
     *  nominano o definiscono: nessun altro campo del prodotto li vincola. Che arrivino a schermo uguali alla pagina lo
     *  controlla chi apre le due facce e ne confronta le cifre.                                                    */
    [TestClass]
    public class OreDelCanaleTests {

        private static readonly string[] Casi = { "m81-lrgb", "m81-halrgb", "sho-oltre-i-tetti", "bordo-stimato",
                                                  "bordo-misurato", "fondo-locale", "tetto-unico", "punti-d-arresto" };

        private static readonly ResourceManager It = new("AstroImage.NINA.Plugin.Localization.Strings_it", typeof(SequenceModel).Assembly);
        private static readonly ResourceManager En = new("AstroImage.NINA.Plugin.Localization.Strings_en", typeof(SequenceModel).Assembly);

        private static string Cartella =>
            Path.Combine(Path.GetDirectoryName(typeof(OreDelCanaleTests).Assembly.Location)!, "Fixtures", "servizio");

        private static JsonObject Prodotto(string caso) =>
            JsonNode.Parse(File.ReadAllText(Path.Combine(Cartella, "ore-" + caso + ".json")))!["prodotto"]!.AsObject();

        private static IEnumerable<(string Caso, JsonObject P)> Prodotti() => Casi.Select(c => (c, Prodotto(c)));

        private static JsonObject Prescrizione(JsonObject p) => p["prescrizione"]!.AsObject();
        private static IEnumerable<JsonObject> Alloc(JsonObject p) => Prescrizione(p)["alloc"]!.AsArray().Select(g => g!.AsObject());
        private static IEnumerable<JsonObject> Vivi(JsonObject p) => Alloc(p).Where(g => !Vero(g["dropped"]));
        private static JsonObject? Gruppo(JsonObject p, string id) => Vivi(p).FirstOrDefault(g => S(g["id"]) == id);
        private static JsonObject? Canale(JsonObject p, string? id) =>
            id == null ? null : p["valutazione"]?["budget"]?[id] as JsonObject;
        private static IEnumerable<(string Caso, JsonObject P, string Canale, JsonObject V)> Canali() =>
            Prodotti().SelectMany(x => x.P["valutazione"]!["budget"]!.AsObject().Select(kv => (x.Caso, x.P, kv.Key, kv.Value!.AsObject())));
        private static IEnumerable<string?> Bande(JsonObject g) => g["bands"]!.AsArray().Select(b => S(b));

        private static double? N(JsonNode? n) => n is JsonValue v && v.TryGetValue<double>(out var d) ? d : (double?)null;
        private static string? S(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
        private static bool Vero(JsonNode? n) => n is JsonValue v && v.TryGetValue<bool>(out var b) && b;
        private static double D(JsonNode? n, string dove) {
            var x = N(n);
            Assert.IsNotNull(x, dove + ": il numero non c'e'");
            return x!.Value;
        }
        /*  le relazioni sono esatte, i doppi no: si accetta una differenza di un miliardesimo del piu' grande dei due */
        private static bool Uguali(double a, double b) {
            var grandezza = Math.Max(1.0, Math.Max(Math.Abs(a), Math.Abs(b)));
            return Math.Abs(a - b) <= grandezza / 1e9;
        }

        private static void HaLeParole(string chiave, string dove) {
            Assert.IsFalse(string.IsNullOrEmpty(It.GetString(chiave, CultureInfo.InvariantCulture)), $"{dove}: manca la voce italiana {chiave}");
            Assert.IsFalse(string.IsNullOrEmpty(En.GetString(chiave, CultureInfo.InvariantCulture)), $"{dove}: manca la voce inglese {chiave}");
        }
        private static string Pagina() {
            using var s = typeof(SequenceModel).Assembly.GetManifestResourceStream("AstroImage.NINA.Plugin.Views.Pagina.prova.js");
            Assert.IsNotNull(s, "prova.js non e' incorporata nel plugin");
            return new StreamReader(s!).ReadToEnd();
        }
        /*  un codice di una mappa della pagina: la mappa letterale lo nomina, e la sua voce ha le parole nelle due lingue */
        private static void CodiceNellaMappa(string codice, IReadOnlyDictionary<string, string> mappa, string dove) {
            Assert.IsTrue(mappa.TryGetValue(codice, out var chiave), $"{dove}: il codice «{codice}» non e' nella mappa della pagina: " +
                "a schermo resterebbe una sigla");
            HaLeParole(chiave!, dove);
        }
        /*  un codice che la pagina confronta per nome: il Ponte lo dichiara qui con la sua voce, la pagina lo nomina, e la
         *  voce ha le parole nelle due lingue */
        private static void CodiceNominato(string codice, IReadOnlyDictionary<string, string> conosciuti, string dove) {
            Assert.IsTrue(conosciuti.TryGetValue(codice, out var chiave), $"{dove}: il codice «{codice}» il Ponte non lo conosce: " +
                "a schermo resterebbe una sigla");
            Assert.IsTrue(Pagina().Contains("'" + codice + "'"), $"{dove}: la pagina non nomina il codice «{codice}»");
            HaLeParole(chiave!, dove);
        }

        /*  I GRUPPI E I LORO CANALI. `critical` segna al piu' un gruppo, e quel gruppo e' il critico della prescrizione;
         *  se nessuno e' segnato — la fixture del bordo misurato, dove la scheda indicherebbe un canale che la tecnica non
         *  riprende — il critico resta comunque uno dei gruppi. Con una banda sola il gruppo coincide col canale: i suoi tre
         *  numeri ripetono quelli del budget, oppure, se la tecnica riprende il soggetto in RGB, la terna `soggetto` che il
         *  budget affianca a quella delle stelle. Soltanto la L che comanda ha `oreProprie`: coincidono col budget, e il
         *  gruppo le supera nella soglia e nel tetto. In ogni caso pavimento, utile e tetto sono in ordine crescente. */
        [TestMethod]
        public void IGruppi_SonoILoroCanaliEStannoInFila() {
            int provati = 0, delSoggetto = 0, proprie = 0;
            foreach (var (caso, p) in Prodotti()) {
                var pr = Prescrizione(p);
                var soggetto = S(pr["road"]?["banda_larga"]) == "soggetto";
                var critico = S(pr["critGroup"]);
                var segnati = Alloc(p).Where(g => Vero(g["critical"])).Select(g => S(g["id"])).ToList();
                Assert.IsTrue(segnati.Count <= 1 && segnati.All(i => i == critico), $"{caso}: i gruppi segnati critici sono " +
                    $"«{string.Join(", ", segnati)}», il gruppo critico e' «{critico}»");
                Assert.IsTrue(Alloc(p).Any(g => S(g["id"]) == critico), $"{caso}: il gruppo critico «{critico}» non e' fra i gruppi");
                foreach (var g in Alloc(p)) {
                    var id = S(g["id"]);
                    var dove = caso + "/" + id;
                    if (Vero(g["dropped"])) continue;
                    double f = D(g["floor"], dove), u = D(g["useful"], dove), t = D(g["sat"], dove);
                    Assert.IsTrue(f <= u + 1e-9 && (t <= 0 || u <= t + 1e-9), $"{dove}: pavimento {f}, utile {u}, tetto {t} non stanno in fila");
                    var bande = Bande(g).ToList();
                    if (bande.Count != 1) continue;
                    Assert.AreEqual(id, bande[0], $"{dove}: il gruppo di una banda sola porta la banda «{bande[0]}»");
                    var v = Canale(p, id);
                    Assert.IsNotNull(v, $"{dove}: il gruppo non ha il suo canale nel budget");
                    bool uguali(JsonObject x) => Uguali(f, D(x["floor"], dove)) && Uguali(u, D(x["useful"], dove)) && Uguali(t, D(x["saturates"], dove));
                    var sog = v!["soggetto"] as JsonObject;
                    var own = g["oreProprie"] as JsonObject;
                    Assert.AreEqual(Vero(g["comanda"]), own != null, $"{dove}: «comanda» e le ore proprie non tornano");
                    if (own != null) {
                        Assert.IsTrue(Uguali(D(own["floor"], dove), D(v["floor"], dove)) && Uguali(D(own["useful"], dove), D(v["useful"], dove)) &&
                            Uguali(D(own["saturates"], dove), D(v["saturates"], dove)), $"{dove}: le ore proprie non sono quelle del suo canale");
                        Assert.IsTrue(f >= D(own["floor"], dove) - 1e-9 && t >= D(own["saturates"], dove) - 1e-9 && Uguali(u, D(own["useful"], dove)),
                            $"{dove}: il gruppo ({f}, {u}, {t}) non sta sopra le ore proprie");
                        provati++; proprie++;
                    }
                    else if (uguali(v)) provati++;
                    else {
                        Assert.IsTrue(soggetto && sog != null && uguali(sog), $"{dove}: le ore del gruppo ({f}, {u}, {t}) non sono quelle del suo " +
                            "canale, ne' quelle del soggetto che il budget porta accanto");
                        provati++; delSoggetto++;
                    }
                }
            }
            Assert.IsTrue(provati > 0, "nessun gruppo di una banda sola nelle fixture: questo verde non vale");
            Assert.IsTrue(delSoggetto > 0, "nessun gruppo col soggetto accanto alle stelle: quel verso non e' provato");
            Assert.IsTrue(proprie > 0, "nessuna L che comanda con le sue ore proprie: quel verso non e' provato");
        }

        /*  LE ORE DELLA NOTTE: quelle spese sono la somma dei gruppi, quelle che avanzano sono la notte meno le spese, e
         *  quelle che il pannello dice — `avanzanoDaDire` — sono le stesse, quando il servizio le manda. La soglia sotto
         *  cui non le manda e' sua, e qui non si conosce: si prova che dica il numero giusto, non quando lo dice. */
        [TestMethod]
        public void LeOreCheAvanzano_SonoLaNotteMenoQuelleSpese() {
            int dette = 0, taciute = 0;
            foreach (var (caso, p) in Prodotti()) {
                var pr = Prescrizione(p);
                double ore = D(pr["hours"], caso), spese = D(pr["spent"], caso), avanzano = D(pr["unused"], caso);
                var somma = Vivi(p).Sum(g => D(g["hours"], caso));
                Assert.IsTrue(Uguali(somma, spese), $"{caso}: le ore spese sono {spese}, i gruppi ne hanno {somma}");
                Assert.IsTrue(Uguali(avanzano, Math.Max(0, ore - spese)), $"{caso}: avanzano {avanzano} ore, e la notte meno le spese fa {ore - spese}");
                var daDire = N(pr["avanzanoDaDire"]);
                if (daDire == null) { taciute++; continue; }
                dette++;
                Assert.IsTrue(daDire > 0 && Uguali(daDire.Value, avanzano), $"{caso}: il pannello direbbe {daDire} ore, ne avanzano {avanzano}");
            }
            Assert.IsTrue(dette > 0 && taciute > 0, $"le ore che avanzano dette {dette} volte e taciute {taciute}: servono tutti e due i versi");
        }

        /*  DA DOVE VENGONO LE ORE: la provenienza e' un codice che il Ponte conosce, e i suoi numeri sono le ore del canale.
         *  Dalla fotometria la soglia sono le ore del pavimento e l'utile quelle dell'utile; dal limite del SII la soglia e'
         *  il rapporto per la media dell'Hα, e se alza la soglia di famiglia e' il pavimento, se no ci sta sotto; dalla
         *  famiglia il motivo e' un codice con le sue parole. */
        [TestMethod]
        public void LaProvenienza_ISuoiNumeriSonoLeOreDelCanale() {
            var da = new[] { "fotometria", "famiglia", "limite" };
            var motivi = CampiAScermoTests.MappaDellaPagina("MOTIVO_DI_FAMIGLIA");
            int fotometria = 0, limite = 0, famiglia = 0;
            foreach (var (caso, p, canale, v) in Canali()) {
                if (v["ore"] is not JsonObject o) continue;
                var dove = caso + "/" + canale;
                var cosa = S(o["da"]);
                Assert.IsTrue(cosa != null && da.Contains(cosa), $"{dove}: le ore vengono da «{cosa}», che il Ponte non conosce");
                Assert.IsTrue(Pagina().Contains("'" + cosa + "'"), $"{dove}: la pagina non nomina «{cosa}»");
                var soglia = o["soglia"] as JsonObject;
                if (cosa == "fotometria") {
                    fotometria++;
                    Assert.IsTrue(Uguali(D(soglia?["ore"], dove), D(v["floor"], dove)), $"{dove}: la soglia della fotometria non sono le ore del pavimento");
                    if (o["utile"] is JsonObject u)
                        Assert.IsTrue(Uguali(D(u["ore"], dove), D(v["useful"], dove)), $"{dove}: l'utile della fotometria non sono le ore dell'utile");
                    if (soglia!["alPiu"] != null)
                        Assert.AreEqual(Vero(o["alPiu"]), Vero(soglia["alPiu"]), $"{dove}: la soglia e la fotometria dicono due cose diverse sul limite superiore");
                    var scala = S(o["elemento"]?["scala"]);
                    Assert.IsTrue(scala == "elemento" || scala == "2×2", $"{dove}: l'elemento su scala «{scala}», che il Ponte non conosce");
                    Assert.IsTrue(D(o["elemento"]?["fwhm"], dove) > 0 && D(o["snr"], dove) > 0, $"{dove}: l'elemento o l'SNR non sono numeri positivi");
                } else if (cosa == "limite") {
                    limite++;
                    Assert.AreEqual("sii_da_ha", S(soglia?["codice"]), $"{dove}: il limite non e' quello del SII sull'Hα");
                    double os = D(soglia!["ore"], dove), pav = D(v["floor"], dove);
                    Assert.IsTrue(Vero(o["alzato"]) ? Uguali(os, pav) : os <= pav + 1e-9,
                        $"{dove}: il limite dice {(Vero(o["alzato"]) ? "di alzare" : "di stare sotto")} la soglia, e le sue ore sono {os} contro {pav}");
                    var ha = Canale(p, "Ha")?["ore"]?["soglia"];
                    if (ha != null && S(ha["codice"]) == "media")
                        Assert.IsTrue(Uguali(D(soglia["valore"], dove), D(soglia["rapporto"], dove) * D(ha["valore"], dove)),
                            $"{dove}: la soglia del SII non e' il suo rapporto per la media dell'Hα");
                } else {
                    famiglia++;
                    CodiceNellaMappa(S(o["motivo"]) ?? "", motivi, dove);
                }
            }
            Assert.IsTrue(fotometria > 0 && limite > 0 && famiglia > 0,
                $"provenienze nelle fixture: fotometria {fotometria}, limite {limite}, famiglia {famiglia} — servono tutte e tre");
        }

        /*  LA SOGLIA E L'UTILE DICONO LA LORO GRANDEZZA NELLA SUA UNITA': la media dentro D25 e il bordo di D25 in
         *  magnitudini per arcsec², nella stessa banda; la media, il bordo catalogato, stimato, o il fondo locale in
         *  Rayleigh, nella banda del canale. E l'utile e' piu' debole della soglia — il bordo di una cosa e' piu' debole
         *  della sua media — e chiede almeno le sue ore. */
        [TestMethod]
        public void LaSogliaELUtile_DiconoLaGrandezzaNellaSuaUnita() {
            const string Mag = "mag/arcsec²";
            var soglie = new Dictionary<string, string> { ["media_d25"] = "Pag_Prov_Soglia_media_d25", ["media"] = "Pag_Prov_Soglia_media" };
            var utili = new Dictionary<string, string> { ["bordo_d25"] = "Pag_Prov_Utile_bordo_d25", ["bordo_catalogato"] = "Pag_Prov_Utile_bordo_catalogato",
                                                         ["bordo_stimato"] = "Pag_Prov_Utile_bordo_stimato", ["fondo_locale"] = "Pag_Prov_Utile_fondo_locale" };
            int soglieViste = 0, utiliViste = 0;
            foreach (var (caso, _, canale, v) in Canali()) {
                if (v["ore"]?["soglia"] is not JsonObject s) continue;
                var dove = caso + "/" + canale;
                var cs = S(s["codice"]) ?? "";
                /*  la soglia del limite del SII ha la sua frase e la sua relazione nella prova della provenienza */
                if (S(v["ore"]?["da"]) != "limite") CodiceNominato(cs, soglie, dove);
                soglieViste++;
                var unita = S(s["unita"]);
                Assert.AreEqual(cs == "media_d25" ? Mag : "R", unita, $"{dove}: la soglia «{cs}» in «{unita}»");
                if (unita == "R") Assert.AreEqual(canale, S(s["banda"]), $"{dove}: una soglia in Rayleigh e' della banda del canale");
                else Assert.IsFalse(string.IsNullOrEmpty(S(s["banda"])), $"{dove}: la soglia in magnitudini non dice la sua banda");
                if (v["ore"]?["utile"] is not JsonObject u) continue;
                var cu = S(u["codice"]) ?? "";
                CodiceNominato(cu, utili, dove);
                utiliViste++;
                Assert.AreEqual(cu == "bordo_d25" ? Mag : "R", S(u["unita"]), $"{dove}: l'utile «{cu}» in «{S(u["unita"])}»");
                Assert.AreEqual(S(s["unita"]), S(u["unita"]), $"{dove}: soglia e utile in due unita' diverse");
                Assert.AreEqual(S(s["banda"]), S(u["banda"]), $"{dove}: soglia e utile in due bande diverse");
                double vs = D(s["valore"], dove), vu = D(u["valore"], dove);
                Assert.IsTrue(unita == "R" ? vu < vs : vu > vs, $"{dove}: l'utile a {vu} non e' piu' debole della soglia a {vs} {unita}");
                Assert.IsTrue(D(u["ore"], dove) >= D(s["ore"], dove) - 1e-9, $"{dove}: l'utile, piu' debole, chiede meno ore della soglia");
            }
            Assert.IsTrue(soglieViste > 0 && utiliViste > 0, "nessuna soglia o nessun utile nelle fixture: questo verde non vale");
        }

        /*  IL BORDO STIMATO E' LA MEDIA PER IL RAPPORTO: la frase lo dice — «la media per {1}» —, e i numeri lo fanno. Il
         *  rapporto arriva con tre decimali: la tolleranza e' mezzo millesimo, la larghezza dell'arrotondamento che il
         *  prodotto dichiara. E il rapporto sta fra gli estremi delle regioni misurate da cui viene. */
        [TestMethod]
        public void IlBordoStimato_ELaMediaPerIlRapporto() {
            var provati = 0;
            foreach (var (caso, _, canale, v) in Canali()) {
                if (v["ore"]?["utile"] is not JsonObject u || S(u["codice"]) != "bordo_stimato") continue;
                var dove = caso + "/" + canale;
                provati++;
                double r = D(u["rapporto"], dove), media = D(v["ore"]!["soglia"]!["valore"], dove), bordo = D(u["valore"], dove);
                Assert.IsTrue(Math.Abs(bordo / media - r) <= 0.0005 + 1e-9, $"{dove}: il bordo stimato {bordo} non e' la media {media} per {r}");
                Assert.IsTrue(D(u["rapportoMinimo"], dove) <= r && r <= D(u["rapportoMassimo"], dove), $"{dove}: il rapporto {r} sta fuori dai suoi estremi");
                Assert.IsTrue(D(u["regioni"], dove) >= 1, $"{dove}: un rapporto mediano senza regioni misurate");
            }
            Assert.IsTrue(provati > 0, "nessun bordo stimato nelle fixture: questo verde non vale");
        }

        /*  IL FONDO LOCALE DICE PERCHE': il motivo per cui il bordo catalogato non vale e' un codice con le sue parole, e la
         *  regione in cui il bordo cade arriva quando — e solo quando — il motivo e' quello. */
        [TestMethod]
        public void IlFondoLocale_DiceIlSuoMotivo() {
            var motivi = CampiAScermoTests.MappaDellaPagina("MOTIVO_DEL_BORDO");
            var provati = 0;
            foreach (var (caso, _, canale, v) in Canali()) {
                if (v["ore"]?["utile"] is not JsonObject u || S(u["codice"]) != "fondo_locale") continue;
                var dove = caso + "/" + canale;
                var motivo = S(u["motivoCodice"]);
                if (motivo == null) continue;
                provati++;
                CodiceNellaMappa(motivo, motivi, dove);
                Assert.AreEqual(motivo == "dentro_un_altro", !string.IsNullOrEmpty(S(u["dentro"])),
                    $"{dove}: il motivo «{motivo}» e la regione in cui il bordo cade «{S(u["dentro"])}» non tornano");
            }
            Assert.IsTrue(provati > 0, "nessun fondo locale col suo motivo nelle fixture: questo verde non vale");
        }

        /*  IL TETTO DELLA SCHEDA DICE SE STAVA SOTTO L'UTILE, e la confidenza e' una parola che il Ponte conosce. Si provano
         *  i due versi: una scheda sopra l'utile e una sotto. */
        [TestMethod]
        public void IlTettoDellaScheda_DiceSeStavaSottoLUtile() {
            var confidenze = CampiAScermoTests.MappaDellaPagina("PAROLA_DELLA_CONFIDENZA");
            int sotto = 0, sopra = 0, confidenza = 0;
            foreach (var (caso, _, canale, v) in Canali()) {
                var dove = caso + "/" + canale;
                if (S(v["confidence"]) is string c) { confidenza++; CodiceNellaMappa(c, confidenze, dove); }
                if (v["tettoDellaScheda"] is not JsonObject t) continue;
                var scritto = D(t["scritto"], dove);
                var sta = scritto < D(v["useful"], dove) - 1e-9;
                Assert.AreEqual(sta, Vero(t["sotto"]), $"{dove}: la scheda diceva {scritto} h contro un utile di {D(v["useful"], dove)} h, e «sotto» dice {Vero(t["sotto"])}");
                if (sta) sotto++; else sopra++;
            }
            Assert.IsTrue(sotto > 0 && sopra > 0 && confidenza > 0, $"schede sotto {sotto}, sopra {sopra}, confidenze {confidenza}: servono tutti i versi");
        }

        /*  I PUNTI D'ARRESTO SONO I TETTI DEI GRUPPI: il colore si ferma al tetto dell'RGB, la luminanza a quello della L, e
         *  sul progetto sono gli stessi per i riquadri. «La luminanza non si ferma» se e solo se la L comanda; «oltre, come
         *  nella pratica» se e solo se la L porta la divisione della pratica. */
        [TestMethod]
        public void IPuntiDArresto_SonoITettiDeiGruppi() {
            int provati = 0, conLaPratica = 0, senza = 0;
            foreach (var (caso, p) in Prodotti()) {
                var riquadri = D(Prescrizione(p)["panels"], caso);
                foreach (var g in Vivi(p)) {
                    if (g["puntiDArresto"] is not JsonObject a) continue;
                    var dove = caso + "/" + S(g["id"]);
                    provati++;
                    var L = Gruppo(p, "L");
                    Assert.IsNotNull(L, $"{dove}: punti d'arresto senza una L");
                    Assert.IsTrue(Uguali(D(a["colore"], dove), D(g["sat"], dove)), $"{dove}: il colore non si ferma al tetto del suo gruppo");
                    Assert.IsTrue(Uguali(D(a["luminanza"], dove), D(L!["sat"], dove)), $"{dove}: la luminanza non si ferma al tetto della L");
                    Assert.IsTrue(Uguali(D(a["coloreDiProgetto"], dove), D(a["colore"], dove) * riquadri), $"{dove}: il colore sul progetto non e' quello del riquadro per i riquadri");
                    Assert.IsTrue(Uguali(D(a["luminanzaDiProgetto"], dove), D(a["luminanza"], dove) * riquadri), $"{dove}: la luminanza sul progetto non e' quella del riquadro per i riquadri");
                    Assert.AreEqual(Vero(L["comanda"]), Vero(a["luminanzaSenzaArresto"]), $"{dove}: «la luminanza non si ferma» e «la L comanda» non tornano");
                    Assert.AreEqual(L["pratica"] is JsonObject, Vero(a["oltreDallaPratica"]), $"{dove}: «oltre, come nella pratica» e la pratica della L non tornano");
                    if (Vero(a["oltreDallaPratica"])) conLaPratica++; else senza++;
                }
            }
            Assert.IsTrue(provati > 0 && conLaPratica > 0 && senza > 0, $"punti d'arresto {provati}, con la pratica {conLaPratica}, senza {senza}: servono i due versi");
        }

        /*  LA PRATICA DELLA LRGB: oltre i tetti il colore prende la sua parte del rapporto che la frase dichiara — tante ore
         *  di colore per ogni ora di L —, e le due parti fanno cento. */
        [TestMethod]
        public void LaPratica_LePercentualiVengonoDalRapporto() {
            var provati = 0;
            foreach (var (caso, p) in Prodotti())
                foreach (var g in Vivi(p)) {
                    if (g["pratica"] is not JsonObject pr) continue;
                    var dove = caso + "/" + S(g["id"]);
                    provati++;
                    double c = D(pr["coloreSuL"], dove), col = D(pr["percentuali"]?["colore"], dove), lum = D(pr["percentuali"]?["luminanza"], dove);
                    Assert.IsTrue(Uguali(col + lum, 100), $"{dove}: colore {col} % e luminanza {lum} % non fanno cento");
                    Assert.IsTrue(Uguali(col, 100 * c / (1 + c)), $"{dove}: con {c} ore di colore per ora di L il colore prende {100 * c / (1 + c)} %, non {col} %");
                }
            Assert.IsTrue(provati > 0, "nessuna divisione della pratica nelle fixture: questo verde non vale");
        }

        /*  L'AGGIUNTA: un gruppo e' un'aggiunta se e solo se porta i suoi pezzi. La sua quota delle ore che andrebbero alla L
         *  e' il rapporto della pratica — tante ore dell'aggiunta per ogni ora di L —; dopo l'entrata le ore nuove vanno a
         *  L, colore e aggiunta come uno, il colore su L e il rapporto, e fanno cento; il pavimento sul progetto e' quello
         *  del riquadro per i riquadri. */
        [TestMethod]
        public void LAggiunta_LeSuePartiVengonoDaiRapporti() {
            var provati = 0;
            foreach (var (caso, p) in Prodotti()) {
                var riquadri = D(Prescrizione(p)["panels"], caso);
                foreach (var g in Vivi(p)) {
                    var dove = caso + "/" + S(g["id"]);
                    var a = g["aggiunta"] as JsonObject;
                    Assert.AreEqual(Vero(g["additivo"]), a != null, $"{dove}: «additivo» e i pezzi dell'aggiunta non tornano");
                    if (a == null) continue;
                    provati++;
                    var q = a["percentuali"]!;
                    double r = D(a["rapportoDellaPratica"], dove);
                    Assert.IsTrue(Uguali(D(q["quota"], dove), 100 * r / (1 + r)), $"{dove}: con {r} ore per ora di L la quota e' {100 * r / (1 + r)} %, non {D(q["quota"], dove)} %");
                    Assert.IsTrue(Uguali(D(a["pavimentoDiProgetto"], dove), D(g["floor"], dove) * riquadri), $"{dove}: il pavimento sul progetto non e' quello del riquadro per i riquadri");
                    if (q["L"] == null) continue;
                    double c = D(a["coloreSuL"], dove), tutto = 1 + c + r;
                    double pl = D(q["L"], dove), pc = D(q["colore"], dove), pa = D(q["aggiunta"], dove);
                    Assert.IsTrue(Uguali(pl + pc + pa, 100), $"{dove}: le parti dopo l'entrata {pl} + {pc} + {pa} non fanno cento");
                    Assert.IsTrue(Uguali(pl, 100 / tutto) && Uguali(pc, 100 * c / tutto) && Uguali(pa, 100 * r / tutto),
                        $"{dove}: le parti {pl}, {pc}, {pa} non vengono da uno, {c} e {r}");
                }
            }
            Assert.IsTrue(provati > 0, "nessuna aggiunta nelle fixture: questo verde non vale");
        }

        /*  IL TETTO UNICO: sulla matrice il colore ha un punto d'arresto solo — nessuna L viva, nessun punto d'arresto sul
         *  gruppo —, e sul progetto e' il tetto del riquadro per i riquadri. */
        [TestMethod]
        public void IlTettoUnico_ELUnicoPuntoDArrestoDellaMatrice() {
            var provati = 0;
            foreach (var (caso, p) in Prodotti()) {
                var riquadri = D(Prescrizione(p)["panels"], caso);
                foreach (var g in Vivi(p).Where(g => Vero(g["tettoUnico"]))) {
                    var dove = caso + "/" + S(g["id"]);
                    provati++;
                    Assert.IsNull(g["puntiDArresto"], $"{dove}: tetto unico e punti d'arresto insieme");
                    Assert.IsNull(Gruppo(p, "L"), $"{dove}: tetto unico con una L viva");
                    Assert.IsTrue(Uguali(D(g["satDiProgetto"], dove), D(g["sat"], dove) * riquadri), $"{dove}: il tetto sul progetto non e' quello del riquadro per i riquadri");
                }
            }
            Assert.IsTrue(provati > 0, "nessun tetto unico nelle fixture: questo verde non vale");
        }

        /*  LE RIGHE IN PARTI UGUALI: «oltre i tetti fisici» si dice se e solo se una riga della divisione ha piu' ore del suo
         *  tetto. Le righe della divisione sono almeno due, e la tavolozza e' la SHO se e solo se sono tre e c'e' il SII. E
         *  la banda larga dichiarata dalla tecnica: un RGB di sole stelle non decide niente e non ha punti d'arresto. */
        [TestMethod]
        public void LeRighe_OltreITettiSeUnaSuperaIlSuo() {
            int oltre = 0, dentro = 0, righe = 0;
            foreach (var (caso, p) in Prodotti()) {
                var pr = Prescrizione(p);
                var bandaLarga = S(pr["road"]?["banda_larga"]);
                Assert.IsTrue(bandaLarga == "soggetto" || bandaLarga == "stelle", $"{caso}: la banda larga «{bandaLarga}», che il Ponte non conosce");
                Assert.IsTrue(Pagina().Contains("'stelle'"), "la pagina non nomina la banda larga delle sole stelle");
                if (bandaLarga == "stelle" && Gruppo(p, "RGB") is JsonObject rgb)
                    Assert.IsTrue(!Vero(rgb["critical"]) && rgb["puntiDArresto"] == null, $"{caso}: un RGB di sole stelle che decide, o che ha punti d'arresto");
                var riga = Vivi(p).Where(g => g["praticaARiga"] is JsonObject).ToList();
                var supera = riga.Any(g => D(g["hours"], caso) > D(g["sat"], caso) + 1e-6);
                Assert.AreEqual(supera, Vero(pr["oltreITettiARiga"]), $"{caso}: «oltre i tetti fisici» dice {Vero(pr["oltreITettiARiga"])}, e una riga oltre il suo tetto {(supera ? "c'e'" : "non c'e'")}");
                if (Vero(pr["oltreITettiARiga"])) oltre++; else dentro++;
                if (riga.Count == 0) continue;
                righe++;
                var ids = riga.Select(g => S(g["id"])).ToList();
                Assert.IsTrue(riga.Count >= 2 && ids.All(i => i == "Ha" || i == "OIII" || i == "SII"), $"{caso}: le righe della divisione sono {string.Join(", ", ids)}");
                Assert.AreEqual(riga.Count >= 3, ids.Contains("SII"), $"{caso}: la tavolozza SHO e il SII fra le righe non tornano");
            }
            Assert.IsTrue(oltre > 0 && dentro > 0 && righe > 0, $"righe oltre i tetti {oltre}, dentro {dentro}, divisioni {righe}: servono i due versi");
        }

        /*  LA PROFONDITA' DEL CANALE CRITICO: e' quello che la prescrizione nomina, nella sua banda; l'elemento e l'SNR sono
         *  quelli della sua fotometria, quando le ore vengono da li'; μ_lim sta nella stessa unita', e a tre sigma su un
         *  riquadro largo e' piu' in fondo di «arriva a» a SNR 12 nell'elemento. */
        [TestMethod]
        public void LaProfondita_ElementoEMuLimDelCanaleCritico() {
            int provati = 0, elemento = 0;
            foreach (var (caso, p) in Prodotti()) {
                if (p["profondita"] is not JsonObject f) continue;
                provati++;
                var critico = S(Prescrizione(p)["critGroup"]);
                Assert.AreEqual(critico, S(f["canale"]), $"{caso}: la profondita' e' di «{S(f["canale"])}», il canale critico e' «{critico}»");
                var g = Gruppo(p, critico!);
                if (g != null && Bande(g).Count() == 1) Assert.AreEqual(critico, S(f["banda"]), $"{caso}: la profondita' nella banda «{S(f["banda"])}»");
                var unita = S(f["unita"]);
                Assert.IsTrue(unita == "R" || unita == "mag/arcsec²", $"{caso}: la profondita' in «{unita}»");
                var mu = f["muLim"]!;
                Assert.AreEqual(unita, S(mu["unita"]), $"{caso}: μ_lim in un'unita' diversa da «arriva a»");
                double arriva = D(f["arrivi"], caso), lim = D(mu["valore"], caso);
                Assert.IsTrue(unita == "R" ? lim < arriva : lim > arriva, $"{caso}: μ_lim {lim} non e' piu' in fondo di «arriva a» {arriva} {unita}");
                var o = Canale(p, critico)?["ore"];
                if (S(o?["da"]) != "fotometria" || S(o?["elemento"]?["scala"]) != "elemento") continue;
                elemento++;
                Assert.IsTrue(Uguali(D(f["elemento"]?["fwhm"], caso), D(o!["elemento"]!["fwhm"], caso)), $"{caso}: l'elemento della profondita' non e' quello della fotometria del canale");
                Assert.IsTrue(Uguali(D(f["snr"], caso), D(o["snr"], caso)), $"{caso}: l'SNR della profondita' non e' quello della fotometria del canale");
            }
            Assert.IsTrue(provati > 0 && elemento > 0, $"profondita' {provati}, con la fotometria del canale critico {elemento}: questo verde non vale");
        }
    }
}
