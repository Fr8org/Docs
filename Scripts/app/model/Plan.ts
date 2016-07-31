﻿module dockyard.model {
    export class PlanDTO {
        id: string;
        isTempId: boolean;
        name: string;
        tag: string;
        lastUpdated: string;
        description: string;
        planState: PlanState;
        subscribedDocuSignTemplates: Array<string>;
        externalEventSubscription: Array<number>;
        startingSubPlanId: number;
        subPlans: Array<SubPlanDTO>;
        visibility: { hidden: boolean, public: boolean };
        category: string;
    }

    export enum PlanState {
        Inactive = 1,
        Running = 2,
        Saving_Changes = 3
    }

    export enum PlanVisibility {
        Standard = 1,
        Internal = 2
    }

    export class PlanFullDTO {
        plan: PlanDTO
    }

    export class PlanQueryDTO {
        id: string;
        page: number;
        planPerPage: number;
        status:number;
        category: string;
        orderBy: string;
        isDescending: boolean;
        filter: string;
    }

    export class PlanResultDTO {
        plans: Array<interfaces.IPlanVM>;
        currentPage: number;
        totalPlanCount: number;
    }

    // TODO: implement FE PlanTemplateDTO
    //export class PlanTemplateDTO {
    //    id: string;        
    //    name: string;
    //    startingPlanNodeDescriptionId: string;        
    //    description: string;
        
    //    PlanNodeDescriptions: Array<PlanNodeDescriptionDTO>;
    //}
}