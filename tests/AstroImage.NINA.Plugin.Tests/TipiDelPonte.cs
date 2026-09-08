using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AstroImage.NINA.Plugin.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace AstroImage.NINA.Plugin.Tests {

    /*  ENUMERARE I TIPI DEL PONTE, IN UN POSTO SOLO.
     *
     *  Questi test girano SENZA le DLL di N.I.N.A. accanto, ed e' voluto: e' la
     *  dimostrazione che i contratti non ne hanno bisogno. La conseguenza e' che
     *  `Assembly.GetTypes()` SOLLEVA, perche' la classe del plugin eredita da
     *  PluginBase e quel tipo non si carica.
     *
     *  Chi lo scopre la prima volta e' tentato di scrivere un try/catch sul posto. Ne
     *  sono nati due, e sarebbero diventati quattro: sta qui, una volta, con dentro la
     *  guardia che il primo try/catch aveva dimenticato.
     */
    internal static class TipiDelPonte {

        /// <summary>
        /// Quanti tipi di questo assembly possono legittimamente NON caricarsi senza
        /// N.I.N.A. Oggi uno: la classe del plugin, che eredita da PluginBase.
        ///
        /// <para>
        /// Il numero e' una guardia, non una costante di comodo. Un tipo che nomina
        /// N.I.N.A. non si carica, quindi arriva nullo, quindi sparisce prima di essere
        /// esaminato: senza questo conteggio i controlli sulla purezza resterebbero
        /// verdi proprio sulla regressione per cui esistono. Se sale, o qualcuno ha
        /// aggiunto un tipo che eredita da N.I.N.A. — e allora va dichiarato qui — o un
        /// contratto ha smesso di essere indipendente.
        /// </para>
        ///
        /// <para>
        /// Nota: un tipo che usa tipi di N.I.N.A. solo nelle FIRME dei suoi membri si
        /// carica benissimo — il caricamento risolve la classe base e le interfacce,
        /// non le firme. LettoreSetup e SequenceBuilder infatti non contano.
        /// </para>
        /// </summary>
        private const int NonCaricabiliAttesi = 1;

        internal static Type[] Caricati() {
            var asm = typeof(SequenceModel).Assembly;
            Type?[] tutti;
            try { tutti = asm.GetTypes(); }
            catch (ReflectionTypeLoadException e) {
                Assert.AreEqual(NonCaricabiliAttesi, e.Types.Count(t => t is null),
                    "il numero di tipi che non si caricano senza N.I.N.A. e' cambiato. " +
                    "Se hai aggiunto un tipo che eredita da N.I.N.A., dichiaralo in " +
                    "TipiDelPonte.NonCaricabiliAttesi; altrimenti un contratto ha smesso " +
                    "di essere indipendente:\n  " +
                    string.Join("\n  ", e.LoaderExceptions.Select(x => x?.Message)));
                tutti = e.Types;
            }
            return tutti.Where(t => t is not null).Select(t => t!).ToArray();
        }

        /// <summary>I tipi di un namespace esatto: la forma di un contratto.</summary>
        internal static Type[] Nel(string spazio) =>
            Caricati().Where(t => t.Namespace == spazio).ToArray();

        /// <summary>Un namespace e tutto cio' che ci sta sotto: serve alle guardie sulla purezza.</summary>
        internal static Type[] Sotto(string spazio) =>
            Caricati().Where(t => t.Namespace == spazio ||
                                  (t.Namespace != null &&
                                   t.Namespace.StartsWith(spazio + ".", StringComparison.Ordinal)))
                      .ToArray();
    }
}
