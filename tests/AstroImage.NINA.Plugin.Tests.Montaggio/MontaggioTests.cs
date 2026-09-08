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

            var c = b.Costruisci(Modello(), out var ricetta);

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

            var c = b.Costruisci(Modello(), out var ricetta);

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
            Assert.AreEqual(5, pezzi.Length,
                "cinque pezzi: contenitore, posa, autofocus, guida, dither. Se sono di piu', " +
                "qualcuno ha allargato il magazzino e va detto qui");
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
            b.Costruisci(Modello(), out var ricetta);

            Assert.IsTrue(ricetta.Raffredda, "la fixture chiede davvero di raffreddare");
            Assert.AreEqual(0, ricetta.Note.Count, "ma non si consiglia niente su una sequenza mancata");
            Assert.IsTrue(ricetta.Scartati.Count > 0, "si dice invece che cosa e' andato storto");
        }
    }
}
