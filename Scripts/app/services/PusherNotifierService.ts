﻿/// <reference path="../_all.ts"/>

module dockyard.services {    

    export interface IPusherNotifierService {
        bindEventToChannel(channel: string, event: string, callback: Function, context?: any): void;
        bindEventToClient(event: string, callback: Function, context?: any): void;
        removeEvent(channel: string, event: string): void;
        removeEventHandler(channel: string, event: string, handler: Function): void;
        removeAllHandlersForContext(channel: string, context: any);
        removeHandlerForAllEvents(channel: string, handler: Function): void;
        removeAllEvents(channel: string): void;
        disconnect(): void;

        frontendEvent(message: string, eventType: dockyard.enums.NotificationType);
        frontendFailure(message: string): void;
        frontendSuccess(message: string): void;
    }

    declare var appKey: string;

    class PusherNotifierService implements IPusherNotifierService {
        private client: pusherjs.pusher.Pusher;
        private pusher: any;
        private timeout: ng.ITimeoutService;

        constructor(private $pusher: any, private UserService: IUserService, $timeout: ng.ITimeoutService) {
            this.client = new Pusher(appKey, { encrypted: true });
            this.pusher = $pusher(this.client);
            this.timeout = $timeout;
        }       

        public bindEventToChannel(channel: string, event: string, callback: Function, context?: any): void {
            channel = this.buildChannelName(channel);
            var channelInstance = this.pusher.subscribe(channel);
            channelInstance.bind(event, callback, context);
        }

        public bindEventToClient(event: string, callback: Function, context?: any): void {
            this.pusher.bind(event, callback, context);
        }

        public removeEvent(channel: string, event: string): void {
            channel = this.buildChannelName(channel);
            var channelInstance = this.pusher.channel(channel);
            if (channelInstance != undefined) {
                channelInstance.unbind(event);
            }
        }

        public removeEventHandler(channel: string, event: string, handler: Function): void {
            channel = this.buildChannelName(channel);
            var channelInstance = this.pusher.channel(channel);
            if (channelInstance != undefined) {
                channelInstance.unbind(event, handler);
            }
        }

        public removeAllHandlersForContext(channel: string, context: any) {
            channel = this.buildChannelName(channel);
            var channelInstance = this.pusher.channel(channel);
            if (channelInstance != undefined) {
                channelInstance.unbind(null, null, context);
            }
        }

        public removeHandlerForAllEvents(channel: string, handler: Function) {
            channel = this.buildChannelName(channel);
            var channelInstance = this.pusher.channel(channel);
            if (channelInstance != undefined) {
                channelInstance.unbind(null, handler);
            }
        }

        public removeAllEvents(channel: string): void {
            channel = this.buildChannelName(channel);
            var channelInstance = this.pusher.channel(channel);
            if (channelInstance != undefined) {
                channelInstance.unbind();
            }
        }

        public disconnect(): void {
            this.pusher.disconnect();
        }

        private buildChannelName(email: string) {
            // If you change the way how channel name is constructed, be sure to change it also
            // in the server-side code (PusherNotifier.cs). 
            // URI encoding is necessary to remove the characters that Pusher does not support for 
            // Channel name. Since it also does not support %, we replace it either. 
            return 'fr8pusher_' + encodeURI(email).replace('%', '=');
        }

        public frontendEvent(message: string, eventType: dockyard.enums.NotificationType) {
            this.UserService.getCurrentUser().$promise.then(data => {

                let channelName = this.buildChannelName(data.id);

                // to use this way we must enable client side notifications in Pusher.com 
                //var channel = this.client.subscribe(channelName);
                //channel.trigger('client-' + eventType, message);

                // this makes me sick but i can`t see other way now except roundabout call server side notification endpoint to trigger frontend, like loop...
                let callback = this.client.channels.channels[channelName].callbacks._callbacks["_" + dockyard.enums.NotificationType[eventType]][0];

                // we don`t want see '$digest already in progress'
                this.timeout(() => { callback.fn(message);},500,true) ;
                
            });
        }

        public frontendFailure(message: string) {
            this.frontendEvent(message, dockyard.enums.NotificationType.GenericFailure);// pusherNotifierFailureEvent);
        }

        public frontendSuccess(message: string) {
            this.frontendEvent(message, dockyard.enums.NotificationType.GenericSuccess);// pusherNotifierSuccessEvent);
        }
    }

    app.factory('PusherNotifierService', ['$pusher', 'UserService','$timeout', ($pusher: any, UserService:IUserService, $timeout:ng.ITimeoutService): IPusherNotifierService =>
        new PusherNotifierService($pusher, UserService, $timeout)
    ]);
}  