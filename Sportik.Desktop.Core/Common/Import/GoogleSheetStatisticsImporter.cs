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
using Sportik.Desktop.Core.Models.ImportExport;

namespace Sportik.Desktop.Core.Common.Import
{
    public sealed class GoogleSheetStatisticsImporter : StatisticsImporterBase
    {
        private readonly string _sheetUrlOrId;
        private readonly string _exercisesSheetName;
        private readonly string _setsSheetName;

        public GoogleSheetStatisticsImporter(string sheetUrlOrId, string exercisesSheetName, string setsSheetName, bool validateDuplicates)
            : base(validateDuplicates)
        {
            if (string.IsNullOrWhiteSpace(sheetUrlOrId))
            {
                throw new InvalidOperationException("Sheet url or id cannot be empty.");
            }

            if (string.IsNullOrWhiteSpace(exercisesSheetName) && string.IsNullOrWhiteSpace(setsSheetName))
            {
                throw new InvalidOperationException("Exercises and sets sheet name cannot be empty.");
            }

            _sheetUrlOrId = sheetUrlOrId;
            _exercisesSheetName = exercisesSheetName;
            _setsSheetName = setsSheetName;
        }

        protected override bool ToImportExercises()
        {
            return !string.IsNullOrWhiteSpace(_exercisesSheetName);
        }

        protected override bool ToImportSets()
        {
            return !string.IsNullOrWhiteSpace(_setsSheetName);
        }

        protected override async Task<IList<ImportExercise>> GetExercisesAsync(CancellationToken cancellationToken)
        {
            if (!GoogleSheetsHelper.TryParseSheetId(_sheetUrlOrId, out string sheetId))
            {
                throw new InvalidOperationException("Invalid sheet url or id.");
            }

            string serviceAccountJson = GoogleSheetsHelper.LoadServiceAccountJson("google-service-account.json");

            GoogleCredential credential = GoogleCredential
                .FromJson(serviceAccountJson)
                .CreateScoped(SheetsService.Scope.Spreadsheets);

            using SheetsService sheetsService = new SheetsService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "Sportik.Desktop"
            });

            string escapedExercisesSheetName = GoogleSheetsHelper.EscapeSheetName(_exercisesSheetName);
            string readRange = $"{escapedExercisesSheetName}!A:D";

            SpreadsheetsResource.ValuesResource.GetRequest request =
                sheetsService.Spreadsheets.Values.Get(sheetId, readRange);

            ValueRange response = await request.ExecuteAsync(cancellationToken);

            List<ImportExercise> importExercises = new List<ImportExercise>();

            foreach (IList<object> row in response.Values ?? Enumerable.Empty<IList<object>>())
            {
                string exerciseName = row[0].ToString();
                int targetRepetitions = int.Parse(row[1].ToString());
                TimeSpan timeBetweenSets = TimeSpan.FromMinutes(double.Parse(row[2].ToString()));
                TimeSpan executionTime = TimeSpan.FromMinutes(double.Parse(row[3].ToString()));

                importExercises.Add(new ImportExercise(exerciseName, targetRepetitions, timeBetweenSets, executionTime));
            }

            return importExercises;
        }

        protected override async Task<IList<ImportSet>> GetSetsAsync(CancellationToken cancellationToken)
        {
            if (!GoogleSheetsHelper.TryParseSheetId(_sheetUrlOrId, out string sheetId))
            {
                throw new InvalidOperationException("Invalid sheet url or id.");
            }

            string serviceAccountJson = GoogleSheetsHelper.LoadServiceAccountJson("google-service-account.json");

            GoogleCredential credential = GoogleCredential
                .FromJson(serviceAccountJson)
                .CreateScoped(SheetsService.Scope.Spreadsheets);

            using SheetsService sheetsService = new SheetsService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "Sportik.Desktop"
            });

            string escapedSetsSheetName = GoogleSheetsHelper.EscapeSheetName(_setsSheetName);
            string readRange = $"{escapedSetsSheetName}!A:C";

            SpreadsheetsResource.ValuesResource.GetRequest request =
                sheetsService.Spreadsheets.Values.Get(sheetId, readRange);

            ValueRange response = await request.ExecuteAsync(cancellationToken);

            List<ImportSet> importExercises = new List<ImportSet>();

            foreach (IList<object> row in response.Values ?? Enumerable.Empty<IList<object>>())
            {
                string exerciseName = row[0].ToString();
                DateTimeOffset loggedAt = DateTimeOffset.Parse(row[1].ToString());
                int repetitions = int.Parse(row[2].ToString());

                importExercises.Add(new ImportSet(exerciseName, loggedAt, repetitions));
            }

            return importExercises;
        }
    }
}
