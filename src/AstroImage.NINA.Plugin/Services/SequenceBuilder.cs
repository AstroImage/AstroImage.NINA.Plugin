using System;
using System.Collections.Generic;
using System.Linq;
using AstroImage.NINA.Plugin.Models;
using NINA.Astrometry;
using NINA.Core.Model.Equipment;
using NINA.Profile.Interfaces;
using NINA.Sequencer;
using NINA.Sequencer.Conditions;
using NINA.Sequencer.Container;
using NINA.Sequencer.Interfaces.Mediator;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.SequenceItem.Autofocus;
using NINA.Sequencer.SequenceItem.Camera;
using NINA.Sequencer.SequenceItem.FilterWheel;
using NINA.Sequencer.SequenceItem.Guider;
using NINA.Sequencer.SequenceItem.Imaging;
using NINA.Sequencer.SequenceItem.Telescope;
using NINA.Sequencer.Trigger.Guider;

#nullable enable

namespace AstroImage.NINA.Plugin.Services {

    /*  IL MONTAGGIO: dai valori gia' tradotti agli oggetti veri del Sequenziatore.
     *
     *  Qui non si converte e non si decide: si prende cio' che Traduzione ha preparato
     *  e si riempiono i campi. Ogni «se» che resta e' una capacita' dichiarata dal
     *  banco, mai una deduzione.
     *
     *  DUE REGOLE, E LA PRIMA E' MISURATA.
     *
     *  1. NON SI SCRIVE MAI `new` SU UN TIPO DI N.I.N.A. Sempre la fabbrica. Non e'
     *     eleganza: confrontando le assembly della 3.2.0.9001 con quelle della
     *     3.3.0.1057-nightly, quattro dei tipi che servono qui hanno cambiato il
     *     numero di parametri del costruttore — DeepSkyObjectContainer, SmartExposure,
     *     DitherAfterExposures, MeridianFlipTrigger. Le proprieta' invece sono
     *     identiche. Un `new` scritto a mano lega il plugin a una delle due versioni;
     *     la fabbrica risolve il costruttore con l'iniezione delle dipendenze e non se
     *     ne accorge nessuno. E' esattamente il punto in cui Target Scheduler 5.10.3
     *     muore sulla 3.2: «Could not load type ISymbolBroker».
     *
     *  2. NON SI AVVIA NIENTE. `ISequenceMediator` espone StartAdvancedSequence e
     *     questo file non lo nomina. Il ponte consegna; a premere il tasto e' chi
     *     riprende.
     */
    public sealed class SequenceBuilder {

        private readonly ISequencerFactory fabbrica;
        private readonly IProfileService profilo;

        public SequenceBuilder(ISequencerFactory fabbrica, IProfileService profilo) {
            this.fabbrica = fabbrica ?? throw new ArgumentNullException(nameof(fabbrica));
            this.profilo = profilo ?? throw new ArgumentNullException(nameof(profilo));
        }

        /// <summary>
        /// Costruisce il contenitore del bersaglio a partire dal modello. Non lo
        /// consegna a N.I.N.A.: consegnare e' un altro gesto, e va fatto quando chi
        /// riprende ha visto che cosa gli si sta mettendo in sequenza.
        /// </summary>
        /// <param name="ricetta">
        /// Cio' che e' stato tradotto, comprese le cose scartate e il perche'. Va
        /// mostrato: un blocco che non si e' costruito deve vedersi.
        /// </param>
        public IDeepSkyObjectContainer? Costruisci(SequenceModel? modello, out Ricetta ricetta) {
            ricetta = Traduzione.Traduci(modello);
            if (!ricetta.Costruibile) return null;

            var dso = fabbrica.GetContainer<DeepSkyObjectContainer>();
            dso.Name = ricetta.Nome;

            /*  IL BERSAGLIO. Le coordinate arrivano in gradi e si consegnano in gradi:
             *  N.I.N.A. costruisce le proprie ore/minuti/secondi da sole, e una
             *  conversione scritta a mano qui sarebbe un fattore quindici in attesa. */
            dso.Target.TargetName = ricetta.NomeBersaglio;
            dso.Target.InputCoordinates.Coordinates =
                new Coordinates(ricetta.RaGradi, ricetta.DecGradi, Epoch.J2000, Coordinates.RAType.Degrees);
            dso.Target.PositionAngle = ricetta.AngoloDiPosa;

            /*  PRIMA DI RIPRENDERE. L'ordine e' quello che ha senso al telescopio:
             *  prima si porta la camera in temperatura, poi si mette a fuoco, poi si
             *  avvia la guida. Ognuna solo se il banco dichiara di averla. */
            if (ricetta.Raffredda) {
                var freddo = fabbrica.GetItem<CoolCamera>();
                if (ricetta.TemperaturaC is not null) freddo.Temperature = ricetta.TemperaturaC.Value;
                if (ricetta.MinutiFreddo is not null) freddo.Duration = ricetta.MinutiFreddo.Value;
                dso.Add(freddo);
            }
            if (ricetta.Focheggia) dso.Add(fabbrica.GetItem<RunAutofocus>());
            if (ricetta.Guida) dso.Add(fabbrica.GetItem<StartGuiding>());

            /*  I BLOCCHI, nell'ordine in cui il motore li ha messi. L'ordine e' una
             *  decisione gia' presa: la serie corta va in testa al suo gruppo perche'
             *  si fa il nucleo e poi si posa lungo, non il contrario. Qui non si
             *  riordina niente. */
            foreach (var b in ricetta.Blocchi) dso.Add(Ripresa(b));

            /*  DOPO. Si riscalda e si torna a casa, se il banco lo sa fare. */
            if (ricetta.Raffredda) {
                var caldo = fabbrica.GetItem<WarmCamera>();
                if (ricetta.MinutiCaldo is not null) caldo.Duration = ricetta.MinutiCaldo.Value;
                dso.Add(caldo);
            }
            if (ricetta.TornaACasa) dso.Add(fabbrica.GetItem<FindHome>());

            /*  IL DITHERING e' un innesco del contenitore, non un'istruzione in fila:
             *  scatta ogni N pose qualunque cosa stia succedendo. Esiste solo con la
             *  guida, perche' senza guida non c'e' niente da spostare. */
            if (ricetta.DitherOgniPose is not null) {
                var dither = fabbrica.GetTrigger<DitherAfterExposures>();
                dither.AfterExposures = ricetta.DitherOgniPose.Value;
                dso.Add(dither);
            }

            return dso;
        }

        /// <summary>
        /// Un blocco: stesso vetro, stessa posa, ripetuta N volte. In N.I.N.A. e' UNO
        /// SmartExposure con il suo ciclo, non N istruzioni: ricostruire a mano cio'
        /// che il programma sa gia' fare e' il modo piu' sicuro di sbagliare due
        /// programmi invece di uno.
        /// </summary>
        private ISequenceItem Ripresa(RicettaBlocco b) {
            var se = fabbrica.GetItem<SmartExposure>();

            var posa = se.GetTakeExposure();
            posa.ExposureTime = b.Secondi;
            posa.Binning = new BinningMode((short)b.Binning, (short)b.Binning);
            /*  Guadagno e offset si scrivono SOLO se il modello li dichiara. Il -1 del
             *  contratto vuol dire «non specificato», e il modo di non specificarli qui
             *  e' lasciare il valore del profilo dove sta. */
            if (b.Gain is not null) posa.Gain = b.Gain.Value;
            if (b.Offset is not null) posa.Offset = b.Offset.Value;

            se.GetLoopCondition().Iterations = b.Pose;

            /*  Il filtro si cambia solo se c'e' una ruota e se nella ruota quel nome
             *  esiste davvero. Un nome che non c'e' N.I.N.A. non lo segnala: mette il
             *  primo filtro e va avanti, e si riprenderebbero ore con il vetro
             *  sbagliato. Meglio non toccare il filtro e dirlo. */
            if (!string.IsNullOrWhiteSpace(b.Filtro)) {
                var trovato = FiltroDaRuota(b.Filtro!);
                if (trovato is not null) se.GetSwitchFilter().Filter = trovato;
            }

            return se;
        }

        /// <summary>
        /// Cerca il filtro nella ruota del profilo attivo. Confronto senza distinzione
        /// fra maiuscole e spazi ai bordi: «Ha» e «ha » sono lo stesso vetro per
        /// chiunque tranne che per un confronto di stringhe.
        /// </summary>
        public FilterInfo? FiltroDaRuota(string nome) {
            var ruota = profilo?.ActiveProfile?.FilterWheelSettings?.FilterWheelFilters;
            if (ruota is null) return null;
            return ruota.FirstOrDefault(f =>
                string.Equals(f?.Name?.Trim(), nome?.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Consegna il contenitore al Sequenziatore Avanzato come NUOVO bersaglio: non
        /// sostituisce niente di quello che c'e' gia'. E' l'unico gesto che tocca lo
        /// stato di N.I.N.A., ed e' una riga sola di proposito.
        /// </summary>
        public static void Consegna(ISequenceMediator mediatore, IDeepSkyObjectContainer contenitore) {
            if (mediatore is null) throw new ArgumentNullException(nameof(mediatore));
            if (contenitore is null) throw new ArgumentNullException(nameof(contenitore));
            mediatore.AddAdvancedTarget(contenitore);
        }
    }
}
