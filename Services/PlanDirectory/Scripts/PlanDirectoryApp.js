﻿angular.module('PlanDirectoryApp', ['ui.bootstrap'])
    .controller('SearchController', [
        '$scope',
        '$http',
        '$q',
        '$uibModal',
        'urlPrefix',
        '$location',
        function ($scope, $http, $q, $uibModal, urlPrefix, $location) {
            $scope.searchForm = {
                searchText: $location.search().planSearch ? $location.search().planSearch : ''
            };

            $scope.pageSize = 5;
            $scope.totalCount = 0;
            $scope.planTemplates = [];
            $scope.currentPage = 1;
            $scope.pages = [];

            var doSearch = function (pageStart) {
                var url = urlPrefix + '/api/plan_templates/search'
                    + '?text=' + $scope.searchForm.searchText
                    + '&pageStart=' + pageStart
                    + '&pageSize=' + $scope.pageSize;

                Metronic.blockUI({ animate: true });

                var promise = $q(function (resolve, reject) {
                    $http.get(url)
                        .then(function (res) {
                            $scope.totalCount = res.data.TotalCount;
                            $scope.planTemplates = res.data.PlanTemplates;

                            $scope.currentPage = pageStart;
                            $scope.pages = [];
                            for (var i = 0; i < Math.ceil(res.data.TotalCount / $scope.pageSize) ; ++i) {
                                $scope.pages.push(i + 1);
                            }

                            resolve();
                        })
                        .catch(function (err) {
                            reject(err);
                        })
                        .finally(function () {
                            Metronic.unblockUI();
                        });
                });

                return promise;
            };

            var checkAuthentication = function () {
                var url = urlPrefix + '/api/authentication/is_authenticated';

                var promise = $q(function (resolve, reject) {
                    $http.get(url)
                        .then(function (res) {
                            resolve(res.data.authenticated);
                        })
                        .catch(function (err) {
                            reject(err);
                        });
                });

                return promise;
            };

            var checkPrivileged = function () {
                var url = urlPrefix + '/api/authentication/is_privileged';

                var promise = $q(function (resolve, reject) {
                    $http.get(url)
                        .then(function (res) {
                            resolve(res.data.privileged);
                        })
                        .catch(function (err) {
                            reject(err);
                        });
                });

                return promise;
            };

            $scope.submitSearch = function (pageStart) {
                doSearch(pageStart);
            };

            $scope.createPlan = function (planTemplate) {
                Metronic.blockUI({ animate: true });

                checkAuthentication()
                    .then(function (authenticated) {
                        if (!authenticated) {
                            $uibModal.open({
                                templateUrl: '/AuthenticateDialog.html',
                                controller: 'AuthenticateDialogController'
                            });

                            Metronic.unblockUI();
                        }
                        else {
                            var url = urlPrefix + '/api/plan_templates/createplan/?id=' + planTemplate.ParentPlanId;
                            $http.post(url, null)
                                .then(function (data) {
                                    Metronic.unblockUI();
                                    window.location.href = data.data.RedirectUrl;
                                });
                        }
                    })
                    .finally(function () {
                        Metronic.unblockUI();
                    });
            };

            $scope.removePlan = function (planTemplate) {
                Metronic.blockUI({ animate: true });

                var url = urlPrefix + '/api/plan_templates/?id=' + planTemplate.ParentPlanId;
                $http.delete(url)
                    .then(function (data) {
                        Metronic.unblockUI();
                        doSearch($scope.currentPage);
                    });
            };

            $scope.generatePages = function() {
                var url = urlPrefix + '/api/plan_templates/generatepages';
                $http.post(url, null);
            }

            checkPrivileged()
                .then(function (privileged) {
                    $scope.privileged = privileged;
                });
            doSearch(1);
        }
    ])
    .controller('AuthenticateDialogController', [
        '$scope',
        function ($scope) {
        }
    ]);
