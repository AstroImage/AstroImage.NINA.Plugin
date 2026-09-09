using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AstroImage.NINA.Plugin.Localization;

#nullable enable

namespace AstroImage.NINA.Plugin.Services {

    /*  QUALE TUO VETRO FA QUALE BANDA.
     *
     *  Il motore e N.I.N.A. parlano due vocabolari diversi, e finora non se n'era
     *  accorto nessuno perche' su un banco di casa un «L» combaciava per caso. Sul
     *  banco vero no: la prescrizione chiede «HO» e «L», in ruota ci sono
     *  LPS_P2 IDAS, ENHA, ULTIMATE, HA 7NM, V4 IDAS. Nomi commerciali contro nomi di
     *  banda: nessuna somiglianza, e nessuna regola che li possa indovinare — quale
     *  duale serva per Ha+OIII lo sa solo chi l'ha comprato.
     *
     *  IL MOTORE LA ACCETTA GIA'. `sequenceModel` fa
     *  `Object.assign({}, NINA_FILTER, o.filterNames||{})`: una mappa passata fra le
     *  opzioni sovrascrive i nomi di serie. Non c'era niente da inventare — solo da
     *  collegare. E la chiave e' il CANALE, non il filtro: `Ha+OIII`, `RGB`, `Ha`,
     *  `OIII`, `SII`, `L`, `R`, `G`, `B`.
     *
     *  Un file accanto al DLL, come per l'indirizzo, e per la stessa ragione: su un PC
     *  da campo si vede, si legge e si corregge con un editor di testo. La casa
     *  definitiva e' la pagina delle Opzioni, dove i vetri veri si sceglieranno da un
     *  elenco invece di scriverli — ma quello serve a non sbagliare a digitare, non a
     *  far funzionare la cosa.
     */
    public static class MappaFiltri {

        /// <summary>Il file, accanto al DLL, che dice quale vetro fa quale banda.</summary>
        public const string NomeFile = "filtri.json";

        /// <summary>
        /// Legge la mappa canale → nome del vetro. Vuota se il file non c'e', e vuota
        /// con un motivo se c'e' ma non si legge: una mappa sbagliata non deve impedire
        /// di chiedere una prescrizione, deve solo far dire che manca.
        /// </summary>
        public static IReadOnlyDictionary<string, string> Leggi(string? cartella, out string? nota) {
            nota = null;
            var vuota = new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(cartella)) return vuota;

            var file = Path.Combine(cartella!, NomeFile);
            if (!File.Exists(file)) return vuota;

            try {
                var letto = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file));
                if (letto is null || letto.Count == 0) {
                    nota = Loc.F("Mappa_FileVuoto", NomeFile);
                    return vuota;
                }
                /*  Le chiavi e i valori vuoti si buttano qui: una mappa che dice
                 *  «Ha+OIII vale la stringa vuota» sarebbe peggio di non averla. */
                var pulita = new Dictionary<string, string>();
                foreach (var (canale, vetro) in letto)
                    if (!string.IsNullOrWhiteSpace(canale) && !string.IsNullOrWhiteSpace(vetro))
                        pulita[canale.Trim()] = vetro.Trim();
                if (pulita.Count < letto.Count)
                    nota = Loc.F("Mappa_VociVuote", NomeFile, letto.Count - pulita.Count);
                return pulita;
            } catch (Exception e) {
                nota = Loc.F("Mappa_LetturaFallita", NomeFile, e.Message);
                return vuota;
            }
        }
    }
}
