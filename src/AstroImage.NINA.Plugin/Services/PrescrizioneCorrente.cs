using System;
using System.Linq;
using AstroImage.NINA.Plugin.Models;

#nullable enable

namespace AstroImage.NINA.Plugin.Services {

    /*  QUELLO CHE IL PONTE HA IN MANO, e la guardia che impedisce di mandare la cosa
     *  sbagliata.
     *
     *  PERCHE' IL MODELLO NON TORNA INDIETRO DALLA PAGINA. Sarebbe piu' semplice: la
     *  pagina ha gia' la risposta, potrebbe rispedire la notte scelta. Ma allora cio'
     *  che finisce nel Sequenziatore sarebbe cio' che la pagina dice, non cio' che il
     *  motore ha deciso — e fra i due c'e' tutta la differenza fra un ponte e un
     *  modulo da compilare. Il ponte tiene la risposta; la pagina sceglie una riga.
     *
     *  LA GUARDIA, che e' la ragione per cui questa classe esiste invece di due campi.
     *  Chiedi una prescrizione per NGC 6888, poi ne chiedi una per M 31, poi premi
     *  «manda» sulla riga di prima che e' ancora sullo schermo. Senza un
     *  identificativo che leghi la riga alla risposta, il ponte manderebbe M 31
     *  chiamandolo NGC 6888. Non e' un caso di scuola: e' il gesto normale di chi
     *  confronta due oggetti prima di decidere.
     *
     *  Ogni risposta riuscita riceve quindi un identificativo nuovo. La pagina lo
     *  riceve e lo rimanda indietro quando chiede di consegnare. Se non combacia, il
     *  ponte rifiuta e lo dice — invece di indovinare quale delle due volesse.
     */
    public sealed class PrescrizioneCorrente {

        private string? _id;
        private EsitoPrescrizione? _esito;

        /// <summary>L'identificativo della risposta in mano, o null se non ce n'e' una.</summary>
        public string? Id => _id;

        /// <summary>Quante notti porta la risposta in mano. Zero se non ce n'e' una.</summary>
        public int Notti => _esito?.Sequenze.Count ?? 0;

        /// <summary>
        /// Prende in consegna una risposta riuscita e le da' un identificativo nuovo,
        /// che ritorna. Una risposta non riuscita non si tiene: non c'e' niente da
        /// mandare, e tenerla vorrebbe dire poter consegnare l'errore di prima.
        /// </summary>
        public string? Prendi(EsitoPrescrizione? esito) {
            if (esito is null || !esito.Riuscito || esito.Sequenze.Count == 0) {
                _id = null; _esito = null; return null;
            }
            _id = Guid.NewGuid().ToString("N");
            _esito = esito;
            return _id;
        }

        /// <summary>Dimentica quello che ha in mano.</summary>
        public void Lascia() { _id = null; _esito = null; }

        /// <summary>
        /// Cerca la notte chiesta dentro la risposta identificata da
        /// <paramref name="id"/>. Torna null e riempie <paramref name="motivo"/> quando
        /// non si puo' fare, invece di scegliere qualcosa di plausibile.
        /// </summary>
        public SequenzaDiNotte? Notte(string? id, int notte, out string? codice, out string? motivo) {
            codice = null; motivo = null;

            if (_esito is null || _id is null) {
                codice = "nessuna_prescrizione";
                motivo = "Non c'e' nessuna prescrizione da mandare: chiedine una prima.";
                return null;
            }
            if (string.IsNullOrEmpty(id) || !string.Equals(id, _id, StringComparison.Ordinal)) {
                codice = "prescrizione_scaduta";
                motivo = "Quella riga viene da una prescrizione precedente. Il ponte non manda " +
                         "l'oggetto sbagliato con il nome giusto: chiedi di nuovo e riprova.";
                return null;
            }

            var s = _esito.Sequenze.FirstOrDefault(x => x.Notte == notte);
            if (s is null) {
                codice = "notte_assente";
                motivo = $"La prescrizione non ha una notte {notte}: ne ha {_esito.Sequenze.Count}.";
                return null;
            }
            if (s.Modello is null) {
                /*  Il corriere scarta gia' le notti senza modello, quindi qui non si
                 *  dovrebbe arrivare mai. Resta perche' «non si dovrebbe arrivare mai»
                 *  e' esattamente la frase che precede un riferimento nullo. */
                codice = "notte_senza_modello";
                motivo = $"La notte {notte} e' arrivata senza modello: non c'e' niente da costruire.";
                return null;
            }
            return s;
        }
    }
}
