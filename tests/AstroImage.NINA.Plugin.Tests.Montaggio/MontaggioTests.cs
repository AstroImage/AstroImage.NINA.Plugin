using System;
using System.IO;
using System.Linq;
using AstroImage.NINA.Plugin.Models;
using AstroImage.NINA.Plugin.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using global::NINA.Sequencer.SequenceItem.Autofocus;
using global::NINA.Sequencer.SequenceItem.Guider;

#nullable enable

namespace AstroImage.NINA.Plugin.Tests.Montaggio {

    /*  IL COSTRUTTORE SI MONTA DAVVERO, e questo banco esiste per dirlo.
     *
     *  Fino a ieri nessun test lo costruiva: leggevano i metadati del DLL e
     *  verificavano che CHIAMASSE `ISequencerFactory::GetItem`. Centoquindici
     *  verifiche verdi su una dipendenza che nei plugin non esiste — perche' N.I.N.A.
     *  quella fabbrica non la mette nel contenitore, ne' sulla 3.2 ne' sulla 3.3.
     *  Verificare i metadati non e' verificare che il pezzo si possa montare.
     */
    [TestClass]
    public class MontaggioTests {

        private static string Fixture(string nome) =>
            File.ReadAllText(Path.Combine(
                Path.GetDirectoryName(typeof(MontaggioTests).Assembly.Location)!,
                "Fixtures", nome + ".json"));

        private static SequenceModel Modello(string nome = "mono") =>
            SequenceModel.Leggi(Fixture(nome))!;

        /*  UNA FIXTURE VERA, DEGRADATA — l'idioma di questo progetto, invece di
         *  inventare un JSON. Togliere il filtro da ogni blocco serve a provare tutto
         *  cio' che viene DOPO il controllo dei filtri: senza un profilo la ruota
         *  risulta vuota, e un modello che chiede un vetro verrebbe rifiutato prima. */
        private static SequenceModel ModelloSenzaFiltri(string nome = "osc") {
            var j = System.Text.Json.Nodes.JsonNode.Parse(Fixture(nome))!.AsObject();
            foreach (var b in j["blocchi"]!.AsArray()) b!.AsObject().Remove("filtro");
            return SequenceModel.Leggi(j.ToJsonString())!;
        }

        // ------------------------------------------------------- gli assembly ci sono

        [TestMethod]
        public void GliAssemblyDiNinaSiCaricanoDavvero() {
            /*  Se questo passa, il banco ha N.I.N.A. sotto: e' la differenza fra questo
             *  progetto e quello accanto, ed e' voluta in tutti e due i versi. */
            var t = typeof(global::NINA.Sequencer.Container.IDeepSkyObjectContainer);
            Assert.AreEqual("NINA.Sequencer", t.Assembly.GetName().Name);
            Assert.AreEqual("3.2.0.9001", t.Assembly.GetName().Version!.ToString(),
                "il riferimento e' la 3.2: la piu' bassa delle due in giro");
        }

        // ------------------------------------------------- si costruisce, finalmente

        [TestMethod]
        public void SequenceBuilder_SiIstanzia_ConUnaFonteNostra() {
            var b = new SequenceBuilder(FonteFinta.Bugiarda(), null!);
            Assert.IsNotNull(b, "il montatore esiste come oggetto, non solo come metadato");
        }

        [TestMethod]
        public void SequenceBuilder_SenzaFonte_NonSiIstanzia() {
            Assert.ThrowsException<ArgumentNullException>(() => new SequenceBuilder(null!, null!));
        }

        [TestMethod]
        public void SequenceBuilder_SenzaProfilo_SiIstanziaLoStesso() {
            /*  Il profilo serve a una cosa sola, cercare un filtro nella ruota. Senza,
             *  non si cambia vetro — e non e' un motivo per non montare niente. */
            var b = new SequenceBuilder(FonteFinta.Bugiarda(), null!);
            Assert.IsNull(b.FiltroDaRuota("Ha"), "senza profilo nessun vetro, e lo dice tornando null");
        }

        // ---------------------------------------------------------- la fonte e' vuota

        [TestMethod]
        public void FonteVuota_NonEsplode_EScriveIlMotivo() {
            var fonte = FonteFinta.Vuota("nessun modello di bersaglio da cui copiare");
            var b = new SequenceBuilder(fonte, null!);

            var c = b.Costruisci(ModelloSenzaFiltri(), out var ricetta);

            Assert.IsNull(c, "senza pezzi non si consegna niente");
            Assert.AreEqual(1, ricetta.Scartati.Count, "e si dice una volta sola perche'");
            StringAssert.Contains(ricetta.Scartati[0], "nessun modello di bersaglio da cui copiare",
                "il motivo della fonte arriva fino a chi legge");
            CollectionAssert.DoesNotContain(fonte.Chieste, nameof(FonteFinta.Contenitore),
                "e non si chiede niente a un magazzino che ha gia' detto di non avere niente");
        }

        [TestMethod]
        public void FonteSenzaContenitore_NonEsplode_ELoDice() {
            var fonte = FonteFinta.Bugiarda();   // disponibile, ma non da' il contenitore
            var b = new SequenceBuilder(fonte, null!);

            var c = b.Costruisci(ModelloSenzaFiltri(), out var ricetta);

            Assert.IsNull(c);
            Assert.IsTrue(ricetta.Scartati.Any(s => s.Contains("contenitore")),
                "il caso peggiore — una fonte che promette e non mantiene — non passa in silenzio");
            CollectionAssert.Contains(fonte.Chieste, nameof(FonteFinta.Contenitore));
        }

        [TestMethod]
        public void ModelloNullo_NonEsplode() {
            var b = new SequenceBuilder(FonteFinta.Bugiarda(), null!);
            Assert.IsNull(b.Costruisci(null, out var ricetta));
            Assert.IsFalse(ricetta.Costruibile);
        }

        // ---------------------------------------- i pezzi che fuori da N.I.N.A. nascono

        [TestMethod]
        public void IPezziCheSiRiesconoACostruire_SiClonanoDavvero() {
            /*  Non tutto il Sequenziatore si costruisce fuori da N.I.N.A., ma questi
             *  tre si', e servono a provare che Clone() non e' una speranza: restituisce
             *  un'istanza DISTINTA, che e' l'unica cosa che rende sensato un magazzino
             *  fatto di cloni. */
            var af = new RunAutofocus(null!, null!, null!, null!, null!, null!);
            var gu = new StartGuiding(null!);

            foreach (var (nome, originale) in new (string, ICloneable)[] { ("RunAutofocus", af), ("StartGuiding", gu) }) {
                var clone = originale.Clone();
                Assert.IsNotNull(clone, $"{nome}: il clone esiste");
                Assert.AreNotSame(originale, clone, $"{nome}: ed e' un altro oggetto");
                Assert.AreEqual(originale.GetType(), clone.GetType(), $"{nome}: dello stesso tipo");
            }
        }

        // ------------------------------------------- niente vita di sessione nel bersaglio

        /*  LA REGOLA PIU' IMPORTANTE DI QUESTO PASSO, e non e' affidata a un test:
         *  e' affidata alla forma di IFonteDiPezzi, che quei pezzi non li offre. Il test
         *  qui verifica che la forma sia rimasta quella, perche' un'interfaccia si
         *  allarga in trenta secondi e nessuno se ne accorge.
         *
         *  Perche' non devono starci: Sequence2VM.AddTarget mette il bersaglio in
         *  Items[1], l'area centrale, fra lo Start e l'End della sequenza — che sono di
         *  chi riprende. Raffreddare dentro un bersaglio vorrebbe dire raffreddare una
         *  volta PER BERSAGLIO; riscaldare e parcheggiare, farlo dopo ognuno. */
        [TestMethod]
        public void LaFonteNonOffreVitaDiSessione() {
            var vietati = new[] { "CoolCamera", "WarmCamera", "FindHome", "ParkScope",
                                  "UnparkScope", "SetTracking", "ShutdownPC" };

            /*  Solo i metodi veri: GetMethods() conta anche i getter delle proprieta'. */
            var pezzi = typeof(IFonteDiPezzi).GetMethods().Where(m => !m.IsSpecialName).ToArray();
            foreach (var m in pezzi) {
                var reso = m.ReturnType.Name;
                Assert.IsFalse(vietati.Contains(reso),
                    $"IFonteDiPezzi.{m.Name} offre {reso}: la vita della sessione non e' " +
                    "affare di un bersaglio, e cio' che non si puo' chiedere non si puo' aggiungere");
            }
            Assert.AreEqual(4, pezzi.Length,
                "quattro pezzi: contenitore, posa, autofocus, guida. Se sono di piu', qualcuno " +
                "ha allargato il magazzino e va detto qui");
            /*  IL DITHER NON E' PIU' UN PEZZO, e non e' una semplificazione: ogni
             *  SmartExposure porta gia' il proprio, e un secondo innesco sul contenitore
             *  ditherebbe DUE volte contando le stesse pose. Cio' che non si puo'
             *  chiedere non si puo' aggiungere per sbaglio. */
            Assert.IsFalse(pezzi.Any(m => m.Name.Contains("Dither")),
                "il dither si imposta sul blocco, non si chiede al magazzino");
        }

        // ------------------------------------- fedelta': il filtro che non c'e'

        /*  LA REGRESSIONE PIU' GRAVE DELLA PRIMA CONSEGNA VERA.
         *
         *  Il bersaglio e' comparso nel Sequenziatore con pose, tempi, guadagno, offset
         *  e coordinate esatti, e con il filtro del primo blocco su «(Corrente)» perche'
         *  la ruota non aveva l'HO richiesto. Ventinove pose da seicento secondi — quasi
         *  cinque ore — con qualunque vetro fosse montato, e nessuna riga lo diceva.
         *
         *  Adesso il bersaglio non si consegna affatto. Fra saltare il blocco e
         *  rifiutare tutto vale l'asimmetria del danno: un rifiuto si corregge in un
         *  clic, cinque ore col vetro sbagliato non si recuperano. */
        [TestMethod]
        public void FiltroPrescrittoMaNonInRuota_IlBersaglioNonSiConsegna() {
            var fonte = FonteFinta.Bugiarda();
            /*  Senza profilo la ruota risulta vuota: e' il caso peggiore, e la fixture
             *  mono chiede cinque vetri. */
            var b = new SequenceBuilder(fonte, null!);

            var c = b.Costruisci(Modello("mono"), out var ricetta);

            Assert.IsNull(c, "un bersaglio che non rispetta la prescrizione non si consegna");
            Assert.IsTrue(ricetta.Scartati.Any(s => s.Contains("NON e' stato consegnato")),
                "e si dice a chiare lettere, non fra le righe");
            foreach (var vetro in new[] { "O", "H", "R", "G", "B" })
                Assert.IsTrue(ricetta.Scartati.Any(s => s.Contains("«" + vetro + "»")),
                    $"il vetro {vetro} mancante va nominato");
            Assert.IsTrue(ricetta.Scartati.Any(s => s.Contains("nessun filtro in ruota")),
                "e si dice anche che cosa c'e' in ruota, o che non c'e' niente");
        }

        /*  E SI RIFIUTA PRIMA DI COSTRUIRE. Non e' solo eleganza: se il controllo
         *  venisse dopo, il montaggio avrebbe gia' toccato N.I.N.A. per costruire
         *  qualcosa da buttare — e questa regola non si potrebbe provare qui. */
        [TestMethod]
        public void IlControlloDeiFiltri_VienePrimaDiChiedereIPezzi() {
            var fonte = FonteFinta.Bugiarda();
            var b = new SequenceBuilder(fonte, null!);

            b.Costruisci(Modello("mono"), out _);

            Assert.AreEqual(0, fonte.Chieste.Count,
                "al magazzino non si e' chiesto niente: si sapeva gia' che non si poteva consegnare");
        }

        [TestMethod]
        public void SenzaFiltriPrescritti_IlControlloNonSiIntromette() {
            var fonte = FonteFinta.Bugiarda();
            var b = new SequenceBuilder(fonte, null!);

            b.Costruisci(ModelloSenzaFiltri(), out var ricetta);

            Assert.IsFalse(ricetta.Scartati.Any(s => s.Contains("filtro")),
                "un modello che non chiede vetri non ha filtri mancanti");
            CollectionAssert.Contains(fonte.Chieste, nameof(FonteFinta.Contenitore),
                "e il montaggio prosegue fino a chiedere i pezzi");
        }

        // ------------------------------ la fonte chiede ogni volta, non si ricorda

        /*  LA REGRESSIONE CHE CI E' COSTATA UN GIRO DENTRO N.I.N.A.
         *
         *  La prima versione di FonteDaModello chiedeva i modelli nel COSTRUTTORE. Ma
         *  N.I.N.A. compone i pannelli all'avvio, quando il Sequenziatore non ha ancora
         *  letto niente: la fonte nasceva vuota e restava vuota per tutta la sessione.
         *  Il pannello diceva «non disponibile» con i modelli sotto il naso.
         *
         *  Il conteggio delle chiamate e' la prova diretta. Se restasse a uno, la fonte
         *  si sarebbe fidata di una fotografia scattata nel momento sbagliato. */
        [TestMethod]
        public void LaFonte_ChiedeIModelliOgniVolta_NonSoloAllAvvio() {
            var mediatore = new MediatoreFinto();
            var fonte = new FonteDaModello(mediatore);

            Assert.AreEqual(0, mediatore.Chiamate,
                "costruire la fonte non deve chiedere niente: all'avvio non c'e' ancora niente da chiedere");

            _ = fonte.Disponibile;
            var dopoPrima = mediatore.Chiamate;
            _ = fonte.Disponibile;
            _ = fonte.PerCheNo;

            Assert.IsTrue(dopoPrima >= 1, "la prima domanda vera deve interrogare il mediatore");
            Assert.IsTrue(mediatore.Chiamate > dopoPrima,
                "e ogni domanda successiva pure: i modelli sono dell'utente, che ne aggiunge " +
                "e ne toglie mentre N.I.N.A. e' aperto");
        }

        [TestMethod]
        public void LaFonte_SenzaModelli_SpiegaDoveAndarliAPrendere() {
            var fonte = new FonteDaModello(new MediatoreFinto());   // elenco vuoto

            Assert.IsFalse(fonte.Disponibile);
            StringAssert.Contains(fonte.PerCheNo ?? "", "Modelli",
                "un motivo deve dire anche che cosa fare, non solo che cosa manca");
            Assert.IsNull(fonte.Contenitore(), "e non si inventa un contenitore");
            Assert.IsNull(fonte.Posa());
        }

        [TestMethod]
        public void LaFonte_SenzaMediatore_NonEsplode() {
            var fonte = new FonteDaModello(null);

            Assert.IsFalse(fonte.Disponibile);
            StringAssert.Contains(fonte.PerCheNo ?? "", "mediatore");
            Assert.IsNull(fonte.Contenitore());
        }

        [TestMethod]
        public void QuandoNonSiCostruisceNiente_NonSiDannoConsigliFuoriLuogo() {
            /*  La ricetta CHIEDE di raffreddare — la fixture ha una camera raffreddata —
             *  e il ponte non lo mette nel bersaglio. Ma se non si e' costruito niente
             *  non si spiega nemmeno dove andrebbe messo: un consiglio su una sequenza
             *  che non esiste e' rumore sopra un errore.
             *
             *  Che la nota ci sia quando invece si costruisce non e' verificabile qui:
             *  ci vorrebbe un contenitore vero, e riempirne il bersaglio chiama NOVAS,
             *  una libreria nativa che sta nella cartella di N.I.N.A. e non nel
             *  pacchetto. Resta da vedere dentro N.I.N.A., ed e' dichiarato. */
            var fonte = FonteFinta.Bugiarda();
            var b = new SequenceBuilder(fonte, null!);
            b.Costruisci(ModelloSenzaFiltri(), out var ricetta);

            /*  Le note NON sono tutte del costruttore: Traduzione ne aggiunge di sue,
             *  e confondere le due origini era il difetto di questo test. Si confronta
             *  quindi con la sola traduzione, che e' il termine di paragone giusto. */
            var soloTradotta = Traduzione.Traduci(ModelloSenzaFiltri());

            Assert.IsTrue(ricetta.Raffredda, "la fixture chiede davvero di raffreddare");
            Assert.AreEqual(soloTradotta.Note.Count, ricetta.Note.Count,
                "il costruttore non ha aggiunto consigli su una sequenza che non si e' costruita");
            Assert.IsTrue(ricetta.Scartati.Count > 0, "si dice invece che cosa e' andato storto");
        }
    }
}
