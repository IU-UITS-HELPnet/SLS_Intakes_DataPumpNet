using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.SqlClient;

namespace SLS_Intakes_DataPumpNet
{
    class Program
    {
        static void Main(string[] args)
        {
            SLS_Intakes_DataPumpEntities db = new SLS_Intakes_DataPumpEntities();
            SLS_LegalServicesEntities db2 = new SLS_LegalServicesEntities();

            #region Copy Intakes from Website
            // Remove all records
            db.Database.ExecuteSqlCommand("TRUNCATE TABLE [ap_form_12828_RAW]");

            // Get MAX ID from Archive
            int maxId = 0;

            List<ap_form_12828_RAW> raws = db.ap_form_12828_RAW.ToList();

            if (raws.Count() > 0)
            {
                maxId = db.ap_form_12828_ARCHIVE.Max(m => m.id);
            }

            // Connect to MySQL
            string machServer = "bl-sser-pweb01.ads.iu.edu"; // 10.56.115.70
            string machPassword = "Guessanumberfrom1toTen";
            string machUser = "machuser";
            string machDB = "machdbp";
            string machConnectionString = String.Format("server={0};uid={1};pwd={2};database={3}", machServer, machUser, machPassword, machDB);

            MySqlConnection conn = new MySqlConnection();
            MySqlCommand cmd;

            try
            {
                conn.ConnectionString = machConnectionString;
                conn.Open();

                // Pull Intakes with ID greater than MAX ID

                string qry = GetIntakesQuery(maxId);
                cmd = new MySqlCommand(qry, conn);
                cmd.CommandTimeout = 360;

                MySqlDataReader rdr = cmd.ExecuteReader();

                DataSet ds = new DataSet();
                DataTable dataTable = new DataTable();
                dataTable.TableName = "ap_form_12828_RAW";
                dataTable.Load(rdr);
                rdr.Close();

                string connectionString = @"Data Source = IN-UITS-CSLT028; Integrated Security=true; Initial Catalog=SLS_Intakes_DataPump";

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (SqlBulkCopy bulkCopy = new SqlBulkCopy(connection))
                    {

                        bulkCopy.DestinationTableName = "ap_form_12828_RAW";
                        try
                        {
                            bulkCopy.BulkCopyTimeout = 360;
                            bulkCopy.WriteToServer(dataTable);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                if (conn != null) conn.Close();
            }
            #endregion

            #region Process Intakes

            // ###############
            // PROCESS INTAKES
            // ###############

            // Get Intakes
            IEnumerable<ap_form_12828_RAW> ap_Form_12828_RAWs = db.ap_form_12828_RAW.OrderBy(o => o.id);

            foreach (ap_form_12828_RAW raw in ap_Form_12828_RAWs)
            {
                // Get ID

                int id = raw.id;

                Guid userId = new Guid("A8FC9784-78AC-4B6F-B0DC-987A0BE072A7");

                #region Case
                // Insert Intake into Cases
                Case _case = new Case()
                {
                    Status = "Pending",
                    CreationDate = DateTime.Now,
                    CreatedById = userId,
                    FirstName = raw.element_1_1,
                    LastName = raw.element_1_2,
                    IUStudentId = raw.element_2,
                    Gender = raw.element_52,
                    Number = raw.id,
                    SocialStatus = raw.element_51,
                    MayWeRecordYourInterview = raw.element_32 == "Yes" ? true : false,
                    EnrollmentVerified = false,
                    StudentActivityFeeVerified = false
                };

                db2.Cases.Add(_case);
                db2.SaveChanges();
                #endregion

                #region Case Resources
                // Insert Case Resources
                List<ReferralSourceFromMachform> referralSources = SetupReferralSources();

                foreach (ReferralSourceFromMachform referralSource in referralSources)
                {
                    string rsElement = referralSource.ElementName;
                    string valueString = raw.GetType().GetProperty(rsElement).GetValue(raw, null).ToString();
                    bool value = valueString == "1" ? true : false;

                    if(value)
                    {
                        string connectionString = @"Data Source = IN-UITS-CSLT028; Integrated Security=true; Initial Catalog=SLS_LegalServices";

                        using (SqlConnection _connection = new SqlConnection(connectionString))
                        {
                            try
                            {
                                _connection.Open();

                                string qry = "INSERT INTO dbo.CaseReferralSources(ReferralSourceId, CaseId) VALUES(@ReferralSourceId, @CaseId)";

                                SqlCommand cmd2 = new SqlCommand(qry, _connection);
                                cmd2.Parameters.Add("@ReferralSourceId", SqlDbType.Int);
                                cmd2.Parameters["@ReferralSourceId"].Value = GetReferralSourceId(referralSources, rsElement);
                                cmd2.Parameters.Add("@CaseId", SqlDbType.Int);
                                cmd2.Parameters["@CaseId"].Value = _case.CaseId;

                                cmd2.ExecuteNonQuery();

                            } catch (SqlException ex)
                            {
                                Console.WriteLine(ex.Message);
                            }
                            finally
                            {
                                _connection.Close();
                            }
                        }
                    }
                }
                #endregion

                #region Case Notes
                // Insert Case Notes
                CaseNote caseNote = new CaseNote()
                {
                    CaseId = _case.CaseId,
                    Detail = raw.element_30,
                    IsCriticalDate = false,
                    CreationDate = DateTime.Now,
                    CreatedById = userId
                };
                db2.CaseNotes.Add(caseNote);
                db2.SaveChanges();
                #endregion

                #region Primary Telephone
                // Insert Case Primary Telephone
                if (!string.IsNullOrWhiteSpace(raw.element_43))
                {
                    Telephone telephone = new Telephone()
                    {
                        Type = "Primary",
                        CaseId = _case.CaseId,
                        Number = raw.element_43
                    };
                    db2.Telephones.Add(telephone);
                    db2.SaveChanges();
                }
                #endregion

                #region Alternate Telephone

                // Insert Case Alternate Telephone
                if (!string.IsNullOrWhiteSpace(raw.element_44))
                {
                    Telephone telephone = new Telephone()
                    {
                        Type = "Alternate",
                        CaseId = _case.CaseId,
                        Number = raw.element_44
                    };
                    db2.Telephones.Add(telephone);
                    db2.SaveChanges();
                }
                #endregion

                #region Case Email
                // Insert Case Email
                if (!string.IsNullOrWhiteSpace(raw.element_41))
                {
                    Email email = new Email()
                    {
                        Type = "Primary",
                        CaseId = _case.CaseId,
                        Email1 = raw.element_41
                    };
                    db2.Emails.Add(email);
                    db2.SaveChanges();
                }
                #endregion

                #region Local Address
                // Insert Case Local Address
                if (!string.IsNullOrWhiteSpace(raw.element_3_1) || !string.IsNullOrWhiteSpace(raw.element_3_2) || !string.IsNullOrWhiteSpace(raw.element_3_3) || !string.IsNullOrWhiteSpace(raw.element_3_4) || !string.IsNullOrWhiteSpace(raw.element_3_5))
                {
                    Address address = new Address()
                    {
                        Type = "Local",
                        CaseId = _case.CaseId,
                        Address1 = raw.element_3_1,
                        Address2 = raw.element_3_2,
                        City = raw.element_3_3,
                        State = raw.element_3_4,
                        PostalCode = raw.element_3_5,
                        Country = raw.element_3_6
                    };
                    db2.Addresses.Add(address);
                    db2.SaveChanges();
                }
                #endregion

                #region Alternate Address
                // Insert Case Alternate Address
                if (!string.IsNullOrWhiteSpace(raw.element_19_1) || !string.IsNullOrWhiteSpace(raw.element_19_2) || !string.IsNullOrWhiteSpace(raw.element_19_3) || !string.IsNullOrWhiteSpace(raw.element_19_4) || !string.IsNullOrWhiteSpace(raw.element_19_5))
                {
                    Address address = new Address()
                    {
                        Type = "Alternate",
                        CaseId = _case.CaseId,
                        Address1 = raw.element_19_1,
                        Address2 = raw.element_19_2,
                        City = raw.element_19_3,
                        State = raw.element_19_4,
                        PostalCode = raw.element_19_5,
                        Country = raw.element_19_6
                    };
                    db2.Addresses.Add(address);
                    db2.SaveChanges();
                }
                #endregion

                #region AdverseParty
                // Insert Adverse Party
                if (!string.IsNullOrWhiteSpace(raw.element_5_1) || !string.IsNullOrWhiteSpace(raw.element_5_2))
                {
                    // Insert Case Adverse Party Telephone
                    int adversePartyTelephoneId = 0;
                    if (!string.IsNullOrWhiteSpace(raw.element_7))
                    {
                        Telephone telephone = new Telephone()
                        {
                            Type = "Adverse Party",
                            CaseId = _case.CaseId,
                            Number = raw.element_7
                        };
                        db2.Telephones.Add(telephone);
                        db2.SaveChanges();
                        adversePartyTelephoneId = telephone.TelephoneId;
                    }

                    // Insert Case Adverse Party Email
                    int adversePartyEmailId = 0;
                    if (!string.IsNullOrWhiteSpace(raw.element_9))
                    {
                        Email email = new Email()
                        {
                            Type = "Adverse Party",
                            CaseId = _case.CaseId,
                            Email1 = raw.element_9
                        };
                        db2.Emails.Add(email);
                        db2.SaveChanges();
                        adversePartyEmailId = email.EmailId;
                    }

                    // Insert Case Adverse Party Address
                    int adversePartyAddressId = 0;
                    if (!string.IsNullOrWhiteSpace(raw.element_6_1) || !string.IsNullOrWhiteSpace(raw.element_6_2) || !string.IsNullOrWhiteSpace(raw.element_6_3) || !string.IsNullOrWhiteSpace(raw.element_6_4) || !string.IsNullOrWhiteSpace(raw.element_6_5))
                    {
                        Address address = new Address()
                        {
                            Type = "Adverse Party",
                            CaseId = _case.CaseId,
                            Address1 = raw.element_6_1,
                            Address2 = raw.element_6_2,
                            City = raw.element_6_3,
                            State = raw.element_6_4,
                            PostalCode = raw.element_6_5,
                            Country = raw.element_6_6
                        };
                        db2.Addresses.Add(address);
                        db2.SaveChanges();
                        adversePartyAddressId = address.AddressId;
                    }

                    CaseParty adverseParty = new CaseParty()
                    {
                        CaseId = _case.CaseId,
                        OrganizationName = raw.element_5_1 + " " + raw.element_5_2,
                        PartyType = "Adverse Party",
                        IsIUStudent = raw.element_20 == "Yes" ? true : false,
                    };
                    if(adversePartyAddressId > 0)
                    {
                        adverseParty.AddressId = adversePartyAddressId;
                    }
                    if (adversePartyEmailId > 0)
                    {
                        adverseParty.EmailId = adversePartyEmailId;
                    }
                    if (adversePartyTelephoneId > 0)
                    {
                        adverseParty.TelephoneId = adversePartyTelephoneId;
                    }
                    db2.CaseParties.Add(adverseParty);
                    db2.SaveChanges();
                }
                #endregion

                #region AP 1
                // Insert AP 1
                if (!string.IsNullOrWhiteSpace(raw.element_12_1) || !string.IsNullOrWhiteSpace(raw.element_12_2))
                {
                    string partyType = raw.element_21 == "Yes" ? "Adverse Party" : "Additional Party";

                    // Insert Case Party Telephone - does not exist

                    // Insert Case Party Email
                    int partyEmailId = 0;
                    if (!string.IsNullOrWhiteSpace(raw.element_16))
                    {
                        Email email = new Email()
                        {
                            Type = partyType,
                            CaseId = _case.CaseId,
                            Email1 = raw.element_16
                        };
                        db2.Emails.Add(email);
                        db2.SaveChanges();
                        partyEmailId = email.EmailId;
                    }

                    // Insert Case Party Address
                    int partyAddressId = 0;
                    if (!string.IsNullOrWhiteSpace(raw.element_13_1) || !string.IsNullOrWhiteSpace(raw.element_13_2) || !string.IsNullOrWhiteSpace(raw.element_13_3) || !string.IsNullOrWhiteSpace(raw.element_13_4) || !string.IsNullOrWhiteSpace(raw.element_13_5))
                    {
                        Address address = new Address()
                        {
                            Type = partyType,
                            CaseId = _case.CaseId,
                            Address1 = raw.element_13_1,
                            Address2 = raw.element_13_2,
                            City = raw.element_13_3,
                            State = raw.element_13_4,
                            PostalCode = raw.element_13_5,
                            Country = raw.element_13_6
                        };
                        db2.Addresses.Add(address);
                        db2.SaveChanges();
                        partyAddressId = address.AddressId;
                    }

                    CaseParty caseParty = new CaseParty()
                    {
                        CaseId = _case.CaseId,
                        OrganizationName = raw.element_12_1 + " " + raw.element_12_2,
                        PartyType = partyType,
                        IsIUStudent = raw.element_22 == "Yes" ? true : false,
                    };
                    if (partyAddressId > 0)
                    {
                        caseParty.AddressId = partyAddressId;
                    }
                    if (partyEmailId > 0)
                    {
                        caseParty.EmailId = partyEmailId;
                    }
                    db2.CaseParties.Add(caseParty);
                    db2.SaveChanges();
                }
                #endregion

                #region AP 2
                if (!string.IsNullOrWhiteSpace(raw.element_25_1) || !string.IsNullOrWhiteSpace(raw.element_25_2))
                {
                    string partyType = raw.element_24 == "Yes" ? "Adverse Party" : "Additional Party";

                    // Insert Case Party Telephone
                    int adversePartyTelephoneId = 0;
                    if (!string.IsNullOrWhiteSpace(raw.element_14))
                    {
                        Telephone telephone = new Telephone()
                        {
                            Type = partyType,
                            CaseId = _case.CaseId,
                            Number = raw.element_14
                        };
                        db2.Telephones.Add(telephone);
                        db2.SaveChanges();
                        adversePartyTelephoneId = telephone.TelephoneId;
                    }

                    // Insert Case Party Email
                    int partyEmailId = 0;
                    if (!string.IsNullOrWhiteSpace(raw.element_27))
                    {
                        Email email = new Email()
                        {
                            Type = partyType,
                            CaseId = _case.CaseId,
                            Email1 = raw.element_27
                        };
                        db2.Emails.Add(email);
                        db2.SaveChanges();
                        partyEmailId = email.EmailId;
                    }

                    // Insert Case Party Address
                    int partyAddressId = 0;
                    if (!string.IsNullOrWhiteSpace(raw.element_26_1) || !string.IsNullOrWhiteSpace(raw.element_26_2) || !string.IsNullOrWhiteSpace(raw.element_26_3) || !string.IsNullOrWhiteSpace(raw.element_26_4) || !string.IsNullOrWhiteSpace(raw.element_26_5))
                    {
                        Address address = new Address()
                        {
                            Type = partyType,
                            CaseId = _case.CaseId,
                            Address1 = raw.element_26_1,
                            Address2 = raw.element_26_2,
                            City = raw.element_26_3,
                            State = raw.element_26_4,
                            PostalCode = raw.element_26_5,
                            Country = raw.element_26_6
                        };
                        db2.Addresses.Add(address);
                        db2.SaveChanges();
                        partyAddressId = address.AddressId;
                    }

                    CaseParty caseParty = new CaseParty()
                    {
                        CaseId = _case.CaseId,
                        OrganizationName = raw.element_25_1 + " " + raw.element_25_2,
                        PartyType = partyType,
                        IsIUStudent = raw.element_23 == "Yes" ? true : false,
                    };
                    if (partyAddressId > 0)
                    {
                        caseParty.AddressId = partyAddressId;
                    }
                    if (partyEmailId > 0)
                    {
                        caseParty.EmailId = partyEmailId;
                    }
                    db2.CaseParties.Add(caseParty);
                    db2.SaveChanges();
                }
                #endregion

                #region AP 3
                if (!string.IsNullOrWhiteSpace(raw.element_70_1) || !string.IsNullOrWhiteSpace(raw.element_70_2))
                {
                    string partyType = raw.element_82 == "Yes" ? "Adverse Party" : "Additional Party";

                    // Insert Case Party Telephone
                    int adversePartyTelephoneId = 0;
                    if (!string.IsNullOrWhiteSpace(raw.element_76))
                    {
                        Telephone telephone = new Telephone()
                        {
                            Type = partyType,
                            CaseId = _case.CaseId,
                            Number = raw.element_76
                        };
                        db2.Telephones.Add(telephone);
                        db2.SaveChanges();
                        adversePartyTelephoneId = telephone.TelephoneId;
                    }

                    // Insert Case Party Email
                    int partyEmailId = 0;
                    if (!string.IsNullOrWhiteSpace(raw.element_79))
                    {
                        Email email = new Email()
                        {
                            Type = partyType,
                            CaseId = _case.CaseId,
                            Email1 = raw.element_79
                        };
                        db2.Emails.Add(email);
                        db2.SaveChanges();
                        partyEmailId = email.EmailId;
                    }

                    // Insert Case Party Address
                    int partyAddressId = 0;
                    if (!string.IsNullOrWhiteSpace(raw.element_73_1) || !string.IsNullOrWhiteSpace(raw.element_73_2) || !string.IsNullOrWhiteSpace(raw.element_73_3) || !string.IsNullOrWhiteSpace(raw.element_73_4) || !string.IsNullOrWhiteSpace(raw.element_73_5))
                    {
                        Address address = new Address()
                        {
                            Type = partyType,
                            CaseId = _case.CaseId,
                            Address1 = raw.element_73_1,
                            Address2 = raw.element_73_2,
                            City = raw.element_73_3,
                            State = raw.element_73_4,
                            PostalCode = raw.element_73_5,
                            Country = raw.element_73_6
                        };
                        db2.Addresses.Add(address);
                        db2.SaveChanges();
                        partyAddressId = address.AddressId;
                    }

                    CaseParty caseParty = new CaseParty()
                    {
                        CaseId = _case.CaseId,
                        OrganizationName = raw.element_70_1 + " " + raw.element_70_2,
                        PartyType = partyType,
                        IsIUStudent = raw.element_85 == "Yes" ? true : false,
                    };
                    if (partyAddressId > 0)
                    {
                        caseParty.AddressId = partyAddressId;
                    }
                    if (partyEmailId > 0)
                    {
                        caseParty.EmailId = partyEmailId;
                    }
                    db2.CaseParties.Add(caseParty);
                    db2.SaveChanges();
                }
                #endregion

                #region AP 4
                if (!string.IsNullOrWhiteSpace(raw.element_69_1) || !string.IsNullOrWhiteSpace(raw.element_69_2))
                {
                    string partyType = raw.element_81 == "Yes" ? "Adverse Party" : "Additional Party";

                    // Insert Case Party Telephone
                    int adversePartyTelephoneId = 0;
                    if (!string.IsNullOrWhiteSpace(raw.element_75))
                    {
                        Telephone telephone = new Telephone()
                        {
                            Type = partyType,
                            CaseId = _case.CaseId,
                            Number = raw.element_75
                        };
                        db2.Telephones.Add(telephone);
                        db2.SaveChanges();
                        adversePartyTelephoneId = telephone.TelephoneId;
                    }

                    // Insert Case Party Email
                    int partyEmailId = 0;
                    if (!string.IsNullOrWhiteSpace(raw.element_78))
                    {
                        Email email = new Email()
                        {
                            Type = partyType,
                            CaseId = _case.CaseId,
                            Email1 = raw.element_78
                        };
                        db2.Emails.Add(email);
                        db2.SaveChanges();
                        partyEmailId = email.EmailId;
                    }

                    // Insert Case Party Address
                    int partyAddressId = 0;
                    if (!string.IsNullOrWhiteSpace(raw.element_72_1) || !string.IsNullOrWhiteSpace(raw.element_72_2) || !string.IsNullOrWhiteSpace(raw.element_72_3) || !string.IsNullOrWhiteSpace(raw.element_72_4) || !string.IsNullOrWhiteSpace(raw.element_72_5))
                    {
                        Address address = new Address()
                        {
                            Type = partyType,
                            CaseId = _case.CaseId,
                            Address1 = raw.element_72_1,
                            Address2 = raw.element_72_2,
                            City = raw.element_72_3,
                            State = raw.element_72_4,
                            PostalCode = raw.element_72_5,
                            Country = raw.element_72_6
                        };
                        db2.Addresses.Add(address);
                        db2.SaveChanges();
                        partyAddressId = address.AddressId;
                    }

                    CaseParty caseParty = new CaseParty()
                    {
                        CaseId = _case.CaseId,
                        OrganizationName = raw.element_69_1 + " " + raw.element_69_2,
                        PartyType = partyType,
                        IsIUStudent = raw.element_84 == "Yes" ? true : false,
                    };
                    if (partyAddressId > 0)
                    {
                        caseParty.AddressId = partyAddressId;
                    }
                    if (partyEmailId > 0)
                    {
                        caseParty.EmailId = partyEmailId;
                    }
                    db2.CaseParties.Add(caseParty);
                    db2.SaveChanges();
                }
                #endregion

                #region AP 5
                if (!string.IsNullOrWhiteSpace(raw.element_68_1) || !string.IsNullOrWhiteSpace(raw.element_68_2))
                {
                    string partyType = raw.element_80 == "Yes" ? "Adverse Party" : "Additional Party";

                    // Insert Case Party Telephone
                    int adversePartyTelephoneId = 0;
                    if (!string.IsNullOrWhiteSpace(raw.element_74))
                    {
                        Telephone telephone = new Telephone()
                        {
                            Type = partyType,
                            CaseId = _case.CaseId,
                            Number = raw.element_74
                        };
                        db2.Telephones.Add(telephone);
                        db2.SaveChanges();
                        adversePartyTelephoneId = telephone.TelephoneId;
                    }

                    // Insert Case Party Email
                    int partyEmailId = 0;
                    if (!string.IsNullOrWhiteSpace(raw.element_77))
                    {
                        Email email = new Email()
                        {
                            Type = partyType,
                            CaseId = _case.CaseId,
                            Email1 = raw.element_77
                        };
                        db2.Emails.Add(email);
                        db2.SaveChanges();
                        partyEmailId = email.EmailId;
                    }

                    // Insert Case Party Address
                    int partyAddressId = 0;
                    if (!string.IsNullOrWhiteSpace(raw.element_71_1) || !string.IsNullOrWhiteSpace(raw.element_71_2) || !string.IsNullOrWhiteSpace(raw.element_71_3) || !string.IsNullOrWhiteSpace(raw.element_71_4) || !string.IsNullOrWhiteSpace(raw.element_71_5))
                    {
                        Address address = new Address()
                        {
                            Type = partyType,
                            CaseId = _case.CaseId,
                            Address1 = raw.element_71_1,
                            Address2 = raw.element_71_2,
                            City = raw.element_71_3,
                            State = raw.element_71_4,
                            PostalCode = raw.element_71_5,
                            Country = raw.element_71_6
                        };
                        db2.Addresses.Add(address);
                        db2.SaveChanges();
                        partyAddressId = address.AddressId;
                    }

                    CaseParty caseParty = new CaseParty()
                    {
                        CaseId = _case.CaseId,
                        OrganizationName = raw.element_68_1 + " " + raw.element_68_2,
                        PartyType = partyType,
                        IsIUStudent = raw.element_83 == "Yes" ? true : false,
                    };
                    if (partyAddressId > 0)
                    {
                        caseParty.AddressId = partyAddressId;
                    }
                    if (partyEmailId > 0)
                    {
                        caseParty.EmailId = partyEmailId;
                    }
                    db2.CaseParties.Add(caseParty);
                    db2.SaveChanges();
                }
                #endregion


                // Pull Uploads

            }
            #endregion
        }

        #region Helpers

        private static string GetIntakesQuery(int id)
        {
            StringBuilder query = new StringBuilder();
            query.Append("SELECT id, date_created, ip_address, status, element_1_1, element_1_2, LPAD(element_2, 10, '0') AS element_2, element_43, element_44, element_41, element_3_1, element_3_2, element_3_3, ");
            query.Append("element_3_4, element_3_5, element_3_6, element_19_1, element_19_2, element_19_3, element_19_4, element_19_5, element_19_6, ");
            query.Append("(SELECT `option` FROM ap_element_options WHERE form_id = 12828 AND element_id = 32 and option_id = Element_32 ) AS Element_32, ");
            query.Append("ifnull(ifnull((SELECT `option` FROM ap_element_options WHERE form_id = 12828 AND element_id = 49 and option_id = Element_49), element_49_other), '') AS Element_49, ");
            query.Append("ifnull((SELECT `option` FROM ap_element_options WHERE form_id = 12828 AND element_id = 50 and option_id = Element_50 ), '') AS Element_50, ");
            query.Append("ifnull((SELECT `option` FROM ap_element_options WHERE form_id = 12828 AND element_id = 51 and option_id = Element_51 ), '') AS Element_51, ");
            query.Append("ifnull((SELECT `option` FROM ap_element_options WHERE form_id = 12828 AND element_id = 52 and option_id = Element_52 ), '') AS Element_52, ");
            query.Append("element_5_1, element_5_2, element_6_1, element_6_2, element_6_3, element_6_4, element_6_5, element_6_6, element_7, element_9, ");
            query.Append("ifnull((SELECT `option` FROM ap_element_options WHERE form_id = 12828 AND element_id = 20 and option_id = Element_20 ), '') AS Element_20, ");
            query.Append("element_12_1, element_12_2, element_13_1, element_13_2, element_13_3, element_13_4, element_13_5, element_13_6, element_16, ");
            query.Append("ifnull((SELECT `option` FROM ap_element_options WHERE form_id = 12828 AND element_id = 21 and option_id = Element_21 ), '') AS Element_21, ");
            query.Append("ifnull((SELECT `option` FROM ap_element_options WHERE form_id = 12828 AND element_id = 22 and option_id = Element_22 ), '') AS Element_22, ");
            query.Append("element_25_1, element_25_2, element_26_1, element_26_2, element_26_3, element_26_4, element_26_5, element_26_6, element_14, element_27, ");
            query.Append("ifnull((SELECT `option` FROM ap_element_options WHERE form_id = 12828 AND element_id = 24 and option_id = Element_24 ), '') AS Element_24, ");
            query.Append("ifnull((SELECT `option` FROM ap_element_options WHERE form_id = 12828 AND element_id = 23 and option_id = Element_23 ), '') AS Element_23, ");
            query.Append("element_70_1, element_70_2, element_73_1, element_73_2, element_73_3, element_73_4, element_73_5, element_73_6, element_76, element_79, ");
            query.Append("ifnull((SELECT `option` FROM ap_element_options WHERE form_id = 12828 AND element_id = 82 and option_id = Element_82 ), '') AS Element_82, ");
            query.Append("ifnull((SELECT `option` FROM ap_element_options WHERE form_id = 12828 AND element_id = 85 and option_id = Element_85 ), '') AS Element_85, ");
            query.Append("element_69_1, element_69_2, element_72_1, element_72_2, element_72_3, element_72_4, element_72_5, element_72_6, element_75, element_78, ");
            query.Append("ifnull((SELECT `option` FROM ap_element_options WHERE form_id = 12828 AND element_id = 81 and option_id = Element_81 ), '') AS Element_81, ");
            query.Append("ifnull((SELECT `option` FROM ap_element_options WHERE form_id = 12828 AND element_id = 84 and option_id = Element_84 ), '') AS Element_84, ");
            query.Append("element_68_1, element_68_2, element_71_1, element_71_2, element_71_3, element_71_4, element_71_5, element_71_6, element_74, element_77, element_83, ");
            query.Append("ifnull((SELECT `option` FROM ap_element_options WHERE form_id = 12828 AND element_id = 80 and option_id = Element_80 ), '') AS Element_80, element_33_11, element_33_12, element_33_13, element_33_14, element_33_15, ");
            query.Append("element_33_16, element_33_17, element_33_18, element_33_19, element_33_20, element_33_21, element_33_22, element_33_other, ");
            query.Append("ifnull((SELECT `option` FROM ap_element_options WHERE form_id = 12828 AND element_id = 67 and option_id = Element_67 ), 'No') AS Element_67, ");
            query.Append("element_30, element_42, ifnull((SELECT `option` FROM ap_element_options WHERE form_id = 12828 AND element_id = 34 and option_id = Element_34 ), '') AS Element_34 ");
            query.Append("FROM ap_form_12828 WHERE date_created >= '2019-01-01' AND id > ");
            query.Append(id.ToString());
            query.Append(" LIMIT 10");

            return query.ToString();
        }

        private static List<ReferralSourceFromMachform> SetupReferralSources()
        {
            List<ReferralSourceFromMachform> referralSourceFromMachforms = new List<ReferralSourceFromMachform>();

            referralSourceFromMachforms.Add(new ReferralSourceFromMachform { ElementName = "element_33_11", Option = "Friend" });
            referralSourceFromMachforms.Add(new ReferralSourceFromMachform { ElementName = "element_33_12", Option = "Former Client" });
            referralSourceFromMachforms.Add(new ReferralSourceFromMachform { ElementName = "element_33_13", Option = "Facebook" });
            referralSourceFromMachforms.Add(new ReferralSourceFromMachform { ElementName = "element_33_14", Option = "Website" });
            referralSourceFromMachforms.Add(new ReferralSourceFromMachform { ElementName = "element_33_15", Option = "Twitter" });
            referralSourceFromMachforms.Add(new ReferralSourceFromMachform { ElementName = "element_33_16", Option = "Brochure" });
            referralSourceFromMachforms.Add(new ReferralSourceFromMachform { ElementName = "element_33_17", Option = "Campus Bus Advertising" });
            referralSourceFromMachforms.Add(new ReferralSourceFromMachform { ElementName = "element_33_18", Option = "IDS" });
            referralSourceFromMachforms.Add(new ReferralSourceFromMachform { ElementName = "element_33_19", Option = "Court/Police" });
            referralSourceFromMachforms.Add(new ReferralSourceFromMachform { ElementName = "element_33_20", Option = "Landlord" });
            referralSourceFromMachforms.Add(new ReferralSourceFromMachform { ElementName = "element_33_21", Option = "Orientation" });
            referralSourceFromMachforms.Add(new ReferralSourceFromMachform { ElementName = "element_33_22", Option = "Other" });

            return referralSourceFromMachforms;
        }

        private static int GetReferralSourceId(List<ReferralSourceFromMachform> referrals, string element)
        {
            SLS_LegalServicesEntities db2 = new SLS_LegalServicesEntities();

            string referralSourceName = referrals.Where(w => w.ElementName == element).FirstOrDefault().Option;

            int referralSourceId = db2.ReferralSources.Where(w => w.ReferralSource1 == referralSourceName).FirstOrDefault().ReferralSourceId;

            return referralSourceId;
        }
        #endregion
    }

    #region Models
    public class ReferralSourceFromMachform
    {
        public string ElementName { get; set; }
        public string Option { get; set; }
    }
    #endregion
}
