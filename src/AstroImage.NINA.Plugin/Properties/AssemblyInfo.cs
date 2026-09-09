using System.Reflection;
using System.Runtime.InteropServices;

// IL MANIFEST DEL PLUGIN NON E' UN FILE: E' L'ASSEMBLY STESSO.
//
// PluginBase legge per riflessione gli attributi qui sotto e ne popola le proprieta'
// di IPluginManifest. Non esiste un manifest.json dentro il plugin — quello del
// catalogo e' un'altra cosa, e sta nel repository dei manifest di N.I.N.A.

// L'IDENTITA' PERMANENTE. N.I.N.A. usa questo GUID come chiave stabile del plugin, e il
// catalogo ufficiale ci lega tutte le versioni pubblicate. Cambiarlo dopo la prima
// distribuzione significa creare un plugin diverso agli occhi di tutti.
[assembly: Guid("3AC982AD-64D9-45F0-99EA-56063E94E206")]

// Il nome NON contiene trattini lunghi ne' caratteri fuori dall'ASCII, e la ragione e'
// pratica: la pagina delle opzioni si aggancia con una chiave di DataTemplate che deve
// combaciare con AssemblyTitle CARATTERE PER CARATTERE. Un trattino lungo scritto in un
// posto e corto nell'altro produce una pagina opzioni vuota, senza nessun errore.
[assembly: AssemblyTitle("AstroImage Strategy Bridge")]
[assembly: AssemblyDescription("Ponte fra AstroImage-Strategy e il Sequenziatore Avanzato di N.I.N.A.")]
[assembly: AssemblyCompany("Alessandro Curci")]
[assembly: AssemblyProduct("AstroImage Strategy Bridge")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Alessandro Curci")]
[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]
[assembly: ComVisible(false)]

// --- METADATI CHE IL CATALOGO DI N.I.N.A. LEGGE ---
// Le chiavi sono quelle del template ufficiale isbeorn/nina.plugin.template.
[assembly: AssemblyMetadata("Id", "3AC982AD-64D9-45F0-99EA-56063E94E206")]
[assembly: AssemblyMetadata("Name", "AstroImage Strategy Bridge")]
[assembly: AssemblyMetadata("Author", "Alessandro Curci")]
[assembly: AssemblyMetadata("Homepage", "https://github.com/AstroImage/AstroImage.NINA.Plugin")]
[assembly: AssemblyMetadata("Repository", "https://github.com/AstroImage/AstroImage.NINA.Plugin")]
[assembly: AssemblyMetadata("License", "MPL-2.0")]
[assembly: AssemblyMetadata("LicenseURL", "https://www.mozilla.org/en-US/MPL/2.0/")]
[assembly: AssemblyMetadata("ChangelogURL", "")]
[assembly: AssemblyMetadata("Tags", "AstroImage,Strategy,Sequencer,Planning")]

// La versione di N.I.N.A. sotto la quale questo plugin non va caricato. Coincide con
// quella contro cui si compila: si dichiara cio' che si e' davvero provato.
[assembly: AssemblyMetadata("MinimumApplicationVersion", "3.2.0.9001")]

[assembly: AssemblyMetadata("FeaturedImageURL", "")]
[assembly: AssemblyMetadata("ScreenshotURL", "")]
[assembly: AssemblyMetadata("AltScreenshotURL", "")]
/*  LA DESCRIZIONE E' LA PRIMA COSA CHE UN REVISORE LEGGE, e per nove commit ha detto
 *  «questa versione non fa ancora nulla» mentre il ponte consegnava bersagli su una
 *  montatura vera. Un testo falso lasciato li' diventa un testo che nessuno rilegge:
 *  va corretto quando cambia il fatto, non alla vigilia della candidatura. */
[assembly: AssemblyMetadata("LongDescription",
    "Porta dentro N.I.N.A. la prescrizione prodotta da AstroImage-Strategy.\n\n" +
    "Strategy decide che cosa riprendere — quali canali, quante ore, quale posa, quale " +
    "modo di guadagno — a partire dalla fisica del soggetto, dal sensore, dai filtri " +
    "montati e dal cielo. Questo ponte non decide niente: riceve la prescrizione gia' " +
    "presa e la costruisce nel Sequenziatore Avanzato. Non avvia mai niente: a premere " +
    "Riproduci sei tu.\n\n" +
    "TI SERVE ANCHE IL MOTORE. Da solo questo plugin non fa niente: dall'altra parte " +
    "deve esserci AstroImage-Strategy che risponde su una porta HTTP. Senza, il " +
    "pannello si apre e dice che il motore non risponde.\n\n" +
    "CHE COSA SA FARE. Chiede una prescrizione e la mostra; fa dichiarare quali vetri " +
    "hai davvero in ruota, una volta per profilo di N.I.N.A.; costruisce il bersaglio " +
    "nel Sequenziatore Avanzato con posa, numero di pose, guadagno e filtro presi dalla " +
    "prescrizione. Se un valore non arriva dove doveva, il bersaglio non si consegna e " +
    "il motivo si legge: una sequenza che non rispetta la prescrizione, guardandola, non " +
    "si distingue da una che la rispetta.\n\n" +
    "CHI L'HA FATTO. Autore e manutentore: Alessandro Curci — sue l'architettura, le " +
    "decisioni di progetto e la verifica sul campo, e suo il motore che rende utile " +
    "questo ponte. La stesura del codice e' stata fatta in larga parte da un assistente " +
    "di intelligenza artificiale (Claude, di Anthropic), su sua direzione e sotto la sua " +
    "revisione; ogni commit lo dichiara con un trailer Co-Authored-By.")]
