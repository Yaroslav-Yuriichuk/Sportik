using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Sportik.Desktop.Core.Helpers;
using Sportik.Desktop.Core.Models;
using Sportik.Desktop.Core.Repositories.Interfaces;

namespace Sportik.Desktop.Core.Common.Export
{
    public abstract class StatisticsExporterBase : IStatisticsExporter
    {
        private IExercisesRepository _exercisesRepository;
        private IExerciseStatisticsRepository _exerciseStatisticsRepository;

        void IStatisticsExporter.Initialize(IExercisesRepository exercisesRepository,
            IExerciseStatisticsRepository exerciseStatisticsRepository)
        {
            _exercisesRepository = exercisesRepository;
            _exerciseStatisticsRepository = exerciseStatisticsRepository;
        }

        public async Task ExportAsync(CancellationToken cancellationToken)
        {
            if (_exercisesRepository is null || _exerciseStatisticsRepository is null)
            {
                throw new InvalidOperationException("Exporter is not initialized.");
            }

            IEnumerable<Exercise> exercises = (await _exercisesRepository.GetAllAsync(cancellationToken)).ToList();
            IEnumerable<ExerciseSet> exerciseSets = (await _exerciseStatisticsRepository.GetAllAsync(cancellationToken)).ToList();

            Dictionary<Guid, string> exerciseNamesById = exercises
                .ToDictionary(exercise => exercise.Id, exercise => exercise.Name);

            List<ExportExercise> exportExercises = exercises
                .Where(exercise => !string.IsNullOrWhiteSpace(exercise.Name))
                .Select(exercise => new ExportExercise(
                    exercise.Name,
                    exercise.Settings.TargetRepetitions,
                    exercise.Settings.TimeBetweenSets,
                    exercise.Settings.ExecutionTime))
                .ToList();

            List<ExportSet> exportSets = exerciseSets
                .OrderBy(set => set.LoggedAt)
                .Where(set => exerciseNamesById.TryGetValue(set.ExerciseId, out string exerciseName) && !string.IsNullOrWhiteSpace(exerciseName))
                .Select(set => new ExportSet(
                    exerciseNamesById[set.ExerciseId],
                    set.LoggedAt.ToUniversalTime(),
                    set.Repetitions))
                .ToList();

            List<Task> exportTasks = new List<Task>();

            if (ToExportExercises())
            {
                exportTasks.Add(WriteExercisesAsync(exportExercises, cancellationToken));
            }

            if (ToExportSets())
            {
                exportTasks.Add(WriteSetsAsync(exportSets, cancellationToken));
            }

            await Task.WhenAll(exportTasks);
        }

        protected abstract bool ToExportExercises();

        protected abstract bool ToExportSets();

        protected abstract Task WriteExercisesAsync(IList<ExportExercise> exercises, CancellationToken cancellationToken);

        protected abstract Task WriteSetsAsync(IList<ExportSet> exercises, CancellationToken cancellationToken);
    }
}