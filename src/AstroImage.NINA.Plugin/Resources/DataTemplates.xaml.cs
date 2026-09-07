using System.ComponentModel.Composition;
using System.Windows;

namespace AstroImage.NINA.Plugin.Resources {

    /// <summary>
    /// L'export MEF su <see cref="ResourceDictionary"/> e' il modo in cui N.I.N.A.
    /// scopre i DataTemplate di un plugin e li aggiunge alle risorse dell'applicazione.
    /// Senza questo, i template dichiarati nel file XAML accanto non esisterebbero per
    /// nessuno.
    /// </summary>
    [Export(typeof(ResourceDictionary))]
    public partial class DataTemplates : ResourceDictionary {

        public DataTemplates() {
            InitializeComponent();
        }
    }
}
