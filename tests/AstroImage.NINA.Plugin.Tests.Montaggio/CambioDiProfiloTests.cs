using System;
using System.Collections.Generic;
using System.Reflection;
using AstroImage.NINA.Plugin.Localization;
using AstroImage.NINA.Plugin.Models;
using AstroImage.NINA.Plugin.Services;
using AstroImage.NINA.Plugin.ViewModels;
using global::NINA.Profile;
using global::NINA.Profile.Interfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace AstroImage.NINA.Plugin.Tests.Montaggio {

    /*  IL PANNELLO VERO SENTE IL CAMBIO DI PROFILO.
     *
     *  La ricarica delle dichiarazioni e il ritiro della prescrizione si provano anche
     *  senza N.I.N.A. (DichiarazioniDelProfiloTests), ma quella prova resta verde se il
     *  pannello smette di ascoltare l'evento: prova la correzione, non che la correzione
     *  sia collegata. Questa costruisce `PannelloStrategyVM` su un servizio dei profili
     *  finto che si segna chi si iscrive a `ProfileChanged`, cambia il profilo attivo,
     *  solleva l'evento come farebbe N.I.N.A., e guarda il pannello. Tolta l'iscrizione
     *  dal costruttore, cade.
     */
    [TestClass]
    public class CambioDiProfiloTests {

        private static readonly Guid Plugin = Guid.Parse(PannelloStrategyVM.IdentitaPlugin);

        private static object? DiSerie(Type t) => t.IsValueType && t != typeof(void) ? Activator.CreateInstance(t) : null;

        private static IProfile Profilo(string nome) {
            var impostazioni = new PluginSettings();
            var id = Guid.NewGuid();
            return PersistenzaDelProfiloTests.Finto.Crea<IProfile>((m, a) => m.Name switch {
                "get_PluginSettings" => impostazioni,
                "get_Name" => nome,
                "get_Id" => id,
                _ => DiSerie(m.ReturnType),
            });
        }

        private static EsitoPrescrizione EsitoConUnaNotte() {
            var seq = new List<SequenzaDiNotte> { new SequenzaDiNotte { Notte = 1, Modello = new SequenceModel() } };
            return typeof(EsitoPrescrizione)
                .GetMethod("Riuscita", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, new object?[] { 200, "{}", seq, 0, "1", 1.0 }) as EsitoPrescrizione
                ?? throw new InvalidOperationException("esito non costruito");
        }

        [TestMethod]
        public void IlPannello_SulCambioDiProfilo_RileggeLeDichiarazioni_ERitiraLaPrescrizione() {
            var a = Profilo("A");
            var b = Profilo("B");
            IProfile attivo = a;
            var gestori = new List<EventHandler>();
            var servizio = PersistenzaDelProfiloTests.Finto.Crea<IProfileService>((m, args) => {
                if (m.Name == "get_ActiveProfile") return attivo;
                if (m.Name == "add_ProfileChanged") { gestori.Add((EventHandler)args![0]!); return null; }
                if (m.Name == "remove_ProfileChanged") { gestori.Remove((EventHandler)args![0]!); return null; }
                return DiSerie(m.ReturnType);
            });

            var vm = new PannelloStrategyVM(servizio, null!, null!, null!, null!, null!, null!, null!, null!);

            var ruotaA = new RuotaVirtuale { Vetri = { new VoceRuota { Nina = "ULTIMATE", Motore = "lult" } } };
            Assert.IsTrue(vm.SalvaDichiarazione(ruotaA, out var perCheNo), perCheNo);
            Assert.IsTrue(vm.SalvaSito(new SitoDichiarato { Sqm = 20.8 }, out perCheNo), perCheNo);
            var id = vm.InMano.Prendi(EsitoConUnaNotte());
            Assert.IsNotNull(id, "la prescrizione del profilo A e' in mano");

            attivo = b;
            /*  Solo i gestori del pannello: quelli della classe base di N.I.N.A. non sono l'oggetto della prova, e un
             *  profilo finto non ha tutto quello che leggerebbero. */
            var delPannello = gestori.FindAll(g => g.Method.DeclaringType == typeof(PannelloStrategyVM));
            Assert.AreEqual(1, delPannello.Count, "il pannello si iscrive a ProfileChanged, una volta");
            foreach (var g in delPannello) g(servizio, EventArgs.Empty);

            Assert.AreEqual(0, DichiarazioneRuota.IdDichiarati(vm.Dichiarazione).Count,
                "la ruota del profilo A non si eredita nel profilo B");
            Assert.IsNull(vm.SitoScritto.Sqm, "il sito del profilo A non si eredita nel profilo B");
            Assert.IsNull(vm.InMano.Notte(id, 1, out var codice, out var motivo), "la prescrizione di A non si consegna");
            Assert.AreEqual("prescrizione_ritirata", codice, motivo);
            Assert.AreEqual(1, vm.CambiDiProfilo, "il cambio si conta, perche' la vista lo dica alla pagina");

            attivo = a;
            foreach (var g in delPannello) g(servizio, EventArgs.Empty);
            CollectionAssert.AreEqual(new[] { "lult" }, new List<string>(DichiarazioneRuota.IdDichiarati(vm.Dichiarazione)),
                "tornati ad A, si rilegge quello che A ha");
        }

        private static PannelloStrategyVM PannelloSuUnProfilo() {
            var a = Profilo("A");
            var servizio = PersistenzaDelProfiloTests.Finto.Crea<IProfileService>((m, args) =>
                m.Name == "get_ActiveProfile" ? a : DiSerie(m.ReturnType));
            return new PannelloStrategyVM(servizio, null!, null!, null!, null!, null!, null!, null!, null!);
        }

        /*  IL RITIRO GENERALIZZATO (regia, 16 settembre 2026). Il profilo non e' la sola cosa su cui la prescrizione e'
         *  calcolata: anche la ruota, il banco e il sito dichiarati. Salvarne uno diverso ritira la prescrizione in mano, e
         *  il perche' e' quello del salvataggio, non del profilo. Salvare la stessa dichiarazione non tocca niente. */
        [TestMethod]
        public void IlPannello_SalvandoRuotaBancoOSitoDiversi_RitiraLaPrescrizione_ColSuoPerche() {
            var profilo = Loc.T("Presc_RitirataPerProfilo");
            var motivi = new List<string>();
            var banco = DichiarazioneBanco.DalMessaggio(
                System.Text.Json.Nodes.JsonNode.Parse("{\"corpo\":{\"banco\":{\"tel.apertura_mm\":80}}}"), out var malformato);
            Assert.IsNotNull(banco, malformato);
            var salvataggi = new (string Nome, Func<PannelloStrategyVM, bool> Salva)[] {
                ("ruota", vm => vm.SalvaDichiarazione(new RuotaVirtuale { Vetri = { new VoceRuota { Nina = "ULTIMATE", Motore = "lult" } } }, out _)),
                ("banco", vm => vm.SalvaBanco(banco!, out _)),
                ("sito", vm => vm.SalvaSito(new SitoDichiarato { Sqm = 20.5 }, out _)),
            };
            foreach (var (nome, salva) in salvataggi) {
                var vm = PannelloSuUnProfilo();
                var id = vm.InMano.Prendi(EsitoConUnaNotte());
                Assert.IsTrue(salva(vm), nome + ": il salvataggio va a terra");
                Assert.IsNull(vm.InMano.Notte(id, 1, out var codice, out var motivo), nome + ": la prescrizione di prima non si consegna");
                Assert.AreEqual("prescrizione_ritirata", codice, nome + ": " + motivo);
                Assert.AreNotEqual(profilo, motivo, nome + ": il perche' e' il salvataggio, non il profilo");
                motivi.Add(motivo!);

                /*  la stessa dichiarazione, di nuovo: una prescrizione nuova resta consegnabile */
                var nuovo = vm.InMano.Prendi(EsitoConUnaNotte());
                Assert.IsTrue(salva(vm), nome + ": il secondo salvataggio va a terra");
                Assert.IsNotNull(vm.InMano.Notte(nuovo, 1, out var c2, out var m2), nome + ": riscrivere la stessa dichiarazione non ritira — " + c2 + " " + m2);
            }
            Assert.AreEqual(3, new HashSet<string>(motivi).Count, "tre salvataggi, tre perche': " + string.Join(" | ", motivi));
        }

        /*  LA CAMERA COLLEGATA O SCOLLEGATA RITIRA LA PRESCRIZIONE (regia, 16 settembre 2026). La camera entra nel banco —
         *  la geometria dal driver, la voce riconosciuta —: collegarne o scollegarne una cambia il banco anche
         *  senza salvare, e la prescrizione in mano valeva per quello di prima. Il pannello ascolta gli eventi del mediatore
         *  della camera; senza una prescrizione in mano non ritira niente e non disturba la pagina. */
        private static (PannelloStrategyVM Vm, List<Delegate> Gestori) PannelloConCamera() {
            var a = Profilo("A");
            var servizio = PersistenzaDelProfiloTests.Finto.Crea<IProfileService>((m, args) =>
                m.Name == "get_ActiveProfile" ? a : DiSerie(m.ReturnType));
            var gestori = new List<Delegate>();
            var camera = PersistenzaDelProfiloTests.Finto.Crea<global::NINA.Equipment.Interfaces.Mediator.ICameraMediator>((m, args) => {
                if (m.Name == "add_Connected" || m.Name == "add_Disconnected") { gestori.Add((Delegate)args![0]!); return null; }
                if (m.Name == "remove_Connected" || m.Name == "remove_Disconnected") { gestori.Remove((Delegate)args![0]!); return null; }
                return DiSerie(m.ReturnType);
            });
            return (new PannelloStrategyVM(servizio, null!, null!, null!, camera, null!, null!, null!, null!), gestori);
        }

        [TestMethod]
        public void IlPannello_SulCambioDellaCamera_RitiraLaPrescrizione() {
            var (vm, gestori) = PannelloConCamera();
            var delPannello = gestori.FindAll(g => g.Method.DeclaringType == typeof(PannelloStrategyVM));
            Assert.AreEqual(2, delPannello.Count, "il pannello ascolta la camera che si collega e quella che si scollega");
            var id = vm.InMano.Prendi(EsitoConUnaNotte());
            var prima = CambiDiCamera(vm);
            foreach (var g in delPannello) {
                var r = g.DynamicInvoke(null, EventArgs.Empty);
                if (r is System.Threading.Tasks.Task t) t.GetAwaiter().GetResult();
            }
            Assert.IsNull(vm.InMano.Notte(id, 1, out var codice, out var motivo), "la prescrizione dell'altro banco non si consegna");
            Assert.AreEqual("prescrizione_ritirata", codice, motivo);
            Assert.AreEqual(Loc.T("Presc_RitirataPerCamera"), motivo, "il perche' e' la camera");
            Assert.AreEqual(prima + 1, CambiDiCamera(vm), "il ritiro si conta, perche' la vista lo dica alla pagina");
        }

        [TestMethod]
        public void IlPannello_SenzaPrescrizione_CollegareLaCameraNonRitiraNiente() {
            var (vm, gestori) = PannelloConCamera();
            foreach (var g in gestori.FindAll(x => x.Method.DeclaringType == typeof(PannelloStrategyVM))) {
                var r = g.DynamicInvoke(null, EventArgs.Empty);
                if (r is System.Threading.Tasks.Task t) t.GetAwaiter().GetResult();
            }
            Assert.IsNull(vm.InMano.Notte(null, 1, out var codice, out _));
            Assert.AreEqual("nessuna_prescrizione", codice);
            Assert.AreEqual(0, CambiDiCamera(vm), "senza niente da ritirare la pagina non si disturba");
        }

        private static int CambiDiCamera(PannelloStrategyVM vm) =>
            (int?)typeof(PannelloStrategyVM).GetProperty("CambiDiCamera")?.GetValue(vm) ?? -1;

        /*  Senza una prescrizione in mano un salvataggio non inventa un ritiro: chi preme «manda» sente «chiedine una». */
        [TestMethod]
        public void IlPannello_SenzaUnaPrescrizioneInMano_UnSalvataggioNonRitiraNiente() {
            var vm = PannelloSuUnProfilo();
            Assert.IsTrue(vm.SalvaSito(new SitoDichiarato { Sqm = 20.5 }, out _));
            Assert.IsNull(vm.InMano.Notte(null, 1, out var codice, out _));
            Assert.AreEqual("nessuna_prescrizione", codice);
        }
    }
}
