using System;
using NINA.Core.Utility;
using NINA.Profile.Interfaces;

#nullable enable

namespace AstroImage.NINA.Plugin.Services {

    /*  DOVE VIVE LA DICHIARAZIONE DEI VETRI.
     *
     *  Dentro il profilo di N.I.N.A., e non accanto al DLL. La ragione e' che la
     *  dichiarazione PARLA DI UN PROFILO: il profilo con l'RC8 e la monocromatica ha
     *  una ruota, quello con l'Askar e la 2600MC ne ha un'altra, e una configurazione
     *  sola per tutti sarebbe sbagliata per almeno uno dei due. `IProfile.PluginSettings`
     *  e' esattamente quel posto — verificato leggendo i metadati di N.I.N.A., identico
     *  fra 3.2.0.9001 e 3.3.0.1057 — e `PluginOptionsAccessor` ci ritaglia dentro lo
     *  spazio del nostro GUID, senza pestare i piedi a nessun altro plugin.
     *
     *  SI SALVA UNA STRINGA, e non e' una scorciatoia: quell'interfaccia offre solo
     *  accessori scalari — stringa, intero, booleano, colore, data — e nessuna
     *  collezione. Quindi il documento viaggia serializzato, ed e' proprio per questo
     *  che porta un numero di versione: un JSON dentro una stringa non si migra da solo.
     *
     *  L'interfaccia esiste per una ragione precisa: il nucleo del ponte si prova senza
     *  N.I.N.A. — c'e' un test che lo dimostra — e un banco di prova non puo' montare
     *  un profilo vero. Di qua due metodi, di la' l'unica implementazione che tocca
     *  N.I.N.A.
     */
    public interface IMemoriaRuota {
        /// <summary>Il documento salvato, o null se non e' mai stato scritto.</summary>
        string? Leggi();
        /// <summary>Lo sostituisce. Non solleva: chi configura non deve perdere il pannello.</summary>
        bool Scrivi(string documento, out string? perCheNo);
    }

    /// <summary>La dichiarazione dentro il profilo attivo di N.I.N.A.</summary>
    public sealed class MemoriaNelProfilo : IMemoriaRuota {

        /// <summary>La chiave dentro lo spazio del nostro plugin.</summary>
        public const string Chiave = "ruotaVirtuale";

        private readonly IPluginOptionsAccessor? opzioni;

        public MemoriaNelProfilo(IPluginOptionsAccessor? opzioni) => this.opzioni = opzioni;

        public string? Leggi() {
            if (opzioni is null) return null;
            try {
                var s = opzioni.GetValueString(Chiave, string.Empty);
                return string.IsNullOrWhiteSpace(s) ? null : s;
            } catch (Exception e) {
                Logger.Warning("[AstroImage] la dichiarazione dei filtri non si e' potuta leggere: " + e.Message);
                return null;
            }
        }

        public bool Scrivi(string documento, out string? perCheNo) {
            perCheNo = null;
            if (opzioni is null) {
                perCheNo = "N.I.N.A. non ha fornito lo spazio dove salvare le impostazioni " +
                           "del plugin: la configurazione non e' stata scritta.";
                return false;
            }
            try {
                opzioni.SetValueString(Chiave, documento ?? string.Empty);
                return true;
            } catch (Exception e) {
                perCheNo = "La configurazione non si e' potuta salvare: " + e.Message;
                Logger.Error("[AstroImage] salvataggio della dichiarazione dei filtri fallito", e);
                return false;
            }
        }
    }

    /// <summary>Una memoria che non ricorda niente. Serve quando N.I.N.A. non ha dato
    /// lo spazio: il pannello resta usabile, e chi guarda scopre che non si salva
    /// provando a salvare, non dopo un riavvio.</summary>
    public sealed class MemoriaAssente : IMemoriaRuota {
        public string? Leggi() => null;
        public bool Scrivi(string documento, out string? perCheNo) {
            perCheNo = "Non c'e' dove salvare: N.I.N.A. non ha fornito le impostazioni del plugin.";
            return false;
        }
    }
}
