using System.Reflection;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("Chatterer /L Unleashed")]
[assembly: AssemblyDescription("Add some SSTV, beeps, and nonsensical radio chatter between your crewed command pods and Mission Control")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany(Chatterer.LegalMamboJambo.Company)]
[assembly: AssemblyProduct(Chatterer.LegalMamboJambo.Product)]
[assembly: AssemblyCopyright(Chatterer.LegalMamboJambo.Copyright)]
[assembly: AssemblyTrademark(Chatterer.LegalMamboJambo.Trademark)]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("a79bd9aa-f0d5-4bd7-a5ce-a34205114836")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version 
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers 
// by using the '*' as shown below:
[assembly: AssemblyVersion(Chatterer.Version.Number)]
[assembly: AssemblyFileVersion(Chatterer.Version.Number)]
[assembly: KSPAssembly("Chatterer", Chatterer.Version.major, Chatterer.Version.minor)]
[assembly: KSPAssemblyDependency("KSPe", 2, 4)]
[assembly: KSPAssemblyDependency("KSPe.UI", 2, 4)]