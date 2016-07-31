﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Fr8.Infrastructure.Interfaces;

using NUnit.Framework;
using StructureMap;
using Fr8.Testing.Integration;
using Fr8.Testing.Integration.Tools.Activities;
using Fr8.Testing.Integration.Tools.Plans;
using terminalGoogle.Services;
using terminalGoogle.Services.Authorization;

namespace terminalIntegrationTests.EndToEnd
{
    [Explicit]
    public class Query_DocuSign_Into_Google_Sheet_Tests : BaseHubIntegrationTest
    {
        #region Properties

        private readonly IntegrationTestTools plansHelper;
        private readonly IntegrationTestTools_terminalDocuSign docuSignActivityConfigurator;
        private readonly IntegrationTestTools_terminalGoogle googleActivityConfigurator;
        public override string TerminalName => "terminalGoogle";

        #endregion

        public Query_DocuSign_Into_Google_Sheet_Tests()
        {
            plansHelper = new IntegrationTestTools(this);
            docuSignActivityConfigurator = new IntegrationTestTools_terminalDocuSign(this);
            googleActivityConfigurator = new IntegrationTestTools_terminalGoogle(this);
        }

        [Test, Ignore, Category("Integration.terminalGoogle")]
        public async Task Query_DocuSign_Into_Google_Sheet_End_To_End()
        {
           
            var terminalGoogleTools = new Fr8.Testing.Integration.Tools.Terminals.IntegrationTestTools_terminalGoogle(this);
            var googleAuthTokenId = await terminalGoogleTools.ExtractGoogleDefaultToken();
            var defaultGoogleAuthToken = terminalGoogleTools.GetGoogleAuthToken(googleAuthTokenId);

            //create a new plan
            var thePlan = await plansHelper.CreateNewPlan();

            //configure an query_DocuSign activity
            await docuSignActivityConfigurator.AddAndConfigure_QueryDocuSign(thePlan, 1, 2);

            //configure a save_to google activity
            var newSpeadsheetName = Guid.NewGuid().ToString();
            var googleSheetApi = new GoogleSheet(new GoogleIntegration(ObjectFactory.GetInstance<IRestfulServiceClient>()), new GoogleDrive());
            var spreadsheetId = await googleSheetApi.CreateSpreadsheet(newSpeadsheetName, defaultGoogleAuthToken);

            await googleActivityConfigurator.AddAndConfigureSaveToGoogleSheet(thePlan, 2, "Docusign Envelope v3", "DocuSign Envelope Data", newSpeadsheetName);
            

            try
            {
                //run the plan
                await plansHelper.RunPlan(thePlan.Id);

                //add asserts here
                var googleSheets = await googleSheetApi.GetSpreadsheets(defaultGoogleAuthToken);

                Assert.IsNotNull(googleSheets.FirstOrDefault(x => x.Value == newSpeadsheetName), "New created spreadsheet was not found into existing google files.");
                var spreadSheeturl = googleSheets.FirstOrDefault(x => x.Value == newSpeadsheetName).Key;

                //find spreadsheet
                var worksheets = await googleSheetApi.GetWorksheets(spreadSheeturl, defaultGoogleAuthToken);
                Assert.IsNotNull(worksheets.FirstOrDefault(x => x.Value == "Sheet1"), "Worksheet was not found into newly created google excel file.");
                var worksheetUri = worksheets.FirstOrDefault(x => x.Value == "Sheet1").Key;
                var dataRows = await googleSheetApi.GetData(spreadSheeturl, worksheetUri, defaultGoogleAuthToken);

                //file should contain 11 envelopes saved
                var numberOfEnvelopes = dataRows.ToList().Count();
                Assert.AreNotEqual(0, numberOfEnvelopes, "Failed to read any envelope data from excel rows. Run method may failed to write data into excel file");
                //Assert.AreEqual(6, numberOfEnvelopes, "Number of read rows/envelopes was not in the correct count");
            }
            finally
            {
                //cleanup. erase the sheet
                await googleSheetApi.DeleteSpreadSheet(spreadsheetId, defaultGoogleAuthToken);
            }
            
        }
    }
}
