using System;
using System.Collections.Generic;
using System.Reflection;
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
    }
}
