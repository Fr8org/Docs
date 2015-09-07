﻿/// <reference path="../_all.ts" />

module dockyard.interfaces {

    export interface IOperator {
        text: string,
        value: string
    }

    export interface IQueryBuilderWidgetScope extends ng.IScope {
        operators: Array<IOperator>,
        fields: Array<model.Field>,
        rows: Array<model.Condition>,
        defaultOperator: string,
        isActionValid: (action: interfaces.IActionVM) => boolean;
        addRow: () => void,
        removeRow: (index: number) => void,
        valueChanged: (row: model.Condition) => void
    }

}
 