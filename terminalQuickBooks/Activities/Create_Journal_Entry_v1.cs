﻿using System.Linq;
using System.Threading.Tasks;
using Data.Crates;
using Data.Interfaces.DataTransferObjects;
using Data.Interfaces.Manifests;
using Data.States;
using TerminalBase.BaseClasses;
using StructureMap;
using terminalQuickBooks.Interfaces;

namespace terminalQuickBooks.Actions
{
    public class Create_Journal_Entry_v1 : BaseQuickbooksTerminalActivity<Create_Journal_Entry_v1.ActivityUi>
    {
        public class ActivityUi : StandardConfigurationControlsCM { }

        private readonly IJournalEntry _journalEntry;
        public Create_Journal_Entry_v1()
        {
            _journalEntry = ObjectFactory.GetInstance<IJournalEntry>();
        }

        protected override async Task Initialize(RuntimeCrateManager runtimeCrateManager)
        {
            //get StandardAccountingTransactionCM
            var upstreamCrates = await GetCratesByDirection<StandardAccountingTransactionCM>(
                CurrentActivity,
                CrateDirection.Upstream);
            if (upstreamCrates.Count == 0)
            {
                CurrentPayloadStorage.Add(upstreamCrates.First());
            }
            else
            {
                Error(
                    "When this Action runs, it will be expecting to find a Crate of Standard Accounting Transactions. " +
                    "Right now, it doesn't detect any Upstream Actions that produce that kind of Crate. " +
                    "Please add an activity upstream (to the left) of this action that does so.");
            }
        }
        
        protected override Task Configure(RuntimeCrateManager runtimeCrateManager)
        {
            // No extra configuration required
            return Task.FromResult(0);
        }

        protected override async Task RunCurrentActivity()
        {
            //Obtain the crate of type StandardAccountingTransactionCM that holds the required information
            var curStandardAccountingTransactionCM = CurrentPayloadStorage.CratesOfType<StandardAccountingTransactionCM>().Single().Content;
            //Obtain the crate of type OperationalStateCM to extract the correct StandardAccountingTransactionDTO
            var curOperationalStateCM = CurrentPayloadStorage.CratesOfType<OperationalStateCM>().Single().Content;
            //Get the LoopId that is equal to the Action.Id for to obtain the correct StandardAccountingTransactionDTO
            //Validate fields of the StandardAccountingTransactionCM crate
            StandardAccountingTransactionCM.Validate(curStandardAccountingTransactionCM);
            //Get the list of the StandardAccountingTransactionDTO
            var curTransactionList = curStandardAccountingTransactionCM.AccountingTransactions;
            //Get the current index of Accounting Transactions
            var currentIndexOfTransactions = GetLoopIndex(curOperationalStateCM);
            //Take StandardAccountingTransactionDTO from curTransactionList using core function GetCurrentElement
            var curStandardAccountingTransactionDTO = (StandardAccountingTransactionDTO)GetCurrentElement(curTransactionList, currentIndexOfTransactions);
            //Check that all required fields exists in the StandardAccountingTransactionDTO object
            StandardAccountingTransactionCM.ValidateAccountingTransation(curStandardAccountingTransactionDTO);
            //Use service to create Journal Entry Object
            _journalEntry.Create(curStandardAccountingTransactionDTO, GetQuickbooksAuthToken());
        }
    }
}