using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sportik.Desktop.Core.Events;
using Sportik.Desktop.Core.Models;
using Sportik.Desktop.Core.Models.Settings;

namespace Sportik.Desktop.Core.Common.Synchronization
{
    internal sealed class AllSynchronizer : Synchronizer
    {
        public override async Task SyncAsync(CancellationToken cancellationToken)
        {
            await SyncExercisesAsync(cancellationToken);
            await SyncExerciseStatisticsAsync(cancellationToken);
            await SyncExerciseSettingsAsync(cancellationToken);
        }

        private async Task SyncExercisesAsync(CancellationToken cancellationToken)
        {
            Task<IEnumerable<Exercise>> remoteExercisesTask = RemoteExercisesRepository.GetAllAsync(cancellationToken);
            Task<IEnumerable<Exercise>> localExercisesTask = LocalExercisesRepository.GetAllAsync(cancellationToken);

            await Task.WhenAll(remoteExercisesTask, localExercisesTask);

            IEnumerable<Exercise> remoteExercises = remoteExercisesTask.Result.ToList();
            IEnumerable<Exercise> localExercises = localExercisesTask.Result.ToList();

            HashSet<Guid> remoteExerciseIds = remoteExercises.Select(e => e.Id).ToHashSet();

            List<AddExerciseModel> localExercisesToAdd = localExercises
                .Where(e => !remoteExerciseIds.Contains(e.Id))
                .Select(e => new AddExerciseModel(e.Id, e.Name, e.Settings))
                .ToList();

            Task<IEnumerable<Exercise>> addLocalExercisesTask = RemoteExercisesRepository.AddRangeAsync(localExercisesToAdd, cancellationToken);

            HashSet<Guid> localExerciseIds = localExercises.Select(e => e.Id).ToHashSet();

            List<AddExerciseModel> remoteExercisesToAdd = remoteExercises
                .Where(e => !localExerciseIds.Contains(e.Id))
                .Select(e => new AddExerciseModel(e.Id, e.Name, e.Settings))
                .ToList();

            Task<IEnumerable<Exercise>> addRemoteExercisesTask = LocalExercisesRepository.AddRangeAsync(remoteExercisesToAdd, cancellationToken);

            await Task.WhenAll(addLocalExercisesTask, addRemoteExercisesTask);

            foreach (Exercise exercise in addLocalExercisesTask.Result)
            {
                EventsService.RaiseEvent(new ExerciseCreatedEventArgs(exercise, CreationSource.Synchronization));
            }
        }

        private async Task SyncExerciseSettingsAsync(CancellationToken cancellationToken)
        {
            IEnumerable<Exercise> remoteExercises = await RemoteExercisesRepository.GetAllAsync(cancellationToken);

            List<UpdateExerciseSettingsModel> updateModels = remoteExercises.Select(e =>
                {
                    ExerciseSettingsDelta delta = new ExerciseSettingsDelta
                    {
                        Change = ExerciseSettingsChange.IsEnabled | ExerciseSettingsChange.TargetRepetitions |
                                 ExerciseSettingsChange.TimeBetweenSets | ExerciseSettingsChange.ExecutionTime,
                        IsEnabled = e.Settings.IsEnabled,
                        TargetRepetitions = e.Settings.TargetRepetitions,
                        TimeBetweenSets = e.Settings.TimeBetweenSets,
                        ExecutionTime = e.Settings.ExecutionTime
                    };

                    return new UpdateExerciseSettingsModel(e.Id, delta);
                })
                .ToList();

            await LocalExerciseSettingsRepository.UpdateRangeAsync(updateModels, cancellationToken);
        }

        private async Task SyncExerciseStatisticsAsync(CancellationToken cancellationToken)
        {
            Task<IEnumerable<ExerciseSet>> remoteSetsTask = RemoteExerciseStatisticsRepository.GetAllAsync(cancellationToken);
            Task<IEnumerable<ExerciseSet>> localSetsTask = LocalExerciseStatisticsRepository.GetAllAsync(cancellationToken);

            await Task.WhenAll(remoteSetsTask, localSetsTask);

            IEnumerable<ExerciseSet> remoteSets = remoteSetsTask.Result.ToList();
            IEnumerable<ExerciseSet> localSets = localSetsTask.Result.ToList();

            HashSet<Guid> remoteSetIds = remoteSets.Select(set => set.Id).ToHashSet();

            List<AddExerciseSetModel> localSetsToAdd = localSets
                .Where(set => !remoteSetIds.Contains(set.Id))
                .Select(set => new AddExerciseSetModel(set.Id, set.Repetitions, set.LoggedAt, set.ExerciseId))
                .ToList();

            Task<IEnumerable<ExerciseSet>> addLocalSetsTask = RemoteExerciseStatisticsRepository.AddRangeAsync(localSetsToAdd, cancellationToken);

            HashSet<Guid> localSetIds = localSets.Select(set => set.Id).ToHashSet();

            List<AddExerciseSetModel> remoteSetsToAdd = remoteSets
                .Where(set => !localSetIds.Contains(set.Id))
                .Select(set => new AddExerciseSetModel(set.Id, set.Repetitions, set.LoggedAt, set.ExerciseId))
                .ToList();

            Task<IEnumerable<ExerciseSet>> addRemoteSetsTask = LocalExerciseStatisticsRepository.AddRangeAsync(remoteSetsToAdd, cancellationToken);

            await Task.WhenAll(addLocalSetsTask, addRemoteSetsTask);

            foreach (ExerciseSet exerciseSet in addLocalSetsTask.Result)
            {
                EventsService.RaiseEvent(new ExerciseSetAddedEventArgs(exerciseSet, false));
            }
        }
    }
}