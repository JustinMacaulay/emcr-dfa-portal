﻿// -------------------------------------------------------------------------
//  Copyright © 2020 Province of British Columbia
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
using System.Linq;
using System.Threading.Tasks;
using EMBC.Registrants.API.Shared;
using EMBC.Registrants.API.Utils;
using EMBC.ResourceAccess.Dynamics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Dynamics.CRM;
using Microsoft.OData;
using Microsoft.OData.Client;
using Microsoft.OData.Edm;

namespace EMBC.Registrants.API.RegistrationsModule
{
    public interface IRegistrationManager
    {
        Task<string> CreateRegistrationAnonymous(AnonymousRegistration registration);

        Task<string> CreateRegistrantEvacuation(RegistrantEvacuation evacuation);

        Task<OkResult> CreateProfile(Registration profileRegistration);

        Task<Registration> GetProfileById(Guid contactId);

        Task<Registration> GetProfileByBcscId(string bcscId);
        Task<Registration> PatchProfileById(Guid id, Registration profileRegistration);
    }

    public class RegistrationManager : IRegistrationManager
    {
        private readonly DynamicsClientContext dynamicsClient;
        private readonly IEmailSender emailSender;
        private DateTimeOffset now;

        public RegistrationManager(DynamicsClientContext dynamicsClient, IEmailSender emailSender)
        {
            this.dynamicsClient = dynamicsClient;
            this.now = DateTimeOffset.UtcNow;
            this.emailSender = emailSender;
        }

        /// <summary>
        /// Create a Profile Registration (dynamics contact)
        /// </summary>
        /// <param name="profileRegistration">Registration</param>
        /// <returns>OkResult</returns>
        public async Task<OkResult> CreateProfile(Registration profileRegistration)
        {
            // Create New Contact (Primary Registrant)
            var newRegistrant = CreateNewContact(profileRegistration, true);

            // save changes to dynamics
            //var results = await dynamicsClient.SaveChangesAsync();
            var results = await dynamicsClient.SaveChangesAsync(SaveChangesOptions.BatchWithSingleChangeset);

            // Check if email address defined for profile
            if (profileRegistration.ContactDetails.Email != null)
            {
                // Send email notification of new registrant record created
                EmailAddress registrantEmailAddress = new EmailAddress
                {
                    Name = profileRegistration.PersonalDetails.FirstName + " " + profileRegistration.PersonalDetails.LastName,
                    Address = profileRegistration.ContactDetails.Email
                };
                SendRegistrationNotificationEmail(registrantEmailAddress);
            }

            return new OkResult();
        }

        /// <summary>
        /// Create an Anonymous Registration (evacuation file, self needs assessment, primary registrant and household members)
        /// </summary>
        /// <param name="registration">AnonymousRegistration</param>
        /// <returns>ESS File Number</returns>
        public async Task<string> CreateRegistrationAnonymous(AnonymousRegistration registration)
        {
#pragma warning disable CA5394 // Do not use insecure randomness
            var essFileNumber = new Random().Next(999999999); //temporary ESS file number random generator
#pragma warning restore CA5394 // Do not use insecure randomness

            // New evacuation file
            var evacuationFile = new era_evacuationfile
            {
                era_evacuationfileid = Guid.NewGuid(),
                era_essfilenumber = essFileNumber,
                era_evacuationfiledate = now,
                era_addressline1 = registration.PreliminaryNeedsAssessment.EvacuatedFromAddress.AddressLine1,
                era_addressline2 = registration.PreliminaryNeedsAssessment.EvacuatedFromAddress.AddressLine2,
                era_city = registration.PreliminaryNeedsAssessment.EvacuatedFromAddress.AddressLine1,
                era_Jurisdiction = Lookup(registration.PreliminaryNeedsAssessment.EvacuatedFromAddress.Jurisdiction),
                era_province = registration.PreliminaryNeedsAssessment.EvacuatedFromAddress.StateProvince.Code,
                era_country = registration.PreliminaryNeedsAssessment.EvacuatedFromAddress.Country.Code,
                era_secrettext = registration.RegistrationDetails.SecretPhrase,
            };

            // New needs assessment
            var needsAssessment = new era_needassessment
            {
                era_needassessmentid = Guid.NewGuid(),
                era_needsassessmentdate = now,
                era_EvacuationFile = evacuationFile,
                era_needsassessmenttype = 174360000,
                era_foodrequirement = Lookup(registration.PreliminaryNeedsAssessment.RequiresFood), //to be deleted
                era_clothingrequirement = Lookup(registration.PreliminaryNeedsAssessment.RequiresClothing), //to be deleted
                era_incidentalrequirement = Lookup(registration.PreliminaryNeedsAssessment.RequiresIncidentals), //to be deleted
                era_lodgingrequirement = Lookup(registration.PreliminaryNeedsAssessment.RequiresLodging), //to be deleted
                era_transportationrequirement = Lookup(registration.PreliminaryNeedsAssessment.RequiresTransportation), //to be deleted
                era_canevacueeprovidefood = Lookup(registration.PreliminaryNeedsAssessment.CanEvacueeProvideFood),
                era_canevacueeprovideclothing = Lookup(registration.PreliminaryNeedsAssessment.CanEvacueeProvideClothing),
                era_canevacueeprovideincidentals = Lookup(registration.PreliminaryNeedsAssessment.CanEvacueeProvideIncidentals),
                era_canevacueeprovidelodging = Lookup(registration.PreliminaryNeedsAssessment.CanEvacueeProvideLodging),
                era_canevacueeprovidetransportation = Lookup(registration.PreliminaryNeedsAssessment.CanEvacueeProvideTransportation),
                era_dietaryrequirement = registration.PreliminaryNeedsAssessment.HaveSpecialDiet,
                era_dietaryrequirementdetails = registration.PreliminaryNeedsAssessment.SpecialDietDetails,
                era_medicationrequirement = registration.PreliminaryNeedsAssessment.HaveMedication,
                era_insurancecoverage = Lookup(registration.PreliminaryNeedsAssessment.Insurance),
                era_collectionandauthorization = registration.RegistrationDetails.InformationCollectionConsent,
                era_sharingrestriction = registration.RegistrationDetails.RestrictedAccess,
                era_phonenumberrefusal = string.IsNullOrEmpty(registration.RegistrationDetails.ContactDetails.Phone),
                era_emailrefusal = string.IsNullOrEmpty(registration.RegistrationDetails.ContactDetails.Email)
            };

            // New Contact (Primary Registrant)
            var newPrimaryRegistrant = CreateNewContact(registration.RegistrationDetails, true);

            // New Contacts (Household Members)
            var members = (registration.PreliminaryNeedsAssessment.FamilyMembers ?? Array.Empty<PersonDetails>()).Select(fm => new contact
            {
                contactid = Guid.NewGuid(),
                era_registranttype = 174360001,
                era_authenticated = false,
                era_verified = false,
                era_registrationdate = now,
                firstname = fm.FirstName,
                lastname = fm.LastName,
                era_preferredname = fm.PreferredName,
                era_initial = fm.Initials,
                gendercode = LookupGender(fm.Gender),
                birthdate = FromDateTime(DateTime.Parse(fm.DateOfBirth)),
                era_collectionandauthorization = registration.RegistrationDetails.InformationCollectionConsent,
                era_sharingrestriction = registration.RegistrationDetails.RestrictedAccess,

                address1_line1 = registration.RegistrationDetails.PrimaryAddress.AddressLine1,
                address1_line2 = registration.RegistrationDetails.PrimaryAddress.AddressLine2,
                address1_city = registration.RegistrationDetails.PrimaryAddress.Jurisdiction.Name,
                address1_country = registration.RegistrationDetails.PrimaryAddress.Country.Code,
                era_City = Lookup(registration.RegistrationDetails.PrimaryAddress.Jurisdiction),
                era_ProvinceState = Lookup(registration.RegistrationDetails.PrimaryAddress.StateProvince),
                era_Country = Lookup(registration.RegistrationDetails.PrimaryAddress.Country),
                address1_postalcode = registration.RegistrationDetails.PrimaryAddress.PostalCode,

                address2_line1 = registration.RegistrationDetails.MailingAddress.AddressLine1,
                address2_line2 = registration.RegistrationDetails.MailingAddress.AddressLine2,
                address2_city = registration.RegistrationDetails.MailingAddress.Jurisdiction.Name,
                address2_country = registration.RegistrationDetails.MailingAddress.Country.Code,
                era_MailingCity = Lookup(registration.RegistrationDetails.MailingAddress.Jurisdiction),
                era_MailingProvinceState = Lookup(registration.RegistrationDetails.MailingAddress.StateProvince),
                era_MailingCountry = Lookup(registration.RegistrationDetails.MailingAddress.Country),
                address2_postalcode = registration.RegistrationDetails.MailingAddress.PostalCode,

                emailaddress1 = registration.RegistrationDetails.ContactDetails.Email,
                address1_telephone1 = registration.RegistrationDetails.ContactDetails.Phone,

                era_phonenumberrefusal = string.IsNullOrEmpty(registration.RegistrationDetails.ContactDetails.Phone),
                era_emailrefusal = string.IsNullOrEmpty(registration.RegistrationDetails.ContactDetails.Email),
                era_secrettext = registration.RegistrationDetails.SecretPhrase
            });

            // New needs assessment evacuee as pet
            var pets = (registration.PreliminaryNeedsAssessment.Pets ?? Array.Empty<Pet>()).Select(p => new era_needsassessmentevacuee
            {
                era_needsassessmentevacueeid = Guid.NewGuid(),
                era_numberofpets = Convert.ToInt32(p.Quantity),
                era_typeofpet = p.Type,
                era_evacueetype = LookupEvacueeType("Pet")
            });

            // add evacuation file to dynamics context
            dynamicsClient.AddToera_evacuationfiles(evacuationFile);
            // add needs assessment to dynamics context
            dynamicsClient.AddToera_needassessments(needsAssessment);
            // link evacuation file to needs assessment
            dynamicsClient.AddLink(evacuationFile, nameof(evacuationFile.era_needsassessment_EvacuationFile), needsAssessment);

            // New needs assessment evacuee as primary registrant
            var newNeedsAssessmentEvacueeRegistrant = new era_needsassessmentevacuee
            {
                era_needsassessmentevacueeid = Guid.NewGuid(),
                era_isprimaryregistrant = true,
                era_evacueetype = LookupEvacueeType("Person")
            };
            dynamicsClient.AddToera_needsassessmentevacuees(newNeedsAssessmentEvacueeRegistrant);
            // link registrant and needs assessment to evacuee record
            dynamicsClient.AddLink(newPrimaryRegistrant, nameof(newPrimaryRegistrant.era_NeedsAssessmentEvacuee_RegistrantID), newNeedsAssessmentEvacueeRegistrant);
            dynamicsClient.AddLink(needsAssessment, nameof(needsAssessment.era_NeedsAssessmentEvacuee_NeedsAssessmentID), newNeedsAssessmentEvacueeRegistrant);

            // Add New needs assessment evacuee members to dynamics context
            foreach (var member in members)
            {
                dynamicsClient.AddTocontacts(member);
                var newNeedsAssessmentEvacueeMember = new era_needsassessmentevacuee
                {
                    era_needsassessmentevacueeid = Guid.NewGuid(),
                    era_isprimaryregistrant = false,
                    era_evacueetype = LookupEvacueeType("Person")
                };
                dynamicsClient.AddToera_needsassessmentevacuees(newNeedsAssessmentEvacueeMember);
                // link members and needs assessment to evacuee record
                dynamicsClient.AddLink(member, nameof(member.era_NeedsAssessmentEvacuee_RegistrantID), newNeedsAssessmentEvacueeMember);
                dynamicsClient.AddLink(needsAssessment, nameof(needsAssessment.era_NeedsAssessmentEvacuee_NeedsAssessmentID), newNeedsAssessmentEvacueeMember);
            }

            // Add New needs assessment evacuee pets to dynamics context
            foreach (var petMember in pets)
            {
                dynamicsClient.AddToera_needsassessmentevacuees(petMember);
                // link pet to evacuee record
                dynamicsClient.AddLink(needsAssessment, nameof(needsAssessment.era_NeedsAssessmentEvacuee_NeedsAssessmentID), petMember);
            }

            //post as batch is not accepted by SSG. Sending with default option (multiple requests to the server stopping on the first failure)
            var results = await dynamicsClient.SaveChangesAsync(SaveChangesOptions.BatchWithSingleChangeset);
            //var results = await dynamicsClient.SaveChangesAsync();

            var queryResult = dynamicsClient.era_evacuationfiles
                //.Expand(f => f.era_city)
                //.Expand(f => f.era_province)
                //.Expand(f => f.era_country)
                .Where(f => f.era_evacuationfileid == evacuationFile.era_evacuationfileid).FirstOrDefault();

            return $"{essFileNumber:D9}";
            //return queryResult.era_essfilenumber.ToString();
        }

        /// <summary>
        /// Get a Registrant Profile
        /// </summary>
        /// <param name="contactId">Contact Id</param>
        /// <returns>Registration</returns>
        public Task<Registration> GetProfileById(Guid contactId)
        {
            var profile = newRegistrationObject();

            // get dynamics contact
            contact contactQueryResult = GetDynamicsContact(contactId);

            if (contactQueryResult != null)
            {
                // return contact as a profile
                CopyContactToProfile(contactQueryResult, profile);
            }

            return Task.FromResult(profile);
        }

        private contact GetDynamicsContact(Guid contactId)
        {
            contact queryResult = null;
            try
            {
                queryResult = dynamicsClient.contacts
                        .Expand(c => c.era_City)
                        .Expand(c => c.era_ProvinceState)
                        .Expand(c => c.era_Country)
                        .Expand(c => c.era_MailingCity)
                        .Expand(c => c.era_MailingProvinceState)
                        .Expand(c => c.era_MailingCountry)
                        .Where(c => c.contactid == contactId).FirstOrDefault();
            }
            catch (DataServiceQueryException ex)
            {
                //Client level Exception message
                //Console.WriteLine(ex.Message);

                //The InnerException of DataServiceQueryException contains DataServiceClientException
                DataServiceClientException dataServiceClientException = ex.InnerException as DataServiceClientException;

                // don't throw an exception if contact is not found, return an empty profile
                if (dataServiceClientException.StatusCode == 404)
                {
                    return null;
                }
                else
                {
                    Console.WriteLine("dataServiceClientException: " + dataServiceClientException.Message);
                }

                ODataErrorException odataErrorException = dataServiceClientException.InnerException as ODataErrorException;
                if (odataErrorException != null)
                {
                    Console.WriteLine(odataErrorException.Message);
                    throw dataServiceClientException;
                }
            }
            return queryResult;
        }

        private contact GetDynamicsContactByBCSC(string BCServicesCardId)
        {
            contact queryResult = null;
            try
            {
                queryResult = dynamicsClient.contacts
                        .Expand(c => c.era_City)
                        .Expand(c => c.era_ProvinceState)
                        .Expand(c => c.era_Country)
                        .Expand(c => c.era_MailingCity)
                        .Expand(c => c.era_MailingProvinceState)
                        .Expand(c => c.era_MailingCountry)
                        .Where(c => c.era_bcservicescardid == BCServicesCardId).FirstOrDefault();
            }
            catch (DataServiceQueryException ex)
            {
                DataServiceClientException dataServiceClientException = ex.InnerException as DataServiceClientException;
                // don't throw an exception if contact is not found, return an empty profile
                if (dataServiceClientException.StatusCode == 404)
                {
                    return null;
                }
                else
                {
                    Console.WriteLine("dataServiceClientException: " + dataServiceClientException.Message);
                }
                ODataErrorException odataErrorException = dataServiceClientException.InnerException as ODataErrorException;
                if (odataErrorException != null)
                {
                    Console.WriteLine(odataErrorException.Message);
                    throw dataServiceClientException;
                }
            }
            return queryResult;
        }

        private Registration CopyContactToProfile(contact contact, Registration profile)
        {
            profile.ContactId = contact.contactid.ToString();
            profile.BCServicesCardId = contact.era_bcservicescardid;
            // Personal Details
            profile.PersonalDetails.FirstName = contact.firstname;
            profile.PersonalDetails.LastName = contact.lastname;
            profile.PersonalDetails.DateOfBirth = Convert.ToDateTime(contact.birthdate.ToString()).ToShortDateString(); //MM/dd/yyyy
            profile.PersonalDetails.Initials = contact.era_initial;
            profile.PersonalDetails.PreferredName = contact.era_preferredname;
            profile.PersonalDetails.Gender = LookupGenderValue(contact.gendercode);
            // Contact Details
            profile.ContactDetails.Email = contact.emailaddress1;
            profile.ContactDetails.HideEmailRequired = contact.era_emailrefusal.HasValue ? contact.era_emailrefusal.Value : false;
            profile.ContactDetails.Phone = contact.address1_telephone1;
            profile.ContactDetails.HidePhoneRequired = contact.era_phonenumberrefusal.HasValue ? contact.era_phonenumberrefusal.Value : false;
            // Primary Address
            profile.PrimaryAddress.AddressLine1 = contact.address1_line1;
            profile.PrimaryAddress.AddressLine2 = contact.address1_line2;
            profile.PrimaryAddress.Jurisdiction.Code = contact.era_City?.era_jurisdictionid.ToString();
            profile.PrimaryAddress.Jurisdiction.Name = contact.era_City?.era_jurisdictionname;
            profile.PrimaryAddress.StateProvince.Code = contact.era_ProvinceState?.era_code;
            profile.PrimaryAddress.StateProvince.Name = contact.era_ProvinceState?.era_name;
            profile.PrimaryAddress.Country.Code = contact.era_Country?.era_countrycode;
            profile.PrimaryAddress.Country.Name = contact.era_Country?.era_name;
            profile.PrimaryAddress.PostalCode = contact.address1_postalcode;
            // Mailing Address
            profile.MailingAddress.AddressLine1 = contact.address2_line1;
            profile.MailingAddress.AddressLine2 = contact.address2_line2;
            profile.MailingAddress.Jurisdiction.Code = contact.era_MailingCity?.era_jurisdictionid.ToString();
            profile.MailingAddress.Jurisdiction.Name = contact.era_MailingCity?.era_jurisdictionname;
            profile.MailingAddress.StateProvince.Code = contact.era_MailingProvinceState?.era_code;
            profile.MailingAddress.StateProvince.Name = contact.era_MailingProvinceState?.era_name;
            profile.MailingAddress.Country.Code = contact.era_MailingCountry?.era_countrycode;
            profile.MailingAddress.Country.Name = contact.era_MailingCountry?.era_name;
            profile.MailingAddress.PostalCode = contact.address2_postalcode;
            // Other
            profile.InformationCollectionConsent = contact.era_collectionandauthorization.HasValue ? contact.era_collectionandauthorization.Value : false;
            profile.RestrictedAccess = contact.era_sharingrestriction.HasValue ? contact.era_sharingrestriction.Value : false;
            profile.SecretPhrase = contact.era_secrettext;

            return profile;
        }

        /// <summary>
        /// Get a Registrant Profile
        /// </summary>
        /// <param name="bcscId">BCSC Id</param>
        /// <returns>Registration</returns>
        public Task<Registration> GetProfileByBcscId(string bcscId)
        {
            var profile = newRegistrationObject();
            contact queryResult = null;

            try
            {
                queryResult = dynamicsClient.contacts
                        .Expand(c => c.era_City)
                        .Expand(c => c.era_ProvinceState)
                        .Expand(c => c.era_Country)
                        .Expand(c => c.era_MailingCity)
                        .Expand(c => c.era_MailingProvinceState)
                        .Expand(c => c.era_MailingCountry)
                        .Where(c => c.era_bcservicescardid == bcscId).FirstOrDefault();
            }
            catch (DataServiceQueryException ex)
            {
                //The InnerException of DataServiceQueryException contains DataServiceClientException
                DataServiceClientException dataServiceClientException = ex.InnerException as DataServiceClientException;

                // don't throw an exception if contact is not found, return an empty profile
                if (dataServiceClientException.StatusCode == 404)
                {
                    return Task.FromResult(profile);
                }
                else
                {
                    Console.WriteLine("dataServiceClientException: " + dataServiceClientException.Message);
                }

                ODataErrorException odataErrorException = dataServiceClientException.InnerException as ODataErrorException;
                if (odataErrorException != null)
                {
                    Console.WriteLine(odataErrorException.Message);
                    throw dataServiceClientException;
                }
            }

            if (queryResult != null)
            {
                profile.ContactId = queryResult.contactid.ToString();
                profile.BCServicesCardId = queryResult.era_bcservicescardid;
                // Personal Details
                profile.PersonalDetails.FirstName = queryResult.firstname;
                profile.PersonalDetails.LastName = queryResult.lastname;
                profile.PersonalDetails.DateOfBirth = Convert.ToDateTime(queryResult.birthdate.ToString()).ToShortDateString(); //MM/dd/yyyy
                profile.PersonalDetails.Initials = queryResult.era_initial;
                profile.PersonalDetails.PreferredName = queryResult.era_preferredname;
                profile.PersonalDetails.Gender = LookupGenderValue(queryResult.gendercode);
                // Contact Details
                profile.ContactDetails.Email = queryResult.emailaddress1;
                profile.ContactDetails.HideEmailRequired = queryResult.era_emailrefusal.HasValue ? queryResult.era_emailrefusal.Value : false;
                profile.ContactDetails.Phone = queryResult.address1_telephone1;
                profile.ContactDetails.HidePhoneRequired = queryResult.era_phonenumberrefusal.HasValue ? queryResult.era_phonenumberrefusal.Value : false;
                // Primary Address
                profile.PrimaryAddress.AddressLine1 = queryResult.address1_line1;
                profile.PrimaryAddress.AddressLine2 = queryResult.address1_line2;
                profile.PrimaryAddress.Jurisdiction.Code = queryResult.era_City?.era_jurisdictionid.ToString();
                profile.PrimaryAddress.Jurisdiction.Name = queryResult.era_City?.era_jurisdictionname;
                profile.PrimaryAddress.StateProvince.Code = queryResult.era_ProvinceState?.era_code;
                profile.PrimaryAddress.StateProvince.Name = queryResult.era_ProvinceState?.era_name;
                profile.PrimaryAddress.Country.Code = queryResult.era_Country?.era_countrycode;
                profile.PrimaryAddress.Country.Name = queryResult.era_Country?.era_name;
                profile.PrimaryAddress.PostalCode = queryResult.address1_postalcode;
                // Mailing Address
                profile.MailingAddress.AddressLine1 = queryResult.address2_line1;
                profile.MailingAddress.AddressLine2 = queryResult.address2_line2;
                profile.MailingAddress.Jurisdiction.Code = queryResult.era_MailingCity?.era_jurisdictionid.ToString();
                profile.MailingAddress.Jurisdiction.Name = queryResult.era_MailingCity?.era_jurisdictionname;
                profile.MailingAddress.StateProvince.Code = queryResult.era_MailingProvinceState?.era_code;
                profile.MailingAddress.StateProvince.Name = queryResult.era_MailingProvinceState?.era_name;
                profile.MailingAddress.Country.Code = queryResult.era_MailingCountry?.era_countrycode;
                profile.MailingAddress.Country.Name = queryResult.era_MailingCountry?.era_name;
                profile.MailingAddress.PostalCode = queryResult.address2_postalcode;
                // Other
                profile.InformationCollectionConsent = queryResult.era_collectionandauthorization.HasValue ? queryResult.era_collectionandauthorization.Value : false;
                profile.RestrictedAccess = queryResult.era_sharingrestriction.HasValue ? queryResult.era_sharingrestriction.Value : false;
                profile.SecretPhrase = queryResult.era_secrettext;
            }
            return Task.FromResult(profile);
        }

        /// <summary>
        /// Patch a Profile Registration (dynamics contact)
        /// </summary>
        /// <param name="id">Contact Id</param>
        /// <param name="updatedRegistration">Registration</param>
        /// <returns><see cref="Registration"/></returns>
        public async Task<Registration> PatchProfileById(Guid id, Registration updatedRegistration)
        {
            Registration profile = newRegistrationObject();
            contact existingContact = null;
            contact newContact = null;
            try
            {
                // search for contact
                existingContact = dynamicsClient.contacts
                                    .Expand(c => c.era_City)
                                    .Expand(c => c.era_ProvinceState)
                                    .Expand(c => c.era_Country)
                                    .Expand(c => c.era_MailingCity)
                                    .Expand(c => c.era_MailingProvinceState)
                                    .Expand(c => c.era_MailingCountry)
                                    .Where(c => c.contactid == id).SingleOrDefault<contact>();
            }
            catch (DataServiceQueryException ex)
            {
                //The InnerException of DataServiceQueryException contains DataServiceClientException
                DataServiceClientException dataServiceClientException = ex.InnerException as DataServiceClientException;

                // don't throw an exception if contact is not found, return an empty profile
                if (dataServiceClientException.StatusCode == 404)
                {
                    return profile;
                }
                else
                {
                    Console.WriteLine("dataServiceClientException: " + dataServiceClientException.Message);
                }

                ODataErrorException odataErrorException = dataServiceClientException.InnerException as ODataErrorException;
                if (odataErrorException != null)
                {
                    Console.WriteLine(odataErrorException.Message);
                    throw dataServiceClientException;
                }
            }

            if (existingContact != null)
            {
                newContact = UpdateExistingContact(existingContact, updatedRegistration);

                if (newContact != null)
                {
                    try
                    {
                        // create an update request
                        dynamicsClient.UpdateObject(newContact);
                        // save changes to dynamics
                        await dynamicsClient.SaveChangesAsync();
                    }
                    catch (DataServiceRequestException ex)
                    {
                        throw new ApplicationException(
                            "An error occurred when saving changes.", ex);
                    }
                    // Populate registration with updated contact data
                    profile = PopulateRegistration(profile, newContact);
                }
            }

            return profile;
        }

        private contact UpdateExistingContact(contact existingContact, Registration registration)
        {
            // Create a new contact and copy the details over from either the registration if it has the relevant value or the existing contract otherwise
            contact newContact = new contact();

            newContact.contactid = existingContact.contactid; // Get contact ID from existing contact - cannot be changed by registration
            newContact.era_bcservicescardid = registration.BCServicesCardId ?? existingContact.era_bcservicescardid;

            // Personal Details
            newContact.firstname = registration.PersonalDetails?.FirstName ?? existingContact.firstname;
            newContact.lastname = registration.PersonalDetails?.LastName ?? existingContact.lastname;
            newContact.era_preferredname = registration.PersonalDetails?.PreferredName ?? existingContact.era_preferredname;
            newContact.era_initial = registration.PersonalDetails?.Initials ?? existingContact.era_initial;
            newContact.gendercode = LookupGender(registration.PersonalDetails?.Gender) ?? existingContact.gendercode;
            newContact.birthdate = FromDateTime(DateTime.Parse(registration.PersonalDetails?.DateOfBirth)) ?? existingContact.birthdate;
            newContact.era_collectionandauthorization = registration.InformationCollectionConsent; // Always has value
            newContact.era_sharingrestriction = registration.RestrictedAccess; // Always has value

            // Contact Details - Check booleans
            newContact.emailaddress1 = registration.ContactDetails?.Email ?? existingContact.emailaddress1;
            newContact.address1_telephone1 = registration.ContactDetails?.Phone ?? existingContact.address1_telephone1;
            newContact.era_phonenumberrefusal = string.IsNullOrEmpty(newContact.address1_telephone1);
            newContact.era_emailrefusal = string.IsNullOrEmpty(newContact.emailaddress1);

            // Primary Address
            newContact.address1_line1 = registration.PrimaryAddress?.AddressLine1 ?? existingContact.address1_line1;
            newContact.address1_line2 = registration.PrimaryAddress?.AddressLine2 ?? existingContact.address1_line2;
            newContact.address1_postalcode = registration.PrimaryAddress?.PostalCode ?? existingContact.address1_postalcode;

            // Mailing Address
            newContact.address2_line1 = registration.MailingAddress?.AddressLine1 ?? existingContact.address2_line1;
            newContact.address2_line2 = registration.MailingAddress?.AddressLine2 ?? existingContact.address2_line2;
            newContact.address2_postalcode = registration.MailingAddress?.PostalCode ?? existingContact.address2_postalcode;

            // Other
            newContact.era_secrettext = registration.SecretPhrase ?? existingContact.era_secrettext;

            // link registrant primary and mailing address city, province, country
            var primaryAddressCountry = Lookup(registration.PrimaryAddress?.Country) ?? existingContact.era_Country;
            var primaryAddressProvince = Lookup(registration.PrimaryAddress?.StateProvince) ?? existingContact.era_ProvinceState;
            var primaryAddressCity = Lookup(registration.PrimaryAddress?.Jurisdiction) ?? existingContact.era_City;
            var mailingAddressCountry = Lookup(registration.MailingAddress?.Country) ?? existingContact.era_MailingCountry;
            var mailingAddressProvince = Lookup(registration.MailingAddress?.StateProvince) ?? existingContact.era_MailingProvinceState;
            var mailingAddressCity = Lookup(registration.MailingAddress?.Jurisdiction) ?? existingContact.era_MailingCity;

            newContact.era_City = new era_jurisdiction
            {
                era_jurisdictionid = primaryAddressCity.era_jurisdictionid,
                era_jurisdictionname = primaryAddressCity.era_jurisdictionname
            };
            newContact.era_ProvinceState = new era_provinceterritories
            {
                era_code = primaryAddressProvince.era_code,
                era_name = primaryAddressProvince.era_name
            };
            newContact.era_Country = new era_country
            {
                era_countrycode = primaryAddressCountry.era_countrycode,
                era_name = primaryAddressCountry.era_name
            };

            newContact.era_MailingCity = new era_jurisdiction
            {
                era_jurisdictionid = mailingAddressCity.era_jurisdictionid,
                era_jurisdictionname = mailingAddressCity.era_jurisdictionname
            };
            newContact.era_MailingProvinceState = new era_provinceterritories
            {
                era_code = mailingAddressProvince.era_code,
                era_name = mailingAddressProvince.era_name
            };
            newContact.era_MailingCountry = new era_country
            {
                era_countrycode = mailingAddressCountry.era_countrycode,
                era_name = mailingAddressCountry.era_name
            };

            // Delete the existing contact to allow it to be replaced by adding the new contact.
            // Note: this is a workaround as the standard approach to patch/update was triggering a mysterious exception
            dynamicsClient.DeleteObject(existingContact);
            dynamicsClient.AddTocontacts(newContact);

            // Add links to new client

            // country
            dynamicsClient.AddLink(primaryAddressCountry, nameof(primaryAddressCountry.era_contact_Country), newContact);
            // province
            if (primaryAddressProvince != null && !string.IsNullOrEmpty(primaryAddressProvince.era_code))
            {
                dynamicsClient.AddLink(primaryAddressProvince, nameof(primaryAddressProvince.era_provinceterritories_contact_ProvinceState), newContact);
            }
            // city
            if (primaryAddressCity == null || !primaryAddressCity.era_jurisdictionid.HasValue)
            {
                newContact.address1_city = primaryAddressCity.era_jurisdictionname;
            }
            else
            {
                dynamicsClient.AddLink(primaryAddressCity, nameof(primaryAddressCity.era_jurisdiction_contact_City), newContact);
            }

            // country
            dynamicsClient.AddLink(mailingAddressCountry, nameof(mailingAddressCountry.era_country_contact_MailingCountry), newContact);
            // province
            if (mailingAddressProvince != null && !string.IsNullOrEmpty(mailingAddressProvince.era_code))
            {
                dynamicsClient.AddLink(mailingAddressProvince, nameof(mailingAddressProvince.era_provinceterritories_contact_MailingProvinceState), newContact);
            }
            // city
            if (mailingAddressCity == null || !mailingAddressCity.era_jurisdictionid.HasValue)
            {
                newContact.address2_city = mailingAddressCity.era_jurisdictionname;
            }
            else
            {
                dynamicsClient.AddLink(mailingAddressCity, nameof(mailingAddressCity.era_jurisdiction_contact_MailingCity), newContact);
            }

            return newContact;
        }

        private Registration PopulateRegistration(Registration registration, contact contact)
        {
            // Check each incoming field for updated values and, if found, use them, otherwise use the existing value retrieved from Dynamics for the field.
            registration.BCServicesCardId = contact.era_bcservicescardid;
            // Personal Details
            registration.PersonalDetails.FirstName = contact.firstname;
            registration.PersonalDetails.LastName = contact.lastname;
            registration.PersonalDetails.DateOfBirth = contact.birthdate.ToString();
            registration.PersonalDetails.Initials = contact.era_initial;
            registration.PersonalDetails.PreferredName = contact.era_preferredname;
            registration.PersonalDetails.Gender = contact.gendercode.ToString();
            // Contact Details
            registration.ContactDetails.Email = contact.emailaddress1;
            registration.ContactDetails.HideEmailRequired = contact.era_emailrefusal.HasValue ? contact.era_emailrefusal.Value : false;
            registration.ContactDetails.Phone = contact.address1_telephone1;
            registration.ContactDetails.HidePhoneRequired = contact.era_phonenumberrefusal.HasValue ? contact.era_phonenumberrefusal.Value : false;
            // Primary Address
            registration.PrimaryAddress.AddressLine1 = contact.address1_line1;
            registration.PrimaryAddress.AddressLine2 = contact.address1_line2;
            registration.PrimaryAddress.Jurisdiction.Code = contact.era_City?.era_jurisdictionid.ToString();
            registration.PrimaryAddress.Jurisdiction.Name = contact.era_City?.era_jurisdictionname;
            registration.PrimaryAddress.StateProvince.Code = contact.era_ProvinceState?.era_code;
            registration.PrimaryAddress.StateProvince.Name = contact.era_ProvinceState?.era_name;
            registration.PrimaryAddress.Country.Code = contact.era_Country?.era_countrycode;
            registration.PrimaryAddress.Country.Name = contact.era_Country?.era_name;
            registration.PrimaryAddress.PostalCode = contact.address1_postalcode;
            // Mailing Address
            registration.MailingAddress.AddressLine1 = contact.address2_line1;
            registration.MailingAddress.AddressLine2 = contact.address2_line2;
            registration.MailingAddress.Jurisdiction.Code = contact.era_MailingCity?.era_jurisdictionid.ToString();
            registration.MailingAddress.Jurisdiction.Name = contact.era_MailingCity?.era_jurisdictionname;
            registration.MailingAddress.StateProvince.Code = contact.era_MailingProvinceState?.era_code;
            registration.MailingAddress.StateProvince.Name = contact.era_MailingProvinceState?.era_name;
            registration.MailingAddress.Country.Code = contact.era_MailingCountry?.era_countrycode;
            registration.MailingAddress.Country.Name = contact.era_MailingCountry?.era_name;
            registration.MailingAddress.PostalCode = contact.address2_postalcode;
            // Other
            registration.InformationCollectionConsent = contact.era_collectionandauthorization.HasValue ? contact.era_collectionandauthorization.Value : false;
            registration.RestrictedAccess = contact.era_restriction.HasValue ? contact.era_restriction.Value : false;
            registration.SecretPhrase = contact.era_secrettext;

            return registration;
        }

        private Registration newRegistrationObject()
        {
            var registration = new Registration();
            registration.PersonalDetails = new PersonDetails();
            registration.ContactDetails = new ContactDetails();
            registration.PrimaryAddress = new Address();
            registration.PrimaryAddress.Jurisdiction = new Jurisdiction();
            registration.PrimaryAddress.StateProvince = new StateProvince();
            registration.PrimaryAddress.Country = new Country();
            registration.MailingAddress = new Address();
            registration.MailingAddress.Jurisdiction = new Jurisdiction();
            registration.MailingAddress.StateProvince = new StateProvince();
            registration.MailingAddress.Country = new Country();

            return registration;
        }

        private era_country Lookup(Country country) =>
            country == null || string.IsNullOrEmpty(country.Code)
            ? null
            : dynamicsClient.era_countries.Where(c => c.era_countrycode == country.Code).FirstOrDefault();

        private era_country Lookup(era_country country) =>
            country == null || string.IsNullOrEmpty(country.era_countrycode)
            ? null
            : dynamicsClient.era_countries.Where(c => c.era_countrycode == country.era_countrycode).FirstOrDefault();

        private int Lookup(bool? value) => value.HasValue ? value.Value ? 174360000 : 174360001 : 174360002;

        private int? Lookup(NeedsAssessment.InsuranceOption value) => value switch
        {
            NeedsAssessment.InsuranceOption.No => 174360000,
            NeedsAssessment.InsuranceOption.Yes => 174360001,
            NeedsAssessment.InsuranceOption.Unsure => 174360002,
            NeedsAssessment.InsuranceOption.Unknown => 174360003,
            _ => null
        };

        private int? LookupGender(string value) => value switch
        {
            "Male" => 1,
            "Female" => 2,
            "X" => 3,
            _ => null
        };

        private string LookupGenderValue(int? value) => value switch
        {
            1 => "Male",
            2 => "Female",
            3 => "X",
            _ => null
        };

        private int? LookupEvacueeType(string value) => value switch
        {
            "Person" => 174360000,
            "Pet" => 174360001,
            _ => null
        };

        private era_provinceterritories Lookup(StateProvince stateProvince)
        {
            if (stateProvince == null || string.IsNullOrEmpty(stateProvince.Code))
                return null;

            return dynamicsClient.era_provinceterritorieses.Where(p => p.era_code == stateProvince.Code).FirstOrDefault();
        }

        private era_jurisdiction Lookup(Jurisdiction jurisdiction)
        {
            if (jurisdiction == null || string.IsNullOrEmpty(jurisdiction.Code))
                return null;

            return dynamicsClient.era_jurisdictions.Where(j => j.era_jurisdictionid == Guid.Parse(jurisdiction.Code)).FirstOrDefault();
        }

        private Date? FromDateTime(DateTime? dateTime) => dateTime.HasValue ? new Date(dateTime.Value.Year, dateTime.Value.Month, dateTime.Value.Day) : (Date?)null;

        /// <summary>
        /// Create a new Dynamics Contact (Profile)
        /// </summary>
        /// <param name="profileRegistration">Registration values</param>
        /// <param name="isPrimary">Primary or Member Registrant</param>
        /// <returns>Contact</returns>
        private contact CreateNewContact(Registration profileRegistration, bool isPrimary)
        {
            var contact = new contact();
            contact.contactid = Guid.NewGuid();
            if (isPrimary)
                contact.era_registranttype = 174360000; // Primary
            else
                contact.era_registranttype = 174360001; // Memeber
            contact.era_authenticated = true;
            contact.era_verified = false;
            contact.era_registrationdate = now;
            contact.firstname = profileRegistration.PersonalDetails.FirstName;
            contact.lastname = profileRegistration.PersonalDetails.LastName;
            contact.era_preferredname = profileRegistration.PersonalDetails.PreferredName;
            contact.era_initial = profileRegistration.PersonalDetails.Initials;
            contact.gendercode = LookupGender(profileRegistration.PersonalDetails.Gender);
            contact.birthdate = FromDateTime(DateTime.Parse(profileRegistration.PersonalDetails.DateOfBirth));
            contact.era_collectionandauthorization = profileRegistration.InformationCollectionConsent;
            contact.era_sharingrestriction = profileRegistration.RestrictedAccess;
            contact.era_bcservicescardid = profileRegistration.BCServicesCardId;

            contact.address1_line1 = profileRegistration.PrimaryAddress.AddressLine1;
            contact.address1_line2 = profileRegistration.PrimaryAddress.AddressLine2;
            contact.address1_postalcode = profileRegistration.PrimaryAddress.PostalCode;

            contact.address2_line1 = profileRegistration.MailingAddress.AddressLine1;
            contact.address2_line2 = profileRegistration.MailingAddress.AddressLine2;
            contact.address2_postalcode = profileRegistration.MailingAddress.PostalCode;

            contact.emailaddress1 = profileRegistration.ContactDetails.Email;
            contact.address1_telephone1 = profileRegistration.ContactDetails.Phone;

            contact.era_phonenumberrefusal = string.IsNullOrEmpty(profileRegistration.ContactDetails.Phone);
            contact.era_emailrefusal = string.IsNullOrEmpty(profileRegistration.ContactDetails.Email);
            contact.era_secrettext = profileRegistration.SecretPhrase;

            // add contact to dynamics client
            dynamicsClient.AddTocontacts(contact);

            // add links to dynamics client

            /* NOTES:
             * There are 2 city fields in dynamics, one mapping to a jurisdiction and another set as free text. If the city
             * doesn't exist as a jurisdiction then it shoudl be stored in the free text field, otherwise as jurisdiction.
             * The province/state field is only captured by the front end when country is Canada or USA
             */

            // link registrant primary and mailing address city, province, country
            var primaryAddressCountry = Lookup(profileRegistration.PrimaryAddress.Country);
            var primaryAddressProvince = Lookup(profileRegistration.PrimaryAddress.StateProvince);
            var primaryAddressCity = Lookup(profileRegistration.PrimaryAddress.Jurisdiction);
            // country
            dynamicsClient.AddLink(primaryAddressCountry, nameof(primaryAddressCountry.era_contact_Country), contact);
            // province
            if (primaryAddressProvince != null && !string.IsNullOrEmpty(primaryAddressProvince.era_code))
            {
                dynamicsClient.AddLink(primaryAddressProvince, nameof(primaryAddressProvince.era_provinceterritories_contact_ProvinceState), contact);
            }
            // city
            if (primaryAddressCity == null || !primaryAddressCity.era_jurisdictionid.HasValue)
            {
                contact.address1_city = profileRegistration.PrimaryAddress.Jurisdiction.Name;
            }
            else
            {
                dynamicsClient.AddLink(primaryAddressCity, nameof(primaryAddressCity.era_jurisdiction_contact_City), contact);
            }

            var mailingAddressCountry = Lookup(profileRegistration.MailingAddress.Country);
            var mailingAddressProvince = Lookup(profileRegistration.MailingAddress.StateProvince);
            var mailingAddressCity = Lookup(profileRegistration.MailingAddress.Jurisdiction);
            // country
            dynamicsClient.AddLink(mailingAddressCountry, nameof(mailingAddressCountry.era_country_contact_MailingCountry), contact);
            // province
            if (mailingAddressProvince != null && !string.IsNullOrEmpty(mailingAddressProvince.era_code))
            {
                dynamicsClient.AddLink(mailingAddressProvince, nameof(mailingAddressProvince.era_provinceterritories_contact_MailingProvinceState), contact);
            }
            // city
            if (mailingAddressCity == null || !mailingAddressCity.era_jurisdictionid.HasValue)
            {
                contact.address2_city = profileRegistration.MailingAddress.Jurisdiction.Name;
            }
            else
            {
                dynamicsClient.AddLink(mailingAddressCity, nameof(mailingAddressCity.era_jurisdiction_contact_MailingCity), contact);
            }

            //return the new contact created
            return contact;
        }

        private void SendRegistrationNotificationEmail(EmailAddress toAddress)
        {
            System.Collections.Generic.List<EmailAddress> toList = new System.Collections.Generic.List<EmailAddress> { toAddress };
            string emailSubject = "Registration completed successfully";
            string emailBody = $@"
<p>This email has been generated by the Emergency Support Services program to confirm your profile has been created within the Evacuee Registration and Assistant application (ERA).
<p>
<p>Please use the following link to login to the system and review your profile information, start an evacuation file if required, or review existing evacuation file and support information.
<p>";
            emailBody += $"<p>Go to https://ess.gov.bc.ca/ and select the 'Already have an account? Log in\" link.";

            EmailMessage emailMessage = new EmailMessage(toList, emailSubject, emailBody);
            emailSender.Send(emailMessage);
        }

        /// <summary>
        /// Creates a Registrant Evacuation (new evacuation file, new self needs assessment, new household members and links the primary registrant)
        /// </summary>
        /// <param name="evacuation">Evacuation model</param>
        /// <returns>ESS File Number</returns>
        public async Task<string> CreateRegistrantEvacuation(RegistrantEvacuation evacuation)
        {
            //if (!Guid.TryParse(evacuation.ContactId, out Guid contactId))
            //    throw new Exception("Contact ID is not a valid GUID");

            var profile = newRegistrationObject();

            // get dynamics contact by contactId
            //contact dynamicsContact = GetDynamicsContact(contactId);

            // get dynamics contact by BCServicesCardId
            contact dynamicsContact = GetDynamicsContactByBCSC(evacuation.Id);

            if (dynamicsContact != null)
            {
                // return contact as a profile
                CopyContactToProfile(dynamicsContact, profile);
            }

            var essFileNumber = new Random().Next(999999999); // temporary ESS file number random generator

            // New evacuation file
            var evacuationFile = new era_evacuationfile
            {
                era_evacuationfileid = Guid.NewGuid(),
                era_essfilenumber = essFileNumber,
                era_evacuationfiledate = now,
                era_addressline1 = evacuation.PreliminaryNeedsAssessment.EvacuatedFromAddress.AddressLine1,
                era_addressline2 = evacuation.PreliminaryNeedsAssessment.EvacuatedFromAddress.AddressLine2,
                era_city = evacuation.PreliminaryNeedsAssessment.EvacuatedFromAddress.AddressLine1,
                era_Jurisdiction = Lookup(evacuation.PreliminaryNeedsAssessment.EvacuatedFromAddress.Jurisdiction),
                era_province = evacuation.PreliminaryNeedsAssessment.EvacuatedFromAddress.StateProvince.Code,
                era_country = evacuation.PreliminaryNeedsAssessment.EvacuatedFromAddress.Country.Code,
                era_secrettext = profile.SecretPhrase,
            };

            // New needs assessment
            var needsAssessment = new era_needassessment
            {
                era_needassessmentid = Guid.NewGuid(),
                era_needsassessmentdate = now,
                era_EvacuationFile = evacuationFile,
                era_needsassessmenttype = 174360000,
                era_foodrequirement = Lookup(evacuation.PreliminaryNeedsAssessment.RequiresFood), //to be deleted
                era_clothingrequirement = Lookup(evacuation.PreliminaryNeedsAssessment.RequiresClothing), //to be deleted
                era_incidentalrequirement = Lookup(evacuation.PreliminaryNeedsAssessment.RequiresIncidentals), //to be deleted
                era_lodgingrequirement = Lookup(evacuation.PreliminaryNeedsAssessment.RequiresLodging), //to be deleted
                era_transportationrequirement = Lookup(evacuation.PreliminaryNeedsAssessment.RequiresTransportation), //to be deleted
                era_canevacueeprovidefood = Lookup(evacuation.PreliminaryNeedsAssessment.CanEvacueeProvideFood),
                era_canevacueeprovideclothing = Lookup(evacuation.PreliminaryNeedsAssessment.CanEvacueeProvideClothing),
                era_canevacueeprovideincidentals = Lookup(evacuation.PreliminaryNeedsAssessment.CanEvacueeProvideIncidentals),
                era_canevacueeprovidelodging = Lookup(evacuation.PreliminaryNeedsAssessment.CanEvacueeProvideLodging),
                era_canevacueeprovidetransportation = Lookup(evacuation.PreliminaryNeedsAssessment.CanEvacueeProvideTransportation),
                era_dietaryrequirement = evacuation.PreliminaryNeedsAssessment.HaveSpecialDiet,
                era_dietaryrequirementdetails = evacuation.PreliminaryNeedsAssessment.SpecialDietDetails,
                era_medicationrequirement = evacuation.PreliminaryNeedsAssessment.HaveMedication,
                era_insurancecoverage = Lookup(evacuation.PreliminaryNeedsAssessment.Insurance),
                era_emailrefusal = string.IsNullOrEmpty(profile.ContactDetails.Email)
            };

            // New Contacts (Household Members)
            var members = (evacuation.PreliminaryNeedsAssessment.FamilyMembers ?? Array.Empty<PersonDetails>()).Select(fm => new contact
            {
                contactid = Guid.NewGuid(),
                era_registranttype = 174360001,
                era_authenticated = false,
                era_verified = false,
                era_registrationdate = now,
                firstname = fm.FirstName,
                lastname = fm.LastName,
                era_preferredname = fm.PreferredName,
                era_initial = fm.Initials,
                gendercode = LookupGender(fm.Gender),
                birthdate = FromDateTime(DateTime.Parse(fm.DateOfBirth)),
                era_collectionandauthorization = profile.InformationCollectionConsent,
                era_sharingrestriction = profile.RestrictedAccess,

                address1_line1 = profile.PrimaryAddress.AddressLine1,
                address1_line2 = profile.PrimaryAddress.AddressLine2,
                address1_city = profile.PrimaryAddress.Jurisdiction.Name,
                address1_country = profile.PrimaryAddress.Country.Code,
                era_City = Lookup(profile.PrimaryAddress.Jurisdiction),
                era_ProvinceState = Lookup(profile.PrimaryAddress.StateProvince),
                era_Country = Lookup(profile.PrimaryAddress.Country),
                address1_postalcode = profile.PrimaryAddress.PostalCode,

                address2_line1 = profile.MailingAddress.AddressLine1,
                address2_line2 = profile.MailingAddress.AddressLine2,
                address2_city = profile.MailingAddress.Jurisdiction.Name,
                address2_country = profile.MailingAddress.Country.Name,
                era_MailingCity = Lookup(profile.MailingAddress.Jurisdiction),
                era_MailingProvinceState = Lookup(profile.MailingAddress.StateProvince),
                era_MailingCountry = Lookup(profile.MailingAddress.Country),
                address2_postalcode = profile.MailingAddress.PostalCode,

                emailaddress1 = profile.ContactDetails.Email,
                address1_telephone1 = profile.ContactDetails.Phone,

                era_phonenumberrefusal = string.IsNullOrEmpty(profile.ContactDetails.Phone),
                era_emailrefusal = string.IsNullOrEmpty(profile.ContactDetails.Email),
                era_secrettext = profile.SecretPhrase
            });

            // New needs assessment evacuee as pet
            var pets = (evacuation.PreliminaryNeedsAssessment.Pets ?? Array.Empty<Pet>()).Select(p => new era_needsassessmentevacuee
            {
                era_needsassessmentevacueeid = Guid.NewGuid(),
                era_numberofpets = Convert.ToInt32(p.Quantity),
                era_typeofpet = p.Type,
                era_evacueetype = LookupEvacueeType("Pet")
            });

            // add evacuation file to dynamics context
            dynamicsClient.AddToera_evacuationfiles(evacuationFile);
            // add needs assessment to dynamics context
            dynamicsClient.AddToera_needassessments(needsAssessment);
            // link evacuation file to needs assessment
            dynamicsClient.AddLink(evacuationFile, nameof(evacuationFile.era_needsassessment_EvacuationFile), needsAssessment);

            // New needs assessment evacuee as primary registrant
            var newNeedsAssessmentEvacueeRegistrant = new era_needsassessmentevacuee
            {
                era_needsassessmentevacueeid = Guid.NewGuid(),
                era_isprimaryregistrant = true,
                era_evacueetype = LookupEvacueeType("Person")
            };
            dynamicsClient.AddToera_needsassessmentevacuees(newNeedsAssessmentEvacueeRegistrant);
            // link registrant (contact) and needs assessment to evacuee record
            dynamicsClient.AddLink(dynamicsContact, nameof(dynamicsContact.era_NeedsAssessmentEvacuee_RegistrantID), newNeedsAssessmentEvacueeRegistrant);
            dynamicsClient.AddLink(needsAssessment, nameof(needsAssessment.era_NeedsAssessmentEvacuee_NeedsAssessmentID), newNeedsAssessmentEvacueeRegistrant);

            // Add New needs assessment evacuee members to dynamics context
            foreach (var member in members)
            {
                dynamicsClient.AddTocontacts(member);
                var newNeedsAssessmentEvacueeMember = new era_needsassessmentevacuee
                {
                    era_needsassessmentevacueeid = Guid.NewGuid(),
                    era_isprimaryregistrant = false,
                    era_evacueetype = LookupEvacueeType("Person")
                };
                dynamicsClient.AddToera_needsassessmentevacuees(newNeedsAssessmentEvacueeMember);
                // link members and needs assessment to evacuee record
                dynamicsClient.AddLink(member, nameof(member.era_NeedsAssessmentEvacuee_RegistrantID), newNeedsAssessmentEvacueeMember);
                dynamicsClient.AddLink(needsAssessment, nameof(needsAssessment.era_NeedsAssessmentEvacuee_NeedsAssessmentID), newNeedsAssessmentEvacueeMember);
            }

            // Add New needs assessment evacuee pets to dynamics context
            foreach (var petMember in pets)
            {
                dynamicsClient.AddToera_needsassessmentevacuees(petMember);
                // link pet to evacuee record
                dynamicsClient.AddLink(needsAssessment, nameof(needsAssessment.era_NeedsAssessmentEvacuee_NeedsAssessmentID), petMember);
            }

            //post as batch is not accepted by SSG. Sending with default option (multiple requests to the server stopping on the first failure)
            var results = await dynamicsClient.SaveChangesAsync(SaveChangesOptions.BatchWithSingleChangeset);
            //var results = await dynamicsClient.SaveChangesAsync();

            var queryResult = dynamicsClient.era_evacuationfiles
                //.Expand(f => f.era_city)
                //.Expand(f => f.era_province)
                //.Expand(f => f.era_country)
                .Where(f => f.era_evacuationfileid == evacuationFile.era_evacuationfileid).FirstOrDefault();

            return $"{essFileNumber:D9}";
        }
    }
}
