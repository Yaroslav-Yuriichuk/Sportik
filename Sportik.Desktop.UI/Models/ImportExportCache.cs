namespace Sportik.Desktop.UI.Models
{
    internal sealed class ImportExportCache
    {
        public ImportExportScope LastImportScope { get; set; }

        public string LastImportGoogleSheetUrlOrId { get; set; }

        public string LastImportExercisesSheetName { get; set; }

        public string LastImportSetsSheetName { get; set; }

        public ImportExportScope LastExportScope { get; set; }

        public string LastExportGoogleSheetUrlOrId { get; set; }

        public string LastExportExercisesSheetName { get; set; }

        public string LastExportSetsSheetName { get; set; }
    }
}