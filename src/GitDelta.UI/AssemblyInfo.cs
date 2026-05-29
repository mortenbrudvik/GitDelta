using System.Runtime.CompilerServices;
using System.Windows;

[assembly: ThemeInfo(
    ResourceDictionaryLocation.None,            //where theme specific resource dictionaries are located
                                                //(used if a resource is not found in the page,
                                                // or application resource dictionaries)
    ResourceDictionaryLocation.SourceAssembly   //where the generic resource dictionary is located
                                                //(used if a resource is not found in the page,
                                                // app, or any theme specific resource dictionaries)
)]

// Expose internal types to the UI unit-test project so pure helpers
// (e.g. CliHelpText) can be tested without making them public.
[assembly: InternalsVisibleTo("GitDelta.UI.UnitTests")]
