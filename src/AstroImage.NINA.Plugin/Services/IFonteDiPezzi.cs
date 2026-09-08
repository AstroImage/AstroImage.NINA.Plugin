using System;
using NINA.Sequencer;
using NINA.Sequencer.Container;
using NINA.Sequencer.SequenceItem.Autofocus;
using NINA.Sequencer.SequenceItem.Guider;
using NINA.Sequencer.SequenceItem.Imaging;
using NINA.Sequencer.Trigger.Guider;

#nullable enable

namespace AstroImage.NINA.Plugin.Services {

    /*  IL MAGAZZINO DEI PEZZI.
     *
     *  Perche' esiste. `SequenceBuilder` chiedeva i pezzi a `ISequencerFactory`, e
     *  quella fabbrica ai plugin NON ARRIVA: `PluginLoader.GetContainer` di N.I.N.A.
     *  compone quaranta servizi nel contenitore dei plugin e la fabbrica non e' fra
     *  quelli — ne' sulla 3.2 ne' sulla 3.3, dove ne aggiungono due altri. Il ponte si
     *  e' caricato lo stesso, ha detto che non poteva consegnare, e cosi' l'abbiamo
     *  scoperto. Centoquindici verifiche verdi non se ne erano accorte, perche'
     *  nessuna montava davvero il costruttore: leggevano i metadati.
     *
     *  Questa interfaccia e' il rimedio. Il costruttore ora dipende da qualcosa che
     *  possediamo NOI, e quel qualcosa un banco di prova lo puo' fingere.
     *
     *  I PEZZI NON SI COSTRUISCONO, SI CLONANO. `FonteDaModello` prende il modello di
     *  bersaglio di N.I.N.A. e ne clona le parti. Non e' un ripiego: e' meglio del
     *  `new`. Fra la 3.2 e la 3.3 il costruttore di `SmartExposure` ha cambiato numero
     *  di argomenti — cinque e sette contro otto — e `TakeExposure` ha perfino cambiato
     *  namespace. L'ho verificato compilando, non dedotto. Un clone e' fatto da
     *  N.I.N.A. con i servizi che N.I.N.A. gli ha dato, e di quelle firme non sa nulla.
     *
     *  CHE COSA NON C'E' QUI DENTRO, E PERCHE' E' LA COSA PIU' IMPORTANTE.
     *  Niente `CoolCamera`, niente `WarmCamera`, niente `FindHome`, niente parcheggio.
     *  Non perche' non si riesca a procurarli — anche se e' vero, stanno nei modelli di
     *  avvio e di chiusura, che non sono contenitori di bersaglio — ma perche' NON
     *  DEVONO STARCI. `Sequence2VM.AddTarget` mette il bersaglio in `Items[1]`, cioe'
     *  nell'area centrale, fra lo Start e l'End che appartengono a chi riprende.
     *  Raffreddare dentro un bersaglio vorrebbe dire raffreddare una volta per
     *  bersaglio; riscaldare e parcheggiare, farlo dopo ognuno. La vita della sessione
     *  e' dell'utente. Il divieto non e' affidato a un test: e' la forma di questa
     *  interfaccia, e cio' che non si puo' chiedere non si puo' aggiungere.
     *
     *  Ogni metodo puo' tornare null: un pezzo che non c'e' non e' un'eccezione, e'
     *  una notizia. Chi costruisce lo scrive negli scartati e va avanti con il resto.
     */
    public interface IFonteDiPezzi {

        /// <summary>Falso se non si puo' montare niente. Guardare qui prima di provare.</summary>
        bool Disponibile { get; }

        /// <summary>Perche' no, in una frase leggibile. Null quando si puo'.</summary>
        string? PerCheNo { get; }

        /// <summary>Un contenitore di bersaglio vuoto, pronto da riempire.</summary>
        IDeepSkyObjectContainer? Contenitore();

        /// <summary>Un blocco di ripresa: vetro, posa, ciclo, dither.</summary>
        SmartExposure? Posa();

        RunAutofocus? Autofocus();

        StartGuiding? AvvioGuida();

        /// <summary>L'innesco che sposta ogni N pose.</summary>
        DitherAfterExposures? Dither();
    }

    /*  LA FONTE CHE USA LA FABBRICA, se un giorno arrivasse.
     *
     *  Oggi non serve a niente, e sta qui lo stesso per due ragioni. La prima: se
     *  N.I.N.A. un domani componesse `ISequencerFactory` per i plugin, questa riga
     *  diventa la strada buona senza toccare il costruttore. La seconda: tenere la
     *  fabbrica dietro la stessa interfaccia dimostra che l'interfaccia e' del posto
     *  giusto — se una sola delle due fonti ci stesse comoda, sarebbe un'astrazione
     *  inventata attorno a un caso solo.
     */
    public sealed class FonteDaFabbrica : IFonteDiPezzi {

        private readonly ISequencerFactory fabbrica;

        public FonteDaFabbrica(ISequencerFactory fabbrica) =>
            this.fabbrica = fabbrica ?? throw new ArgumentNullException(nameof(fabbrica));

        public bool Disponibile => true;
        public string? PerCheNo => null;

        public IDeepSkyObjectContainer? Contenitore() => fabbrica.GetContainer<DeepSkyObjectContainer>();
        public SmartExposure? Posa() => fabbrica.GetItem<SmartExposure>();
        public RunAutofocus? Autofocus() => fabbrica.GetItem<RunAutofocus>();
        public StartGuiding? AvvioGuida() => fabbrica.GetItem<StartGuiding>();
        public DitherAfterExposures? Dither() => fabbrica.GetTrigger<DitherAfterExposures>();
    }
}
