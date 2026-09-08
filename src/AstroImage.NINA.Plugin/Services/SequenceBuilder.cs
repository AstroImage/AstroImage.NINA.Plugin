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
using NINA.Sequencer.Trigger;
using NINA.Sequencer.Interfaces.Mediator;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.SequenceItem.Autofocus;
using NINA.Sequencer.SequenceItem.FilterWheel;
using NINA.Sequencer.SequenceItem.Guider;
using NINA.Sequencer.SequenceItem.Imaging;
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

        private readonly IFonteDiPezzi fonte;
        private readonly IProfileService profilo;

        /// <param name="fonte">
        /// Da dove vengono i pezzi. Non e' piu' <c>ISequencerFactory</c> perche' quella
        /// ai plugin N.I.N.A. non la fornisce: e' un'interfaccia nostra, e questo e' il
        /// motivo per cui adesso un banco di prova puo' montare davvero un costruttore.
        /// </param>
        /// <param name="profilo">
        /// Serve a una cosa sola, cercare un filtro nella ruota. Puo' essere nullo: senza
        /// profilo non si cambia vetro, e <see cref="FiltroDaRuota"/> lo dice tornando null
        /// invece di far cadere il montaggio.
        /// </param>
        public SequenceBuilder(IFonteDiPezzi fonte, IProfileService profilo) {
            this.fonte = fonte ?? throw new ArgumentNullException(nameof(fonte));
            this.profilo = profilo;
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

            /*  I FILTRI SI CONTROLLANO PRIMA DI TUTTO, e il target si rifiuta.
             *
             *  La prima consegna vera ha mostrato il difetto: la prescrizione chiedeva
             *  HO, la ruota non ce l'aveva, il codice lasciava il filtro «corrente» e
             *  non lo diceva a nessuno. In sequenza sono finite 29 pose da 600 secondi
             *  — quasi cinque ore — con qualunque vetro fosse montato. Una sequenza che
             *  SEMBRA la prescrizione e non lo e'.
             *
             *  Non sostituire il filtro resta giusto: mettere il vetro sbagliato sarebbe
             *  peggio. Ma allora il bersaglio non si consegna affatto. Fra le due strade
             *  — saltare il blocco o rifiutare tutto — vale l'asimmetria del danno: un
             *  rifiuto si corregge in un clic, cinque ore riprese col vetro sbagliato
             *  non si recuperano. E un bersaglio consegnato a meta' non si distingue,
             *  guardandolo, da uno conforme.
             *
             *  Si controlla PRIMA di chiedere i pezzi: non si costruisce cio' che si
             *  buttera' via, e cosi' la regola si puo' provare senza N.I.N.A. accesa. */
            var inRuota = NomiInRuota();
            var mancanti = ricetta.Blocchi
                .Select(b => ScartoPerFiltro(b.Etichetta, b.Filtro, inRuota, b.Pose, b.Secondi))
                .Where(x => x is not null).ToList();
            if (mancanti.Count > 0) {
                foreach (var m in mancanti) ricetta.Scartati.Add(m!);
                ricetta.Scartati.Add(inRuota.Count > 0
                    ? "In ruota ci sono: " + string.Join(", ", inRuota) + "."
                    : "Nel profilo attivo non risulta nessun filtro in ruota.");
                ricetta.Scartati.Add("Il bersaglio NON e' stato consegnato: una sequenza che non " +
                    "rispetta la prescrizione, in sequenza, non si distingue da una che la rispetta.");
                return null;
            }

            if (!fonte.Disponibile) {
                ricetta.Scartati.Add("Non c'e' da dove prendere i pezzi: " +
                                     (fonte.PerCheNo ?? "motivo non dichiarato") + ".");
                return null;
            }

            var dso = fonte.Contenitore();
            if (dso is null) {
                ricetta.Scartati.Add("Il contenitore del bersaglio non si e' potuto ottenere.");
                return null;
            }
            dso.Name = ricetta.Nome;

            /*  IL BERSAGLIO. Le coordinate arrivano in gradi e si consegnano in gradi:
             *  N.I.N.A. costruisce le proprie ore/minuti/secondi da sole, e una
             *  conversione scritta a mano qui sarebbe un fattore quindici in attesa. */
            dso.Target.TargetName = ricetta.NomeBersaglio;
            dso.Target.InputCoordinates.Coordinates =
                new Coordinates(ricetta.RaGradi, ricetta.DecGradi, Epoch.J2000, Coordinates.RAType.Degrees);
            dso.Target.PositionAngle = ricetta.AngoloDiPosa;

            /*  PRIMA DI RIPRENDERE: fuoco e guida, e in quest'ordine.
             *
             *  QUI C'ERA IL RAFFREDDAMENTO, e non c'e' piu'. `Sequence2VM.AddTarget`
             *  mette il bersaglio in `Items[1]`, l'area centrale, fra lo Start e l'End
             *  della sequenza — che sono di chi riprende. Una `CoolCamera` dentro un
             *  bersaglio raffredderebbe una volta PER BERSAGLIO invece che una volta per
             *  sessione. La vita della sessione non e' affare di un bersaglio, e la
             *  fonte dei pezzi non offre nemmeno piu' quel blocco: vedi IFonteDiPezzi. */
            if (ricetta.Raffredda) {
                ricetta.Note.Add("Il raffreddamento non entra nel bersaglio: appartiene all'avvio " +
                                 "della sequenza, che resta tuo. Mettilo nell'area di Start.");
            }
            if (ricetta.Focheggia) {
                var af = fonte.Autofocus();
                if (af is not null) dso.Add(af);
                else ricetta.Scartati.Add("Messa a fuoco automatica: nessun blocco disponibile da cui copiarla.");
            }
            if (ricetta.Guida) {
                var g = fonte.AvvioGuida();
                if (g is not null) dso.Add(g);
                else ricetta.Scartati.Add("Avvio della guida: nessun blocco disponibile da cui copiarlo.");
            }

            /*  I BLOCCHI, nell'ordine in cui il motore li ha messi. L'ordine e' una
             *  decisione gia' presa: la serie corta va in testa al suo gruppo perche'
             *  si fa il nucleo e poi si posa lungo, non il contrario. Qui non si
             *  riordina niente. */
            var costruiti = 0;
            foreach (var b in ricetta.Blocchi) {
                var r = Ripresa(b, ricetta.DitherOgniPose);
                if (r is not null) { dso.Add(r); costruiti++; }
                else ricetta.Scartati.Add(
                    $"{b.Etichetta}: nessun blocco di ripresa disponibile da cui copiare la posa.");
            }
            /*  Un contenitore senza riprese non e' un bersaglio dimezzato: e' un
             *  bersaglio che non fa niente, e consegnarlo sarebbe peggio che dire di no. */
            if (costruiti == 0) return null;

            /*  QUI C'ERANO IL RISCALDAMENTO E IL RITORNO A CASA. Stessa ragione del
             *  raffreddamento: dentro un bersaglio si eseguirebbero dopo OGNI bersaglio.
             *  Appartengono all'area di End, che e' di chi riprende. */
            if (ricetta.Raffredda || ricetta.TornaACasa) {
                ricetta.Note.Add("Riscaldamento e ritorno a casa non entrano nel bersaglio: " +
                                 "appartengono alla chiusura della sequenza, che resta tua.");
            }

            /*  IL DITHER NON E' UN INNESCO DI CONTENITORE, e prima lo era.
             *
             *  Ogni SmartExposure porta gia' il proprio DitherAfterExposures — e' cosi'
             *  che lo fa N.I.N.A. nei suoi modelli — e Ripresa lo imposta sul valore
             *  prescritto. Aggiungerne un altro sul contenitore vorrebbe dire ditherare
             *  DUE volte: due inneschi che contano le stesse pose.
             *
             *  Per questo la fonte dei pezzi non offre piu' un dither. Non e' una
             *  semplificazione: e' la stessa regola di CoolCamera e compagnia — cio' che
             *  non si puo' chiedere non si puo' aggiungere per sbaglio. */

            return dso;
        }

        /// <summary>
        /// Un blocco: stesso vetro, stessa posa, ripetuta N volte. In N.I.N.A. e' UNO
        /// SmartExposure con il suo ciclo, non N istruzioni: ricostruire a mano cio'
        /// che il programma sa gia' fare e' il modo piu' sicuro di sbagliare due
        /// programmi invece di uno.
        /// </summary>
        private ISequenceItem? Ripresa(RicettaBlocco b, int? ditherOgniPose) {
            var se = fonte.Posa();
            if (se is null) return null;

            var posa = se.GetTakeExposure();
            posa.ExposureTime = b.Secondi;
            posa.Binning = new BinningMode((short)b.Binning, (short)b.Binning);
            /*  Guadagno e offset si scrivono SOLO se il modello li dichiara. Il -1 del
             *  contratto vuol dire «non specificato», e il modo di non specificarli qui
             *  e' lasciare il valore del profilo dove sta. */
            if (b.Gain is not null) posa.Gain = b.Gain.Value;
            if (b.Offset is not null) posa.Offset = b.Offset.Value;

            se.GetLoopCondition().Iterations = b.Pose;

            /*  IL DITHER SI IMPOSTA, NON SI EREDITA, e questa riga viene da un difetto
             *  visto in sequenza: il modello di serie di N.I.N.A. porta «ogni 3 pose»,
             *  la prescrizione diceva «ogni 2», e il clone teneva il 3. Un numero
             *  plausibile, al posto giusto, che nessuno guarda due volte.
             *
             *  Zero NON disattiva: `ProgressExposures` diventa sempre zero e il dither
             *  scatterebbe a OGNI posa. Per non ditherare si toglie l'innesco. */
            var innesco = se.GetDitherAfterExposures();
            if (innesco is not null) {
                if (ditherOgniPose is int ogni && ogni > 0) innesco.AfterExposures = ogni;
                else se.Remove(innesco);
            }

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
            return ruota.FirstOrDefault(f => StessoVetro(f?.Name, nome));
        }

        /// <summary>I nomi dei vetri che il profilo attivo dichiara in ruota.</summary>
        public IReadOnlyList<string> NomiInRuota() =>
            profilo?.ActiveProfile?.FilterWheelSettings?.FilterWheelFilters
                ?.Select(f => f?.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n!).ToList()
            ?? new List<string>();

        /*  LA REGOLA DEL CONFRONTO, in un posto solo: senza distinzione fra maiuscole e
         *  senza spazi ai bordi. «Ha» e «ha » sono lo stesso vetro per chiunque tranne
         *  che per un confronto di stringhe. */
        private static bool StessoVetro(string? a, string? b) =>
            string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

        /*  LA DECISIONE SUL FILTRO, SENZA N.I.N.A. INTORNO.
         *
         *  Sta qui, statica e su stringhe e numeri, per una ragione precisa: e' la
         *  regola che ha sbagliato in campo, e una regola che ha sbagliato una volta
         *  deve poter essere provata in laboratorio. Tutto cio' che sta piu' in basso
         *  nel montaggio ha bisogno di N.I.N.A. accesa; questo no. */
        /// <summary>
        /// Null se il blocco va bene: nessun filtro prescritto, oppure prescritto e
        /// presente in ruota. Altrimenti la frase che dice che cosa manca e quanto costa.
        /// </summary>
        public static string? ScartoPerFiltro(string? etichetta, string? richiesto,
                                              IReadOnlyList<string>? inRuota, int pose, double secondi) {
            if (string.IsNullOrWhiteSpace(richiesto)) return null;
            var ruota = inRuota ?? new List<string>();
            if (ruota.Any(n => StessoVetro(n, richiesto))) return null;

            var ore = pose * secondi / 3600.0;
            return $"{(string.IsNullOrWhiteSpace(etichetta) ? "un blocco" : etichetta)}: " +
                   $"la prescrizione chiede il filtro «{richiesto!.Trim()}», che in ruota non c'e'. " +
                   $"Sono {pose} pose da {secondi:0.#} s, cioe' {ore:0.00} h che verrebbero riprese " +
                   "con il vetro montato adesso, qualunque sia.";
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
