using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sportik.Desktop.Core.Events;
using Sportik.Desktop.Core.Models;
using Sportik.Desktop.Core.Models.ImportExport;
using Sportik.Desktop.Core.Models.Settings;
using Sportik.Desktop.Core.Repositories.Interfaces;
using Sportik.Desktop.Core.Services.Interfaces;

namespace Sportik.Desktop.Core.Common.Import
{
    public abstract class StatisticsImporterBase : IStatisticsImporter
    {
        private readonly bool _validateDuplicates;

        private IExercisesRepository _exercisesRepository;
        private IExerciseStatisticsRepository _exerciseStatisticsRepository;
        private IEventsService _eventsService;

        protected StatisticsImporterBase(bool validateDuplicates)
        {
            _validateDuplicates = validateDuplicates;
        }

        void IStatisticsImporter.Initialize(IExercisesRepository exercisesRepository, IExerciseStatisticsRepository exerciseStatisticsRepository,
            IEventsService eventsService)
        {
            _exercisesRepository = exercisesRepository;
            _exerciseStatisticsRepository = exerciseStatisticsRepository;
            _eventsService = eventsService;
        }

        public async Task ImportAsync(CancellationToken cancellationToken)
        {
            if (_exercisesRepository is null || _exerciseStatisticsRepository is null || _eventsService is null)
            {
                throw new InvalidOperationException("Importer is not initialized.");
            }

            if (ToImportExercises())
            {
                IList<ImportExercise> importExercises = await GetExercisesAsync(cancellationToken);
                IList<AddExerciseModel> exercisesToAdd = await FilterExercises(importExercises, cancellationToken);

                if (exercisesToAdd.Count == 0)
                {
                    return;
                }

                IEnumerable<Exercise> addedExercises = await _exercisesRepository.AddRangeAsync(exercisesToAdd, cancellationToken);

                foreach (Exercise addedExercise in addedExercises)
                {
                    _eventsService.RaiseEvent(new ExerciseCreatedEventArgs(addedExercise, CreationSource.Import));
                }
            }

            if (ToImportSets())
            {
                IList<ImportSet> importSets = await GetSetsAsync(cancellationToken);
                IList<AddExerciseSetModel> setsToAdd = await FilterSets(importSets, cancellationToken);

                if (setsToAdd.Count == 0)
                {
                    return;
                }

                IEnumerable<ExerciseSet> addedSets = await _exerciseStatisticsRepository.AddRangeAsync(setsToAdd, cancellationToken);

                foreach (ExerciseSet addedSet in addedSets)
                {
                    _eventsService.RaiseEvent(new ExerciseSetAddedEventArgs(addedSet, true));
                }
            }
        }

        protected abstract bool ToImportExercises();

        protected abstract bool ToImportSets();

        protected abstract Task<IList<ImportExercise>> GetExercisesAsync(CancellationToken cancellationToken);

        protected abstract Task<IList<ImportSet>> GetSetsAsync(CancellationToken cancellationToken);

        private async Task<IList<AddExerciseModel>> FilterExercises(IList<ImportExercise> importExercises, CancellationToken cancellationToken)
        {
            if (importExercises.Count == 0)
            {
                return new List<AddExerciseModel>();
            }

            IEnumerable<Exercise> existingExercises = await _exercisesRepository.GetAllAsync(cancellationToken);
            HashSet<string> existingNames = new HashSet<string>(existingExercises.Select(e => e.Name ?? string.Empty));

            List<AddExerciseModel> exercisesToAdd = new List<AddExerciseModel>();

            foreach (ImportExercise importExercise in importExercises)
            {
                if (string.IsNullOrWhiteSpace(importExercise.Name) || existingNames.Contains(importExercise.Name))
                {
                    continue;
                }

                ExerciseSettings exerciseSettings = new ExerciseSettings(
                    false,
                    importExercise.TargetRepetitions,
                    importExercise.TimeBetweenSets,
                    importExercise.ExecutionTime);

                exercisesToAdd.Add(new AddExerciseModel(null, importExercise.Name, exerciseSettings));
            }

            return exercisesToAdd;
        }

        private async Task<IList<AddExerciseSetModel>> FilterSets(IList<ImportSet> importSets, CancellationToken cancellationToken)
        {
            if (importSets.Count == 0)
            {
                return new List<AddExerciseSetModel>();
            }

            IEnumerable<Exercise> exercises = await _exercisesRepository.GetAllAsync(cancellationToken);
            exercises = exercises as List<Exercise> ?? exercises.ToList();

            IEnumerable<ExerciseSet> existingSets = await _exerciseStatisticsRepository.GetAllAsync(cancellationToken);
            Dictionary<Guid, string> exerciseNamesById = exercises.ToDictionary(e => e.Id, e => e.Name ?? string.Empty);

            HashSet<ImportKey> existingKeys = new HashSet<ImportKey>();

            foreach (ExerciseSet existingSet in existingSets)
            {
                if (!exerciseNamesById.TryGetValue(existingSet.ExerciseId, out string name) || string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                existingKeys.Add(new ImportKey(name, existingSet.Repetitions, existingSet.LoggedAt));
            }

            List<AddExerciseSetModel> setsToAdd = new List<AddExerciseSetModel>();
            HashSet<ImportKey> incomingKeys = new HashSet<ImportKey>();

            Dictionary<string, Exercise> exercisesByName = exercises
                .GroupBy(e => e.Name ?? string.Empty)
                .ToDictionary(g => g.Key, g => g.First());

            foreach (ImportSet importSet in importSets)
            {
                if (!exercisesByName.TryGetValue(importSet.Name, out Exercise exercise))
                {
                    continue;
                }

                ImportKey key = new ImportKey(importSet.Name, importSet.Repetitions, importSet.LoggedAt);

                if (_validateDuplicates)
                {
                    if (existingKeys.Contains(key) || incomingKeys.Contains(key))
                    {
                        continue;
                    }
                }

                incomingKeys.Add(key);
                setsToAdd.Add(new AddExerciseSetModel(null, importSet.Repetitions, importSet.LoggedAt, exercise.Id));
            }

            return setsToAdd;
        }

        private readonly struct ImportKey : IEquatable<ImportKey>
        {
            private readonly string _exerciseName;
            private readonly int _repetitions;
            private readonly DateTimeOffset _loggedAt;

            public ImportKey(string exerciseName, int repetitions, DateTimeOffset loggedAt)
            {
                _exerciseName = exerciseName ?? string.Empty;
                _repetitions = repetitions;
                _loggedAt = loggedAt.ToUniversalTime();
            }

            public bool Equals(ImportKey other)
            {
                return _repetitions == other._repetitions &&
                       _loggedAt.Equals(other._loggedAt) &&
                       string.Equals(_exerciseName, other._exerciseName, StringComparison.OrdinalIgnoreCase);
            }

            public override bool Equals(object obj)
            {
                return obj is ImportKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;

                    hash = hash * 31 + StringComparer.OrdinalIgnoreCase.GetHashCode(_exerciseName ?? string.Empty);
                    hash = hash * 31 + _repetitions.GetHashCode();
                    hash = hash * 31 + _loggedAt.GetHashCode();

                    return hash;
                }
            }
        }
    }
}