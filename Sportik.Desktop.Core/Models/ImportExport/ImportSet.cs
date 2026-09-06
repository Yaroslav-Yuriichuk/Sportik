using System;

namespace Sportik.Desktop.Core.Models.ImportExport
{
    public sealed class ImportSet
    {
        public string Name { get; }

        public DateTimeOffset LoggedAt { get; }

        public int Repetitions { get; }

        public ImportSet(string name, DateTimeOffset loggedAt, int repetitions)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            LoggedAt = loggedAt;
            Repetitions = repetitions;
        }
    }
}