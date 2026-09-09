using AstroImage.NINA.Plugin.Localization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AstroImage.NINA.Plugin.Tests {

    /*  I TEST PARLANO ITALIANO, E LO DICONO INVECE DI SPERARLO.
     *
     *  Quasi duecento asserzioni di questa suite guardano dentro il testo di un
     *  messaggio — «non dice niente», «29 pose», «4,83 h» — perche' quello che conta
     *  non e' che il ponte rifiuti, e' che dica PERCHE'. Sono test giusti e non si
     *  toccano.
     *
     *  Ma dal momento in cui le parole vivono in due lingue, quelle asserzioni
     *  dipenderebbero dalla lingua del computer che le esegue: verdi su questa
     *  macchina, che ha Windows in italiano, e rosse su una macchina inglese o su un
     *  servizio di integrazione continua. Un test che passa per via dell'ambiente non
     *  e' un test.
     *
     *  Quindi la lingua si impone qui, una volta, per tutta la suite. L'inglese non
     *  resta scoperto: le sue voci sono verificate da LocalizzazioneTests, che
     *  controlla che ci siano tutte, che non siano vuote e che i segnaposto combacino.
     */
    [TestClass]
    public static class LinguaDeiTest {

        [AssemblyInitialize]
        public static void PrimaDiTutto(TestContext _) => Loc.Instance.ForzaLingua("it");
    }
}
