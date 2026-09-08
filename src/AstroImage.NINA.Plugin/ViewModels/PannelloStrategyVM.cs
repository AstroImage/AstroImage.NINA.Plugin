using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Net.Http;
using AstroImage.NINA.Plugin.Services;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.Core.Utility;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Interfaces.Mediator;
using NINA.WPF.Base.ViewModel;

namespace AstroImage.NINA.Plugin.ViewModels {

    /*  IL PANNELLO, cioe' il posto dove il ponte diventa visibile.
     *
     *  N.I.N.A. scopre i pannelli con MEF: cerca gli IDockableVM e li mette
     *  nell'elenco delle finestre agganciabili. La vista si lega a questo oggetto per
     *  convenzione di nome — un DataTemplate la cui chiave e' NomeCompleto_Dockable —
     *  e come per la pagina delle opzioni non c'e' nessun errore se la chiave non
     *  combacia: esce un pannello vuoto e basta. Vedi Resources/DataTemplates.xaml.
     *
     *  QUESTO OGGETTO E' LA RADICE DI COMPOSIZIONE, ed e' l'unico posto in tutto il
     *  ponte dove sta scritto un indirizzo. ClienteStrategy non ne conosce nessuno e
     *  c'e' un test che lo verifica; la pagina non ne riceve nessuno, e non deve —
     *  altrimenti chiunque riesca a parlare alla pagina userebbe il ponte per bussare
     *  dove vuole. L'indirizzo lo tiene il C#, e la pagina puo' solo chiedere.
     *
     *  L'INDIRIZZO DI OGGI E' UNA PROVA. 127.0.0.1 in chiaro va bene per dimostrare che
     *  il giro si chiude; il giorno in cui il motore sara' un servizio vero, qui
     *  cambia una stringa — e diventera' un'impostazione, non una costante. Il resto
     *  del ponte non se ne accorgera'.
     */

    [Export(typeof(IDockableVM))]
    public class PannelloStrategyVM : DockableVM {

        /// <summary>Dove sta il motore quando nessuno dice altrimenti.</summary>
        public const string RadiceDiProva = "http://127.0.0.1:8791/";

        /// <summary>Il file, accanto al DLL, in cui si puo' scrivere un altro indirizzo.</summary>
        public const string FileIndirizzo = "strategy.url";

        /*  L'INDIRIZZO SI PUO' SCRIVERE ACCANTO AL DLL, e questa e' la versione
         *  provvisoria di un'impostazione vera.
         *
         *  Serve perche' il banco di prova con i filtri veri e' un ALTRO computer: il
         *  mini PC in campo, dove il plugin gira e il motore no. Un indirizzo cablato
         *  nel codice funziona solo sulla macchina di chi l'ha scritto — e' lo stesso
         *  difetto per cui, in ASTROFOTO, due strumenti di misura leggevano da /tmp e
         *  non avevano mai funzionato per nessun altro.
         *
         *  Un file di testo di una riga, non una variabile d'ambiente: su un PC da campo
         *  si vede accanto al DLL, si legge e si corregge senza riavviare niente di
         *  sistema. La casa definitiva sara' la pagina delle Opzioni; questo e' il
         *  ponteggio, e si comporta come un ponteggio — se non c'e', si torna a casa. */
        private static string RadiceInUso(out string da) {
            da = "valore predefinito";
            try {
                var accanto = Path.GetDirectoryName(typeof(PannelloStrategyVM).Assembly.Location);
                if (accanto is null) return RadiceDiProva;
                var file = Path.Combine(accanto, FileIndirizzo);
                if (!File.Exists(file)) return RadiceDiProva;

                var scritto = File.ReadAllText(file).Trim();
                if (scritto.Length == 0) return RadiceDiProva;
                /*  Uno slash finale mancante cambierebbe il significato di Uri relativo:
                 *  «…:8791» + «v1/salute» diventerebbe «…/v1/salute» al posto giusto solo
                 *  per caso. Si aggiunge invece di sperare. */
                if (!scritto.EndsWith("/")) scritto += "/";
                if (!Uri.TryCreate(scritto, UriKind.Absolute, out var u)
                        || (u.Scheme != Uri.UriSchemeHttp && u.Scheme != Uri.UriSchemeHttps)) {
                    da = $"il file {FileIndirizzo} dice «{scritto}», che non e' un indirizzo http: ignorato";
                    return RadiceDiProva;
                }
                da = FileIndirizzo;
                return u.ToString();
            } catch (Exception e) {
                da = $"il file {FileIndirizzo} non si e' potuto leggere ({e.Message}): ignorato";
                return RadiceDiProva;
            }
        }

        private static readonly HttpClient Trasporto = new HttpClient {
            /*  Un minuto e' generoso per un calcolo che ne impiega ottanta millesimi,
             *  ma il servizio potrebbe essere appena partito e stare ancora leggendo il
             *  catalogo. Meglio aspettare che dire «spento» a chi e' solo lento. */
            Timeout = TimeSpan.FromSeconds(60)
        };

        /// <summary>Il corriere verso Strategy. La vista lo usa e non ne costruisce altri.</summary>
        public ClienteStrategy Cliente { get; }

        /// <summary>La radice in uso, mostrata nel pannello: chi guarda deve poter
        /// vedere con chi sta parlando.</summary>
        public string Radice { get; }

        /// <summary>La prescrizione che il ponte ha in mano, con la guardia che
        /// impedisce di consegnare una riga di una risposta precedente.</summary>
        public PrescrizioneCorrente InMano { get; } = new PrescrizioneCorrente();

        /// <summary>Da dove vengono i pezzi del Sequenziatore.</summary>
        public IFonteDiPezzi Fonte { get; }

        /// <summary>Il montatore, o null se non c'e' da dove prendere i pezzi.</summary>
        public SequenceBuilder Costruttore { get; }

        /// <summary>Chi accetta il bersaglio nel Sequenziatore, o null.</summary>
        public ISequenceMediator Mediatore { get; }

        /*  SI CHIEDE AL MOMENTO, non all'avvio. Quando N.I.N.A. compone questo pannello
         *  il Sequenziatore i suoi modelli non li ha ancora letti: una risposta calcolata
         *  nel costruttore sarebbe stata «non disponibile» per tutta la sessione. E
         *  comunque i modelli sono dell'utente, che ne aggiunge e ne toglie mentre il
         *  programma e' aperto: qualunque fotografia scattata all'avvio invecchia. */
        public string PerCheNonConsegna =>
            Mediatore is null ? "N.I.N.A. non ha fornito il mediatore delle sequenze."
            : Costruttore is null ? "Il montatore non e' stato costruito."
            : !Fonte.Disponibile ? Fonte.PerCheNo
            : null;

        /*  IL MEDIATORE SI CHIEDE CON AllowDefault, e la ragione e' il modo in cui si
         *  guasta MEF. Un parametro di [ImportingConstructor] che non si risolve non
         *  produce un pannello a meta': fa fallire la composizione dell'intera parte, e
         *  il plugin sparisce dall'elenco senza spiegazioni. AllowDefault lo trasforma
         *  in un null, e allora il pannello si carica lo stesso e puo' DIRE che cosa gli
         *  manca — che e' l'unica forma utile di questo guasto. E si e' gia' guadagnato
         *  il posto: e' cosi' che abbiamo scoperto la fabbrica mancante, leggendo un
         *  «non disponibile» invece di cercare per un'ora un plugin sparito.
         *
         *  QUI C'ERA ANCHE ISequencerFactory, e non c'e' piu'. Non e' una semplificazione:
         *  N.I.N.A. quella fabbrica ai plugin non la fornisce. `PluginLoader.GetContainer`
         *  compone trentanove servizi sulla 3.2 e quarantuno sulla 3.3 — la 3.3 aggiunge
         *  symbolBroker e templateLinkResolver — e in nessuna delle due liste c'e'.
         *  I pezzi arrivano clonando un modello di bersaglio: vedi FonteDaModello.
         */
        [ImportingConstructor]
        public PannelloStrategyVM(
                IProfileService profileService,
                [Import(AllowDefault = true)] ISequenceMediator mediatore) : base(profileService) {
            Title = "AstroImage Strategy";
            CanClose = true;
            /*  Nessuna icona: N.I.N.A. accetta un pannello senza geometria e ne disegna
             *  il solo titolo. Meglio di un disegno provvisorio che resta per anni. */
            ImageGeometry = null;

            Radice = RadiceInUso(out var daDove);
            Logger.Info($"[AstroImage] motore su {Radice} (da {daDove})");
            Cliente = new ClienteStrategy(Trasporto, new Uri(Radice));

            Mediatore = mediatore;
            /*  I pezzi si prendono clonando un modello di bersaglio di N.I.N.A.: la
             *  fabbrica del Sequenziatore ai plugin non viene fornita, e questa e' la
             *  strada che resta — che e' anche la piu' solida fra le versioni.
             *
             *  Il montatore si costruisce SEMPRE, anche se adesso non c'e' un modello:
             *  qui siamo all'avvio di N.I.N.A. e il Sequenziatore non ha ancora letto
             *  niente. E' la fonte a dire di volta in volta se puo'; costruire un
             *  montatore non impegna nessuno. */
            Fonte = new FonteDaModello(mediatore);
            Costruttore = new SequenceBuilder(Fonte, profileService);
        }
    }
}
