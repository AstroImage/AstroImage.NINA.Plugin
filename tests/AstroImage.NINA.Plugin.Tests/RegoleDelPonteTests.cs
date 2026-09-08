using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using AstroImage.NINA.Plugin.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace AstroImage.NINA.Plugin.Tests {

    /*  DUE REGOLE DEL PONTE, LETTE DAL BINARIO.
     *
     *  Non si provano con un test funzionale: si provano guardando che cosa il DLL
     *  compilato nomina davvero. E' lo stesso metodo con cui scripts/verifica-scheletro.ps1
     *  controlla il manifest, e ha il pregio di non poter essere aggirato da come si
     *  scrive il codice — se la chiamata c'e', nei metadati si vede.
     *
     *  Girano nel banco che non ha N.I.N.A. accanto: leggono byte, non caricano tipi.
     */
    [TestClass]
    public class RegoleDelPonteTests {

        private static string Ponte =>
            Path.Combine(Path.GetDirectoryName(typeof(Traduzione).Assembly.Location)!,
                         "AstroImage.NINA.Plugin.dll");

        /// <summary>Ogni membro di N.I.N.A. che il ponte nomina, come "Tipo::membro".</summary>
        private static List<string> MembriNominati() {
            var fuori = new List<string>();
            using var fs = File.OpenRead(Ponte);
            using var pe = new PEReader(fs);
            var md = pe.GetMetadataReader();
            foreach (var h in md.MemberReferences) {
                var mr = md.GetMemberReference(h);
                var nome = md.GetString(mr.Name);
                string tipo;
                switch (mr.Parent.Kind) {
                    case HandleKind.TypeReference:
                        var tr = md.GetTypeReference((TypeReferenceHandle)mr.Parent);
                        tipo = md.GetString(tr.Name);
                        break;
                    case HandleKind.TypeDefinition:
                        var td = md.GetTypeDefinition((TypeDefinitionHandle)mr.Parent);
                        tipo = md.GetString(td.Name);
                        break;
                    default:
                        tipo = "?";
                        break;
                }
                fuori.Add(tipo + "::" + nome);
            }
            return fuori;
        }

        [TestMethod]
        public void IlPonteNonSaAvviareUnaSequenza() {
            /*  «Mai avviare la sequenza automaticamente» e' la regola che accompagna
             *  questo progetto da quando esiste il tasto rosso. ISequenceMediator
             *  espone StartAdvancedSequence e CancelAdvancedSequence: il ponte
             *  consegna, e a premere il tasto e' chi riprende.
             *
             *  Detta a parole e' un'intenzione. Qui e' una cosa che fallisce: se un
             *  giorno qualcuno scrive quella chiamata, nei metadati compare. */
            var vietati = new[] { "StartAdvancedSequence", "CancelAdvancedSequence", "SetAdvancedSequence" };
            var trovati = MembriNominati()
                .Where(m => vietati.Any(v => m.EndsWith("::" + v, StringComparison.Ordinal)))
                .Distinct().ToList();

            Assert.AreEqual(0, trovati.Count,
                "il ponte non deve avviare, annullare o sostituire una sequenza:\n  " +
                string.Join("\n  ", trovati));
        }

        [TestMethod]
        public void IlPonteNonCostruisceMaiUnOggettoDiNina() {
            /*  LA REGOLA MISURATA, non scelta per gusto.
             *
             *  Confrontando le assembly della 3.2.0.9001 con quelle della
             *  3.3.0.1057-nightly, quattro dei tipi che servono al builder hanno
             *  cambiato il numero di parametri del costruttore: DeepSkyObjectContainer,
             *  SmartExposure, DitherAfterExposures, MeridianFlipTrigger. Le proprieta'
             *  invece sono identiche. Un `new` scritto a mano lega il plugin a UNA delle
             *  due versioni e lo fa morire sull'altra al caricamento — che e' esattamente
             *  come muore Target Scheduler 5.10.3 sulla 3.2.
             *
             *  ISequencerFactory risolve il costruttore con l'iniezione delle
             *  dipendenze e non se ne accorge nessuno. Quindi: mai `new`. */
            var tipiDiNina = new[] {
                "DeepSkyObjectContainer", "SmartExposure", "TakeExposure", "SwitchFilter",
                "LoopCondition", "DitherAfterExposures", "MeridianFlipTrigger",
                "CoolCamera", "WarmCamera", "RunAutofocus", "StartGuiding", "FindHome",
                "SequentialContainer", "InputTarget",
            };
            var costruiti = MembriNominati()
                .Where(m => m.EndsWith("::.ctor", StringComparison.Ordinal))
                .Where(m => tipiDiNina.Contains(m.Split(new[] { "::" }, StringSplitOptions.None)[0]))
                .Distinct().ToList();

            Assert.AreEqual(0, costruiti.Count,
                "un tipo del Sequenziatore costruito a mano invece che dalla fabbrica:\n  " +
                string.Join("\n  ", costruiti) +
                "\nusa fabbrica.GetItem<T>() / GetContainer<T>() / GetTrigger<T>()");
        }

        [TestMethod]
        public void LaProvaNonEVuota() {
            /*  Le due verifiche sopra passerebbero anche su un DLL che non nomina
             *  N.I.N.A. per niente. Questa controlla che stiano guardando un binario
             *  che il Sequenziatore lo usa davvero: se il builder sparisse, o se il
             *  lettore di metadati smettesse di funzionare, se ne accorgerebbe. */
            var membri = MembriNominati();
            Assert.IsTrue(membri.Count > 50, "troppo pochi membri letti: " + membri.Count);
            foreach (var atteso in new[] { "ISequencerFactory::GetItem", "ISequencerFactory::GetContainer",
                                           "ISequenceMediator::AddAdvancedTarget" })
                Assert.IsTrue(membri.Contains(atteso),
                    "il ponte non nomina piu' " + atteso + ": le altre due verifiche non provano piu' niente");
        }
    }
}
