using System.Collections.Generic;

#nullable enable

namespace AstroImage.NINA.Plugin.Models {

    /*  IL BANCO DEL PROFILO ATTIVO, COME LO DA' L'OSPITE ALLA PAGINA.
     *
     *  Quando il pannello ospita la pagina vera di AstroImage-Strategy, la pagina chiede il profilo di N.I.N.A. con le
     *  stesse domande che farebbe all'Advanced API, e l'ospite risponde con gli stessi nomi. Questo e' quello che
     *  l'ospite legge per rispondere: i campi del banco, e niente altro del profilo.
     *
     *  NIENTE ALTRO, E C'E' UNA RAGIONE MISURATA. Il profilo intero, come lo da' l'Advanced API, porta anche la chiave
     *  personale di astrometry.net e i percorsi completi dei file, col nome dell'utente di Windows dentro (misurato il
     *  21 ottobre 2026). Una pagina web non ha motivo di vederli, e qui non entrano: il file dell'orizzonte arriva col
     *  suo nome, non col percorso.
     *
     *  Dati muti, come ogni modello: chi li legge da N.I.N.A. sta in ProfiloDaNina, chi li scrive per la pagina in
     *  OspiteDellaPagina. I nomi sono italiani; quelli di N.I.N.A. li mette chi scrive la risposta.
     */
    public sealed class ProfiloPerLaPagina {

        public string? Nome { get; set; }

        public double? Latitudine { get; set; }
        public double? Longitudine { get; set; }
        public double? Quota { get; set; }

        /// <summary>Il nome del file dell'orizzonte, senza la cartella.</summary>
        public string? FileOrizzonte { get; set; }

        public string? CameraId { get; set; }
        public double? PixelUm { get; set; }

        /// <summary>La matrice come la scrive N.I.N.A.: «None», «Auto», «RGGB»…</summary>
        public string? Matrice { get; set; }

        public string? Telescopio { get; set; }
        public string? Montatura { get; set; }
        public double? FocaleMm { get; set; }
        public double? Rapporto { get; set; }

        /// <summary>La geometria del Framing Assistant, che N.I.N.A. scrive solo quando la camera si collega.</summary>
        public int? LarghezzaPx { get; set; }
        public int? AltezzaPx { get; set; }

        public List<FiltroDelProfilo> Filtri { get; set; } = new List<FiltroDelProfilo>();

        /// <summary>Null se il profilo non ha un orizzonte: non se ne inventa uno piatto.</summary>
        public OrizzonteDelProfilo? Orizzonte { get; set; }
    }

    public sealed class FiltroDelProfilo {
        public string? Nome { get; set; }
        public int Posizione { get; set; }
    }

    /*  Due elenchi della stessa lunghezza, un'altezza per ogni azimut, come li da' l'Advanced API: da 0 a 360 gradi
     *  compresi, un grado per volta. */
    public sealed class OrizzonteDelProfilo {
        public double[] Azimut { get; set; } = new double[0];
        public double[] Altezza { get; set; } = new double[0];
    }
}
