using System;
using System.Globalization;
using System.Reflection;

#nullable enable

namespace AstroImage.NINA.Plugin.Services {

    /*  SCRIVERE NON E' AVER SCRITTO.
     *
     *  Questo modulo rilegge dall'oggetto di N.I.N.A. il valore che il ponte gli ha
     *  appena messo, e lo confronta con quello prescritto. Serve a una cosa sola:
     *  garantire che la sequenza consegnata dica quello che la prescrizione diceva.
     *
     *  PERCHE' ESISTE. Sul banco vero il Sequenziatore mostrava «# 20» accanto a
     *  «Progresso 0/151»: l'assegnazione era andata a buon fine, il valore era un
     *  altro. N.I.N.A. 3.3 ha aggiunto a trentaquattro proprieta' del Sequenziatore la
     *  coppia `<Nome>Definition:String` + `<Nome>Expression`, e in un caso — le
     *  iterazioni — la definizione era gia' stata riempita dal clone e vinceva.
     *
     *  SI CONFRONTA IL VALORE EFFETTIVO, NON LA DEFINIZIONE, e la ragione l'ha data il
     *  campo. Sullo stesso blocco, con lo stesso codice:
     *
     *      Gain    prescritto 100  ->  Gain = 100    GainDefinition = "100"
     *      Offset  prescritto  50  ->  Offset = 50   OffsetDefinition = ""
     *
     *  Il guadagno propaga nella definizione, l'offset no — N.I.N.A. considera «non
     *  impostato» un valore che coincide con quello del profilo, e infatti lo mostra
     *  fra parentesi graffe. Sono entrambi CORRETTI: il valore che esegue e' quello
     *  intero, ed e' 50 in tutti e due i casi. Una verifica che pretendesse la
     *  definizione uguale rifiuterebbe una consegna giusta — e un rifiuto falso, a
     *  furia di ripetersi, insegna a ignorare i rifiuti.
     *
     *  SI LEGGE PER RIFLESSIONE SUL TIPO A RUNTIME, e non e' un dettaglio: e' cio' che
     *  fa leggere la proprieta' della 3.3 anche a un plugin compilato sulla 3.2. Con
     *  `se.Iterations` scritto in C# il compilatore sceglie la proprieta' della BASE, e
     *  su 3.3 quella non e' piu' la stessa cosa. Leggendo per nome si ottiene quella
     *  che l'oggetto ha davvero.
     */
    public static class Garanzia {

        /// <summary>
        /// Il valore di una proprieta' letto dal tipo che l'oggetto ha DAVVERO. Null se
        /// la proprieta' non esiste o non si e' potuta leggere — e null non e' zero.
        /// </summary>
        public static object? Leggi(object? o, string proprieta) {
            if (o is null || string.IsNullOrWhiteSpace(proprieta)) return null;
            try {
                return o.GetType().GetProperty(proprieta, BindingFlags.Public | BindingFlags.Instance)
                        ?.GetValue(o);
            } catch (Exception) { return null; }
        }

        /// <summary>
        /// Verifica un numero. <paramref name="tolleranza"/> perche' i tempi di posa sono
        /// in virgola mobile e un confronto esatto fra double e' un difetto in attesa.
        /// </summary>
        public static bool Numero(object? o, string proprieta, double atteso,
                                  string cosa, out string? perCheNo, double tolleranza = 1e-6) {
            perCheNo = null;
            var v = Leggi(o, proprieta);
            if (v is null) {
                perCheNo = $"{cosa}: non si e' potuto rileggere «{proprieta}» dopo averlo scritto, " +
                           "quindi non si puo' garantire che il valore sia arrivato.";
                return false;
            }
            double letto;
            try { letto = Convert.ToDouble(v, CultureInfo.InvariantCulture); }
            catch (Exception) {
                perCheNo = $"{cosa}: «{proprieta}» non e' un numero ({v}).";
                return false;
            }
            if (Math.Abs(letto - atteso) > tolleranza) {
                perCheNo = $"{cosa}: chiesto {Testo(atteso)}, l'oggetto dice {Testo(letto)}. " +
                           "Il valore non ha attecchito e il blocco non si consegna: una sequenza " +
                           "che dice un numero diverso dalla prescrizione, guardandola, non si " +
                           "distingue da una conforme.";
                return false;
            }
            return true;
        }

        /// <summary>
        /// Verifica un testo. Senza distinzione fra maiuscole e senza spazi ai bordi,
        /// che e' la stessa regola con cui il ponte cerca un filtro nella ruota: «Ha» e
        /// «ha » sono lo stesso vetro per chiunque tranne che per un confronto di stringhe.
        /// </summary>
        public static bool Parola(string? letto, string? atteso, string cosa, out string? perCheNo) {
            perCheNo = null;
            if (string.Equals(letto?.Trim(), atteso?.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
            perCheNo = $"{cosa}: chiesto «{atteso ?? "(niente)"}», l'oggetto dice " +
                       $"«{letto ?? "(niente)"}». Il blocco non si consegna: riprendere col vetro " +
                       "sbagliato non si recupera.";
            return false;
        }

        private static string Testo(double d) =>
            d == Math.Floor(d) && Math.Abs(d) < 1e15
                ? ((long)d).ToString(CultureInfo.InvariantCulture)
                : d.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
