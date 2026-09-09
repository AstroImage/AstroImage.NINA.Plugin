using System;
using NINA.Core.Utility;
using NINA.Profile.Interfaces;
using AstroImage.NINA.Plugin.Localization;

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
        /*  LA CHIAVE E' UN PARAMETRO perche' i documenti da ricordare per profilo
         *  sono piu' di uno: la ruota dichiarata, e i parametri del sito che N.I.N.A.
         *  non conosce. Tenerli separati invece di infilarli in un unico documento e'
         *  la differenza fra due cose che si versionano da sole e una che si rompe
         *  tutta insieme. */
        /// <summary>Il documento salvato sotto questa chiave, o null se non e' mai stato scritto.</summary>
        string? Leggi(string chiave);
        /// <summary>Lo sostituisce. Non solleva: chi configura non deve perdere il pannello.</summary>
        bool Scrivi(string chiave, string documento, out string? perCheNo);
    }

    /// <summary>La dichiarazione dentro il profilo attivo di N.I.N.A.</summary>
    public sealed class MemoriaNelProfilo : IMemoriaRuota {

        /// <summary>La chiave della ruota dichiarata, dentro lo spazio del nostro plugin.</summary>
        public const string ChiaveRuota = "ruotaVirtuale";

        /// <summary>I parametri del sito che N.I.N.A. non sa: SQM dichiarato, seeing,
        /// RMS, notti serene, altezza minima.</summary>
        public const string ChiaveSito = "sitoDichiarato";

        private readonly IPluginOptionsAccessor? opzioni;

        public MemoriaNelProfilo(IPluginOptionsAccessor? opzioni) => this.opzioni = opzioni;

        public string? Leggi(string chiave) {
            if (opzioni is null) return null;
            try {
                var s = opzioni.GetValueString(chiave, string.Empty);
                return string.IsNullOrWhiteSpace(s) ? null : s;
            } catch (Exception e) {
                Logger.Warning("[AstroImage] could not read the filter declaration: " + e.Message);
                return null;
            }
        }

        public bool Scrivi(string chiave, string documento, out string? perCheNo) {
            perCheNo = null;
            if (opzioni is null) {
                perCheNo = Loc.T("Memoria_SenzaSpazio");
                return false;
            }
            try {
                opzioni.SetValueString(chiave, documento ?? string.Empty);
                return true;
            } catch (Exception e) {
                perCheNo = Loc.F("Memoria_SalvataggioFallito", e.Message);
                Logger.Error("[AstroImage] saving the filter declaration failed", e);
                return false;
            }
        }
    }

    /// <summary>Una memoria che non ricorda niente. Serve quando N.I.N.A. non ha dato
    /// lo spazio: il pannello resta usabile, e chi guarda scopre che non si salva
    /// provando a salvare, non dopo un riavvio.</summary>
    public sealed class MemoriaAssente : IMemoriaRuota {
        public string? Leggi(string chiave) => null;
        public bool Scrivi(string chiave, string documento, out string? perCheNo) {
            perCheNo = Loc.T("Memoria_NienteDoveSalvare");
            return false;
        }
    }
}
