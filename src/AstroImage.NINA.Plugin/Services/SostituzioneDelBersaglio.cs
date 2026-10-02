using System;
using System.Collections.Generic;
using AstroImage.NINA.Plugin.Localization;

#nullable enable

namespace AstroImage.NINA.Plugin.Services {

    /*  IL POSTO DI UN BERSAGLIO NEL SEQUENZIATORE: quanto basta per sostituirlo, e niente di piu'.
     *
     *  Di N.I.N.A. servono quattro gesti — dov'e', toglilo, mettine un altro li', che cosa c'e' adesso li' — e stanno
     *  dietro questa interfaccia perche' il nucleo si prova senza le sue DLL, come Garanzia e IndirizzoDelMotore.
     *  L'unica implementazione che tocca N.I.N.A. e' in SequenceBuilder. */
    public interface IPostoNellaSequenza {
        /// <summary>La posizione dell'elemento fra i fratelli, o -1 se non c'e'.</summary>
        int Indice(object elemento);
        /// <summary>Lo toglie. Falso se non c'era.</summary>
        bool Togli(object elemento);
        /// <summary>Lo mette in quella posizione.</summary>
        void Metti(int indice, object elemento);
        /// <summary>Che cosa c'e' in quella posizione adesso, o null.</summary>
        object? In(int indice);
    }

    /*  SOSTITUIRE UN BERSAGLIO, NON LA SEQUENZA (2 ottobre 2026, richiesta di chi riprende).
     *
     *  «Manda» aggiungeva sempre un bersaglio nuovo: una sera, due prescrizioni, due bersagli nel Sequenziatore. N.I.N.A.
     *  dal Framing Assistant sa anche aggiornare un bersaglio che c'e' gia', scelto da un elenco. Qui il bersaglio
     *  scelto lascia il posto a quello della prescrizione: stesso contenitore padre, stessa posizione, e lo Start e
     *  l'End della sequenza restano di chi riprende. Si sostituisce solo quello che chi riprende ha indicato, e
     *  solo se e' ancora quello che ha visto: un elenco vecchio di un minuto puo' indicare un altro bersaglio.
     *
     *  SCRIVERE NON E' AVER SCRITTO, come in Garanzia: dopo il gesto si rilegge il posto. Se il nuovo non c'e', il
     *  vecchio torna dov'era — un bersaglio sparito senza sostituto e' il guasto peggiore, e N.I.N.A. non ha un
     *  «annulla». */
    public static class SostituzioneDelBersaglio {

        /// <summary>
        /// Vero se <paramref name="indice"/> indica ancora il bersaglio di nome <paramref name="nome"/> fra quelli
        /// presenti. Il confronto e' senza maiuscole e senza spazi ai bordi.
        /// </summary>
        public static bool AncoraQuello(IReadOnlyList<string?> presenti, int indice, string? nome, out string? perCheNo) {
            perCheNo = null;
            if (string.IsNullOrWhiteSpace(nome)) {
                perCheNo = Loc.T("Manda_SostituzioneSenzaNome");
                return false;
            }
            if (presenti is null || indice < 0 || indice >= presenti.Count) {
                perCheNo = Loc.F("Manda_BersaglioCambiato", indice + 1, nome!.Trim());
                return false;
            }
            if (!string.Equals(presenti[indice]?.Trim(), nome!.Trim(), StringComparison.OrdinalIgnoreCase)) {
                perCheNo = Loc.F("Manda_BersaglioCambiato", indice + 1, nome!.Trim());
                return false;
            }
            return true;
        }

        /// <summary>
        /// Mette <paramref name="nuovo"/> al posto di <paramref name="vecchio"/>. Vero solo se, riletto, il posto ha il
        /// nuovo; altrimenti il vecchio e' rimesso dov'era e <paramref name="perCheNo"/> dice perche'.
        /// </summary>
        public static bool Sostituisci(IPostoNellaSequenza posto, object vecchio, object nuovo, out string? perCheNo) {
            perCheNo = null;
            var i = posto.Indice(vecchio);
            if (i < 0) {
                perCheNo = Loc.T("Manda_BersaglioNonTrovato");
                return false;
            }
            /*  PRIMA SI METTE IL NUOVO, POI SI TOGLIE IL VECCHIO. Al contrario, un inserimento rifiutato — una versione di
             *  N.I.N.A. diversa da quella su cui il Ponte e' compilato — lascerebbe la sequenza senza il bersaglio di
             *  prima, e rimetterlo passerebbe dallo stesso gesto che ha appena fallito. Cosi' il vecchio non si tocca
             *  finche' il nuovo non e' al suo posto. */
            try {
                posto.Metti(i, nuovo);
            } catch (Exception e) {
                Ritira(posto, nuovo);
                perCheNo = Loc.F("Manda_SostituzioneRifiutata", e.Message);
                return false;
            }
            if (!ReferenceEquals(posto.In(i), nuovo)) {
                Ritira(posto, nuovo);
                perCheNo = Loc.T("Manda_SostituzioneNonAttecchita");
                return false;
            }
            bool tolto;
            try { tolto = posto.Togli(vecchio); } catch (Exception) { tolto = false; }
            if (!tolto || posto.Indice(vecchio) >= 0) {
                Ritira(posto, nuovo);
                perCheNo = Loc.T("Manda_SostituzioneNonAttecchita");
                return false;
            }
            return true;
        }

        /*  Il nuovo, se e' finito da qualche parte, va via: meglio la sequenza di prima che una con due bersagli. */
        private static void Ritira(IPostoNellaSequenza posto, object nuovo) {
            try { if (posto.Indice(nuovo) >= 0) posto.Togli(nuovo); } catch (Exception) { }
        }
    }
}
