using System.Collections.Generic;
using AstroImage.NINA.Plugin.Services;
using global::NINA.Sequencer.Container;
using global::NINA.Sequencer.SequenceItem.Autofocus;
using global::NINA.Sequencer.SequenceItem.Guider;
using global::NINA.Sequencer.SequenceItem.Imaging;
using global::NINA.Sequencer.Trigger.Guider;

#nullable enable

namespace AstroImage.NINA.Plugin.Tests.Montaggio {

    /*  IL MAGAZZINO FINTO, e quello che si puo' e non si puo' metterci dentro.
     *
     *  I pezzi del Sequenziatore fuori da N.I.N.A. quasi non nascono, e l'ho provato
     *  uno per uno con i pacchetti NuGet in mano:
     *
     *      LoopCondition   RunAutofocus   StartGuiding      si costruiscono, e si clonano
     *      SwitchFilter    DitherAfterExposures              NullReferenceException nel costruttore
     *      SmartExposure   DeepSkyObjectContainer            NullReferenceException nel costruttore
     *
     *  Il contenitore chiede un `IProfileService` funzionante — dereferenzia
     *  `ActiveProfile.AstrometrySettings.Latitude` e ci registra due gestori di eventi
     *  deboli — e riempire il suo `InputTarget` chiama NOVAS, una libreria NATIVA che
     *  sta nella cartella di N.I.N.A. e non nel pacchetto. Portarsela dietro vorrebbe
     *  dire un percorso di questa macchina dentro il banco di prova.
     *
     *  Quindi questa fonte fornisce cio' che esiste e null per il resto — che non e'
     *  una resa: e' esattamente il caso che ci interessa. Il guasto di ieri sera non
     *  era un valore sbagliato, era un pezzo che non c'era. Un magazzino che sa essere
     *  vuoto, e a meta', e pieno per quello che puo', prova la cosa giusta.
     */
    internal sealed class FonteFinta : IFonteDiPezzi {

        public bool Disponibile { get; set; } = true;
        public string? PerCheNo { get; set; }

        /// <summary>Che cosa e' stato chiesto, nell'ordine. Un montaggio si giudica
        /// anche da cio' che NON ha chiesto.</summary>
        public List<string> Chieste { get; } = new List<string>();

        public IDeepSkyObjectContainer? DaDare { get; set; }
        public SmartExposure? PosaDaDare { get; set; }
        public RunAutofocus? AutofocusDaDare { get; set; }
        public StartGuiding? GuidaDaDare { get; set; }
        public DitherAfterExposures? DitherDaDare { get; set; }

        public IDeepSkyObjectContainer? Contenitore() { Chieste.Add(nameof(Contenitore)); return DaDare; }
        public SmartExposure? Posa() { Chieste.Add(nameof(Posa)); return PosaDaDare; }
        public RunAutofocus? Autofocus() { Chieste.Add(nameof(Autofocus)); return AutofocusDaDare; }
        public StartGuiding? AvvioGuida() { Chieste.Add(nameof(AvvioGuida)); return GuidaDaDare; }
        public DitherAfterExposures? Dither() { Chieste.Add(nameof(Dither)); return DitherDaDare; }

        /// <summary>Una fonte che non ha niente e lo dichiara.</summary>
        internal static FonteFinta Vuota(string perche = "niente in magazzino") =>
            new FonteFinta { Disponibile = false, PerCheNo = perche };

        /// <summary>Una fonte disponibile ma senza pezzi: il caso peggiore, perche'
        /// promette e non mantiene.</summary>
        internal static FonteFinta Bugiarda() => new FonteFinta { Disponibile = true };
    }
}
