using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using global::NINA.Sequencer.Container;
using global::NINA.Sequencer.Interfaces.Mediator;
using global::NINA.Sequencer.SequenceItem;

#nullable enable

namespace AstroImage.NINA.Plugin.Tests.Montaggio {

    /*  UN MEDIATORE CHE CONTA QUANTE VOLTE GLI SI CHIEDONO I MODELLI.
     *
     *  Serve a provare una cosa sola, ed e' il difetto che ci e' costato l'ultimo giro:
     *  la fonte dei pezzi NON deve fotografare i modelli una volta e tenerseli. Quando
     *  N.I.N.A. compone i pannelli, all'avvio, il Sequenziatore non ha ancora letto
     *  niente — e una fonte nata in quel momento resta vuota per tutta la sessione,
     *  dicendo «non disponibile» a mezzanotte con i modelli sotto il naso.
     *
     *  Il conteggio delle chiamate e' la prova diretta: se sale, la fonte ha chiesto di
     *  nuovo; se resta a uno, si e' fidata di una fotografia.
     *
     *  LE FIRME SONO QUELLE DELLA 3.2, lette dall'assembly e non dedotte dalla sorgente
     *  su develop: fra le due l'interfaccia differisce — la 3.2 ha anche `Initialized` e
     *  due eventi — ed e' la stessa lezione di SmartExposure, che qui si paga in
     *  compilazione invece che a runtime.
     */
    internal sealed class MediatoreFinto : ISequenceMediator {

        public int Chiamate { get; private set; }
        public IList<IDeepSkyObjectContainer>? DaRestituire { get; set; }

        public IList<IDeepSkyObjectContainer> GetDeepSkyObjectContainerTemplates() {
            Chiamate++;
            return DaRestituire ?? new List<IDeepSkyObjectContainer>();
        }

        public bool Initialized => true;

        /*  Tutto il resto non serve a questi test, e non deve nemmeno poter essere usato
         *  per sbaglio: se un giorno il ponte chiamasse qualcos'altro sul mediatore
         *  durante un montaggio, questo banco lo direbbe alzando la voce invece di
         *  restituire un valore innocuo. */
        private static T Mai<T>([System.Runtime.CompilerServices.CallerMemberName] string chi = "") =>
            throw new NotSupportedException($"il montaggio non deve chiamare {chi} sul mediatore");

        /*  I due eventi dell'interfaccia esistono per soddisfarla e nessuno li solleva:
         *  #pragma invece di un finto uso, perche' un finto uso sarebbe una bugia
         *  scritta per far tacere un avviso. */
#pragma warning disable CS0067
        public event Func<object, EventArgs, Task>? SequenceStarting;
        public event Func<object, EventArgs, Task>? SequenceFinished;
#pragma warning restore CS0067

        public void AddAdvancedTarget(IDeepSkyObjectContainer c) => Mai<object>();
        public void AddSimpleTarget(global::NINA.Astrometry.Interfaces.IDeepSkyObject o) => Mai<object>();
        public void AddTargetToTargetList(IDeepSkyObjectContainer c) => Mai<object>();
        public void CancelAdvancedSequence() => Mai<object>();
        public IReadOnlyCollection<ISequenceItem> GetAdvancedSequencerCurrentRunningItems() => Mai<IReadOnlyCollection<ISequenceItem>>();
        public string GetAdvancedSequencerSavePath() => Mai<string>();
        public IList<IDeepSkyObjectContainer> GetAllTargetsInAdvancedSequence() => Mai<IList<IDeepSkyObjectContainer>>();
        public IList<IDeepSkyObjectContainer> GetAllTargetsInSimpleSequence() => Mai<IList<IDeepSkyObjectContainer>>();
        public bool IsAdvancedSequenceRunning() => Mai<bool>();
        public void RegisterSequenceNavigation(global::NINA.ViewModel.Sequencer.ISequenceNavigationVM v) => Mai<object>();
        public Task SaveContainer(ISequenceContainer c, string p, CancellationToken t) => Mai<Task>();
        public void SetAdvancedSequence(ISequenceRootContainer c) => Mai<object>();
        public Task StartAdvancedSequence(bool s) => Mai<Task>();
        public void SwitchToAdvancedView() => Mai<object>();
        public void SwitchToOverview() => Mai<object>();
    }
}
