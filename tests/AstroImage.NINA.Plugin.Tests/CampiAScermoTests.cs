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

    /*  IL CAMPO CHE VA A SCHERMO PORTA CON SE' IL SUO TESTIMONE.
     *  ═══════════════════════════════════════════════════════════════════════════════════════════════════════
     *  Un numero o un codice che il pannello mostra, e che nessuna prova si accorgerebbe se cambiasse, e' un campo
     *  che puo' dire il falso a schermo senza che nessuno lo sappia. Il riquadro del piano mette a schermo quattro campi
     *  che fino a ieri non avevano testimoni: di che cosa e' il numero del canale (`perBanda`), a quale canale
     *  appartiene il blocco (`gruppo`), chi ha deciso il guadagno (`gainFonte`) e chi ha deciso i secondi
     *  (`limite`). Qui ciascuno riceve il suo.
     *
     *  SI PROVANO LE RELAZIONI, NON I NUMERI. Un'asserzione come «OIII vale 900 s» cadrebbe a ogni cambio di cielo e
     *  si smetterebbe di leggerla; una relazione — che vale su qualunque prescrizione — cade solo quando il campo
     *  dice qualcosa di incoerente con gli altri. Se il valore cambia da solo, la relazione si rompe e la prova cade.
     *
     *  IL DISEGNO VERO LO PROVA ALTRO. Queste prove leggono il modello, non eseguono la pagina: qui non c'e' un
     *  interprete JavaScript. Che il pannello mostri davvero gli stessi numeri della prescrizione lo verifica chi la
     *  pagina la esegue.                                                                                          */
    [TestClass]
    public class CampiAScermoTests {

        private static readonly string[] Vive = { "mono", "completo", "osc", "osc-hdr", "mono-hdr",
                                                  "forma-unica", "nucleo-di-classe", "senza-serie" };

        /*  LE MAPPE CHE LA PAGINA USA DAVVERO. Una prova che compone la chiave — "Pag_Ruolo_" + codice — guarda il
         *  dizionario, non il pannello: se il codice entrasse nei .resx e non nella mappa della pagina, la prova
         *  resterebbe verde e il pannello non scriverebbe niente. Qui si legge la mappa letterale da prova.js, com'e'
         *  incorporata nel plugin, e si pretendono le chiavi che lei nomina. */
        private static Dictionary<string, string> MappaDellaPagina(string nome) {
            using var s = typeof(SequenceModel).Assembly.GetManifestResourceStream("AstroImage.NINA.Plugin.Views.Pagina.prova.js");
            Assert.IsNotNull(s, "prova.js non e' incorporata nel plugin");
            var js = new StreamReader(s!).ReadToEnd();
            var m = System.Text.RegularExpressions.Regex.Match(js, @"const\s+" + nome + @"\s*=\s*\{([^}]*)\}");
            Assert.IsTrue(m.Success, "la pagina non ha piu' la mappa " + nome);
            var mappa = new Dictionary<string, string>();
            foreach (System.Text.RegularExpressions.Match c in System.Text.RegularExpressions.Regex.Matches(m.Groups[1].Value, @"(\w+)\s*:\s*'(Pag_\w+)'"))
                mappa[c.Groups[1].Value] = c.Groups[2].Value;
            Assert.IsTrue(mappa.Count > 0, "la mappa " + nome + " non ha voci: questo verde non varrebbe");
            return mappa;
        }

        private static string CartellaFixture =>
            Path.Combine(Path.GetDirectoryName(typeof(CampiAScermoTests).Assembly.Location)!, "Fixtures");

        private static SequenceModel Modello(string nome) =>
            SequenceModel.Leggi(File.ReadAllText(Path.Combine(CartellaFixture, nome + ".json")))!;

        private static IEnumerable<(string Fixture, Blocco Blocco)> Blocchi() =>
            Vive.SelectMany(n => Modello(n).Blocchi.Select(b => (n, b)));

        private static string Nome(string f, Blocco b) => f + "/" + string.Join("+", b.Canali);

        /*  `perBanda`, IL VERSO CHE MANCAVA. Un blocco che ha fuso piu' canali non puo' dirsi «per banda» — quello c'era
         *  gia'. Ma anche il contrario: un blocco di UN canale solo porta pose che sono di quella banda, e deve dirlo.
         *  Senza questo verso il campo poteva valere qualunque cosa sui blocchi di un canale — cioe' quasi tutti — e
         *  il pannello avrebbe scritto «sul canale» o «per banda» a caso, con le prove verdi. */
        [TestMethod]
        public void PerBanda_TornaConIlNumeroDiCanaliDelBlocco() {
            var provati = 0;
            foreach (var (f, b) in Blocchi()) {
                if (b.PoseCanale is null || b.PerBanda is null) continue;
                provati++;
                var atteso = b.Canali.Count == 1;
                Assert.AreEqual(atteso, b.PerBanda.Value,
                    $"{Nome(f, b)}: {b.Canali.Count} canali e perBanda={b.PerBanda} — " +
                    (atteso ? "le pose di un canale solo sono di quella banda" : "un blocco fuso ha sommato le bande"));
            }
            Assert.IsTrue(provati > 0, "nessun blocco porta il numero del canale: questo verde non vale");
        }

        /*  `gruppo`: il canale a cui il blocco appartiene, ed e' l'etichetta della riga nel riquadro. La relazione:
         *  le bande dei blocchi di uno stesso gruppo, messe insieme, sono il gruppo — «RGB» e' R, G e B; «OIII» e'
         *  OIII; «Ha+OIII» e' il blocco del duale. Se il valore cambia da solo, la riga porterebbe il nome di un
         *  canale che quei blocchi non compongono, e la relazione si rompe. */
        [TestMethod]
        public void Gruppo_ELeSueBandeMesseInsieme() {
            var provati = 0;
            foreach (var nome in Vive) {
                foreach (var g in Modello(nome).Blocchi.Where(b => b.Gruppo != null).GroupBy(b => b.Gruppo!)) {
                    provati++;
                    var bande = g.SelectMany(b => b.Canali).Where(c => c != null).Distinct().ToList();
                    var composto = string.Join("", bande) == g.Key || string.Join("+", bande) == g.Key;
                    Assert.IsTrue(composto,
                        $"{nome}: il gruppo «{g.Key}» raccoglie le bande {string.Join(", ", bande)}, che non lo compongono");
                }
            }
            Assert.IsTrue(provati > 0, "nessun blocco dice a quale canale appartiene: questo verde non vale");
        }

        /*  `totale.perGruppo`: le ore di ogni canale stanotte, scritte accanto al canale. La relazione: sommate, fanno le
         *  ore della notte; e ognuna e' la somma dei blocchi di quel canale. Il pannello non la fa — qui la prova si',
         *  perche' e' la prova che il numero mostrato torni con quelli da cui viene. */
        [TestMethod]
        public void OreDelCanale_SommateFannoLaNotte_EOgnunaTornaCoiSuoiBlocchi() {
            var provati = 0;
            foreach (var nome in Vive) {
                var m = Modello(nome);
                var pg = m.Totale?.PerGruppo;
                if (pg is null || m.Totale?.Ore is null) continue;
                provati++;
                Assert.AreEqual(m.Totale.Ore.Value, pg.Values.Sum(), 1e-9,
                    $"{nome}: le ore dei canali sommano {pg.Values.Sum():F4} h e la notte ne dice {m.Totale.Ore:F4}");
                foreach (var g in m.Blocchi.GroupBy(b => b.Gruppo ?? string.Join("+", b.Canali))) {
                    Assert.IsTrue(pg.ContainsKey(g.Key), $"{nome}: il canale «{g.Key}» non ha le sue ore");
                    var somma = g.Sum(b => (b.Sec ?? 0) * (b.N ?? 0) / 3600.0);
                    Assert.AreEqual(somma, pg[g.Key], 1e-9, $"{nome}/{g.Key}: i blocchi fanno {somma:F4} h, il canale ne dice {pg[g.Key]:F4}");
                }
            }
            Assert.IsTrue(provati > 0, "nessuna fixture porta le ore dei canali: questo verde non vale");
        }

        /*  I PEZZI IN ORE E MINUTI tornano col decimale che accompagnano, entro mezzo minuto: sono lo stesso tempo, arrotondato
         *  una volta. Se un pezzo cambiasse da solo, il pannello scriverebbe un tempo che non e' quello della prescrizione. */
        private static void TornaCol(string dove, OreEMinuti? p, double? h) {
            if (p is null || h is null) return;
            var minuti = p.Ore * 60 + p.Minuti;
            Assert.IsTrue(p.Minuti >= 0 && p.Minuti < 60, $"{dove}: {p.Minuti} minuti non sono minuti");
            Assert.IsTrue(System.Math.Abs(minuti - h.Value * 60) <= 0.5 + 1e-9,
                $"{dove}: {p.Ore} h {p.Minuti}′ non e' {h.Value:F4} h arrotondato al minuto");
        }

        [TestMethod]
        public void OreEMinuti_TornanoColDecimaleCheAccompagnano() {
            var provati = 0;
            foreach (var nome in Vive) {
                var m = Modello(nome);
                foreach (var b in m.Blocchi) if (b.OreCanaleHM != null) { provati++; TornaCol(nome + "/" + string.Join("+", b.Canali) + " oreCanale", b.OreCanaleHM, b.OreCanale); }
                var t = m.Totale;
                if (t == null) continue;
                if (t.OreHM != null) { provati++; TornaCol(nome + " ore", t.OreHM, t.Ore); }
                if (t.OrologioHM != null) { provati++; TornaCol(nome + " orologio", t.OrologioHM, t.Orologio); }
                if (t.PerGruppoHM != null && t.PerGruppo != null)
                    foreach (var kv in t.PerGruppoHM) { provati++; TornaCol(nome + "/" + kv.Key, kv.Value, t.PerGruppo.TryGetValue(kv.Key, out var h) ? h : (double?)null); }
            }
            Assert.IsTrue(provati > 0, "nessun tempo arriva in ore e minuti: questo verde non vale");
        }

        /*  `gainFonte`: chi ha deciso il guadagno, in un insieme chiuso, e coerente col guadagno che accompagna. Un -1
         *  vuol dire «lascia quello della camera», e allora non c'e' nessuno che l'abbia deciso; un guadagno scelto dal
         *  motore porta il modo per cui l'ha scelto. */
        [TestMethod]
        public void GainFonte_EDiUnInsiemeChiusoETornaColGuadagno() {
            var ammessi = new HashSet<string?> { "dichiarato", "motore", null };
            var provati = 0;
            foreach (var (f, b) in Blocchi()) {
                provati++;
                Assert.IsTrue(ammessi.Contains(b.GainFonte),
                    $"{Nome(f, b)}: gainFonte «{b.GainFonte}» non e' uno di dichiarato, motore o assente");
                if (b.Gain == -1)
                    Assert.IsNull(b.GainFonte, $"{Nome(f, b)}: guadagno lasciato alla camera, e qualcuno l'avrebbe deciso");
                else
                    Assert.IsNotNull(b.GainFonte, $"{Nome(f, b)}: guadagno {b.Gain} senza dire chi l'ha deciso");
                if (b.GainFonte == "motore")
                    Assert.IsFalse(string.IsNullOrWhiteSpace(b.Modo), $"{Nome(f, b)}: scelto dal motore, ma per quale modo?");
            }
            Assert.IsTrue(provati > 0, "nessun blocco: questo verde non vale");
        }

        /*  `limite`: ogni codice che arriva il pannello lo deve saper DIRE, nelle due lingue. Un codice nuovo che il
         *  dizionario non conosce finirebbe a schermo come sigla nuda, o sparirebbe; qui cade prima. Il dizionario e'
         *  quello del Ponte: e' lui a dichiarare che cosa sa dire, non un elenco copiato da chi manda. */
        [TestMethod]
        public void Limite_OgniCodiceHaLaSuaParolaNelleDueLingue() {
            var it = new ResourceManager("AstroImage.NINA.Plugin.Localization.Strings_it", typeof(SequenceModel).Assembly);
            var en = new ResourceManager("AstroImage.NINA.Plugin.Localization.Strings_en", typeof(SequenceModel).Assembly);
            var mappa = MappaDellaPagina("PAROLA_DEL_LIMITE");
            var provati = 0;
            foreach (var (f, b) in Blocchi()) {
                if (b.Limite is null) continue;
                provati++;
                Assert.IsTrue(mappa.TryGetValue(b.Limite, out var chiave),
                    $"{Nome(f, b)}: il limite «{b.Limite}» non e' nella mappa della pagina: il pannello non lo direbbe");
                Assert.IsFalse(string.IsNullOrEmpty(it.GetString(chiave, CultureInfo.InvariantCulture)),
                    $"{Nome(f, b)}: il limite «{b.Limite}» non ha una parola italiana ({chiave})");
                Assert.IsFalse(string.IsNullOrEmpty(en.GetString(chiave, CultureInfo.InvariantCulture)),
                    $"{Nome(f, b)}: il limite «{b.Limite}» non ha una parola inglese ({chiave})");
            }
            Assert.IsTrue(provati > 0, "nessun blocco dice chi ha deciso i secondi: questo verde non vale");
        }

        /*  `ruolo`: che cosa fa la serie nel suo canale. La relazione, nei due versi: un blocco e' `nucleo` SE E SOLO SE nel
         *  suo canale c'e' una serie piu' lunga delle stesse bande — la serie corta esiste per fondersi con quella. E sul
         *  nucleo il limite non c'e': i suoi secondi non li decide il limite della serie lunga. Se il campo cambiasse da
         *  solo — tolto dal blocco corto, o messo su uno che non ha una serie lunga accanto — la relazione si rompe. */
        [TestMethod]
        public void Ruolo_NucleoSeESoloSeNelCanaleCeUnaSerieLungaDelleStesseBande() {
            int provati = 0, nuclei = 0;
            foreach (var nome in Vive) {
                var bl = Modello(nome).Blocchi;
                foreach (var b in bl) {
                    provati++;
                    var lunga = bl.Any(o => !ReferenceEquals(o, b) && (o.Gruppo ?? "") == (b.Gruppo ?? "") &&
                                            o.Canali.Intersect(b.Canali).Any() && (o.Sec ?? 0) > (b.Sec ?? 0));
                    var eNucleo = b.Ruolo == "nucleo";
                    if (eNucleo) nuclei++;
                    Assert.AreEqual(lunga, eNucleo, $"{Nome(nome, b)} a {b.Sec} s: ruolo «{b.Ruolo}» — " + (lunga
                        ? "accanto c'e' una serie piu' lunga delle stesse bande, e questa non si dice nucleo"
                        : "si dice nucleo senza una serie lunga delle stesse bande accanto"));
                    if (eNucleo)
                        Assert.IsNull(b.Limite, $"{Nome(nome, b)}: il nucleo porta il limite «{b.Limite}», che e' della serie lunga");
                }
            }
            Assert.IsTrue(nuclei > 0, "nessun nucleo nelle fixture: il verso che dice «nucleo» non e' provato");
            Assert.IsTrue(provati > nuclei, "solo nuclei nelle fixture: il verso che tace non e' provato");
        }

        /*  E ogni ruolo che arriva il pannello lo deve saper DIRE, nelle due lingue, con la sua spiegazione: un codice che
         *  il dizionario non conosce non si scrive, e il blocco sembrerebbe una serie normale. */
        [TestMethod]
        public void Ruolo_OgniCodiceHaLaSuaParolaELaSuaSpiegazioneNelleDueLingue() {
            var it = new ResourceManager("AstroImage.NINA.Plugin.Localization.Strings_it", typeof(SequenceModel).Assembly);
            var en = new ResourceManager("AstroImage.NINA.Plugin.Localization.Strings_en", typeof(SequenceModel).Assembly);
            var parole = MappaDellaPagina("PAROLA_DEL_RUOLO");
            var spiegazioni = MappaDellaPagina("SPIEGAZIONE_DEL_RUOLO");
            var provati = 0;
            foreach (var (f, b) in Blocchi()) {
                if (b.Ruolo is null) continue;
                provati++;
                Assert.IsTrue(parole.TryGetValue(b.Ruolo, out var parola) && spiegazioni.ContainsKey(b.Ruolo),
                    $"{Nome(f, b)}: il ruolo «{b.Ruolo}» non e' nelle mappe della pagina: la serie si leggerebbe come una posa normale");
                foreach (var chiave in new[] { parola!, spiegazioni[b.Ruolo] }) {
                    Assert.IsFalse(string.IsNullOrEmpty(it.GetString(chiave, CultureInfo.InvariantCulture)),
                        $"{Nome(f, b)}: il ruolo «{b.Ruolo}» non ha la voce italiana {chiave}");
                    Assert.IsFalse(string.IsNullOrEmpty(en.GetString(chiave, CultureInfo.InvariantCulture)),
                        $"{Nome(f, b)}: il ruolo «{b.Ruolo}» non ha la voce inglese {chiave}");
                }
            }
            Assert.IsTrue(provati > 0, "nessun blocco dice il suo ruolo: questo verde non vale");
        }

        /*  LA SERIE CORTA DI OGNI BANDA, IN PEZZI (22 settembre 2026). Il pannello compone da questi pezzi il perche' della
         *  serie — sotto il canale e sul nucleo — e ognuno va a schermo. Le relazioni che seguono valgono su qualunque
         *  prescrizione: dicono come i pezzi stanno fra loro e coi blocchi della notte, non quanto valgono. */
        private static IEnumerable<(string Fixture, SequenceModel Modello, string Banda, SerieDellaBanda Pezzi)> Serie() =>
            Vive.Select(n => (n, Modello(n))).Where(x => x.Item2.SerieCorta != null)
                .SelectMany(x => x.Item2.SerieCorta!.PerBanda.Select(kv => (x.n, x.Item2, kv.Key, kv.Value)));

        /*  Il nucleo ha la sua serie, e la serie e' quella del nucleo: un blocco `nucleo` ha la sua banda fra i pezzi, in
         *  forma doppia, coi secondi della serie, e le pose del canale sono le pose della serie — di una banda, o del blocco
         *  intero quando la matrice le ha fuse. E ogni banda coi pezzi ha un blocco in questa notte. */
        [TestMethod]
        public void SerieCorta_IlNucleoHaLaSuaSerieELaSerieIlSuoNucleo() {
            int nuclei = 0, bande = 0;
            foreach (var nome in Vive) {
                var m = Modello(nome);
                var sc = m.SerieCorta;
                if (sc is null) continue;
                var qui = new HashSet<string>(m.Blocchi.SelectMany(b => b.Canali).OfType<string>());
                foreach (var banda in sc.PerBanda.Keys) {
                    bande++;
                    Assert.IsTrue(qui.Contains(banda), $"{nome}: i pezzi della serie di {banda} in una notte che non riprende {banda}");
                }
                foreach (var b in m.Blocchi.Where(b => b.Ruolo == "nucleo")) {
                    nuclei++;
                    var canali = b.Canali.OfType<string>().ToList();
                    Assert.AreEqual(b.Canali.Count, canali.Count, $"{Nome(nome, b)}: un nucleo con un canale senza nome");
                    foreach (var c in canali) {
                        Assert.IsTrue(sc.PerBanda.TryGetValue(c, out var q), $"{Nome(nome, b)}: un nucleo senza i pezzi della sua serie");
                        Assert.AreEqual("doppia", q!.Forma, $"{Nome(nome, b)}: c'e' il nucleo, e la forma non e' doppia");
                        Assert.AreEqual(q.SerieSec, b.Sec, $"{Nome(nome, b)}: il nucleo e' a {b.Sec} s, la sua serie a {q.SerieSec} s");
                    }
                    var attese = b.PerBanda == false ? canali.Sum(c => sc.PerBanda[c].SeriePose ?? 0) : sc.PerBanda[canali[0]].SeriePose;
                    Assert.AreEqual(attese, b.PoseCanale, $"{Nome(nome, b)}: {b.PoseCanale} pose sul canale, e la serie ne dice {attese}");
                }
            }
            Assert.IsTrue(nuclei > 0 && bande > 0, "nessun nucleo, o nessuna banda coi pezzi: questo verde non vale");
        }

        /*  La posa dei blocchi e' quella della forma: nella doppia la serie lunga e' alla posa principale, nella unica il
         *  canale e' tutto alla posa unica e il nucleo non c'e'. */
        [TestMethod]
        public void SerieCorta_LaPosaDeiBlocchiEQuellaDellaForma() {
            int doppie = 0, uniche = 0;
            foreach (var (f, m, banda, q) in Serie()) {
                if (q.Decisa != "fisica") continue;
                var lunghe = m.Blocchi.Where(b => b.Ruolo == null && b.Canali.Contains(banda)).ToList();
                if (q.Forma == "doppia") {
                    doppie++;
                    Assert.IsTrue(lunghe.All(b => b.Sec == q.PosaPrincipale),
                        $"{f}/{banda}: la serie lunga e' a {string.Join(", ", lunghe.Select(b => b.Sec))} s, la posa principale a {q.PosaPrincipale} s");
                } else {
                    uniche++;
                    Assert.IsTrue(lunghe.Count > 0 && lunghe.All(b => b.Sec == q.PosaUnica),
                        $"{f}/{banda}: forma unica a {q.PosaUnica} s, e i blocchi sono a {string.Join(", ", lunghe.Select(b => b.Sec))} s");
                    Assert.IsFalse(m.Blocchi.Any(b => b.Ruolo == "nucleo" && b.Canali.Contains(banda)), $"{f}/{banda}: forma unica, e c'e' un nucleo");
                }
            }
            Assert.IsTrue(doppie > 0 && uniche > 0, $"le fixture portano {doppie} forme doppie e {uniche} uniche decise dalla fisica: servono tutte e due");
        }

        /*  Il limite sta fra la serie e la posa principale: la principale lo supera — per questo c'e' la serie —, mentre
         *  serie e posa unica restano entro di lui; fa eccezione la serie quando anche la posa corta piu' breve lo supera.
         *  Le pose che si scattano non sono meno di quelle che devono arrivare. I secondi del limite arrivano interi: il
         *  confronto e' coi due versi dell'arrotondamento. */
        [TestMethod]
        public void SerieCorta_IlLimiteStaFraLaSerieELaPosaPrincipale() {
            var provati = 0;
            foreach (var (f, m, banda, q) in Serie()) {
                if (q.Decisa != "fisica") continue;
                provati++;
                Assert.IsNotNull(q.SicuroFinoA, $"{f}/{banda}: decisa dalla fisica, e senza il suo limite");
                Assert.IsTrue(q.PosaPrincipale >= q.SicuroFinoA, $"{f}/{banda}: la posa principale di {q.PosaPrincipale} s non supera il limite di {q.SicuroFinoA} s");
                if (q.Forma == "doppia") {
                    Assert.IsNotNull(q.NonBasta, $"{f}/{banda}: forma doppia, e non dice se il gradino basta");
                    Assert.IsTrue(q.NonBasta == true ? q.SerieSec >= q.SicuroFinoA : q.SerieSec <= q.SicuroFinoA,
                        $"{f}/{banda}: serie a {q.SerieSec} s col limite a {q.SicuroFinoA} s, e «nemmeno il gradino piu' corto basta» vale {q.NonBasta}");
                    Assert.IsTrue(q.SeriePose >= q.SerieDaConsegnare, $"{f}/{banda}: {q.SeriePose} pose per consegnarne {q.SerieDaConsegnare}");
                }
                if (q.PosaUnica != null)
                    Assert.IsTrue(q.PosaUnica <= q.SicuroFinoA, $"{f}/{banda}: la posa unica di {q.PosaUnica} s supera il limite di {q.SicuroFinoA} s");
            }
            Assert.IsTrue(provati > 0, "nessuna serie decisa dalla fisica: questo verde non vale");
        }

        /*  Vince la forma con piu' ore equivalenti, cioe' quella con cui, a ore uguali, la parte debole arriva piu' giu'; e
         *  nessuna delle due supera le ore del canale, perche' sono espresse alla posa piu' lunga delle due. La posa di
         *  riferimento e' quella della forma doppia quando si puo' fare, se no la posa unica. */
        [TestMethod]
        public void SerieCorta_VinceLaFormaConPiuOreEquivalenti() {
            var provati = 0;
            foreach (var (f, m, banda, q) in Serie()) {
                if (q.Decisa != "fisica") continue;
                provati++;
                Assert.IsNotNull(q.Equivalenti, $"{f}/{banda}: decisa dalla fisica, e senza le ore equivalenti");
                double? d = q.Equivalenti!.Doppia, u = q.Equivalenti.Unica;
                var vinceUnica = u != null && (d == null || u > d);
                Assert.AreEqual(vinceUnica ? "unica" : "doppia", q.Forma,
                    $"{f}/{banda}: {d?.ToString("F3") ?? "—"} h con la serie corta, {u?.ToString("F3") ?? "—"} h tutto alla posa corta, e la forma e' «{q.Forma}»");
                foreach (var x in new[] { d, u })
                    if (x != null) Assert.IsTrue(x <= q.OrePari + 1e-9, $"{f}/{banda}: {x:F3} h equivalenti su {q.OrePari:F3} h del canale");
                if (d != null && q.Forma == "doppia")
                    Assert.AreEqual(q.PosaPrincipale, q.PosaRiferimento, $"{f}/{banda}: forma doppia, e le ore sono espresse a {q.PosaRiferimento} s invece che alla principale");
                if (d == null && q.PosaUnica != null)
                    Assert.AreEqual(q.PosaUnica, q.PosaRiferimento, $"{f}/{banda}: la doppia non si puo' fare, e il riferimento non e' la posa unica");
            }
            Assert.IsTrue(provati > 0, "nessuna serie decisa dalla fisica: questo verde non vale");
        }

        /*  `magProtetta`: LA CIFRA A SCHERMO E' QUELLA DELLA POSA (22 settembre 2026). Il pannello scrive «le stelle fino
         *  alla magnitudine X»: quella X dev'essere la magnitudine con cui la posa di quel canale e' stata decisa, e la
         *  risposta intera le porta tutt'e due — il perche' nella sequenza, la posa in `posa.<canale>.ex.protectMag`. Se
         *  il numero del perche' cambiasse da solo, il pannello direbbe una magnitudine che nessuna posa ha protetto. Il
         *  limite di plausibilita' della prova dei codici resta, ed e' un'altra cosa: quello dice che e' una magnitudine,
         *  questo che e' LA magnitudine. */
        [TestMethod]
        public void MagProtetta_ELaMagnitudineConCuiLaPosaEStataDecisa() {
            var testo = File.ReadAllText(Path.Combine(CartellaFixture, "servizio", "prescrizione-stelle.json"));
            var prodotto = JsonNode.Parse(testo)!["prodotto"]!;
            var posa = prodotto["posa"]!.AsObject();
            var provati = 0;
            foreach (var s in prodotto["sequenze"]!.AsArray()) {
                var sc = s!["modello"]!["serieCorta"];
                if (sc is null) continue;
                foreach (var kv in sc["perBanda"]!.AsObject()) {
                    var q = kv.Value!;
                    if (q["chiBrucia"]?.GetValue<string>() != "stelle") continue;
                    provati++;
                    var dallaPosa = posa[kv.Key]?["ex"]?["protectMag"];
                    Assert.IsNotNull(dallaPosa, $"{kv.Key}: la serie dice di proteggere le stelle, e la posa non dice fino a quale magnitudine");
                    Assert.AreEqual(dallaPosa!.GetValue<double>(), q["magProtetta"]!.GetValue<double>(), 1e-9,
                        $"{kv.Key}: il perche' dice magnitudine {q["magProtetta"]}, la posa e' stata decisa su {dallaPosa}");
                }
            }
            Assert.IsTrue(provati > 0, "nessuna serie decisa dalle stelle nella risposta: questo verde non vale");
        }

        /*  I pezzi in ore e minuti tornano col decimale che accompagnano, e ci sono se e solo se c'e' lui. */
        [TestMethod]
        public void SerieCorta_OreEMinutiTornanoColDecimale() {
            var provati = 0;
            foreach (var (f, m, banda, q) in Serie()) {
                Assert.AreEqual(q.OrePari is null, q.OrePariHM is null, $"{f}/{banda}: le ore del canale e i loro pezzi arrivano insieme");
                if (q.OrePariHM != null) { provati++; TornaCol($"{f}/{banda} orePari", q.OrePariHM, q.OrePari); }
                if (q.Equivalenti is null) continue;
                Assert.IsNotNull(q.EquivalentiHM, $"{f}/{banda}: le ore equivalenti senza i loro pezzi");
                foreach (var (dec, hm, forma) in new[] { (q.Equivalenti.Doppia, q.EquivalentiHM!.Doppia, "doppia"), (q.Equivalenti.Unica, q.EquivalentiHM.Unica, "unica") }) {
                    Assert.AreEqual(dec is null, hm is null, $"{f}/{banda}: le ore equivalenti della forma {forma} e i loro pezzi arrivano insieme");
                    if (hm != null) { provati++; TornaCol($"{f}/{banda} equivalenti {forma}", hm, dec); }
                }
            }
            Assert.IsTrue(provati > 0, "nessun tempo della serie corta in ore e minuti: questo verde non vale");
        }

        /*  Chi ha deciso dice quali pezzi ci sono: la fisica porta il conto, la classe e il progetto solo la serie. E ogni
         *  codice che arriva — chi brucia, e perche' una classe non vuole la serie — il pannello lo sa dire nelle due lingue,
         *  dalle mappe che usa davvero. La classe che non vuole la serie non ne porta nessuna, e nemmeno un nucleo. */
        [TestMethod]
        public void SerieCorta_OgniCodiceDiceQualiPezziCiSonoEHaLeSueParole() {
            var it = new ResourceManager("AstroImage.NINA.Plugin.Localization.Strings_it", typeof(SequenceModel).Assembly);
            var en = new ResourceManager("AstroImage.NINA.Plugin.Localization.Strings_en", typeof(SequenceModel).Assembly);
            void Parole(string dove, string chiave) {
                Assert.IsFalse(string.IsNullOrEmpty(it.GetString(chiave, CultureInfo.InvariantCulture)), $"{dove}: manca la voce italiana {chiave}");
                Assert.IsFalse(string.IsNullOrEmpty(en.GetString(chiave, CultureInfo.InvariantCulture)), $"{dove}: manca la voce inglese {chiave}");
            }
            var chi = MappaDellaPagina("PAROLA_DI_CHI_BRUCIA");
            var senza = MappaDellaPagina("SPIEGAZIONE_SENZA_SERIE");
            var ragioni = MappaDellaPagina("RAGIONE_DELLA_CLASSE");
            int fisica = 0, altre = 0, senzaSerie = 0;
            var motivi = new HashSet<string>();
            foreach (var (f, m, banda, q) in Serie()) {
                var dove = f + "/" + banda;
                Assert.IsTrue(q.Forma == "doppia" || q.Forma == "unica", $"{dove}: la forma «{q.Forma}» non e' doppia ne' unica");
                if (q.Decisa == "fisica") {
                    fisica++;
                    Assert.IsTrue(q.ChiBrucia != null && chi.TryGetValue(q.ChiBrucia, out _), $"{dove}: chi brucia, «{q.ChiBrucia}», il pannello non lo sa dire");
                    Parole(dove, chi[q.ChiBrucia!]);
                    Assert.IsTrue(q.PosaPrincipale != null && q.OrePari != null, $"{dove}: decisa dalla fisica, e senza il suo conto");
                    Assert.AreEqual(q.ChiBrucia == "stelle", q.MagProtetta != null, $"{dove}: la magnitudine protetta c'e' se e solo se bruciano le stelle");
                    /*  la magnitudine protetta non ha una relazione con gli altri pezzi: si prova il suo dominio, una
                     *  magnitudine di stelle da proteggere — un limite di plausibilita', dichiarato come tale */
                    if (q.MagProtetta != null)
                        Assert.IsTrue(q.MagProtetta > -2 && q.MagProtetta <= 20, $"{dove}: {q.MagProtetta} non e' una magnitudine di stelle da proteggere");
                    Assert.IsNull(q.MotivoDiClasse, $"{dove}: decisa dalla fisica, e porta il perche' di una decisione della classe");
                } else {
                    Assert.IsTrue(q.Decisa == "classe" || q.Decisa == "progetto", $"{dove}: decisa da «{q.Decisa}», che non e' fisica, classe ne' progetto");
                    altre++;
                    Assert.AreEqual("doppia", q.Forma, $"{dove}: una serie della {q.Decisa} e' sempre accanto alla posa principale");
                    Assert.IsTrue(q.SerieSec != null && q.SeriePose != null, $"{dove}: la serie della {q.Decisa} senza i suoi secondi o le sue pose");
                    Assert.IsTrue(q.SicuroFinoA is null && q.ChiBrucia is null && q.OrePari is null && q.Equivalenti is null && q.PosaPrincipale is null,
                        $"{dove}: decisa dalla {q.Decisa}, e porta un conto che solo la fisica puo' fare");
                    /*  e la classe dice PERCHE' decide lei, con un codice che il pannello sa dire: la banda senza una
                     *  brillanza misurata, o il canale di due righe. Il progetto no: li' ha deciso una consegna. */
                    if (q.Decisa == "classe") {
                        Assert.IsTrue(q.MotivoDiClasse != null && ragioni.TryGetValue(q.MotivoDiClasse, out _),
                            $"{dove}: decide la classe, e il perche' «{q.MotivoDiClasse}» il pannello non lo sa dire");
                        Parole(dove, ragioni[q.MotivoDiClasse!]);
                        motivi.Add(q.MotivoDiClasse!);
                    } else {
                        Assert.IsNull(q.MotivoDiClasse, $"{dove}: decisa dal progetto, e porta il perche' della classe");
                    }
                }
            }
            foreach (var nome in Vive) {
                var m = Modello(nome);
                if (m.SerieCorta?.SenzaSerie is null) continue;
                senzaSerie++;
                Assert.IsTrue(senza.TryGetValue(m.SerieCorta.SenzaSerie, out var chiave), $"{nome}: «{m.SerieCorta.SenzaSerie}» il pannello non lo sa dire");
                Parole(nome, chiave!);
                Assert.AreEqual(0, m.SerieCorta.PerBanda.Count, $"{nome}: la classe non vuole la serie corta, e ne arrivano i pezzi");
                Assert.IsFalse(m.Blocchi.Any(b => b.Ruolo == "nucleo"), $"{nome}: la classe non vuole la serie corta, e c'e' un nucleo");
            }
            Assert.IsTrue(fisica > 0 && altre > 0 && senzaSerie > 0,
                $"le fixture portano {fisica} serie della fisica, {altre} della classe o del progetto, {senzaSerie} classi senza serie: servono tutte e tre");
            /*  e tutt'e due le ragioni della classe, o una delle due non sarebbe provata da nessuna fixture */
            CollectionAssert.AreEquivalent(new[] { "banda_senza_misura", "canale_doppio" }, motivi.ToList(),
                "le fixture non portano tutt'e due i perche' della classe: " + string.Join(", ", motivi));
        }
    }
}
