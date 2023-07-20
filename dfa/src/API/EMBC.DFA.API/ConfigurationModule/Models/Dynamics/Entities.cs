﻿using Org.BouncyCastle.Asn1.Mozilla;
using Org.BouncyCastle.Bcpg.OpenPgp;

namespace EMBC.DFA.API.ConfigurationModule.Models.Dynamics
{
    public class Country
    {
        public string era_countrycode { get; set; }
        public string era_name { get; set; }
        public bool statecode { get; set; }
    }

    public class dfa_appcontact
    {
        public string dfa_firstname { get; set; }
        public string dfa_lastname { get; set; }
        public string dfa_initial { get; set; }
        public string dfa_residencetelephonenumber { get; set; }
        public string dfa_cellphonenumber { get; set; }
        public string dfa_alternatephonenumber { get; set; }
        public string dfa_emailaddress { get; set; }
        public string dfa_bcservicecardid { get; set; }
        public string dfa_primaryaddressline1 { get; set; }
        public string? dfa_primaryaddressline2 { get; set; }
        public string? dfa_primarycity { get; set; }
        public string? dfa_primarystateprovince { get; set; }
        public string? dfa_primarypostalcode { get; set; }
        public string dfa_secondaryaddressline1 { get; set; }
        public string? dfa_secondaryaddressline2 { get; set; }
        public string? dfa_secondarycity { get; set; }
        public string? dfa_secondarystateprovince { get; set; }
        public string? dfa_secondarypostalcode { get; set; }
        public bool? dfa_isindigenous { get; set; }
        public int dfa_isprimaryandsecondaryaddresssame { get; set; }
    }

    public class dfa_appapplicationstart
    {
        public int dfa_applicanttype { get; set; } // required (already existing)
        public int dfa_doyouhaveinsurancecoverage2 { get; set; } // required
        public string dfa_appcontactid { get; set; } // required string passed in to PROC, PROC looks up appcontact to fill in application fields
        public int dfa_primaryapplicantsignednoins { get; set; } // required Yes or No option set
        public string? dfa_primaryapplicantprintnamenoins { get; set; } // optional string
        public string? dfa_primaryapplicantsigneddatenoins { get; set; } // optional Date and Time (Date Only)
        public string? dfa_primaryapplicantsignaturenoins { get; set; } // new field Dynamics annotation attachment
        public int dfa_secondaryapplicantsignednoins { get; set; } // required OptionSet existing Yes or No option set
        public string? dfa_secondaryapplicantprintnamenoins { get; set; } // optional string
        public string? dfa_secondaryapplicantsigneddatenoins { get; set; } // optional  Date and Time (Date Only)
        public string? dfa_secondaryapplicantsignaturenoins { get; set; } // new field Dynamics annotation attachment
    }

    public class dfa_appapplicationmain
    {
        public string dfa_appapplicationid { get; set; } // required
        public int? dfa_isprimaryanddamagedaddresssame { get; set; } // optional Two Options
        public string? dfa_damagedpropertystreet1 { get; set; } // optional string
        public string? dfa_damagedpropertystreet2 { get; set; } // optional string
        public string? dfa_damagedpropertycity { get; set; } // optional string
        public string? dfa_damagedpropertyprovince { get; set; } // optional string
        public string? dfa_damagedpropertypostalcode { get; set; } // optional string
        public int? dfa_isthispropertyyourp { get; set; } // optional Two Options
        public int? dfa_indigenousreserve { get; set; } // optional Two Options
        public string? dfa_nameoffirstnationsr { get; set; } // optional string
        public int? dfa_manufacturedhom { get; set; } // optional Two Options
        public int? dfa_eligibleforbchomegrantonthisproperty { get; set; } // optional Two Options
        public string? dfa_contactfirstname { get; set; } // optional string
        public string? dfa_contactlastname { get; set; } // optional string
        public string? dfa_contactphone1 { get; set; } // optional string
        public string? dfa_contactemail { get; set; } // optional string
        public int dfa_acopyofarentalagreementorlease { get; set; } // required Two Options
        public int? dfa_areyounowresidingintheresidence { get; set; } // optional Two Options
        public int? dfa_causeofdamageflood { get; set; } // optional Two Options
        public int? dfa_causeofdamagestorm { get; set; } // optoinal Two Options
        public int? dfa_causeofdamagewildfire { get; set; } // optional Two Options
        public int? dfa_causeofdamagelandslide { get; set; } // optional Two Options
        public int? dfa_causeofdamageother { get; set; } // optional Two Options
        public string? dfa_causeofdamageloss { get; set; } // optional string
        public string? dfa_dateofdamage { get; set; } // optoinal date only
        public string? dfa_dateofdamageto { get; set; } // optional date only
        public string? dfa_dateofreturntotheresidence { get; set; } // optional date only
        public string? dfa_description { get; set; } // optional string
        public int? dfa_doyourlossestotalmorethan1000 { get; set; } // optional Two Options
        public int dfa_haveinvoicesreceiptsforcleanuporrepairs { get; set; } // required Two Options
        public string? dfa_primaryapplicantprintname { get; set; } // optional string
        public int dfa_primaryapplicantsigned { get; set; } // required Two Options
        public string? dfa_primaryapplicantsigneddate { get; set; } // optional string
        public string? dfa_primaryapplicantsignature { get; set; } // optional string
        public string? dfa_secondaryapplicantprintname { get; set; } // optional string
        public int dfa_secondaryapplicantsigned { get; set; } // required Two Options
        public string? dfa_secondaryapplicantsigneddate { get; set; } // optional string
        public string? dfa_secondaryapplicantsignature { get; set; } // optional string
        public int? dfa_wereyouevacuatedduringtheevent { get; set; } // optional Two Options
    }

    public enum ApplicantTypeOptionSet
    {
        CharitableOrganization = 222710000,
        FarmOwner = 222710001,
        HomeOwner = 222710002,
        ResidentialTenant = 222710003,
        SmallBusinessOwner = 222710004,
        GovernmentBody = 222710005,
        Incorporated = 222710006
    }

    public enum YesNoOptionSet
    {
        Yes = 222710000,
        No = 222710001
    }

    public enum InsuranceTypeOptionSet
    {
        Yes = 222710000,
        No = 222710002,
        YesBut = 222710001
    }

    public class dfa_appapplication
    {
        public string dfa_appapplicationid { get; set; }
        public string dfa_applicanttype { get; set; }
    }
}
