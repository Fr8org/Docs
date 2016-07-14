﻿using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fr8.Infrastructure.Data.Control;
using Fr8.Infrastructure.Data.Crates;
using Fr8.Infrastructure.Data.DataTransferObjects;
using Fr8.Infrastructure.Data.Managers;
using Fr8.Infrastructure.Data.Manifests;
using Fr8.Infrastructure.Data.States;
using Fr8.TerminalBase.Models;
using Salesforce.Force;
using terminalSalesforce.Infrastructure;
using Salesforce.Common.Models;
using Salesforce.Common;
using Salesforce.Chatter;
using Newtonsoft.Json.Linq;
using Salesforce.Chatter.Models;
using Fr8.TerminalBase.Interfaces;
using Fr8.Infrastructure.Utilities;
using Newtonsoft.Json;
using AutoMapper;

namespace terminalSalesforce.Services
{
    public class SalesforceManager : ISalesforceManager
    {
        private readonly Authentication _authentication;
        private readonly ICrateManager _crateManager;
        private readonly IHubCommunicator _hubCommunicator;

        public SalesforceManager(ICrateManager crateManager, Authentication authentication, IHubCommunicator hubCommunicator)
        {
            _crateManager = crateManager;
            _authentication = authentication;
            _hubCommunicator = hubCommunicator;
        }

        public async Task<string> Create(SalesforceObjectType type, IDictionary<string, object> @object, AuthorizationToken authToken)
        {
            var result = await ExecuteClientOperationWithTokenRefresh(CreateForceClient, x => x.CreateAsync(type.ToString(), @object), authToken);
            return result?.Id ?? string.Empty;
        }

        public async Task<StandardTableDataCM> Query(SalesforceObjectType type, IEnumerable<FieldDTO> propertiesToRetrieve, string filter, AuthorizationToken authToken)
        {
            var whatToSelect = propertiesToRetrieve.Select(p => p.Name).ToArray();
            var selectQuery = $"SELECT {(whatToSelect.Length == 0 ? "*" : string.Join(", ", whatToSelect))} FROM {type}";
            if (!string.IsNullOrEmpty(filter))
            {
                selectQuery += " WHERE " + NormalizeFilterByFiedType(propertiesToRetrieve, filter);
            }
            var result = await ExecuteClientOperationWithTokenRefresh(CreateForceClient, x => x.QueryAsync<object>(selectQuery), authToken);
            var table = ParseQueryResult(result);
            table.FirstRowHeaders = true;
            var headerRow = whatToSelect.Length > 0
                                ? whatToSelect.Select(x => new TableCellDTO { Cell = new KeyValueDTO(x, x)}).ToList()
                                : (await GetProperties(type, authToken)).Select(x => new TableCellDTO { Cell = new KeyValueDTO(x.Name, x.Label) }).ToList();
            table.Table.Insert(0, new TableRowDTO { Row = headerRow });
            return table;
        }

        public async Task<List<FieldDTO>> GetProperties(SalesforceObjectType type, AuthorizationToken authToken, bool updatableOnly = false, string label = null)
        {
            var responce = (JObject)await ExecuteClientOperationWithTokenRefresh(CreateForceClient, x => x.DescribeAsync<object>(type.ToString()), authToken);
            var objectFields = new List<FieldDTO>();
            JToken resultFields;
            if (responce.TryGetValue("fields", out resultFields) && resultFields is JArray)
            {
                if (updatableOnly)
                {
                    resultFields = new JArray(resultFields.Where(fieldDescription => (fieldDescription.Value<bool>("updateable") == true)));
                }

                var fields = resultFields
                    .Select(fieldDescription =>
                        /*
                        Select Fields as FieldDTOs with                                    

                        Key -> Field Name
                        Value -> Field Lable
                        AvailabilityType -> Run Time
                        FieldType -> Field Type

                        IsRequired -> The Field is required when ALL the below conditions are true.
                            nillable            = false, Meaning, the field must have a valid value. The field's value should not be NULL or NILL or Empty
                            defaultedOnCreate   = false, Meaning, Salesforce itself does not assign default value for this field when object is created (ex. ID)
                            updateable          = true,  Meaning, The filed's value must be updatable by the user. 
                                                                User must be able to set or modify the value of this field.
                        */
                        new FieldDTO(fieldDescription.Value<string>("name"))
                        {
                            FieldType = ExtractFieldType(fieldDescription.Value<string>("type")),

                            IsRequired = fieldDescription.Value<bool>("nillable") == false &&
                                            fieldDescription.Value<bool>("defaultedOnCreate") == false &&
                                            fieldDescription.Value<bool>("updateable") == true,
                            Availability = AvailabilityType.RunTime,
                            Label = fieldDescription.Value<string>("label"),
                            Data = ExtractFieldData(fieldDescription.ToObject<JObject>()),
                            SourceCrateLabel = label
                        }
                    )
                    .OrderBy(field => field.Name);

                objectFields.AddRange(fields);
            }
            return objectFields;
        }

        private string ExtractFieldType(string salesforceFieldType)
        {
            switch (salesforceFieldType)
            {
                case "picklist":
                    return FieldType.PickList;
                case "date":
                case "datetime":
                    return FieldType.Date;
                default:
                    return FieldType.String;
            }
        }

        private Dictionary<string, JToken> ExtractFieldData(JObject obj)
        {
            JToken pickListValuesToken;
            if (obj.TryGetValue("picklistValues", out pickListValuesToken)
                && pickListValuesToken is JArray)
            {
                var pickListValues = (JArray)pickListValuesToken;
                var fields = pickListValues
                    .Select(x => new JObject(
                        new JProperty("key", x.Value<string>("label")),
                        new JProperty("value", x.Value<string>("value"))
                    ));

                var dataDict = new Dictionary<string, JToken>();
                dataDict.Add(FieldDTO.Data_AllowableValues, new JArray(fields));

                return dataDict;
            }

            return null;
        }
        
        public T CreateSalesforceDTO<T>(ActivityPayload curActivity, PayloadDTO curPayload) where T : new()
        {
            var requiredType = typeof(T);
            var requiredObject = (T)Activator.CreateInstance(requiredType);
            var requiredProperties = requiredType.GetProperties().Where(p => !p.Name.Equals("Id"));

            var designTimeCrateStorage = curActivity.CrateStorage;
            var runTimeCrateStorage = _crateManager.FromDto(curPayload.CrateStorage);
            var controls = designTimeCrateStorage.CrateContentsOfType<StandardConfigurationControlsCM>().FirstOrDefault();

            if (controls == null)
            {
                throw new InvalidOperationException("Failed to find configuration controls crate");
            }

            requiredProperties.ToList().ForEach(prop =>
            {
                try
                {
                    var textSourceControl = controls.Controls.SingleOrDefault(c => c.Name == prop.Name) as TextSource;

                    if (textSourceControl == null)
                    {
                        throw new InvalidOperationException($"Unable to find TextSource control with name '{prop.Name}'");
                    }

                    var propValue = textSourceControl.GetValue(runTimeCrateStorage);
                    prop.SetValue(requiredObject, propValue);
                }
                catch (ApplicationException applicationException)
                {
                    //If it can not extract the property, user did not enter any value for this property.
                    //No problems. We can treat that value as empty and continue.
                    if (applicationException.Message.Equals("Could not extract recipient, unknown recipient mode."))
                    {
                        prop.SetValue(requiredObject, string.Empty);
                    }
                    else if (applicationException.Message.StartsWith("No field found with specified key:"))
                    {
                        //FR-2502 - This else case handles, the user asked to pick up the value from the current payload.
                        //But the payload does not contain the value of this property. In that case, set it as "Not Available"
                        prop.SetValue(requiredObject, "Not Available");
                    }
                }
            });

            return requiredObject;
        }
        public async Task<string> PostToChatter(string message, string parentObjectId, AuthorizationToken authToken)
        {
            var currentChatterUser = await ExecuteClientOperationWithTokenRefresh(CreateChatterClient, x => x.MeAsync<UserDetail>(), authToken);
            //set the message segment with the given feed text
            var messageSegment = new MessageSegmentInput
            {
                Text = message,
                Type = "Text"
            };
            //prepare the body
            var body = new MessageBodyInput { MessageSegments = new List<MessageSegmentInput> { messageSegment } };
            //prepare feed item input by setting the given parent object id
            var feedItemInput = new FeedItemInput()
            {
                Attachment = null,
                Body = body,
                SubjectId = parentObjectId,
                FeedElementType = "FeedItem"
            };
            var feedItem = await ExecuteClientOperationWithTokenRefresh(CreateChatterClient, x => x.PostFeedItemAsync<FeedItem>(feedItemInput, currentChatterUser.id), authToken);
            return feedItem?.Id ?? string.Empty;
        }

        public IEnumerable<FieldDTO> GetSalesforceObjectTypes(SalesforceObjectOperations filterByOperations = SalesforceObjectOperations.None, SalesforceObjectProperties filterByProperties = SalesforceObjectProperties.None)
        {
            var salesforceTypes = Enum.GetValues(typeof(SalesforceObjectType));
            foreach (var salesforceType in salesforceTypes)
            {
                var sourceValues = salesforceType.GetType().GetField(salesforceType.ToString()).GetCustomAttributes<SalesforceObjectDescriptionAttribute>().FirstOrDefault();
                if (sourceValues.AvailableProperties.HasFlag(filterByProperties) && sourceValues.AvailableOperations.HasFlag(filterByOperations))
                {
                    yield return new FieldDTO(salesforceType.ToString());
                }
            }
        }

        public IEnumerable<FieldDTO> GetObjectProperties()
        {
            return objectDescriptions ?? (objectDescriptions = new FieldDTO[]
                                                {
                                                    new FieldDTO("Account"),
                                                    new FieldDTO("Case"),
                                                    new FieldDTO("Contact"),
                                                    new FieldDTO("Contract"),
                                                    new FieldDTO("Document"),
                                                    new FieldDTO("Group"),
                                                    new FieldDTO("Lead"),
                                                    new FieldDTO("Opportunity"),
                                                    new FieldDTO("Order"),
                                                    new FieldDTO("Product2"),
                                                    new FieldDTO("Solution"),
                                                    new FieldDTO("User")
                                                });
        }

        public async Task<bool> Delete(SalesforceObjectType objectType, string objectId, AuthorizationToken authToken)
        {
            return await ExecuteClientOperationWithTokenRefresh(CreateForceClient, x => x.DeleteAsync(objectType.ToString(), objectId), authToken);
        }
        [Obsolete("Use Task<StandardTableDataCM> Query(SalesforceObjectType, IEnumerable<string>, string, AuthorizationTokenDO) instead")]
        public async Task<IList<KeyValueDTO>> GetUsersAndGroups(AuthorizationToken authToken)
        {
            var chatterObjectSelectPredicate = new Dictionary<string, Func<JToken, KeyValueDTO>>();
            chatterObjectSelectPredicate.Add("groups", x => new KeyValueDTO(x.Value<string>("name"), x.Value<string>("id")));
            chatterObjectSelectPredicate.Add("users", x => new KeyValueDTO(x.Value<string>("displayName"), x.Value<string>("id")));
            var chatterNamesList = new List<KeyValueDTO>();
            //get chatter groups and persons
            var chatterObjects = (JObject)await ExecuteClientOperationWithTokenRefresh(CreateChatterClient, x => x.GetGroupsAsync<object>(), authToken);
            chatterObjects.Merge((JObject)await ExecuteClientOperationWithTokenRefresh(CreateChatterClient, x => x.GetUsersAsync<object>(), authToken));
            foreach (var predicatePair in chatterObjectSelectPredicate)
            {
                JToken requiredChatterObjects;
                if (chatterObjects.TryGetValue(predicatePair.Key, out requiredChatterObjects) && requiredChatterObjects is JArray)
                {
                    chatterNamesList.AddRange(requiredChatterObjects.Select(a => predicatePair.Value(a)));
                }
            }
            return chatterNamesList;
        }

        #region Implemetation details

        private string NormalizeFilterByFiedType(IEnumerable<FieldDTO> fields, string filter)
        {
            //split the filter by " AND "
            var filterList = filter.Split(new string[] { " AND " }, StringSplitOptions.None).ToList();

            foreach (FieldDTO field in fields)
            {
                //if filed exists in the filter list and field type is Date, normalize it to Salesforce expected date.
                if (filterList.Any(filterItem => filterItem.Contains(field.Name) && field.FieldType.Equals(FieldType.Date)))
                {
                    var matchedFilter = filterList.Single(stringToCheck => stringToCheck.Contains(field.Name));
                    if (!string.IsNullOrEmpty(matchedFilter))
                    {
                        var requiredDateFormat = matchedFilter.Substring(matchedFilter.IndexOf("'")).Replace("'", "");

                        var normalizedValue = matchedFilter.Substring(0, matchedFilter.IndexOf("'")) + 
                                           DateTime.Parse(requiredDateFormat).ToString("yyyy-MM-ddTHH:mm:ssZ");

                        filterList[filterList.IndexOf(matchedFilter)] = normalizedValue;
                    }
                }
            }

            return string.Join(" AND ", filterList.ToArray()); ;
        }

        private StandardTableDataCM ParseQueryResult(QueryResult<object> queryResult)
        {
            var parsedObjects = new List<JObject>();
            if (queryResult.Records.Count > 0)
            {
                parsedObjects = queryResult.Records.Select(record => ((JObject)record)).ToList();
            }
            return new StandardTableDataCM
            {
                Table = parsedObjects.Select(x => x.Properties()
                                            .Where(y => y.Value.Type == JTokenType.String && !string.IsNullOrEmpty(y.Value.Value<string>()))
                                            .Select(y => new TableCellDTO
                                            { Cell = new KeyValueDTO
                                                {
                                                    Key = y.Name,
                                                    Value = y.Value.Value<string>()
                                                }
                                            }))
                                     .Select(x => new TableRowDTO { Row = x.ToList() })
                                     .ToList()
            };
        }

        private async Task<TResult> ExecuteClientOperationWithTokenRefresh<TClient, TResult>(
            Func<AuthorizationToken, bool, Task<TClient>> clientProvider,
            Func<TClient, Task<TResult>> operation,
            AuthorizationToken authToken)
        {
            var client = await clientProvider(authToken, false);
            var retried = false;
            Execution:
            try
            {
                return await operation(client);
            }
            catch (ForceException ex)
            {
                if (ex.Message.Contains("Session expired or invalid", StringComparison.InvariantCulture) && !retried)
                {
                    retried = true;
                    client = await clientProvider(authToken, true);
                    goto Execution;
                }
                else
                {
                    throw;
                }
            }
        }

        private async Task<ForceClient> CreateForceClient(AuthorizationToken authToken, bool isRefreshTokenRequired = false)
        {
            if (isRefreshTokenRequired)
            {
                authToken = await RefreshToken(authToken);                
            }
            var salesforceToken = ToSalesforceToken(authToken);
            return new ForceClient(salesforceToken.InstanceUrl, salesforceToken.Token, salesforceToken.ApiVersion);
        }

        private async Task<ChatterClient> CreateChatterClient(AuthorizationToken authToken, bool isRefreshTokenRequired = false)
        {
            if (isRefreshTokenRequired)
            {
                authToken = await RefreshToken(authToken);
            }
            var salesforceToken = ToSalesforceToken(authToken);

            // When debugging, decimal point gets messed up and Salesforce client rejects to work properly.
            // var ci = new System.Globalization.CultureInfo(System.Globalization.CultureInfo.CurrentCulture.Name);
            // if (ci.NumberFormat.NumberDecimalSeparator != ".")
            // {
            //     ci.NumberFormat.NumberDecimalSeparator = ".";
            //     System.Threading.Thread.CurrentThread.CurrentCulture = ci;
            // }

            return new ChatterClient(salesforceToken.InstanceUrl, salesforceToken.Token, salesforceToken.ApiVersion);
        }

        private async Task<AuthorizationToken> RefreshToken(AuthorizationToken token)
        {
            token = await _authentication.RefreshAccessToken(token);
            await _hubCommunicator.RenewToken(Mapper.Map<AuthorizationTokenDTO>(token));
            return token;
        }

        private IEnumerable<FieldDTO> objectDescriptions;

        private SalesforceAuthToken ToSalesforceToken(AuthorizationToken ourToken)
        {
            var startIndexOfInstanceUrl = ourToken.AdditionalAttributes.IndexOf("instance_url");
            var startIndexOfApiVersion = ourToken.AdditionalAttributes.IndexOf("api_version");
            var instanceUrl = ourToken.AdditionalAttributes.Substring(startIndexOfInstanceUrl, (startIndexOfApiVersion - 1 - startIndexOfInstanceUrl));
            var apiVersion = ourToken.AdditionalAttributes.Substring(startIndexOfApiVersion, ourToken.AdditionalAttributes.Length - startIndexOfApiVersion);
            instanceUrl = instanceUrl.Replace("instance_url=", "");
            apiVersion = apiVersion.Replace("api_version=", "");
            return new SalesforceAuthToken { ApiVersion = apiVersion, InstanceUrl = instanceUrl, Token = JsonConvert.DeserializeObject<dynamic>(ourToken.Token).AccessToken };
        }

        private struct SalesforceAuthToken
        {
            public string Token { get; set; }

            public string InstanceUrl { get; set; }

            public string ApiVersion { get; set; }
        }

        #endregion
    }
}