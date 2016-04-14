namespace Data.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddSalesforceMMSolution : DbMigration
    {
        public override void Up()
        {
            Sql(@"
MERGE ActivityTemplate AS Target
USING (SELECT 'MailMergeFromSalesforce' as Name) AS SOURCE
ON (Target.Name = Source.Name)
WHEN MATCHED THEN
	UPDATE SET Target.ActivityTemplateState = 1		
WHEN NOT MATCHED BY TARGET THEN
	INSERT (
		Name, 
		Version, 
		TerminalId, 
		Category,
		LastUpdated,
		CreateDate,
		Label,
		MinPaneWidth,
		Tags,
		WebServiceId,
		ActivityTemplateState,
		Description,
		Type,
		NeedsAuthentication,
		ClientVisibility)
	VALUES (
		'MailMergeFromSalesforce', 
		'1', 
		3,
		5,
		GETDATE(),
		GETDATE(),
		'Mail Merge from Salesforce',
		550,
		'UsesReconfigureList',
		7,
		1,
		'Retrieves specified data from Salesforce and process this data using specified email sender',
		1,
		1,
		1);");
            Sql("UPDATE ActivityTemplate SET Tags = 'AggressiveReload,Email Deliverer' WHERE Name = 'Send_DocuSign_Envelope'");
            Sql("UPDATE ActivityTemplate SET Tags = 'Notifier,Email Deliverer' WHERE Name = 'SendEmailViaSendGrid'");
        }
        
        public override void Down()
        {
            Sql("DELETE FROM ActivityTemplate WHERE Name = 'MailMergeFromSalesforce'");
            Sql("UPDATE ActivityTemplate SET Tags = 'AggressiveReload' WHERE Name = 'Send_DocuSign_Envelope'");
            Sql("UPDATE ActivityTemplate SET Tags = 'Notifier' WHERE Name = 'SendEmailViaSendGrid'");
        }
    }
}
