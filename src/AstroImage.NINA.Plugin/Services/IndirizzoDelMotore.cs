using System;
using System.Linq;

#nullable enable

namespace AstroImage.NINA.Plugin.Services {

    /*  DOVE STA IL MOTORE, E CHI LO DICE.
     *
     *  Tre strade, in quest'ordine: quello scritto nelle Opzioni del plugin, quello scritto nel file accanto al DLL, e
     *  l'indirizzo di serie. La prima e' la casa definitiva — chi installa il plugin non sa dove sia la cartella dei
     *  plugin, e non deve saperlo; la seconda resta per i PC gia' configurati cosi', e perche' su un mini PC in campo un
     *  file di testo si corregge anche senza aprire le Opzioni.
     *
     *  QUI NON SI NOMINA N.I.N.A., come in Loc e in ImpostazioniPonte, e per la stessa ragione: le prove girano senza le
     *  sue DLL. Una funzione pura, che dice anche DA DOVE viene la scelta — quella frase finisce nel registro di
     *  N.I.N.A., e il giorno che il pannello guarda dove non ci si aspetta e' l'unica cosa che lo spiega.
     *
     *  Quello che non e' un indirizzo http non vince in silenzio: si dice che cosa c'era scritto e si passa alla strada
     *  dopo. Un errore di battitura che manda il ponte all'indirizzo di serie senza dirlo costa un pomeriggio.
     */
    public static class IndirizzoDelMotore {

        /// <summary>Dove sta il motore quando nessuno dice altrimenti.</summary>
        public const string DiSerie = "http://127.0.0.1:8791/";

        /// <summary>Il file, accanto al DLL, in cui si puo' scrivere un altro indirizzo.</summary>
        public const string FileIndirizzo = "strategy.url";

        /// <summary>
        /// La radice da interrogare, e in <paramref name="da"/> da dove viene — in inglese, perche' finisce nel
        /// registro di N.I.N.A., che e' in inglese.
        /// </summary>
        public static string Scegli(string? dalleOpzioni, string? dalFile, out string da) {
            if (Prova(dalleOpzioni, out var radice, out var scritto)) { da = "options"; return radice!; }
            var notaOpzioni = scritto is null ? null : $"options say «{scritto}», which is not an http address: ignored";

            if (Prova(dalFile, out radice, out scritto)) {
                da = notaOpzioni is null ? FileIndirizzo : notaOpzioni + "; " + FileIndirizzo;
                return radice!;
            }
            var notaFile = scritto is null ? null : $"file {FileIndirizzo} says «{scritto}», which is not an http address: ignored";

            da = string.Join("; ", new[] { notaOpzioni, notaFile, "built-in default" }.Where(x => x is not null));
            return DiSerie;
        }

        /// <summary>
        /// Vero se <paramref name="scritto"/> e' un indirizzo http, e allora <paramref name="radice"/> e' quello con la
        /// barra finale — che non e' un dettaglio: «…:8791» + «v1/salute» finirebbe al posto giusto solo per caso.
        /// Falso se non c'e' niente (<paramref name="testo"/> null) o se c'e' qualcosa che non e' un indirizzo
        /// (<paramref name="testo"/> e' quello che c'era scritto, da riportare a chi legge).
        /// </summary>
        public static bool Prova(string? scritto, out string? radice, out string? testo) {
            radice = null;
            testo = null;
            var s = (scritto ?? string.Empty).Trim();
            if (s.Length == 0) { return false; }
            var conBarra = s.EndsWith("/") ? s : s + "/";
            if (!Uri.TryCreate(conBarra, UriKind.Absolute, out var u)
                    || (u.Scheme != Uri.UriSchemeHttp && u.Scheme != Uri.UriSchemeHttps)) {
                testo = s;
                return false;
            }
            radice = u.ToString();
            return true;
        }
    }
}
