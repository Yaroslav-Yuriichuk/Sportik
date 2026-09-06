using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sportik.Desktop.Core.Common;
using Sportik.Desktop.Core.Events;
using Sportik.Desktop.Core.Models;
using Sportik.Desktop.Core.Models.Settings;
using Sportik.Desktop.Core.Repositories.Interfaces;
using Sportik.Desktop.Core.Services.Interfaces;

namespace Sportik.Desktop.Core.Services.Implementations
{
    internal sealed class ExercisesService : IExercisesService
    {
        private readonly IExercisesRepository _remoteExercisesRepository;
        private readonly IExercisesRepository _localExercisesRepository;
        private readonly IRuntimeCacheService _runtimeCacheService;
        private readonly IEventsService _eventsService;

        private IExercisesRepository ExercisesRepository
        {
            get
            {
                if (!_runtimeCacheService.TryGet(out AppModeCache appModeCache))
                {
                    return _localExercisesRepository;
                }

                return appModeCache.IsGuest ? _localExercisesRepository : _remoteExercisesRepository;
            }
        }

        public ExercisesService(Func<DataSource, IExercisesRepository> exercisesRepositoryFactory,
            IRuntimeCacheService runtimeCacheService, IEventsService eventsService)
        {
            _remoteExercisesRepository = exercisesRepositoryFactory(DataSource.Remote);
            _localExercisesRepository = exercisesRepositoryFactory(DataSource.Local);
            _runtimeCacheService = runtimeCacheService;
            _eventsService = eventsService;
        }

        public async Task<OperationResult<IEnumerable<Exercise>>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                IEnumerable<Exercise> exercises = await ExercisesRepository.GetAllAsync(cancellationToken);
                return OperationResult<IEnumerable<Exercise>>.Success(exercises);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return OperationResult<IEnumerable<Exercise>>.Failure(new[] { "Failed to retrieve exercises." });
            }
        }

        public async Task<OperationResult<Exercise>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            try
            {
                Exercise exercise = await ExercisesRepository.GetByIdAsync(id, cancellationToken);
                return OperationResult<Exercise>.Success(exercise);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return OperationResult<Exercise>.Failure(new[] { "Failed to retrieve exercise." });
            }
        }

        public async Task<OperationResult<IEnumerable<Exercise>>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
        {
            try
            {
                IEnumerable<Exercise> exercises = await ExercisesRepository.GetByIdsAsync(ids, cancellationToken);
                return OperationResult<IEnumerable<Exercise>>.Success(exercises);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return OperationResult<IEnumerable<Exercise>>.Failure(new[] { "Failed to retrieve exercises." });
            }
        }

        public async Task<OperationResult<Exercise>> AddAsync(string name, ExerciseSettings settings, CancellationToken cancellationToken = default)
        {
            try
            {
                Exercise exercise = await ExercisesRepository.AddAsync(new AddExerciseModel(null, name, settings), cancellationToken);
                _eventsService.RaiseEvent(new ExerciseCreatedEventArgs(exercise, CreationSource.User));

                return OperationResult<Exercise>.Success(exercise);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return OperationResult<Exercise>.Failure(new[] { "Failed to add exercise." });
            }
        }
    }
}
