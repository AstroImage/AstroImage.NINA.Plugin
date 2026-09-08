using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AstroImage.NINA.Plugin.Models;
using AstroImage.NINA.Plugin.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace AstroImage.NINA.Plugin.Tests {

    /*  IL CORRIERE, PROVATO SENZA IL SERVIZIO ACCESO.
     *
     *  Le due fixture sono risposte VERE del servizio, catturate mentre girava sul
     *  motore vero: `prescrizione-ok.json` e' una richiesta riuscita su NGC 6888 —
     *  la taglia che il ponte incontrera' davvero — e `prescrizione-errore.json` e'
     *  un bersaglio inesistente. Una risposta inventata proverebbe soltanto che chi
     *  l'ha scritta e chi la legge hanno avuto la stessa idea sbagliata.
     *
     *  LA PROSA E' STATA TOLTA, LA STRUTTURA NO. Dove la risposta vera portava un
     *  testo discorsivo — la scheda dell'oggetto, le trappole di elaborazione, il
     *  comportamento dei filtri, le stringhe di provenienza — la fixture porta
     *  `(prosa non pubblicata)`. Quel testo e' il lavoro editoriale del motore, che
     *  e' in un repository privato, e questo e' pubblico. Non e' un impoverimento
     *  del contratto: i campi ci sono tutti, con lo stesso tipo e nello stesso
     *  posto, e nessun numero e' stato toccato — 3.289 percorsi prima, 3.289 dopo.
     *  Il ponte non legge nulla di quei campi: li trasporta e basta, ed e'
     *  esattamente cio' che questi test verificano.
     *
     *  Qui non si accende niente e non si apre nessuna porta: il trasporto e' finto,
     *  le risposte sono vere. E' la divisione giusta, perche' cio' che si vuole
     *  provare non e' che HTTP funzioni — funziona — ma che il ponte non tocchi
     *  quello che trasporta.
     */
    [TestClass]
    public class ClienteStrategyTests {

        private static string Cartella =>
            Path.Combine(Path.GetDirectoryName(typeof(ClienteStrategyTests).Assembly.Location)!,
                         "Fixtures", "servizio");

        private static string Fixture(string nome) =>
            File.ReadAllText(Path.Combine(Cartella, nome + ".json"));

        /// <summary>Un trasporto finto che registra che cosa gli e' stato chiesto.</summary>
        private sealed class Trasporto : HttpMessageHandler {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _risposta;
            public Uri? Indirizzo;
            public string? Corpo;
            public string? TipoContenuto;
            public Trasporto(Func<HttpRequestMessage, HttpResponseMessage> risposta) => _risposta = risposta;

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) {
                Indirizzo = r.RequestUri;
                TipoContenuto = r.Content?.Headers?.ContentType?.ToString();
                Corpo = r.Content is null ? null : await r.Content.ReadAsStringAsync();
                return _risposta(r);
            }
        }

        private static HttpResponseMessage Risponde(HttpStatusCode stato, string corpo) =>
            new HttpResponseMessage(stato) {
                Content = new StringContent(corpo, Encoding.UTF8, "application/json")
            };

        private static (ClienteStrategy cliente, Trasporto trasporto) Banco(
                HttpStatusCode stato, string corpo, string radice = "http://127.0.0.1:8791/") {
            var t = new Trasporto(_ => Risponde(stato, corpo));
            return (new ClienteStrategy(new HttpClient(t), new Uri(radice)), t);
        }

        // ---------------------------------------------------------------- il giro buono

        [TestMethod]
        public async Task Prescrizione_RispostaVera_LeggeLeSequenze() {
            var json = Fixture("prescrizione-ok");
            var (cliente, _) = Banco(HttpStatusCode.OK, json);

            var esito = await cliente.Prescrizione("{}");

            Assert.IsTrue(esito.Riuscito, esito.Messaggio);
            Assert.AreEqual(200, esito.Stato);
            Assert.AreEqual("1", esito.Contratto, "la versione del contratto arriva");
            Assert.AreEqual(1, esito.Sequenze.Count, "una notte chiesta, una notte tornata");
            Assert.AreEqual(0, esito.Scartate);

            var m = esito.Sequenze[0].Modello!;
            Assert.IsNotNull(m, "il modello e' stato letto, non lasciato in JSON");
            Assert.IsTrue(m.Blocchi!.Count > 0, "e ha dei blocchi da riprendere");
            Assert.AreEqual(1, esito.Sequenze[0].Notte);
        }

        /*  LA PROVA CHE IL PONTE NON TOCCA QUELLO CHE TRASPORTA.
         *
         *  La risposta contiene valutazione, prescrizione, posa e piano: roba
         *  dell'interfaccia, che il ponte non capisce e non deve capire. Deve pero'
         *  arrivare a destinazione intatta, carattere per carattere. Se un giorno
         *  qualcuno decidesse di rileggerla e riscriverla «per pulizia», questo test
         *  diventerebbe rosso — ed e' esattamente quello che deve fare.
         */
        [TestMethod]
        public async Task Prescrizione_IlCorpoTornaIdenticoCarattere_PerCarattere() {
            var json = Fixture("prescrizione-ok");
            var (cliente, _) = Banco(HttpStatusCode.OK, json);

            var esito = await cliente.Prescrizione("{}");

            Assert.AreEqual(json, esito.Corpo, "la risposta non e' stata rimaneggiata");
            Assert.IsTrue(esito.Corpo.Contains("\"valutazione\""), "e porta anche cio' che il ponte non legge");
            Assert.IsTrue(esito.Corpo.Contains("\"posa\""));
            Assert.IsTrue(esito.Corpo.Contains("\"piano\""));
        }

        [TestMethod]
        public async Task Prescrizione_LaDomandaPartaComeEStataScritta() {
            var (cliente, trasporto) = Banco(HttpStatusCode.OK, Fixture("prescrizione-ok"));
            const string domanda = "{\"sito\":{\"lat\":45.9},\"nota\":\"non toccarmi\"}";

            await cliente.Prescrizione(domanda);

            Assert.AreEqual(domanda, trasporto.Corpo, "il corpo non e' stato riscritto");
            StringAssert.Contains(trasporto.TipoContenuto ?? "", "application/json");
        }

        // ------------------------------------------------- l'indirizzo non e' scritto qui

        /*  IL TRASPORTO NON E' DECISO IN QUESTO FILE, e questa e' la prova.
         *
         *  Oggi il servizio gira in chiaro sulla stessa macchina perche' e' una prova.
         *  Il giorno in cui sara' un dominio con TLS deve bastare un Uri diverso. Se
         *  qualcuno scrivesse «127.0.0.1» dentro ClienteStrategy, il secondo caso qui
         *  sotto fallirebbe.
         */
        [DataTestMethod]
        [DataRow("http://127.0.0.1:8791/", "http://127.0.0.1:8791/v1/prescrizione")]
        [DataRow("https://strategy.esempio.invalid/", "https://strategy.esempio.invalid/v1/prescrizione")]
        [DataRow("https://esempio.invalid/motore/", "https://esempio.invalid/motore/v1/prescrizione")]
        public async Task Prescrizione_VaDoveGliSiDice(string radice, string atteso) {
            var (cliente, trasporto) = Banco(HttpStatusCode.OK, Fixture("prescrizione-ok"), radice);

            await cliente.Prescrizione("{}");

            Assert.AreEqual(atteso, trasporto.Indirizzo!.ToString());
        }

        // ---------------------------------------------------------------- quando va male

        [TestMethod]
        public async Task Prescrizione_BersaglioSconosciuto_RiportaIlMotivoDelServizio() {
            var (cliente, _) = Banco((HttpStatusCode)422, Fixture("prescrizione-errore"));

            var esito = await cliente.Prescrizione("{}");

            Assert.IsFalse(esito.Riuscito);
            Assert.AreEqual(422, esito.Stato);
            Assert.AreEqual("bersaglio_sconosciuto", esito.Codice, "il codice e' quello del servizio");
            Assert.IsFalse(string.IsNullOrWhiteSpace(esito.Messaggio), "e c'e' anche una frase per chi legge");
            Assert.AreEqual(0, esito.Sequenze.Count);
        }

        /*  SERVIZIO SPENTO E SERVIZIO CHE RISPONDE MALE sono due cose diverse, e chi
         *  legge deve poterle distinguere: la prima si aggiusta accendendolo. */
        [TestMethod]
        public async Task Prescrizione_ServizioSpento_LoDiceInveceDiEsplodere() {
            var t = new Trasporto(_ => throw new HttpRequestException("connessione rifiutata"));
            var cliente = new ClienteStrategy(new HttpClient(t), new Uri("http://127.0.0.1:8791/"));

            var esito = await cliente.Prescrizione("{}");

            Assert.IsFalse(esito.Riuscito);
            Assert.AreEqual("servizio_irraggiungibile", esito.Codice);
            Assert.AreEqual(0, esito.Stato, "non c'e' stata nessuna risposta HTTP");
        }

        [TestMethod]
        public async Task Prescrizione_RispostaNonJson_NonLaSpacciaPerBuona() {
            var (cliente, _) = Banco(HttpStatusCode.OK, "<html>errore del proxy</html>");

            var esito = await cliente.Prescrizione("{}");

            Assert.IsFalse(esito.Riuscito);
            Assert.AreEqual("risposta_illeggibile", esito.Codice);
        }

        [TestMethod]
        public async Task Prescrizione_RichiestaVuota_NonPartePerNiente() {
            var (cliente, trasporto) = Banco(HttpStatusCode.OK, Fixture("prescrizione-ok"));

            var esito = await cliente.Prescrizione("   ");

            Assert.IsFalse(esito.Riuscito);
            Assert.AreEqual("richiesta_vuota", esito.Codice);
            Assert.IsNull(trasporto.Indirizzo, "e nessuno e' stato disturbato");
        }

        /*  UNA NOTTE SENZA MODELLO NON SI PERDE IN SILENZIO. Un elenco che si accorcia
         *  senza dirlo e' il modo piu' rapido per far riprendere a qualcuno una notte
         *  in meno senza saperlo. */
        [TestMethod]
        public async Task Prescrizione_UnaNotteSenzaModello_VieneScartataEContata() {
            const string corpo = "{\"contratto\":\"1\",\"prodotto\":{\"sequenze\":[" +
                                 "{\"notte\":1,\"modello\":null},{\"notte\":2}]}}";
            var (cliente, _) = Banco(HttpStatusCode.OK, corpo);

            var esito = await cliente.Prescrizione("{}");

            Assert.IsTrue(esito.Riuscito);
            Assert.AreEqual(0, esito.Sequenze.Count);
            Assert.AreEqual(2, esito.Scartate, "e il ponte dice quante ne ha buttate");
        }

        // ------------------------------------------------------------------- c'e' qualcuno?

        [TestMethod]
        public async Task Raggiungibile_DiceSoloSiONo() {
            var (acceso, _) = Banco(HttpStatusCode.OK, "{}");
            Assert.IsTrue(await acceso.Raggiungibile());

            var spentoT = new Trasporto(_ => throw new HttpRequestException("niente"));
            var spento = new ClienteStrategy(new HttpClient(spentoT), new Uri("http://127.0.0.1:1/"));
            Assert.IsFalse(await spento.Raggiungibile());
        }

        // ---------------------------------------------- la catena intera, senza N.I.N.A.

        /*  DAL SERVIZIO ALLA RICETTA, in un test solo.
         *
         *  E' il pezzo di percorso che si puo' provare per intero qui: la risposta e'
         *  quella vera del servizio, il corriere e' quello vero, la traduzione e'
         *  quella vera. Manca soltanto l'ultimo passo — SequenceBuilder, che chiede la
         *  fabbrica del Sequenziatore Avanzato e quindi vuole N.I.N.A. accesa.
         *
         *  Provare fin qui significa che se un giorno il giro completo si rompesse,
         *  questo test direbbe subito da che parte NON e' il guasto.
         */
        [TestMethod]
        public async Task DalServizioAllaRicetta_LaCatenaRegge() {
            var (cliente, _) = Banco(HttpStatusCode.OK, Fixture("prescrizione-ok"));

            var esito = await cliente.Prescrizione("{}");
            Assert.IsTrue(esito.Riuscito);

            var modello = esito.Sequenze[0].Modello!;
            var ricetta = Traduzione.Traduci(modello);

            Assert.IsFalse(string.IsNullOrWhiteSpace(ricetta.NomeBersaglio), "il bersaglio ha un nome");
            Assert.IsTrue(ricetta.RaGradi >= 0 && ricetta.RaGradi < 360, "ascensione retta in gradi");
            Assert.IsTrue(ricetta.DecGradi >= -90 && ricetta.DecGradi <= 90, "declinazione in gradi");
            Assert.IsTrue(ricetta.Costruibile, "e c'e' abbastanza per costruire qualcosa");
            Assert.IsTrue(ricetta.Blocchi.Count > 0, "almeno un blocco da riprendere");
            foreach (var b in ricetta.Blocchi) {
                Assert.IsTrue(b.Secondi > 0, "una posa senza durata non e' una posa");
                Assert.IsTrue(b.Pose > 0, "ne' zero pose sono una ripresa");
            }
            Assert.AreEqual(0, ricetta.Scartati.Count,
                "niente e' stato buttato per strada: " + string.Join(" | ", ricetta.Scartati));
            /*  NGC 6888 e' a 20h 12m circa: se un giorno l'ascensione retta uscisse in
             *  ore invece che in gradi, il controllo di intervallo qui sopra passerebbe
             *  lo stesso. Questo no. */
            Assert.IsTrue(Math.Abs(ricetta.RaGradi - 303.03) < 1.0,
                $"NGC 6888 sta a 303 gradi, non a {ricetta.RaGradi:F2}");
        }

        // ------------------------------------------------- il corriere non sa di scienza

        /*  Il ponte non deve capire la domanda. Se un giorno ClienteStrategy avesse un
         *  tipo per il sito, il banco o il bersaglio, qualcuno prima o poi ci
         *  deciderebbe sopra qualcosa: e' successo in ogni progetto in cui il corriere
         *  ha imparato a leggere. Il metodo prende una stringa, e deve restare tale. */
        [TestMethod]
        public void ClienteStrategy_ChiedeUnaStringa_NonUnOggettoDaInterpretare() {
            var m = typeof(ClienteStrategy).GetMethod(nameof(ClienteStrategy.Prescrizione))!;
            var p = m.GetParameters();
            Assert.AreEqual(typeof(string), p[0].ParameterType,
                "la domanda si trasporta, non si interpreta");
            Assert.IsTrue(p.Length == 2 && p[1].ParameterType == typeof(CancellationToken),
                "e si puo' annullare");
        }

        /*  E non deve avere un indirizzo in tasca: l'unico modo di dirgli dove andare
         *  e' il costruttore. */
        [TestMethod]
        public void ClienteStrategy_NonHaUnIndirizzoScrittoDentro() {
            var costruttori = typeof(ClienteStrategy).GetConstructors();
            Assert.AreEqual(1, costruttori.Length, "un solo modo di costruirlo");
            var p = costruttori[0].GetParameters();
            Assert.IsTrue(p.Any(x => x.ParameterType == typeof(Uri)),
                "e chiede l'indirizzo a chi lo costruisce");
            Assert.IsTrue(p.Any(x => x.ParameterType == typeof(HttpClient)),
                "e anche il trasporto, che non e' affare suo");
        }
    }
}
