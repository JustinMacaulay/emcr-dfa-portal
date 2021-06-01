﻿// -------------------------------------------------------------------------
//  Copyright © 2021 Province of British Columbia
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  https://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
// -------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EMBC.ESS.Shared.Contracts.Submissions;

namespace EMBC.ESS.Resources.Cases
{
    public interface ICaseRepository
    {
        Task<ManageCaseCommandResult> ManageCase(ManageCaseCommand cmd);

        Task<CaseQueryResult> QueryCase(CaseQuery query);
    }

    public abstract class ManageCaseCommand { }

    public class ManageCaseCommandResult
    {
        public string CaseId { get; set; }
    }

    public abstract class CaseQuery
    {
    }

    public class CaseQueryResult
    {
        public IEnumerable<Case> Items { get; set; } = Array.Empty<Case>();
    }

    public abstract class Case
    {
        public string Id { get; set; }
    }

    public class SaveEvacuationFile : ManageCaseCommand
    {
        public EvacuationFile EvacuationFile { get; set; }
    }

    public class DeleteEvacuationFile : ManageCaseCommand
    {
        public string Id { get; set; }
    }

    public class UpdateSecurityPhrase : ManageCaseCommand
    {
        public string Id { get; set; }
        public string SecurityPhrase { get; set; }
    }

    public class EvacuationFilesQuery : CaseQuery
    {
        public string FileId { get; set; }
        public string PrimaryRegistrantId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string DateOfBirth { get; set; }
        public bool IncludeHouseholdMembers { get; set; }
        public bool MaskSecurityPhrase { get; set; } = true;
        public EvacuationFileStatus[] IncludeFilesInStatuses { get; set; } = Array.Empty<EvacuationFileStatus>();
        public DateTime? RegistraionDateFrom { get; set; }
        public DateTime? RegistraionDateTo { get; set; }
        public int? Limit { get; set; }
    }

    public class EvacuationFile : Case
    {
        public string TaskId { get; set; }
        public EvacuationAddress EvacuatedFromAddress { get; set; }
        public IEnumerable<NeedsAssessment> NeedsAssessments { get; set; } = Array.Empty<NeedsAssessment>();
        public string PrimaryRegistrantId { get; set; }
        public string SecurityPhrase { get; set; }
        public bool SecurityPhraseChanged { get; set; } = false;
        public DateTime EvacuationDate { get; set; }
        public EvacuationFileStatus Status { get; set; }
        public bool RestrictedAccess { get; set; }
        public bool IsSecretPhraseMasked { get; set; }
        public string RegistrationLocation { get; set; }
    }

    public class EvacuationAddress
    {
        public string AddressLine1 { get; set; }
        public string AddressLine2 { get; set; }
        public string CommunityCode { get; set; }
        public string StateProvinceCode { get; set; }
        public string CountryCode { get; set; }
        public string PostalCode { get; set; }
    }

    public class NeedsAssessment
    {
        public string Id { get; set; }
        public InsuranceOption Insurance { get; set; }
        public bool? CanEvacueeProvideFood { get; set; }
        public bool? CanEvacueeProvideLodging { get; set; }
        public bool? CanEvacueeProvideClothing { get; set; }
        public bool? CanEvacueeProvideTransportation { get; set; }
        public bool? CanEvacueeProvideIncidentals { get; set; }
        public bool HaveSpecialDiet { get; set; }
        public string SpecialDietDetails { get; set; }
        public bool HaveMedication { get; set; }
        public IEnumerable<HouseholdMember> HouseholdMembers { get; set; } = Array.Empty<HouseholdMember>();
        public IEnumerable<Pet> Pets { get; set; } = Array.Empty<Pet>();
        public bool? HasPetsFood { get; set; }
        public NeedsAssessmentType Type { get; set; }
        public IEnumerable<Note> Notes { get; set; }
        public IEnumerable<ReferralServices> RecommendedReferralServices { get; set; }
    }

    public class HouseholdMember
    {
        public string Id { get; set; }
        public bool IsUnder19 { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Initials { get; set; }
        public string PreferredName { get; set; }
        public string Gender { get; set; }
        public string DateOfBirth { get; set; }
        public bool IsPrimaryRegistrant { get; set; }
        public string LinkedRegistrantId { get; set; }
        public bool RestrictedAccess { get; set; }
    }

    public class Pet
    {
        public string Type { get; set; }
        public string Quantity { get; set; }
    }

    public class Note
    {
        public string Id { get; set; }
        public NoteType Type { get; set; }
        public string Content { get; set; }
        public DateTime AddedOn { get; set; }
        public string CreatingTeamMemberId { get; set; }
    }

    public enum NoteType
    {
        General,
        EvacuationImpact,
        ExternalReferralServices,
        PetCarePlans,
        RecoveryPlan
    }

    public enum ReferralServices
    {
        Inquiry,
        Health,
        FirstAid,
        Personal,
        ChildCare,
        PetCare
    }

    public enum InsuranceOption
    {
        No,
        Yes,
        Unsure,
        Unknown
    }

    public enum NeedsAssessmentType
    {
        Preliminary,
        Assessed
    }
}
