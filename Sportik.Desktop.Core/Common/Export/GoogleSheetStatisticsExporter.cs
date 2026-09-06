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

namespace Sportik.Desktop.Core.Common.Export
{
    public sealed class GoogleSheetStatisticsExporter : StatisticsExporterBase
    {
        private readonly string _sheetUrlOrId;
        private readonly string _exercisesSheetName;
        private readonly string _setsSheetName;

        public GoogleSheetStatisticsExporter(string sheetUrlOrId, string exercisesSheetName, string setsSheetName)
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

        protected override bool ToExportExercises()
        {
            return !string.IsNullOrWhiteSpace(_exercisesSheetName);
        }

        protected override bool ToExportSets()
        {
            return !string.IsNullOrWhiteSpace(_setsSheetName);
        }

        protected override async Task WriteExercisesAsync(IList<ExportExercise> exercises, CancellationToken cancellationToken)
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
            string clearRange = $"{escapedExercisesSheetName}!A:D";
            string writeRange = $"{escapedExercisesSheetName}!A1";

            ClearValuesRequest clearValuesBody = new ClearValuesRequest();

            SpreadsheetsResource.ValuesResource.ClearRequest clearRequest =
                sheetsService.Spreadsheets.Values.Clear(clearValuesBody, sheetId, clearRange);

            await clearRequest.ExecuteAsync(cancellationToken);

            List<IList<object>> values = exercises
                .Select(exercise => (IList<object>)new List<object>
                {
                    exercise.Name,
                    exercise.TargetRepetitions,
                    exercise.TimeBetweenSets.TotalMinutes,
                    exercise.ExecutionTime.TotalMinutes,
                })
                .ToList();

            if (values.Count == 0)
            {
                return;
            }

            ValueRange updateBody = new ValueRange
            {
                Values = values,
            };

            SpreadsheetsResource.ValuesResource.UpdateRequest updateRequest =
                sheetsService.Spreadsheets.Values.Update(updateBody, sheetId, writeRange);

            updateRequest.ValueInputOption =
                SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;

            await updateRequest.ExecuteAsync(cancellationToken);
        }

        protected override async Task WriteSetsAsync(IList<ExportSet> sets, CancellationToken cancellationToken)
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
            string clearRange = $"{escapedSetsSheetName}!A:C";
            string writeRange = $"{escapedSetsSheetName}!A1";

            ClearValuesRequest clearValuesBody = new ClearValuesRequest();

            SpreadsheetsResource.ValuesResource.ClearRequest clearRequest =
                sheetsService.Spreadsheets.Values.Clear(clearValuesBody, sheetId, clearRange);

            await clearRequest.ExecuteAsync(cancellationToken);

            List<IList<object>> values = sets
                .Select(set => (IList<object>)new List<object>
                {
                    set.Name,
                    set.LoggedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                    set.Repetitions,
                })
                .ToList();

            if (values.Count == 0)
            {
                return;
            }

            ValueRange updateBody = new ValueRange
            {
                Values = values,
            };

            SpreadsheetsResource.ValuesResource.UpdateRequest updateRequest =
                sheetsService.Spreadsheets.Values.Update(updateBody, sheetId, writeRange);

            updateRequest.ValueInputOption =
                SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;

            await updateRequest.ExecuteAsync(cancellationToken);
        }
    }
}
