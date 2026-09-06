using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sportik.Desktop.Core.Common;
using Sportik.Desktop.Core.Common.Synchronization;
using Sportik.Desktop.Core.Events;
using Sportik.Desktop.Core.Models;
using Sportik.Desktop.Core.Models.Automation;
using Sportik.Desktop.Core.Models.Settings;
using Sportik.Desktop.Core.Services.Interfaces;

namespace Sportik.Desktop.Core.States.App
{
    internal sealed class AuthenticatedAppState : AppState
    {
        public override ApplicationState ApplicationState => ApplicationState.Authenticated;

        private readonly IEventsService _eventsService;
        private readonly IReminderService _reminderService;
        private readonly IPersistentCacheService _persistentCacheService;
        private readonly IRuntimeCacheService _runtimeCacheService;
        private readonly Func<IExercisesService> _exercisesServiceFactory;
        private readonly Func<ISynchronizationService> _synchronizationServiceFactory;
        private readonly ITrainingService _trainingService;

        public AuthenticatedAppState(AppStatesContext context, IEventsService eventsService,
            IReminderService reminderService, IPersistentCacheService persistentCacheService,
            IRuntimeCacheService runtimeCacheService, Func<IExercisesService> exercisesServiceFactory,
            Func<ISynchronizationService> synchronizationServiceFactory,
            ITrainingService trainingService) : base(context)
        {
            _eventsService = eventsService;
            _reminderService = reminderService;
            _persistentCacheService = persistentCacheService;
            _runtimeCacheService = runtimeCacheService;
            _exercisesServiceFactory = exercisesServiceFactory;
            _synchronizationServiceFactory = synchronizationServiceFactory;
            _trainingService = trainingService;
        }

        protected override void HandleEnter()
        {
            _persistentCacheService.Set(new AppRunCache
            {
                LastIsOnline = true,
            });

            _runtimeCacheService.Set(new AppModeCache
            {
                IsGuest = false,
            });

            _eventsService.AddListener<UserLoggedOutEventArgs>(EventsService_Event);
            _eventsService.AddListener<UserRefreshFailedEventArgs>(EventsService_Event);
            _eventsService.AddListener<ExerciseCreatedEventArgs>(EventsService_Event);
            _eventsService.AddListener<ExerciseIsEnabledChangedEventArgs>(EventsService_Event);
            _eventsService.AddListener<ExerciseTargetRepetitionsChangedEventArgs>(EventsService_Event);
            _eventsService.AddListener<ExerciseTimeBetweenSetsChangedEventArgs>(EventsService_Event);
            _eventsService.AddListener<ExerciseExecutionTimeChangedEventArgs>(EventsService_Event);
            _eventsService.AddListener<ExerciseSetAddedEventArgs>(EventsService_Event);

            ReminderMode reminderMode = _reminderService.Mode;
            bool toStartReminders = true;

            if (_persistentCacheService.TryGet(out ReminderCache cache))
            {
                toStartReminders = cache.IsActive;
            }

            if (toStartReminders)
            {
                _reminderService.Start(reminderMode);
            }

            IExercisesService exercisesService = _exercisesServiceFactory();
            ISynchronizationService synchronizationService = _synchronizationServiceFactory();

            Task.Run(async () =>
            {
                OperationResult<IEnumerable<Exercise>> result = await exercisesService.GetAllAsync(ActiveCancellationToken);

                if (!result.Succeeded)
                {
                    return;
                }

                foreach (Exercise exercise in result.Value)
                {
                    _reminderService.AddExercise(exercise.Id);
                }
            });

            Task.Run(async () =>
            {
                ISynchronizer synchronizer = new AllSynchronizer();
                await synchronizationService.SyncAsync(synchronizer, ActiveCancellationToken);
            });
        }

        protected override void HandleExit()
        {
            _runtimeCacheService.Remove<AppModeCache>();

            _eventsService.RemoveListener<UserLoggedOutEventArgs>(EventsService_Event);
            _eventsService.RemoveListener<UserRefreshFailedEventArgs>(EventsService_Event);
            _eventsService.RemoveListener<ExerciseCreatedEventArgs>(EventsService_Event);
            _eventsService.RemoveListener<ExerciseIsEnabledChangedEventArgs>(EventsService_Event);
            _eventsService.RemoveListener<ExerciseTargetRepetitionsChangedEventArgs>(EventsService_Event);
            _eventsService.RemoveListener<ExerciseTimeBetweenSetsChangedEventArgs>(EventsService_Event);
            _eventsService.RemoveListener<ExerciseExecutionTimeChangedEventArgs>(EventsService_Event);
            _eventsService.RemoveListener<ExerciseSetAddedEventArgs>(EventsService_Event);

            if (_trainingService.IsRunning)
            {
                _trainingService.Stop();
            }

            ReminderCache reminderCache = new ReminderCache
            {
                IsActive = _reminderService.IsRunning,
                Mode = _reminderService.Mode,
            };

            _persistentCacheService.Set(reminderCache);

            foreach (Guid exerciseId in _reminderService.TrackedExerciseIds.ToArray())
            {
                _reminderService.RemoveExercise(exerciseId);
            }

            _reminderService.Stop();
        }

        private void EventsService_Event(UserLoggedOutEventArgs args)
        {
            Context.Switch(Context.LoginAppState);
        }

        private void EventsService_Event(UserRefreshFailedEventArgs args)
        {
            Context.Switch(Context.LoginAppState);
        }

        private void EventsService_Event(ExerciseCreatedEventArgs args)
        {
            _reminderService.AddExercise(args.Exercise.Id);

            if (args.CreationSource == CreationSource.Synchronization)
            {
                return;
            }

            ISynchronizationService synchronizationService = _synchronizationServiceFactory();

            Task.Run(async () =>
            {
                ISynchronizer synchronizer = new CreatedExerciseSynchronizer(args.Exercise);
                await synchronizationService.SyncAsync(synchronizer, ActiveCancellationToken);
            });
        }

        private void EventsService_Event(ExerciseIsEnabledChangedEventArgs args)
        {
            ISynchronizationService synchronizationService = _synchronizationServiceFactory();

            Task.Run(async () =>
            {
                ExerciseSettingsDelta delta = new ExerciseSettingsDelta
                {
                    Change = ExerciseSettingsChange.IsEnabled,
                    IsEnabled = args.IsEnabled,
                };

                ISynchronizer synchronizer = new ExerciseSettingsSynchronizer(args.ExerciseId, delta);
                await synchronizationService.SyncAsync(synchronizer, ActiveCancellationToken);
            });
        }

        private void EventsService_Event(ExerciseTargetRepetitionsChangedEventArgs args)
        {
            ISynchronizationService synchronizationService = _synchronizationServiceFactory();

            Task.Run(async () =>
            {
                ExerciseSettingsDelta delta = new ExerciseSettingsDelta
                {
                    Change = ExerciseSettingsChange.TargetRepetitions,
                    TargetRepetitions = args.TargetRepetitions,
                };

                ISynchronizer synchronizer = new ExerciseSettingsSynchronizer(args.ExerciseId, delta);
                await synchronizationService.SyncAsync(synchronizer, ActiveCancellationToken);
            });
        }

        private void EventsService_Event(ExerciseTimeBetweenSetsChangedEventArgs args)
        {
            ISynchronizationService synchronizationService = _synchronizationServiceFactory();

            Task.Run(async () =>
            {
                ExerciseSettingsDelta delta = new ExerciseSettingsDelta
                {
                    Change = ExerciseSettingsChange.TimeBetweenSets,
                    TimeBetweenSets = args.TimeBetweenSets,
                };

                ISynchronizer synchronizer = new ExerciseSettingsSynchronizer(args.ExerciseId, delta);
                await synchronizationService.SyncAsync(synchronizer, ActiveCancellationToken);
            });
        }

        private void EventsService_Event(ExerciseExecutionTimeChangedEventArgs args)
        {
            ISynchronizationService synchronizationService = _synchronizationServiceFactory();

            Task.Run(async () =>
            {
                ExerciseSettingsDelta delta = new ExerciseSettingsDelta
                {
                    Change = ExerciseSettingsChange.ExecutionTime,
                    ExecutionTime = args.ExecutionTime,
                };

                ISynchronizer synchronizer = new ExerciseSettingsSynchronizer(args.ExerciseId, delta);
                await synchronizationService.SyncAsync(synchronizer, ActiveCancellationToken);
            });
        }

        private void EventsService_Event(ExerciseSetAddedEventArgs args)
        {
            if (!args.SynchronizationRequired)
            {
                return;
            }

            ISynchronizationService synchronizationService = _synchronizationServiceFactory();

            Task.Run(async () =>
            {
                ISynchronizer synchronizer = new AddedSetSynchronizer(args.Set);
                await synchronizationService.SyncAsync(synchronizer, ActiveCancellationToken);
            });
        }
    }
}
