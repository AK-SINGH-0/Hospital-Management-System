namespace HospitalSystem.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class SyncDatabase : DbMigration
    {
        public override void Up()
        {
            AlterColumn("dbo.Bills", "Amount", c => c.Decimal(nullable: false, precision: 18, scale: 2));
            AlterColumn("dbo.Departments", "PriceUnit", c => c.Decimal(nullable: false, precision: 18, scale: 2));
        }
        
        public override void Down()
        {
            AlterColumn("dbo.Departments", "PriceUnit", c => c.Double(nullable: false));
            AlterColumn("dbo.Bills", "Amount", c => c.Double(nullable: false));
        }
    }
}
