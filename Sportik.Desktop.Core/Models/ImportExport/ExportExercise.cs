using System;

namespace Sportik.Desktop.Core.Models.ImportExport
{
    public sealed class ExportExercise
    {
        public string Name { get; }

        public int TargetRepetitions { get; }

        public TimeSpan TimeBetweenSets { get; }

        public TimeSpan ExecutionTime { get; }

        public ExportExercise(string name, int targetRepetitions, TimeSpan timeBetweenSets, TimeSpan executionTime)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            TargetRepetitions = targetRepetitions;
            TimeBetweenSets = timeBetweenSets;
            ExecutionTime = executionTime;
        }
    }
}