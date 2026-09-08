using System;
using System.Collections.Generic;
using System.Linq;
using NINA.Sequencer.Conditions;
using NINA.Sequencer.Container;
using NINA.Sequencer.Interfaces.Mediator;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.SequenceItem.Autofocus;
using NINA.Sequencer.SequenceItem.Guider;
using NINA.Sequencer.SequenceItem.Imaging;
using NINA.Sequencer.Trigger;
using NINA.Sequencer.Trigger.Guider;

#nullable enable

namespace AstroImage.NINA.Plugin.Services {

    /*  I PEZZI SI PRENDONO DAL MODELLO DI N.I.N.A. E SI CLONANO.
     *
     *  `ISequenceMediator.GetDeepSkyObjectContainerTemplates()` restituisce i modelli
     *  di bersaglio: quelli scritti dall'utente e quelli di serie, che N.I.N.A.
     *  distribuisce in `NINA/Sequencer/Examples`. Fra questi c'e'
     *  `Basic Sequence Target.template.json`, 19 KB, e dentro ci sono tutti i pezzi che
     *  servono: un `SmartExposure` con il suo `SwitchFilter`, `TakeExposure`,
     *  `LoopCondition` e `DitherAfterExposures`; `RunAutofocus`; `StartGuiding`; il
     *  trigger di dither. Quindi il magazzino non e' vuoto nemmeno per chi non ha mai
     *  salvato un modello suo.
     *
     *  NON SI CERCA PER NOME. I nomi si traducono, si rinominano, si cancellano: la
     *  forma no. Si prende il primo modello che CONTENGA un `SmartExposure`, perche' e'
     *  quello il pezzo che non si puo' fabbricare in nessun altro modo.
     *
     *  Il clone e' profondo e staccato, e non e' una speranza: `DeepSkyObjectContainer.
     *  Clone()` clona figli, condizioni e trigger uno per uno, costruisce un
     *  `InputTarget` nuovo dal profilo attivo e riaggancia tutto al nuovo genitore con
     *  `AttachNewParent`. L'ho letto nella sorgente e provato sugli oggetti che si
     *  riescono a costruire fuori da N.I.N.A. — `LoopCondition`, `RunAutofocus`,
     *  `StartGuiding` — e ognuno ha restituito un'istanza distinta.
     */
    public sealed class FonteDaModello : IFonteDiPezzi {

        private readonly IDeepSkyObjectContainer? modello;

        public bool Disponibile => modello is not null;
        public string? PerCheNo { get; }

        /// <summary>Il nome del modello scelto, da mostrare: chi guarda deve sapere da
        /// dove escono i pezzi che si trova in sequenza.</summary>
        public string? NomeModello => modello?.Name;

        public FonteDaModello(ISequenceMediator? mediatore) {
            if (mediatore is null) {
                PerCheNo = "N.I.N.A. non ha fornito il mediatore delle sequenze.";
                return;
            }

            IList<IDeepSkyObjectContainer>? modelli = null;
            try { modelli = mediatore.GetDeepSkyObjectContainerTemplates(); }
            catch (Exception e) {
                PerCheNo = "N.I.N.A. non ha saputo elencare i modelli di bersaglio: " + e.Message;
                return;
            }

            if (modelli is null || modelli.Count == 0) {
                PerCheNo = "Non c'e' nessun modello di bersaglio. Di solito ce n'e' almeno uno " +
                           "di serie: se manca anche quello, salvane uno nel Sequenziatore Avanzato.";
                return;
            }

            modello = modelli.FirstOrDefault(m => Dentro<SmartExposure>(m) is not null);
            if (modello is null) {
                PerCheNo = $"Nessuno dei {modelli.Count} modelli di bersaglio contiene una ripresa " +
                           "da cui copiare la posa. Aggiungi uno Smart Exposure a un modello.";
            }
        }

        /*  Il contenitore si clona e si SVUOTA: del modello ci interessa la forma, non
         *  il contenuto: le sue riprese sono quelle di qualcun altro, e lasciarcele
         *  vorrebbe dire consegnare pose che nessuno ha chiesto. */
        public IDeepSkyObjectContainer? Contenitore() {
            if (modello?.Clone() is not IDeepSkyObjectContainer c) return null;
            foreach (var i in c.Items.ToList()) c.Remove(i);
            /*  Trigger e condizioni non stanno su IDeepSkyObjectContainer: li espongono
             *  ITriggerable e IConditionable, che il contenitore concreto implementa. */
            if (c is ITriggerable tr) foreach (var x in tr.Triggers.ToList()) c.Remove(x);
            if (c is IConditionable co) foreach (var x in co.Conditions.ToList()) c.Remove(x);
            return c;
        }

        public SmartExposure? Posa() => Clona(Dentro<SmartExposure>(modello));
        public RunAutofocus? Autofocus() => Clona(Dentro<RunAutofocus>(modello));
        public StartGuiding? AvvioGuida() => Clona(Dentro<StartGuiding>(modello));

        /*  Il dither e' un innesco, non un'istruzione: si cerca fra i trigger. */
        public DitherAfterExposures? Dither() {
            var t = (modello as ITriggerable)?.Triggers?.OfType<DitherAfterExposures>().FirstOrDefault();
            return Clona(t);
        }

        private static T? Clona<T>(T? originale) where T : class =>
            originale is ICloneable c ? c.Clone() as T : null;

        /*  Cerca in profondita': i pezzi di un modello stanno spesso dentro contenitori
         *  annidati, e fermarsi al primo livello vorrebbe dire non trovare quasi niente. */
        private static T? Dentro<T>(ISequenceContainer? dove) where T : class {
            if (dove?.Items is null) return null;
            foreach (var i in dove.Items) {
                if (i is T trovato) return trovato;
                if (i is ISequenceContainer sotto) {
                    var giu = Dentro<T>(sotto);
                    if (giu is not null) return giu;
                }
            }
            return null;
        }
    }
}
