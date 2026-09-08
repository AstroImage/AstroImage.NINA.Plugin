using System;
using System.ComponentModel.Composition;
using System.Net.Http;
using AstroImage.NINA.Plugin.Services;
using NINA.Equipment.Interfaces.ViewModel;
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

        /// <summary>Dove sta il motore. Per ora una prova sulla macchina locale.</summary>
        public const string RadiceDiProva = "http://127.0.0.1:8791/";

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

        /// <summary>Perche' non si puo' consegnare, quando non si puo'. Null se si puo'.</summary>
        public string PerCheNonConsegna { get; }

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

            Radice = RadiceDiProva;
            Cliente = new ClienteStrategy(Trasporto, new Uri(Radice));

            Mediatore = mediatore;
            /*  I pezzi si prendono clonando un modello di bersaglio di N.I.N.A.: la
             *  fabbrica del Sequenziatore ai plugin non viene fornita, e questa e' la
             *  strada che resta — che e' anche la piu' solida fra le versioni. */
            var fonte = new FonteDaModello(mediatore);
            Fonte = fonte;
            Costruttore = fonte.Disponibile ? new SequenceBuilder(fonte, profileService) : null;

            PerCheNonConsegna =
                mediatore is null ? "N.I.N.A. non ha fornito il mediatore delle sequenze."
                : !fonte.Disponibile ? fonte.PerCheNo
                : null;
        }
    }
}
