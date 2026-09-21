using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
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

        private static readonly string[] Vive = { "mono", "completo", "osc", "osc-hdr", "mono-hdr" };

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
    }
}
