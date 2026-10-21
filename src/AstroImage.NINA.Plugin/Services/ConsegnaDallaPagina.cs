using System;
using System.Collections.Generic;
using System.Linq;
using AstroImage.NINA.Plugin.Localization;
using AstroImage.NINA.Plugin.Models;
using NINA.Core.Enum;
using NINA.Core.Utility;
using NINA.Sequencer.Interfaces.Mediator;

#nullable enable

namespace AstroImage.NINA.Plugin.Services {

    /*  LA CONSEGNA CHIESTA DALLA PAGINA VERA (passo 3, 21 ottobre 2026).
     *
     *  Lo stesso montaggio di «Manda» nella pagina di prova — SequenceBuilder, i pezzi clonati dai modelli di N.I.N.A.,
     *  la rilettura di ogni valore, tutti i blocchi o nessuno —, con il modello che arriva dalla pagina invece che dalla
     *  prescrizione in mano. Due cose della pagina di prova qui non ci sono, e di proposito:
     *
     *  · I FILTRI NON SI RISOLVONO CON LA RUOTA DICHIARATA NEL PONTE. Nella pagina vera i nomi per N.I.N.A. li sceglie chi
     *    riprende, nella pagina, e il modello li porta gia'. Il controllo che conta resta: un nome che nella ruota del
     *    profilo non c'e' ferma il bersaglio intero, prima di costruire (SequenceBuilder.Costruisci).
     *  · IL PROGETTO NON SI APRE QUI. I progetti della pagina vera stanno nella pagina.
     *
     *  Ogni passo si scrive nel registro di N.I.N.A., come in «Manda»: un percorso che finisce in un programma altrui deve
     *  lasciare una traccia dove la si va a cercare.
     */
    public static class ConsegnaDallaPagina {

        private const string IO = "[AstroImage] ";

        /// <summary>I bersagli del Sequenziatore Avanzato, nell'ordine di N.I.N.A., col loro stato. Vuoto se non si leggono.</summary>
        public static IReadOnlyList<BersaglioDelSequenziatore> Bersagli(ISequenceMediator? mediatore) {
            if (mediatore is null) return new List<BersaglioDelSequenziatore>();
            try {
                return SequenceBuilder.BersagliPresenti(mediatore)
                    .Select(c => new BersaglioDelSequenziatore { Nome = SequenceBuilder.NomeDelBersaglio(c) ?? "", Stato = c.Status.ToString() })
                    .ToList();
            } catch (Exception ex) {
                Logger.Warning(IO + "page: the targets in the sequencer could not be read — " + ex.Message);
                return new List<BersaglioDelSequenziatore>();
            }
        }

        public static EsitoDellaConsegna Esegui(string? perCheNonConsegna, SequenceBuilder? costruttore,
                                                ISequenceMediator? mediatore, SequenceModel modello,
                                                int? indice, string? nome) {
            Logger.Info(IO + $"page send: «{modello.Nome}», {modello.Blocchi?.Count ?? 0} blocks" +
                        (indice is null ? ", as a new target" : $", in place of target {indice + 1} «{nome}»"));
            if (perCheNonConsegna != null || costruttore is null || mediatore is null) {
                var perche = perCheNonConsegna ?? Loc.T("Vm_SenzaMediatore");
                Logger.Warning(IO + "page send: cannot deliver — " + perche);
                return new EsitoDellaConsegna { PerCheNo = perche };
            }

            var contenitore = costruttore.Costruisci(modello, out var ricetta);
            var esito = new EsitoDellaConsegna {
                Nome = ricetta.Nome, Bersaglio = ricetta.NomeBersaglio,
                Blocchi = ricetta.Blocchi.Count, PosePerBlocco = ricetta.Blocchi.Select(b => b.Pose).ToList(),
                Note = ricetta.Note.ToList(), Scartati = ricetta.Scartati.ToList(),
            };
            if (contenitore is null) {
                esito.Blocchi = 0; esito.PosePerBlocco.Clear();
                esito.PerCheNo = ricetta.Scartati.Count > 0
                    ? string.Join(" ", ricetta.Scartati)
                    : Loc.T("Pannello_NienteDaCostruire") + ".";
                if (ricetta.BloccoMancato) Logger.Error(IO + "page send: a block could not be built, target NOT delivered — " + esito.PerCheNo);
                else Logger.Warning(IO + "page send: nothing delivered — " + esito.PerCheNo);
                return esito;
            }

            try {
                if (indice is int i) {
                    /*  UN BERSAGLIO IN RIPRESA NON SI TOCCA: la pagina lo dice nel menu, e il Ponte lo ricontrolla qui, al
                     *  momento di scrivere, perche' fra il menu e il clic la ripresa puo' essere partita. */
                    var presenti = SequenceBuilder.BersagliPresenti(mediatore);
                    if (i < presenti.Count && presenti[i].Status == SequenceEntityStatus.RUNNING) {
                        Logger.Warning(IO + $"page send: target {i + 1} «{nome}» is running, NOT replaced");
                        esito.Blocchi = 0; esito.PosePerBlocco.Clear();
                        esito.PerCheNo = Loc.F("Ospite_BersaglioInRipresa", nome);
                        return esito;
                    }
                    if (!SequenceBuilder.Sostituisci(mediatore, i, nome, contenitore, out var perCheNoSostituito)) {
                        Logger.Warning(IO + $"page send: target {i + 1} «{nome}» NOT replaced — {perCheNoSostituito}");
                        esito.Blocchi = 0; esito.PosePerBlocco.Clear();
                        esito.PerCheNo = perCheNoSostituito;
                        return esito;
                    }
                    esito.Sostituito = nome;
                    Logger.Info(IO + $"page send: target {i + 1} «{nome}» replaced in the same place");
                } else {
                    SequenceBuilder.Consegna(mediatore, contenitore);
                    Logger.Info(IO + "page send: AddAdvancedTarget called, no exception");
                }
            } catch (Exception ex) {
                Logger.Error(IO + "page send: the sequencer threw", ex);
                esito.Blocchi = 0; esito.PosePerBlocco.Clear();
                esito.PerCheNo = ex.Message;
                return esito;
            }
            esito.Fatta = true;
            Logger.Info(IO + $"page send: delivered «{esito.Bersaglio}», {esito.Blocchi} blocks, exposures per block {string.Join('/', esito.PosePerBlocco)}");
            return esito;
        }
    }
}
