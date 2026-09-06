using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Sportik.Desktop.Core.Common;
using Sportik.Desktop.Core.Common.Import;
using Sportik.Desktop.Core.Extensions;
using Sportik.Desktop.Core.Models;
using Sportik.Desktop.Core.Services.Interfaces;
using Sportik.Desktop.UI.Models;

namespace Sportik.Desktop.UI.ViewModels.Statistics
{
    internal sealed class ImportViewModel : ViewModel, IDisposable
    {
        private bool _isOpen;

        public bool IsOpen
        {
            get => _isOpen;
            private set => SetField(ref _isOpen, value);
        }

        private ObservableCollection<ImportExportScopeOption> _scopeOptions;

        public ObservableCollection<ImportExportScopeOption> ScopeOptions
        {
            get => _scopeOptions;
            private set
            {
                if (SetField(ref _scopeOptions, value))
                {
                    SetField(ref _selectedScopeOption, value[0], nameof(SelectedScopeOption));
                    Scope = SelectedScopeOption.Scope;
                }
            }
        }

        private ImportExportScopeOption _selectedScopeOption;

        public ImportExportScopeOption SelectedScopeOption
        {
            get => _selectedScopeOption;
            set
            {
                if (SetField(ref _selectedScopeOption, value))
                {
                    Scope = SelectedScopeOption.Scope;
                }
            }
        }

        private ImportExportScope _scope;

        public ImportExportScope Scope
        {
            get => _scope;
            private set => SetField(ref _scope, value);
        }

        private string _googleSheetUrlOrId;

        public string GoogleSheetUrlOrId
        {
            get => _googleSheetUrlOrId;
            set => SetField(ref _googleSheetUrlOrId, value);
        }

        private string _exercisesSheetName;

        public string ExercisesSheetName
        {
            get => _exercisesSheetName;
            set => SetField(ref _exercisesSheetName, value);
        }

        private string _setsSetsSheetName;

        public string SetsSheetName
        {
            get => _setsSetsSheetName;
            set => SetField(ref _setsSetsSheetName, value);
        }

        private bool _validateDuplicates = true;

        public bool ValidateDuplicates
        {
            get => _validateDuplicates;
            set => SetField(ref _validateDuplicates, value);
        }

        public ReactiveRelayCommand ImportCommand { get; }
        public ReactiveRelayCommand CloseCommand { get; }

        private IStatisticsImportService StatisticsImportService => App.ServiceProvider.GetRequiredService<IStatisticsImportService>();
        private IPersistentCacheService PersistentCacheService => App.ServiceProvider.GetRequiredService<IPersistentCacheService>();

        private readonly CancellationTokenSource _importCts = new CancellationTokenSource();

        public ImportViewModel()
        {
            ScopeOptions = new ObservableCollection<ImportExportScopeOption>
            {
                new ImportExportScopeOption("Exercises and Sets", ImportExportScope.ExercisesAndSets),
                new ImportExportScopeOption("Exercises only", ImportExportScope.Exercises),
                new ImportExportScopeOption("Sets only", ImportExportScope.Sets)
            };

            ImportCommand = new ReactiveRelayCommand(Import);
            CloseCommand = new ReactiveRelayCommand(Close);

            if (PersistentCacheService.TryGet(out ImportExportCache importExportCache))
            {
                GoogleSheetUrlOrId = importExportCache.LastImportGoogleSheetUrlOrId;
                SetsSheetName = importExportCache.LastImportSheetName;
            }
        }

        public void Dispose()
        {
            _importCts.Cancel();
            _importCts.Dispose();
        }

        public void Open()
        {
            IsOpen = true;
        }

        private void Import()
        {
            _ = ImportAsync(_importCts.Token);
        }

        private void Close()
        {
            IsOpen = false;
        }

        private async Task ImportAsync(CancellationToken cancellationToken)
        {
            ImportCommand.IsExecutable = false;
            CloseCommand.IsExecutable = false;

            string googleSheetUrlOrId = GoogleSheetUrlOrId;
            string exercisesSheetName = ExercisesSheetName;
            string setsSheetName = SetsSheetName;

            IStatisticsImporter importer = Scope switch
            {
                ImportExportScope.Exercises => new GoogleSheetStatisticsImporter(googleSheetUrlOrId, exercisesSheetName, null, ValidateDuplicates),
                ImportExportScope.Sets => new GoogleSheetStatisticsImporter(googleSheetUrlOrId, null, setsSheetName, ValidateDuplicates),
                ImportExportScope.ExercisesAndSets => new GoogleSheetStatisticsImporter(googleSheetUrlOrId, exercisesSheetName, setsSheetName, ValidateDuplicates),
                _ => throw new ArgumentOutOfRangeException(nameof(Scope), Scope, "Invalid import/export scope.")
            };

            OperationResult result = await StatisticsImportService.ImportAsync(importer, cancellationToken);

            ImportCommand.IsExecutable = true;
            CloseCommand.IsExecutable = true;

            if (result.Succeeded)
            {
                ImportExportCache importExportCache = PersistentCacheService.GetOrNew<ImportExportCache>();

                importExportCache.LastImportGoogleSheetUrlOrId = googleSheetUrlOrId;
                importExportCache.LastImportSheetName = setsSheetName;

                PersistentCacheService.Set(importExportCache);

                Close();
            }
        }
    }
}