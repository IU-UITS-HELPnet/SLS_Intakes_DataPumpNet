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
    
    public partial class Telephone
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public Telephone()
        {
            this.CaseParties = new HashSet<CaseParty>();
        }
    
        public int TelephoneId { get; set; }
        public string Type { get; set; }
        public Nullable<int> CaseId { get; set; }
        public string Number { get; set; }
    
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<CaseParty> CaseParties { get; set; }
        public virtual Case Case { get; set; }
    }
}
