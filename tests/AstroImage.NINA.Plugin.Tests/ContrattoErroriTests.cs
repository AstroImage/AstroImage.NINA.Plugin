using System.Collections.Generic;
using System.Linq;
using AstroImage.NINA.Plugin.Localization;
using AstroImage.NINA.Plugin.Models;
using AstroImage.NINA.Plugin.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace AstroImage.NINA.Plugin.Tests {

    /*  I BYTE VERI DEL SERVIZIO, NON UNA LORO IMITAZIONE.
     *
     *  Le prove qui sotto partono dal JSON che Strategy ha mandato davvero, copiato
     *  dalla porta il 10 settembre 2026 e incollato tale e quale. E' la differenza fra
     *  provare il ponte e provare l'idea che il ponte si e' fatto del motore: se un
     *  giorno il campo si chiamasse `data` invece di `dati`, o `ricevuto` diventasse
     *  una stringa invece di un numero, una prova costruita a mano continuerebbe a
     *  passare mentre l'utente vede una frase in italiano.
     *
     *  Sono anche la fotografia del contratto: chi cambia il formato dall'altra parte
     *  fa cadere queste, e il rosso dice esattamente che cosa e' cambiato.
     */
    [TestClass]
    public class ContrattoErroriTests {

        [TestCleanup]
        public void Dopo() => Loc.Instance.ForzaLingua("it");

        //  ─── copiati dalla porta, senza toccare un carattere ───────────────────
        private const string CieloImplausibile =
            @"{""contratto"":""1"",""errore"":{""codice"":""cielo_implausibile"",""dati"":{""ricevuto"":12,""min"":16,""max"":22},""messaggio"":""SQM 12 non è la brillanza di un sito di ripresa (attesa fra 16 e 22 mag/arcsec²). 22 è il cielo naturale senza luce artificiale, 16 è oltre il centro di una grande città.""}}";

        private const string CieloAssente =
            @"{""contratto"":""1"",""errore"":{""codice"":""cielo_assente"",""dati"":{""min"":16,""max"":22},""messaggio"":""Il sito non dichiara un SQM. Senza la brillanza del cielo il motore non può calcolare né le ore né la posa.""}}";

        private const string CieloNonValido =
            @"{""contratto"":""1"",""errore"":{""codice"":""cielo_non_valido"",""dati"":{""ricevuto"":208,""min"":16,""max"":22},""messaggio"":""SQM 208 non è una brillanza di cielo: quasi sempre è il punto decimale, 20.8 battuto 208 oppure 2.08.""}}";

        private const string BersaglioSconosciuto =
            @"{""contratto"":""1"",""errore"":{""codice"":""bersaglio_sconosciuto"",""dati"":{""chiesto"":""non_esiste""},""messaggio"":""Nessun bersaglio con questo identificativo o con questo nome esatto.""}}";

        /// <summary>Il giro intero: byte della porta, lettura, appiattimento, frase.</summary>
        private static string? Frase(string json, string lingua) {
            Loc.Instance.ForzaLingua(lingua);
            var busta = RispostaPrescrizione.Leggi(json);
            Assert.IsNotNull(busta?.Errore, "la busta non porta un errore");
            return MessaggioDelMotore.Rendi(busta!.Errore!.Codice,
                MessaggioDelMotore.Appiattisci(busta.Errore.Dati), busta.Errore.Messaggio);
        }

        [TestMethod]
        public void ILimitiEIlValoreSopravvivonoAlGiroCompleto() {
            var en = Frase(CieloImplausibile, "en");
            StringAssert.Contains(en!, "12");
            StringAssert.Contains(en!, "16");
            StringAssert.Contains(en!, "22");
            StringAssert.Contains(en!, "imaging site");
            //  E la frase italiana del motore NON deve essere quella mostrata: se lo
            //  fosse, il ripiego sarebbe scattato senza che nessuno lo dicesse.
            Assert.IsFalse(en!.Contains("sito di ripresa"),
                "e' uscita la frase del motore: il ripiego e' scattato e non doveva");
        }

        [TestMethod]
        public void ITreCasiDelCieloDannoTreFrasiDIVERSE() {
            var frasi = new[] { CieloAssente, CieloNonValido, CieloImplausibile }
                .Select(j => Frase(j, "en")).ToList();
            CollectionAssert.AllItemsAreUnique(frasi,
                "due situazioni che si correggono in modo diverso dicono la stessa cosa");
            //  Ognuna deve indicare la sua correzione, e sono tre correzioni diverse.
            StringAssert.Contains(frasi[0]!, "missing", "il campo vuoto non dice che e' vuoto");
            StringAssert.Contains(frasi[1]!, "decimal point", "208 non dice del punto decimale");
            StringAssert.Contains(frasi[2]!, "imaging site", "12 non dice che non e' un sito");
        }

        [TestMethod]
        public void IlNomeChiestoTornaAGalla() {
            foreach (var lingua in new[] { "it", "en" })
                StringAssert.Contains(Frase(BersaglioSconosciuto, lingua)!, "non_esiste",
                    lingua + ": chi ha sbagliato a scrivere non vede che cosa ha scritto");
        }

        /*  UN MOTORE PIU' VECCHIO DEL CONTRATTO manda solo codice e messaggio. Il
         *  contratto resta "1" proprio perche' `dati` e' aggiuntivo: qui si verifica
         *  che l'assenza non rompa niente e che si esca in italiano, non nel vuoto. */
        [TestMethod]
        public void UnMotoreVecchioEsceInItalianoENonNelVuoto() {
            const string vecchio =
                @"{""contratto"":""1"",""errore"":{""codice"":""cielo_implausibile"",""messaggio"":""SQM 12 non va bene.""}}";
            Assert.AreEqual("SQM 12 non va bene.", Frase(vecchio, "en"));
        }

        /*  E UN MOTORE PIU' NUOVO DI QUESTO PONTE: un codice che qui non c'e'
         *  ancora. Deve uscire la frase del motore, non una stringa vuota e non il
         *  nome della chiave. E' il caso che rende sicuro aggiungere codici la'. */
        [TestMethod]
        public void UnCodiceDelFuturoEsceInItalianoENonSiPerde() {
            const string futuro =
                @"{""contratto"":""1"",""errore"":{""codice"":""luna_troppo_alta"",""dati"":{""alt"":42},""messaggio"":""La Luna e' a 42 gradi.""}}";
            Assert.AreEqual("La Luna e' a 42 gradi.", Frase(futuro, "en"));
        }

        /*  LA FRASE DEL MOTORE NON E' LA FRASE DELL'INTERFACCIA, e non devono
         *  somigliarsi per forza: quella e' una riga di log, letta con curl da chi
         *  sviluppa; questa e' una riga letta da un astrofotografo alle due di notte.
         *  La prova non impone che siano diverse — impone che l'italiano mostrato
         *  venga dalla resx e non sia una copia trasportata dal filo, perche' se lo
         *  fosse cambiare il testo qui non cambierebbe niente a schermo. */
        [TestMethod]
        public void AncheInItalianoLaFraseVieneDallaResx() {
            var mostrata = Frase(CieloImplausibile, "it");
            var dalMotore = RispostaPrescrizione.Leggi(CieloImplausibile)!.Errore!.Messaggio;
            Assert.AreNotEqual(dalMotore, mostrata,
                "l'italiano mostrato e' quello del filo: la resx non viene nemmeno letta");
            StringAssert.Contains(mostrata!, "12");
        }
    }
}
