// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Linq;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace Microsoft.Health.Fhir.Shared.Core.Features.Search.Converters
{
    public class DeidentificationConverter
    {
        private readonly FhirJsonParser _parser = new FhirJsonParser();
        private readonly FhirJsonSerializer _serializer = new FhirJsonSerializer();

        private static void RedactHumanName(HumanName name)
        {
            name.Text = name.Text == null ? name.Text : string.Empty;
            name.Family = name.Family == null ? name.Family : string.Empty;
            name.Given = name.Given.Select(x => string.Empty);
            name.Prefix = name.Prefix.Select(x => string.Empty);
            name.Suffix = name.Suffix.Select(x => string.Empty);
            name.Period = new Period();
        }

        private static void RedactTelecom(ContactPoint telecom)
        {
            telecom.Value = telecom.Value == null ? telecom.Value : string.Empty;
            telecom.Period = new Period();
        }

        private static void RedactAddress(Address address)
        {
            address.Text = address.Text == null ? address.Text : string.Empty;
            address.Line = address.Line.Select(x => string.Empty);
            address.City = address.City == null ? address.City : string.Empty;
            address.District = address.District == null ? address.District : string.Empty;
            address.PostalCode = address.PostalCode == null ? address.PostalCode : string.Empty;
            address.Period = new Period();
        }

        public static bool NeedDeidentification(string resourceType)
        {
            return string.Equals(resourceType, ResourceType.Patient.ToString(), StringComparison.Ordinal) ||
                string.Equals(resourceType, ResourceType.Account.ToString(), StringComparison.Ordinal);
        }

        public string DeidentifyPatientData(string data)
        {
            Patient patient = _parser.Parse<Patient>(data);

            // Redact name, telecom, birthdate and address
            foreach (HumanName name in patient.Name)
            {
                RedactHumanName(name);
            }

            foreach (ContactPoint telecom in patient.Telecom)
            {
                RedactTelecom(telecom);
            }

            patient.BirthDate = patient.BirthDate == null ? patient.BirthDate : string.Empty;

            foreach (Address address in patient.Address)
            {
                RedactAddress(address);
            }

            return _serializer.SerializeToString(patient);
        }

        public string DeidentifyAccountData(string data)
        {
            Account account = _parser.Parse<Account>(data);

            foreach (Identifier identifier in account.Identifier)
            {
                identifier.Value = string.Empty;
            }

            return _serializer.SerializeToString(account);
        }
    }
}
