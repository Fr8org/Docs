﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fr8Data.Constants;
using Fr8Data.Crates;
using Fr8Data.DataTransferObjects;
using Fr8Data.DataTransferObjects.Helpers;
using Fr8Data.Helpers;
using Fr8Data.Manifests;
using Fr8Data.States;
using TerminalBase.Errors;
using TerminalBase.Services;

namespace TerminalBase.BaseClasses
{
    public abstract class EnhancedTerminalActivity<T> : BaseTerminalActivity
       where T : StandardConfigurationControlsCM
    {

        /**********************************************************************************/

        protected T ActivityUI { get; private set; }
        protected UiBuilder UiBuilder { get; private set; }

        /**********************************************************************************/
        // Functions
        /**********************************************************************************/
        protected EnhancedTerminalActivity(bool isAuthenticationRequired) : base(isAuthenticationRequired)
        {
            UiBuilder = new UiBuilder();
        }

        /**********************************************************************************/

        public sealed override async Task Initialize()
        {
            ActivityUI = CrateActivityUI();
            Storage.Clear();
            Storage.Add(Crate.FromContent(ConfigurationControlsLabel, ActivityUI, AvailabilityType.Configuration));
            await InitializeETA();
            SyncConfControlsBack();
        }

        /**********************************************************************************/

        public sealed override async Task FollowUp()
        {
            SyncConfControls();
            await ConfigureETA();
            SyncConfControlsBack();
        }

        protected sealed override async Task<bool> Validate()
        {
            SyncConfControls();
            return await ValidateETA();
        }

        /**********************************************************************************/

        public sealed override async Task Activate()
        {
            SyncConfControls();
            await ActivateETA();
        }

        /**********************************************************************************/

        public sealed override async Task Deactivate()
        {
            SyncConfControls();
            await DeactivateETA();
        }


        /**********************************************************************************/

        public sealed override async Task Run()
        {
            SyncConfControls();
            await RunETA();
        }
        
        /**********************************************************************************/

        protected T AssignNamesForUnnamedControls(T configurationControls)
        {
            int controlId = 0;
            var controls = configurationControls.EnumerateControlsDefinitions();

            foreach (var controlDefinition in controls)
            {
                if (string.IsNullOrWhiteSpace(controlDefinition.Name))
                {
                    controlDefinition.Name = controlDefinition.GetType().Name + controlId++;
                }
            }

            return configurationControls;
        }
        
        /**********************************************************************************/

        protected virtual T CrateActivityUI()
        {
            var uiBuilderConstructor = typeof(T).GetConstructor(new[] { typeof(UiBuilder) });

            if (uiBuilderConstructor != null)
            {
                return AssignNamesForUnnamedControls((T)uiBuilderConstructor.Invoke(new object[] { UiBuilder }));
            }

            var defaultConstructor = typeof(T).GetConstructor(new Type[0]);

            if (defaultConstructor == null)
            {
                throw new InvalidOperationException($"Unable to find default constructor or constructor accepting UiBuilder for type {typeof(T).FullName}");
            }

            return AssignNamesForUnnamedControls((T)defaultConstructor.Invoke(null));
        }

        protected abstract Task InitializeETA();
        protected abstract Task ConfigureETA();
        protected abstract Task RunETA();


        /**********************************************************************************/



        /**********************************************************************************/

        protected virtual Task<bool> ValidateETA()
        {
            return Task.FromResult(true);
        }

        /**********************************************************************************/

        protected virtual Task ActivateETA()
        {
            return Task.FromResult(0);
        }

        /**********************************************************************************/

        protected virtual Task DeactivateETA()
        {
            return Task.FromResult(0);
        }

        /**********************************************************************************/

        private const string ConfigurationValuesCrateLabel = "Configuration Values";
        /// <summary>
        /// Get or sets value of configuration field with the given key stored in current activity storage
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        protected string this[string key]
        {
            get
            {
                CheckCurrentActivityStorageAvailability();
                var crate = Storage.FirstCrateOrDefault<FieldDescriptionsCM>(x => x.Label == ConfigurationValuesCrateLabel);
                return crate?.Content.Fields.FirstOrDefault(x => x.Key == key)?.Value;
            }
            set
            {
                CheckCurrentActivityStorageAvailability();
                var crate = Storage.FirstCrateOrDefault<FieldDescriptionsCM>(x => x.Label == ConfigurationValuesCrateLabel);
                if (crate == null)
                {
                    crate = Crate<FieldDescriptionsCM>.FromContent(ConfigurationValuesCrateLabel, new FieldDescriptionsCM(), AvailabilityType.Configuration);
                    Storage.Add(crate);
                }
                var field = crate.Content.Fields.FirstOrDefault(x => x.Key == key);
                if (field == null)
                {
                    field = new FieldDTO(key, AvailabilityType.Configuration);
                    crate.Content.Fields.Add(field);
                }
                field.Value = value;
                Storage.ReplaceByLabel(crate);
            }
        }

        private void CheckCurrentActivityStorageAvailability()
        {
            if (Storage == null)
            {
                throw new ApplicationException("Current activity storage is not available");
            }
        }

        /**********************************************************************************/
        // SyncConfControls and SyncConfControlsBack are pair of methods that serves the following reason:
        // We want to work with StandardConfigurationControlsCM in form of ActivityUi that has handy properties to directly access certain controls
        // But when we deserialize activity's crate storage we get StandardConfigurationControlsCM. So we need a way to 'convert' StandardConfigurationControlsCM
        // from crate storage to ActivityUI.
        // SyncConfControls takes properties of controls in StandardConfigurationControlsCM from activity's storage and copies them into ActivityUi.
        private void SyncConfControls()
        {
            if (ConfigurationControls == null)
            {
                throw new InvalidOperationException("Configuration controls crate is missing");
            }

            ActivityUI = CrateActivityUI();
            ActivityUI.SyncWith(ConfigurationControls);
            
            if (ConfigurationControls.Controls != null)
            {
                var dynamicControlsCollection = Fr8ReflectionHelper.GetMembers(ConfigurationControls.GetType()).Where(x => x.CanRead && x.GetCustomAttribute<DynamicControlsAttribute>() != null && CheckIfMemberIsControlsCollection(x)).ToDictionary(x => x.Name, x => x);

                if (dynamicControlsCollection.Count > 0)
                {
                    foreach (var control in ConfigurationControls.Controls)
                    {
                        if (string.IsNullOrWhiteSpace(control.Name))
                        {
                            continue;
                        }

                        var delim = control.Name.IndexOf('_');

                        if (delim <= 0)
                        {
                            continue;
                        }

                        var prefix = control.Name.Substring(0, delim);
                        IMemberAccessor member;

                        if (!dynamicControlsCollection.TryGetValue(prefix, out member))
                        {
                            continue;
                        }

                        var controlsCollection = (IList)member.GetValue(ConfigurationControls);

                        if (controlsCollection == null && (!member.CanWrite || member.MemberType.IsAbstract || member.MemberType.IsInterface))
                        {
                            continue;
                        }

                        if (controlsCollection == null)
                        {
                            controlsCollection = (IList)Activator.CreateInstance(member.MemberType);
                            member.SetValue(ConfigurationControls, controlsCollection);
                        }

                        control.Name = control.Name.Substring(delim + 1);
                        controlsCollection.Add(control);
                    }
                }
            }
        }

        /**********************************************************************************/

        private static bool CheckIfMemberIsControlsCollection(IMemberAccessor member)
        {
            if (member.MemberType.IsInterface && CheckIfTypeIsControlsCollection(member.MemberType))
            {
                return true;
            }

            foreach (var @interface in member.MemberType.GetInterfaces())
            {
                if (CheckIfTypeIsControlsCollection(@interface))
                {
                    return true;
                }
            }

            return false;
        }

        /**********************************************************************************/

        private static bool CheckIfTypeIsControlsCollection(Type type)
        {
            if (type.IsGenericType)
            {
                var genericTypeDef = type.GetGenericTypeDefinition();

                if (typeof(IList<>) == genericTypeDef)
                {
                    if (typeof(IControlDefinition).IsAssignableFrom(type.GetGenericArguments()[0]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        
        /**********************************************************************************/
        // Activity logic can update state of configuration controls. But as long we have copied  configuration controls crate from the storage into 
        // new instance of ActivityUi changes made to ActivityUi won't be reflected in storage.
        // we have to persist in storage changes we've made to ActivityUi.
        // we do this in the most simple way by replacing old StandardConfigurationControlsCM with ActivityUi.
        private void SyncConfControlsBack()
        {
            Storage.Remove<StandardConfigurationControlsCM>();
            // we create new StandardConfigurationControlsCM with controls from ActivityUi.
            // We do this because ActivityUi can has properties to access specific controls. We don't want those propeties exist in serialized crate.

            var configurationControlsToAdd = new StandardConfigurationControlsCM(ActivityUI.Controls);
            Storage.Add(Crate.FromContent(ConfigurationControlsLabel, configurationControlsToAdd, AvailabilityType.Configuration));

            int insertIndex = 0;

            foreach (var member in Fr8ReflectionHelper.GetMembers(ConfigurationControls.GetType()).Where(x => x.CanRead))
            {
                if (member.GetCustomAttribute<DynamicControlsAttribute>() != null && CheckIfMemberIsControlsCollection(member))
                {
                    var collection = member.GetValue(ConfigurationControls) as IList;

                    if (collection != null)
                    {
                        for (int index = 0; index < collection.Count; index++)
                        {
                            var control = collection[index] as ControlDefinitionDTO;

                            if (control != null)
                            {
                                control.Name = member.Name + "_" + control.Name;
                                configurationControlsToAdd.Controls.Insert(insertIndex, control);
                                insertIndex++;
                            }
                        }
                    }
                }

                var controlDef = member.GetValue(ConfigurationControls) as IControlDefinition;
                if (!string.IsNullOrWhiteSpace(controlDef?.Name))
                {
                    for (int i = 0; i < configurationControlsToAdd.Controls.Count; i++)
                    {
                        if (configurationControlsToAdd.Controls[i].Name == controlDef.Name)
                        {
                            insertIndex = i + 1;
                            break;
                        }
                    }
                }
            }
        }
    }
}
