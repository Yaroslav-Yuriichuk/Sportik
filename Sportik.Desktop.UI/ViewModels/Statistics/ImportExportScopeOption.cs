using Sportik.Desktop.UI.Models;

namespace Sportik.Desktop.UI.ViewModels.Statistics
{
    internal sealed class ImportExportScopeOption
    {
        public string Name { get; }

        public ImportExportScope Scope { get; }

        public ImportExportScopeOption(string name, ImportExportScope scope)
        {
            Name = name;
            Scope = scope;
        }
    }
}