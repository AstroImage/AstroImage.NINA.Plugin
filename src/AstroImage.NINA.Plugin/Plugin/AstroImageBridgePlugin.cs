using AstroImage.NINA.Plugin.Services;
using NINA.Core.Utility;
using NINA.Plugin;
using NINA.Plugin.Interfaces;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace AstroImage.NINA.Plugin.Plugin {

    /// <summary>
    /// L'ingresso del plugin, e per ora non fa nient'altro che esistere.
    ///
    /// <para>
    /// N.I.N.A. scopre i plugin con MEF: cerca le classi esportate come
    /// <see cref="IPluginManifest"/> e ne legge i metadati. <see cref="PluginBase"/> li
    /// prende per riflessione dagli attributi di assembly — nome, autore, versione,
    /// licenza, versione minima dell'applicazione — quindi il manifest non e' un file
    /// ma l'assembly stesso, e sta in Properties/AssemblyInfo.cs.
    /// </para>
    ///
    /// <para>
    /// IL CONFINE, che e' la ragione per cui questo progetto esiste separato.
    /// Qui dentro non entrera' mai il motore di AstroImage-Strategy: niente fotometria,
    /// niente scelta dei canali, niente allocazione delle ore, niente strategie di posa,
    /// niente modello del cielo, niente catalogo. Il ponte riceve una prescrizione
    /// gia' presa e la costruisce nel Sequenziatore Avanzato. La prova che il confine
    /// e' nel posto giusto: chi legge questo sorgente impara come si parla a N.I.N.A.,
    /// che e' gia' pubblico, e nulla su come si decide che cosa riprendere.
    /// </para>
    ///
    /// <para>
    /// Initialize e Teardown girano all'avvio e alla chiusura di N.I.N.A. Restano vuoti
    /// finche' non c'e' niente da avviare: un plugin che fa lavoro nel costruttore o
    /// nell'Initialize rallenta l'avvio dell'applicazione di chiunque lo installi.
    /// </para>
    /// </summary>
    [Export(typeof(IPluginManifest))]
    public class AstroImageBridgePlugin : PluginBase {

        /*  LE IMPOSTAZIONI SONO IL DATACONTEXT DELLA PAGINA OPZIONI.
         *
         *  N.I.N.A. cerca un DataTemplate con chiave «<AssemblyTitle>_Options» e ci
         *  mette dentro come DataContext QUESTA istanza del plugin; il template poi
         *  scende su questa proprieta'. E' lo stesso aggancio di AdaptiveAgentForPHD2,
         *  ed e' l'unico modo di avere una pagina opzioni che scrive davvero qualcosa.
         */
        public ImpostazioniPonte Impostazioni { get; }

        /// <summary>
        /// Il costruttore e' marcato per MEF: e' la firma che N.I.N.A. usa per
        /// costruire il plugin, e i servizi che serviranno — il profilo, il mediatore
        /// delle sequenze — si aggiungeranno qui come parametri.
        ///
        /// <para>
        /// Leggere le impostazioni e' l'unico lavoro che si fa qui, ed e' un file di
        /// poche decine di byte: la lingua deve valere PRIMA che si apra qualunque
        /// cosa, o il pannello nascerebbe nella lingua sbagliata e la cambierebbe
        /// sotto gli occhi. Carica applica gia' la lingua a Loc.
        /// </para>
        /// </summary>
        [ImportingConstructor]
        public AstroImageBridgePlugin() {
            Impostazioni = ImpostazioniPonte.Carica();
            /*  Il guasto si scrive qui e non dentro ImpostazioniPonte, che di N.I.N.A.
             *  non sa niente apposta — e' la stessa regola di MemoriaRuota. */
            if (Impostazioni.Nota is not null) {
                Logger.Warning("[AstroImage] settings: " + Impostazioni.Nota);
            }
        }

        public override Task Initialize() {
            return Task.CompletedTask;
        }

        public override Task Teardown() {
            return Task.CompletedTask;
        }
    }
}
