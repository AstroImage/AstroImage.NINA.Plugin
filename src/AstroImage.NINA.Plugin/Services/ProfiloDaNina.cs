using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AstroImage.NINA.Plugin.Models;
using NINA.Profile.Interfaces;

#nullable enable

namespace AstroImage.NINA.Plugin.Services {

    /*  IL PROFILO ATTIVO, LETTO PER LA PAGINA OSPITE.
     *
     *  Si legge com'e', senza correggere niente: un sito a 0,0, una geometria di serie del Framing Assistant, un nome
     *  di telescopio che non dice niente sono informazioni, e chi le giudica e' la pagina. Lo stesso profilo letto
     *  dall'Advanced API deve dare la stessa proposta di banco: l'ospite cambia la strada, non la risposta.
     *
     *  SI RILEGGE A OGNI DOMANDA, e non si tiene: il profilo puo' cambiare fra una domanda e l'altra, e una copia
     *  tenuta qui sarebbe quella di prima.
     *
     *  OGNI CAMPO PER CONTO SUO. Un campo che non si legge — un'impostazione che una versione di N.I.N.A. non ha, un
     *  orizzonte il cui file e' sparito — resta null e non si porta via gli altri.
     */
    public sealed class ProfiloDaNina {

        private readonly IProfileService? profili;

        public ProfiloDaNina(IProfileService? profili) => this.profili = profili;

        /// <summary>Il banco del profilo attivo, o null se N.I.N.A. non ne ha uno.</summary>
        public ProfiloPerLaPagina? Leggi() {
            var p = Protetto(() => profili?.ActiveProfile);
            if (p is null) return null;
            var a = Protetto(() => p.AstrometrySettings);
            var c = Protetto(() => p.CameraSettings);
            var t = Protetto(() => p.TelescopeSettings);
            var f = Protetto(() => p.FramingAssistantSettings);
            var r = new ProfiloPerLaPagina {
                Nome = Protetto(() => p.Name),
                Latitudine = Protetto(() => (double?)a?.Latitude),
                Longitudine = Protetto(() => (double?)a?.Longitude),
                Quota = Protetto(() => (double?)a?.Elevation),
                /*  Il nome, non il percorso: il percorso porta la cartella dell'utente di Windows. */
                FileOrizzonte = Nome(Protetto(() => a?.HorizonFilePath)),
                CameraId = Protetto(() => c?.Id),
                PixelUm = Protetto(() => (double?)c?.PixelSize),
                Matrice = Protetto(() => c?.BayerPattern.ToString()),
                Telescopio = Protetto(() => t?.Name),
                Montatura = Protetto(() => t?.MountName),
                FocaleMm = Protetto(() => (double?)t?.FocalLength),
                Rapporto = Protetto(() => (double?)t?.FocalRatio),
                LarghezzaPx = Protetto(() => (int?)f?.CameraWidth),
                AltezzaPx = Protetto(() => (int?)f?.CameraHeight),
            };
            var filtri = Protetto(() => p.FilterWheelSettings?.FilterWheelFilters?.ToList());
            if (filtri is not null)
                r.Filtri = filtri.Where(x => x is not null)
                                 .Select(x => new FiltroDelProfilo { Nome = x.Name, Posizione = x.Position })
                                 .ToList();

            /*  L'ORIZZONTE COME LO DA' L'ADVANCED API: da 0 a 360 gradi compresi, un grado per volta, 361 altezze
             *  (misurato il 21 ottobre 2026). `GetAltitude` interpola fra i punti del file come fa N.I.N.A. quando lo
             *  disegna. Un'altezza che non si legge toglie il suo azimut, non tutto l'orizzonte; sotto due punti non e'
             *  un orizzonte e resta null. */
            var h = Protetto(() => a?.Horizon);
            if (h is not null) {
                var az = new List<double>(361);
                var alt = new List<double>(361);
                for (var g = 0; g <= 360; g++) {
                    var v = Protetto(() => (double?)h.GetAltitude(g));
                    if (v is null || !double.IsFinite(v.Value)) continue;
                    az.Add(g);
                    alt.Add(v.Value);
                }
                if (az.Count >= 2) r.Orizzonte = new OrizzonteDelProfilo { Azimut = az.ToArray(), Altezza = alt.ToArray() };
            }
            return r;
        }

        private static string? Nome(string? percorso) {
            if (string.IsNullOrWhiteSpace(percorso)) return null;
            try { return Path.GetFileName(percorso.Trim()); } catch (Exception) { return null; }
        }

        private static T? Protetto<T>(Func<T?> f) { try { return f(); } catch (Exception) { return default; } }
    }
}
