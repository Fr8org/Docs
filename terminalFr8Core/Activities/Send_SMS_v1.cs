﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Data.Infrastructure;
using Fr8.Infrastructure.Data.Control;
using Fr8.Infrastructure.Data.Crates;
using Fr8.Infrastructure.Data.DataTransferObjects;
using Fr8.Infrastructure.Data.Managers;
using Fr8.Infrastructure.Data.Manifests;
using Fr8.Infrastructure.Data.States;
using Fr8.TerminalBase.BaseClasses;
using PhoneNumbers;
using StructureMap;
using terminalUtilities.Twilio;
using Twilio;
using Fr8.TerminalBase.Infrastructure;

namespace terminalFr8Core.Activities
{
    public class Send_SMS_v1 : TerminalActivity<Send_SMS_v1.ActivityUi>
    {
        public static ActivityTemplateDTO ActivityTemplateDTO = new ActivityTemplateDTO
        {
            Id = new Guid("61774e73-9151-4c58-8a56-dd6653bc2e8c"),
            Name = "Send_SMS",
            Label = "Send SMS",
            Version = "1",
            Category = ActivityCategory.Forwarders,
            NeedsAuthentication = false,
            MinPaneWidth = 400,
            WebService = TerminalData.WebServiceDTO,
            Categories = new[]
            {
                ActivityCategories.Forward,
                new ActivityCategoryDTO(TerminalData.WebServiceDTO.Name, TerminalData.WebServiceDTO.IconPath)
            }
        };
        protected override ActivityTemplateDTO MyTemplate => ActivityTemplateDTO;

        private ITwilioService _twilio;

        public class ActivityUi : StandardConfigurationControlsCM
        {
            public TextSource SmsNumber { get; set; }
            public TextSource SmsBody { get; set; }

            public ActivityUi()
            {
                SmsNumber = new TextSource("SMS Number", string.Empty, nameof(SmsNumber))
                {
                    Source = new FieldSourceDTO
                    {
                        Label = string.Empty,
                        ManifestType = CrateManifestTypes.StandardDesignTimeFields,
                        FilterByTag = string.Empty,
                        RequestUpstream = true
                    }
                };
                SmsNumber.Events.Add(new ControlEvent("onChange", "requestConfig"));

                SmsBody = new TextSource("SMS Body", string.Empty, nameof(SmsBody))
                {
                    Source = new FieldSourceDTO
                    {
                        Label = string.Empty,
                        ManifestType = CrateManifestTypes.StandardDesignTimeFields,
                        FilterByTag = string.Empty,
                        RequestUpstream = true
                    }
                };
                SmsBody.Events.Add(new ControlEvent("onChange", "requestConfig"));

                Controls = new List<ControlDefinitionDTO> { SmsNumber, SmsBody };
            }
        }

        public Send_SMS_v1(ICrateManager crateManager, ITwilioService twilioService)
            : base(crateManager)
        {
            _twilio = twilioService;
        }

        public override async Task Initialize()
        {
        }

        public override async Task FollowUp()
        {
        }

        protected override Task Validate()
        {
            ValidationManager.Reset();
            if (ActivityUI.SmsNumber.HasValue)
            {
                ValidationManager.ValidatePhoneNumber(GeneralisePhoneNumber(ActivityUI.SmsNumber.TextValue), ActivityUI.SmsNumber);
            }
            else
            {
                ValidationManager.SetError("No SMS Number Provided", ActivityUI.SmsNumber);
            }
            return Task.FromResult(0);
        }

        public override async Task Run()
        {
            Message curMessage;
            try
            {
                var smsFieldDTO = ParseSMSNumberAndMsg();
                string smsNumber = smsFieldDTO.Key;
                string smsBody = smsFieldDTO.Value + "\nThis message was generated by Fr8. http://www.fr8.co";

                try
                {
                    curMessage = _twilio.SendSms(smsNumber, smsBody);
                    EventManager.TwilioSMSSent(smsNumber, smsBody);
                    var curFieldDTOList = CreateKeyValuePairList(curMessage);
                    Payload.Add(Crate.FromContent("Message Data", new StandardPayloadDataCM(curFieldDTOList)));
                }
                catch (Exception ex)
                {
                    EventManager.TwilioSMSSendFailure(smsNumber, smsBody, ex.Message);
                    RaiseError( "Twilio Service Failure due to " + ex.Message);
                }
            }
            catch (ArgumentException appEx)
            {
                RaiseError(appEx.Message);
            }
        }

        public KeyValueDTO ParseSMSNumberAndMsg()
        {
            var smsNumber = GeneralisePhoneNumber(ActivityUI.SmsNumber.TextValue.Trim());
            var smsBody = ActivityUI.SmsBody.TextValue;

            return new KeyValueDTO(smsNumber, smsBody);
        }

        private string GeneralisePhoneNumber(string smsNumber)
        {
            PhoneNumberUtil phoneUtil = PhoneNumberUtil.GetInstance();
            smsNumber = new string(smsNumber.Where(s => char.IsDigit(s) || s == '+' || (phoneUtil.IsAlphaNumber(smsNumber) && char.IsLetter(s))).ToArray());
            if (smsNumber.Length == 10 && !smsNumber.Contains("+"))
                smsNumber = "+1" + smsNumber; //we assume that default region is USA
            return smsNumber;
        }

        private List<KeyValueDTO> CreateKeyValuePairList(Message curMessage)
        {
            List<KeyValueDTO> returnList = new List<KeyValueDTO>();
            returnList.Add(new KeyValueDTO("Status", curMessage.Status));
            returnList.Add(new KeyValueDTO("ErrorMessage", curMessage.ErrorMessage));
            returnList.Add(new KeyValueDTO("Body", curMessage.Body));
            returnList.Add(new KeyValueDTO("ToNumber", curMessage.To));
            return returnList;
        }
    }
}