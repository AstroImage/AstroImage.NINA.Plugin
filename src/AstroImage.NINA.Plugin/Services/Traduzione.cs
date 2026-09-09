using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AstroImage.NINA.Plugin.Models;
using AstroImage.NINA.Plugin.Localization;

#nullable enable

namespace AstroImage.NINA.Plugin.Services {

    /*  LA TRADUZIONE, CIOE' LA META' DOVE VIVRANNO I BUG.
     *
     *  Costruire una sequenza in N.I.N.A. sono due lavori diversi, e conviene che
     *  restino separati.
     *
     *  Il primo e' decidere QUALI VALORI mettere nei campi: l'ascensione retta e' in
     *  gradi o in ore, una posa che manca che cosa diventa, il -1 del guadagno si
     *  scrive o si lascia stare, un blocco senza numero di pose si costruisce lo
     *  stesso. Qui non si chiama nemmeno un metodo di N.I.N.A., e ogni errore e' un
     *  errore di traduzione: un fattore quindici, uno zero al posto di un nulla.
     *
     *  Il secondo e' prendere quei valori e riempire gli oggetti veri. E' meccanico,
     *  e richiede N.I.N.A. viva.
     *
     *  Tenerli separati ha un effetto concreto: la parte pericolosa si prova con le
     *  cinque fixture del motore nel banco che gira SENZA una sola DLL di N.I.N.A.
     *  accanto, e la parte che ne ha bisogno resta sottile abbastanza da guardarla.
     *
     *  Qui non si decide NIENTE di scientifico. Non si sceglie una posa, non si
     *  bilancia un'ora, non si guarda il cielo. Si converte, e dove il dato non c'e'
     *  si dichiara che non c'e' invece di inventarlo.
     */
    public static class Traduzione {

        /// <summary>Epoca in cui il motore esprime le coordinate: J2000, sempre.</summary>
        public const string Epoca = "J2000";

        /// <summary>
        /// Traduce il modello nei valori che N.I.N.A. vuole. Non solleva mai: cio' che
        /// non si puo' costruire finisce in <see cref="Ricetta.Scartati"/>, dove chi
        /// chiama puo' mostrarlo, invece di sparire o di diventare uno zero.
        /// </summary>
        public static Ricetta Traduci(SequenceModel? m) {
            var r = new Ricetta();
            if (m is null) { r.Scartati.Add(Loc.T("Trad_NessunModello")); return r; }

            r.Nome = NomeSequenza(m);

            /*  IL BERSAGLIO. Le coordinate sono quelle del centro SCELTO, spostamento di
             *  inquadratura gia' applicato dal motore: qui non si ricalcola niente, si
             *  copia. In gradi, come il motore le scrive, perche' N.I.N.A. sa costruire
             *  le proprie coordinate dai gradi e una conversione in meno e' un errore
             *  in meno. */
            var b = m.Bersaglio;
            if (b is null) r.Scartati.Add(Loc.T("Trad_SenzaBersaglio"));
            else {
                r.NomeBersaglio = b.Nome ?? m.Nome ?? Loc.T("Trad_SenzaNome");
                r.RaGradi = b.RaDeg;
                r.DecGradi = b.DecDeg;
                r.AngoloDiPosa = b.Rot;
                r.Spostato = b.Spostato;
            }

            /*  IL BINNING. Nullo vuol dire «non dichiarato», e operativamente si riprende
             *  senza binning: 1x1 e' l'unica lettura sensata di «non dichiarato», ed e'
             *  una scelta di traduzione, non un valore inventato dal nulla. */
            r.Binning = m.Ottica?.Bin ?? 1;
            if (m.Ottica?.Bin is null) r.Note.Add(Loc.T("Trad_BinningNonDichiarato"));

            /*  LE CAPACITA'. Se `cap` manca non si suppone niente: si costruiscono le
             *  sole riprese. Sei dichiarazioni assenti non sono sei dichiarazioni false,
             *  ma il risultato operativo e' lo stesso e almeno non si comanda un
             *  focheggiatore che non c'e'. */
            var c = m.Cap;
            if (c is null) r.Note.Add(Loc.T("Trad_BancoNonDichiarato"));
            r.Raffredda = c?.Raffredda ?? false;
            r.CambiaFiltro = c?.Ruota ?? false;
            r.Focheggia = c?.Focheggiatore ?? false;
            r.Guida = c?.Guida ?? false;
            r.TornaACasa = c?.Home ?? false;
            r.TemperaturaC = c?.TempC;
            r.MinutiFreddo = c?.MinutiFreddo;
            r.MinutiCaldo = c?.MinutiCaldo;

            /*  IL DITHERING. Esiste solo con la guida. Il contratto lo esprime come un
             *  reale perche' nella pagina si scrive in un campo che accetta qualsiasi
             *  numero; qui deve diventare un conteggio di pose, e si arrotonda al piu'
             *  vicino con un pavimento a uno. Il caso non e' teorico: e' un campo di
             *  testo, e «ogni 2,5 pose» non vuol dire niente per una camera. */
            if (r.Guida && m.Dither is not null) {
                var ogni = m.Dither.OgniPose;
                if (double.IsFinite(ogni) && ogni >= 0.5) {
                    r.DitherOgniPose = Math.Max(1, (int)Math.Round(ogni, MidpointRounding.AwayFromZero));
                    if (Math.Abs(r.DitherOgniPose.Value - ogni) > 1e-9)
                        r.Note.Add(Loc.F("Trad_DitherArrotondato", Testo(ogni), r.DitherOgniPose));
                } else {
                    r.Scartati.Add(Loc.F("Trad_DitherNonNumero", Testo(ogni)));
                }
            }

            /*  I BLOCCHI, uno per uno, e le ragioni per cui uno non si costruisce. */
            foreach (var (blocco, i) in (m.Blocchi ?? new List<Blocco>()).Select((x, i) => (x, i))) {
                var eti = Etichetta(blocco, i);

                if (blocco.Sec is null || !double.IsFinite(blocco.Sec.Value) || blocco.Sec.Value <= 0) {
                    /*  Una posa che manca non diventa zero secondi. Sarebbe l'errore piu'
                     *  silenzioso possibile: la sequenza parte, scatta, e non c'e' niente
                     *  nei file. */
                    r.Scartati.Add(Loc.F("Trad_SenzaDurata", eti));
                    continue;
                }
                if (blocco.N is null || blocco.N.Value <= 0) {
                    r.Scartati.Add(Loc.F("Trad_SenzaNumero", eti));
                    continue;
                }

                r.Blocchi.Add(new RicettaBlocco {
                    Etichetta = eti,
                    /*  Il filtro si cambia solo se c'e' una ruota E il blocco dice quale.
                     *  Un nome che nella ruota non esiste N.I.N.A. non lo segnala: mette
                     *  il primo filtro e va avanti. Il confronto col profilo e' lavoro di
                     *  un altro pezzo; qui si porta il nome cosi' com'e'. */
                    Filtro = r.CambiaFiltro ? blocco.Filtro : null,
                    Secondi = blocco.Sec.Value,
                    Pose = blocco.N.Value,
                    /*  -1 e' la sentinella di «non specificato» del motore. Scriverla
                     *  nella camera vorrebbe dire chiedere guadagno meno uno; non
                     *  scriverla vuol dire lasciare il valore del profilo, che e' cio'
                     *  che «non specificato» significa. */
                    Gain = blocco.Gain >= 0 ? blocco.Gain : (int?)null,
                    Offset = blocco.Offset >= 0 ? blocco.Offset : (int?)null,
                    Binning = r.Binning,
                    Canali = new List<string>(blocco.Canali.Where(x => x is not null).Select(x => x!)),
                });

                if (r.CambiaFiltro && string.IsNullOrWhiteSpace(blocco.Filtro))
                    r.Note.Add(Loc.F("Trad_SenzaFiltro", eti));
            }

            if (r.Blocchi.Count == 0 && r.Scartati.Count == 0)
                r.Scartati.Add(Loc.T("Trad_SenzaBlocchi"));

            /*  Le anomalie che il motore aveva gia' dichiarato viaggiano fin qui: non
             *  sono un problema del ponte, ma chi guarda la sequenza deve poterle
             *  vedere accanto a quelle del ponte. */
            foreach (var nf in m.NonFusi ?? new List<string>())
                r.Note.Add("dal motore: " + nf);

            return r;
        }

        /// <summary>
        /// Il nome della sequenza. Quello proposto dal motore se c'e'; altrimenti si
        /// compone con cio' che si sa, senza inventare una data che il modello non
        /// porta.
        /// </summary>
        public static string NomeSequenza(SequenceModel m) {
            if (!string.IsNullOrWhiteSpace(m.Nome)) return m.Nome!;
            var nome = m.Bersaglio?.Nome ?? Loc.T("Trad_SenzaNome");
            var quando = m.Quando?.Data;
            var notte = m.Notte;
            var pezzi = new List<string> { nome };
            if (notte is not null) pezzi.Add("notte " + notte.Value.ToString(CultureInfo.InvariantCulture));
            if (quando is not null) pezzi.Add(quando.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            return string.Join(" — ", pezzi);
        }

        private static string Etichetta(Blocco b, int i) {
            var canali = b.Canali is { Count: > 0 }
                ? string.Join("+", b.Canali.Where(x => !string.IsNullOrWhiteSpace(x)))
                : null;
            var nome = !string.IsNullOrWhiteSpace(canali) ? canali
                     : !string.IsNullOrWhiteSpace(b.Filtro) ? b.Filtro
                     : "blocco " + (i + 1);
            return nome!;
        }

        private static string Testo(double x) => x.ToString("0.###", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Cio' che serve a costruire la sequenza, gia' convertito. Non e' un contratto e
    /// non attraversa nessun confine: e' la lista degli argomenti che gli oggetti di
    /// N.I.N.A. vogliono, tenuta separata perche' si possa provare senza N.I.N.A.
    /// </summary>
    public sealed class Ricetta {
        public string Nome { get; set; } = "";
        public string NomeBersaglio { get; set; } = "";
        public double RaGradi { get; set; }
        public double DecGradi { get; set; }
        public double AngoloDiPosa { get; set; }
        public bool Spostato { get; set; }

        public int Binning { get; set; } = 1;
        public bool Raffredda { get; set; }
        public bool CambiaFiltro { get; set; }
        public bool Focheggia { get; set; }
        public bool Guida { get; set; }
        public bool TornaACasa { get; set; }
        public double? TemperaturaC { get; set; }
        public double? MinutiFreddo { get; set; }
        public double? MinutiCaldo { get; set; }
        public int? DitherOgniPose { get; set; }

        public List<RicettaBlocco> Blocchi { get; } = new List<RicettaBlocco>();

        /// <summary>Cio' che NON si e' costruito, e perche'. Da mostrare, non da ignorare.</summary>
        public List<string> Scartati { get; } = new List<string>();

        /// <summary>Cio' che si e' costruito ma con una scelta che va detta.</summary>
        public List<string> Note { get; } = new List<string>();

        /// <summary>Vero se c'e' abbastanza per costruire qualcosa di sensato.</summary>
        public bool Costruibile => Blocchi.Count > 0 && !string.IsNullOrWhiteSpace(NomeBersaglio);
    }

    /// <summary>Un blocco di ripresa, con i valori gia' pronti per N.I.N.A.</summary>
    public sealed class RicettaBlocco {
        public string Etichetta { get; set; } = "";
        public string? Filtro { get; set; }
        public double Secondi { get; set; }
        public int Pose { get; set; }
        public int? Gain { get; set; }
        public int? Offset { get; set; }
        public int Binning { get; set; } = 1;
        public List<string> Canali { get; set; } = new List<string>();
    }
}
