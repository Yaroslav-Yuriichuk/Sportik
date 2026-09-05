using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Sportik.Desktop.Core.Common;
using Sportik.Desktop.Core.Common.Export;
using Sportik.Desktop.Core.Extensions;
using Sportik.Desktop.Core.Models;
using Sportik.Desktop.Core.Services.Interfaces;
using Sportik.Desktop.UI.Models;

namespace Sportik.Desktop.UI.ViewModels.Statistics
{
    internal sealed class ExportViewModel : ViewModel, IDisposable
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

        public ReactiveRelayCommand ExportCommand { get; }
        public ReactiveRelayCommand CloseCommand { get; }

        private IStatisticsExportService StatisticsExportService => App.ServiceProvider.GetRequiredService<IStatisticsExportService>();
        private IPersistentCacheService PersistentCacheService => App.ServiceProvider.GetRequiredService<IPersistentCacheService>();

        private readonly CancellationTokenSource _exportCts = new CancellationTokenSource();

        public ExportViewModel()
        {
            ScopeOptions = new ObservableCollection<ImportExportScopeOption>
            {
                new ImportExportScopeOption("Exercises and Sets", ImportExportScope.ExercisesAndSets),
                new ImportExportScopeOption("Exercises only", ImportExportScope.Exercises),
                new ImportExportScopeOption("Sets only", ImportExportScope.Sets)
            };

            ExportCommand = new ReactiveRelayCommand(Export);
            CloseCommand = new ReactiveRelayCommand(Close);

            if (PersistentCacheService.TryGet(out ImportExportCache importExportCache))
            {
                GoogleSheetUrlOrId = importExportCache.LastExportGoogleSheetUrlOrId;
                SetsSheetName = importExportCache.LastExportSheetName;
            }
        }

        public void Dispose()
        {
            _exportCts.Cancel();
            _exportCts.Dispose();
        }

        public void Open()
        {
            IsOpen = true;
        }

        private void Export()
        {
            _ = ExportAsync(_exportCts.Token);
        }

        private void Close()
        {
            IsOpen = false;
        }

        private async Task ExportAsync(CancellationToken cancellationToken)
        {
            ExportCommand.IsExecutable = false;
            CloseCommand.IsExecutable = false;

            string googleSheetUrlOrId = GoogleSheetUrlOrId;
            string exercisesSheetName = ExercisesSheetName;
            string setsSheetName = SetsSheetName;

            IStatisticsExporter exporter = Scope switch
            {
                ImportExportScope.Exercises => new GoogleSheetStatisticsExporter(googleSheetUrlOrId, exercisesSheetName, null),
                ImportExportScope.Sets => new GoogleSheetStatisticsExporter(googleSheetUrlOrId, null, setsSheetName),
                ImportExportScope.ExercisesAndSets => new GoogleSheetStatisticsExporter(googleSheetUrlOrId, exercisesSheetName, setsSheetName),
                _ => throw new ArgumentOutOfRangeException(nameof(Scope), Scope, "Invalid import/export scope.")
            };

            OperationResult result = await StatisticsExportService.ExportAsync(exporter, cancellationToken);

            ExportCommand.IsExecutable = true;
            CloseCommand.IsExecutable = true;

            if (result.Succeeded)
            {
                ImportExportCache importExportCache = PersistentCacheService.GetOrNew<ImportExportCache>();

                importExportCache.LastExportGoogleSheetUrlOrId = googleSheetUrlOrId;
                importExportCache.LastExportSheetName = setsSheetName;

                PersistentCacheService.Set(importExportCache);

                Close();
            }
        }
    }
}