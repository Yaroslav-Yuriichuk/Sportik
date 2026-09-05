using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Sportik.Desktop.Core.Helpers;
using Sportik.Desktop.Core.Models;

namespace Sportik.Desktop.Core.Common.Import
{
    public sealed class GoogleSheetStatisticsImporter : StatisticsImporterBase
    {
        private readonly string _sheetUrlOrId;
        private readonly string _sheetName;

        public GoogleSheetStatisticsImporter(string sheetUrlOrId, string sheetName, bool validateDuplicates)
            : base(validateDuplicates)
        {
            _sheetUrlOrId = sheetUrlOrId;
            _sheetName = sheetName;
        }

        protected override async Task<IList<ImportSet>> GetExercisesAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_sheetUrlOrId))
            {
                throw new InvalidOperationException("Sheet url or id cannot be empty.");
            }

            if (string.IsNullOrWhiteSpace(_sheetName))
            {
                throw new InvalidOperationException("Sheet name cannot be empty.");
            }

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

            string escapedSheetName = GoogleSheetsHelper.EscapeSheetName(_sheetName);
            string readRange = $"{escapedSheetName}!A:C";

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

