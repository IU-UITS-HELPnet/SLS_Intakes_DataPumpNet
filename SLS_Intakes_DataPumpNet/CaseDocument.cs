//------------------------------------------------------------------------------
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
    using System.Collections.Generic;
    
    public partial class CaseDocument
    {
        public int CaseDocumentId { get; set; }
        public int CaseId { get; set; }
        public string Filename { get; set; }
        public System.DateTime CreationDate { get; set; }
        public System.Guid CreatedById { get; set; }
        public bool IsWorldox { get; set; }
        public string FileType { get; set; }
    
        public virtual Case Case { get; set; }
    }
}
