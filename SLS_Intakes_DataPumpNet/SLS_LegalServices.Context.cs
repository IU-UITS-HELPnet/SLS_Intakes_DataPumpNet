﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace SLS_Intakes_DataPumpNet
{
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Infrastructure;
    
    public partial class SLS_LegalServicesEntities : DbContext
    {
        public SLS_LegalServicesEntities()
            : base("name=SLS_LegalServicesEntities")
        {
        }
    
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            throw new UnintentionalCodeFirstException();
        }
    
        public virtual DbSet<Address> Addresses { get; set; }
        public virtual DbSet<CaseDocument> CaseDocuments { get; set; }
        public virtual DbSet<CaseNote> CaseNotes { get; set; }
        public virtual DbSet<CaseParty> CaseParties { get; set; }
        public virtual DbSet<Case> Cases { get; set; }
        public virtual DbSet<Email> Emails { get; set; }
        public virtual DbSet<ReferralSource> ReferralSources { get; set; }
        public virtual DbSet<Telephone> Telephones { get; set; }
    }
}