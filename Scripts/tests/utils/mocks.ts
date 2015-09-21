﻿module dockyard.tests.utils {


    //The class contains methods to create mocks for complex objects
    export class Factory {
        //Creates a mock for ProcessBuilderController $scope
        public static GetProcessBuilderScope(rootScope: interfaces.IAppRootScope): dockyard.controllers.IProcessBuilderScope {
            var scope = <dockyard.controllers.IProcessBuilderScope>rootScope.$new();
            scope.processTemplateId = 0;
            scope.processNodeTemplates = null;
            scope.fields = null;

            return scope;
        }
    } 

    /*
        Mock for ActionService
    */
    export class ActionServiceMock {
        constructor($q: ng.IQService) {
            this.save = jasmine.createSpy('save').and.callFake(() => {
                var def: any = $q.defer();
                def.resolve();

                def.promise.$promise = def.promise;

                return def.promise;
            });

            this.get = jasmine.createSpy('get');
        }
        public save: any;
        public get: any;
        
    }


    export class ProcessTemplateServiceMock {
        constructor($q: ng.IQService) {

            this.get = jasmine.createSpy('get').and.callFake(() => {
                var def: any = $q.defer();
                def.resolve(fixtures.ProcessBuilder.newProcessTemplate);
                def.promise.$promise = def.promise;
                return def.promise;
            });
        }
        public save: any;
        public get: any;
        public saveCurrent: any;
    }

    export class ActionListServiceMock {
        constructor($q: ng.IQService) {
            this.byProcessNodeTemplate = jasmine.createSpy('byProcessNodeTemplate').and.callFake(() => {
                /*var def: any = $q.defer();
                def.resolve(fixtures.ProcessBuilder.newActionListDTO);
                def.promise.$promise = def.promise;*/
                return fixtures.ProcessBuilder.newActionListDTO;
            });
        }
        public byProcessNodeTemplate: any;
    }

    export class ProcessBuilderServiceMock {
        constructor($q: ng.IQService) {
            this.save = jasmine.createSpy('save').and.callFake(() => {
                var def: any = $q.defer();
                def.resolve();

                def.promise.$promise = def.promise;

                return def.promise;
            });
            this.saveCurrent = jasmine.createSpy('saveCurrent').and.callFake(() => {
                var def: any = $q.defer();
                def.resolve(fixtures.ProcessBuilder.processBuilderState);
                def.promise.$promise = def.promise;
                return def.promise;
            });
        }
        public save: any;
        public get: any;
        public saveCurrent: any;
    }
}