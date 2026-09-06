using System;
using Sportik.Desktop.Core.Models;

namespace Sportik.Desktop.Core.Events
{
    public sealed class ExerciseCreatedEventArgs : EventArgs
    {
        public Exercise Exercise { get; }

        public CreationSource CreationSource { get; }

        public ExerciseCreatedEventArgs(Exercise exercise, CreationSource creationSource)
        {
            Exercise = exercise;
            CreationSource = creationSource;
        }
    }
}