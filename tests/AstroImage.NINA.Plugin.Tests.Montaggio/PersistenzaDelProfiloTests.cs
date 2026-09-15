using System;
using System.Reflection;
using global::NINA.Profile;
using global::NINA.Profile.Interfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace AstroImage.NINA.Plugin.Tests.Montaggio {

    /*  DOVE VIVE UN'OPZIONE DEL PLUGIN, MISURATO SULL'ACCESSOR VERO.
     *
     *  Il ponte scrive la ruota e il sito dichiarati con `PluginOptionsAccessor`, e il
     *  testo che l'utente legge dice «una volta per profilo». Che l'accessor sia per
     *  profilo lo diceva un commento, letto nei metadati; questa prova lo misura: si
     *  costruisce l'accessor di N.I.N.A. 3.2 contro un servizio dei profili finto, si
     *  scrive nel profilo A, si passa al profilo B, e si guarda che cosa si legge.
     *
     *  I due finti rispondono solo a quello che serve — il profilo attivo, le impostazioni
     *  dei plugin, gli eventi — e alzano la voce su tutto il resto: se l'accessor
     *  toccasse altro, la prova lo direbbe invece di restituire un valore innocuo.
     */
    [TestClass]
    public class PersistenzaDelProfiloTests {

        private static readonly Guid Plugin = Guid.Parse("3AC982AD-64D9-45F0-99EA-56063E94E206");

        public class Finto : DispatchProxy {
            public Func<MethodInfo, object?[]?, object?> Risposta { get; set; } = (m, a) => null;
            protected override object? Invoke(MethodInfo? m, object?[]? a) => Risposta(m!, a);
            public static T Crea<T>(Func<MethodInfo, object?[]?, object?> risposta) where T : class {
                var p = Create<T, Finto>();
                ((Finto)(object)p).Risposta = risposta;
                return p;
            }
        }

        private static IProfile Profilo(string nome) {
            var impostazioni = new PluginSettings();
            var id = Guid.NewGuid();
            return Finto.Crea<IProfile>((m, a) => m.Name switch {
                "get_PluginSettings" => impostazioni,
                "get_Name" => nome,
                "get_Id" => id,
                _ when m.Name.StartsWith("add_") || m.Name.StartsWith("remove_") => null,
                _ => throw new NotSupportedException("l'accessor ha chiesto al profilo " + m.Name),
            });
        }

        [TestMethod]
        public void LAccessor_LeggeIlProfiloAttivoAOgniChiamata_ENonQuelloDellaCostruzione() {
            var a = Profilo("A");
            var b = Profilo("B");
            IProfile attivo = a;
            var servizio = Finto.Crea<IProfileService>((m, args) => m.Name switch {
                "get_ActiveProfile" => attivo,
                _ when m.Name.StartsWith("add_") || m.Name.StartsWith("remove_") => null,
                _ => throw new NotSupportedException("l'accessor ha chiesto al servizio dei profili " + m.Name),
            });

            var accessor = new PluginOptionsAccessor(servizio, Plugin);
            accessor.SetValueString("prova", "scritto nel profilo A");

            attivo = b;
            Assert.AreEqual("", accessor.GetValueString("prova", ""),
                "passati al profilo B, il valore scritto in A non si deve leggere: l'accessor e' per profilo");
            accessor.SetValueString("prova", "scritto nel profilo B");

            attivo = a;
            Assert.AreEqual("scritto nel profilo A", accessor.GetValueString("prova", ""),
                "tornati ad A, si rilegge quello di A, e la scrittura in B non l'ha toccato");
        }

        /*  IL CONTROLLO POSITIVO DELLA PROVA SOPRA. Due profili che condividono le stesse impostazioni sono il caso
         *  «globale»: qui il valore di A si deve leggere da B. Se questa prova fosse rossa, la prova sopra non saprebbe
         *  distinguere un accessor per profilo da uno globale, e il suo verde non varrebbe niente. */
        [TestMethod]
        public void ControlloPositivo_ConLeImpostazioniCondivise_IlValorePassaDaUnProfiloAllAltro() {
            var condivise = new PluginSettings();
            IProfile Con(string nome) => Finto.Crea<IProfile>((m, a) => m.Name switch {
                "get_PluginSettings" => condivise,
                "get_Name" => nome,
                _ when m.Name.StartsWith("add_") || m.Name.StartsWith("remove_") => null,
                _ => throw new NotSupportedException("l'accessor ha chiesto al profilo " + m.Name),
            });
            var a = Con("A");
            var b = Con("B");
            IProfile attivo = a;
            var servizio = Finto.Crea<IProfileService>((m, args) => m.Name switch {
                "get_ActiveProfile" => attivo,
                _ when m.Name.StartsWith("add_") || m.Name.StartsWith("remove_") => null,
                _ => throw new NotSupportedException("l'accessor ha chiesto al servizio dei profili " + m.Name),
            });
            var accessor = new PluginOptionsAccessor(servizio, Plugin);
            accessor.SetValueString("prova", "scritto nel profilo A");
            attivo = b;
            Assert.AreEqual("scritto nel profilo A", accessor.GetValueString("prova", ""),
                "con impostazioni condivise la prova deve vedere il valore passare: e' il caso che l'altra prova esclude");
        }

        /*  L'evento con cui il pannello si accorge del cambio di profilo, letto dal tipo e
         *  non dalla memoria: se una versione di N.I.N.A. lo rinominasse, questa prova cade
         *  prima del pannello. */
        [TestMethod]
        public void IlCambioDiProfilo_HaUnEvento() {
            Assert.IsNotNull(typeof(IProfileService).GetEvent("ProfileChanged"), "IProfileService.ProfileChanged");
        }
    }
}
