using System;
using System.ComponentModel.Composition;
using System.Net.Http;
using AstroImage.NINA.Plugin.Services;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.Profile.Interfaces;
using NINA.Sequencer;
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

        /// <summary>Il montatore, o null se N.I.N.A. non ha dato la fabbrica.</summary>
        public SequenceBuilder Costruttore { get; }

        /// <summary>Chi accetta il bersaglio nel Sequenziatore, o null.</summary>
        public ISequenceMediator Mediatore { get; }

        /// <summary>Perche' non si puo' consegnare, quando non si puo'. Null se si puo'.</summary>
        public string PerCheNonConsegna { get; }

        /*  I DUE SERVIZI SI CHIEDONO CON AllowDefault, e la ragione e' il modo in cui si
         *  guasta MEF. Un parametro di [ImportingConstructor] che non si risolve non
         *  produce un pannello a meta': fa fallire la composizione dell'intera parte, e
         *  il plugin sparisce dall'elenco senza spiegazioni. AllowDefault lo trasforma
         *  in un null, e allora il pannello si carica lo stesso e puo' DIRE che cosa gli
         *  manca — che e' l'unica forma utile di questo guasto.
         *
         *  Che siano risolvibili e' verificato, non sperato: SimpleExposure di N.I.N.A.
         *  stessa chiede ISequencerFactory con un [ImportingConstructor], e Orbitals —
         *  installato e funzionante su questa macchina — chiede ISequenceMediator dentro
         *  un DockableVM, che e' esattamente questo caso. Ma «verificato» e «garantito
         *  per sempre» sono due cose diverse, e la differenza costa una riga.
         */
        [ImportingConstructor]
        public PannelloStrategyVM(
                IProfileService profileService,
                [Import(AllowDefault = true)] ISequencerFactory fabbrica,
                [Import(AllowDefault = true)] ISequenceMediator mediatore) : base(profileService) {
            Title = "AstroImage Strategy";
            CanClose = true;
            /*  Nessuna icona: N.I.N.A. accetta un pannello senza geometria e ne disegna
             *  il solo titolo. Meglio di un disegno provvisorio che resta per anni. */
            ImageGeometry = null;

            Radice = RadiceDiProva;
            Cliente = new ClienteStrategy(Trasporto, new Uri(Radice));

            Mediatore = mediatore;
            Costruttore = fabbrica is null ? null : new SequenceBuilder(fabbrica, profileService);

            PerCheNonConsegna =
                fabbrica is null && mediatore is null
                    ? "N.I.N.A. non ha fornito ne' la fabbrica del Sequenziatore ne' il mediatore."
                : fabbrica is null ? "N.I.N.A. non ha fornito la fabbrica del Sequenziatore."
                : mediatore is null ? "N.I.N.A. non ha fornito il mediatore delle sequenze."
                : null;
        }
    }
}
