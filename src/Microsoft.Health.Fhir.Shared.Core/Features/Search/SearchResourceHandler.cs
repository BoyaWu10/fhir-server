// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EnsureThat;
using Hl7.Fhir.Model;
using MediatR;
using Microsoft.Health.Fhir.Core.Features.Persistence;
using Microsoft.Health.Fhir.Core.Messages.Search;
using Microsoft.Health.Fhir.Core.Models;
using Microsoft.Health.Fhir.Shared.Core.Features.Search.Converters;

namespace Microsoft.Health.Fhir.Core.Features.Search
{
    /// <summary>
    /// Handler for searching resource.
    /// </summary>
    public class SearchResourceHandler : IRequestHandler<SearchResourceRequest, SearchResourceResponse>
    {
        private readonly ISearchService _searchService;
        private readonly IBundleFactory _bundleFactory;
        private readonly DeidentificationConverter _deidConverter = new DeidentificationConverter();

        /// <summary>
        /// Initializes a new instance of the <see cref="SearchResourceHandler"/> class.
        /// </summary>
        /// <param name="searchService">The search service to execute the search operation.</param>
        /// <param name="bundleFactory">The bundle factory.</param>
        public SearchResourceHandler(ISearchService searchService, IBundleFactory bundleFactory)
        {
            EnsureArg.IsNotNull(searchService, nameof(searchService));
            EnsureArg.IsNotNull(bundleFactory, nameof(bundleFactory));

            _searchService = searchService;
            _bundleFactory = bundleFactory;
        }

        /// <inheritdoc />
        public async Task<SearchResourceResponse> Handle(SearchResourceRequest message, CancellationToken cancellationToken)
        {
            EnsureArg.IsNotNull(message, nameof(message));

            SearchResult searchResult = await _searchService.SearchAsync(message.ResourceType, message.Queries, cancellationToken);

            if (!DeidentificationConverter.NeedDeidentification(message.ResourceType))
            {
                ResourceElement bundle = _bundleFactory.CreateSearchBundle(searchResult);
                return new SearchResourceResponse(bundle);
            }
            else
            {
                var redactedEntries = new List<SearchResultEntry>();
                foreach (SearchResultEntry entry in searchResult.Results)
                {
                    string redactedData = string.Empty;
                    if (string.Equals(message.ResourceType, ResourceType.Patient.ToString(), StringComparison.Ordinal))
                    {
                        redactedData = _deidConverter.DeidentifyPatientData(entry.Resource.RawResource.Data);
                    }
                    else if (string.Equals(message.ResourceType, ResourceType.Account.ToString(), StringComparison.Ordinal))
                    {
                        redactedData = _deidConverter.DeidentifyAccountData(entry.Resource.RawResource.Data);
                    }

                    var redactedEntry = new SearchResultEntry(
                    new ResourceWrapper(
                        entry.Resource.ResourceId,
                        entry.Resource.Version,
                        entry.Resource.ResourceTypeName,
                        new RawResource(redactedData, FhirResourceFormat.Json),
                        entry.Resource.Request,
                        entry.Resource.LastModified,
                        entry.Resource.IsDeleted,
                        entry.Resource.SearchIndices,
                        entry.Resource.CompartmentIndices,
                        entry.Resource.LastModifiedClaims),
                    entry.SearchEntryMode);
                    redactedEntries.Add(redactedEntry);
                }

                var redactedSearchResult = new SearchResult(
                    redactedEntries as IEnumerable<SearchResultEntry>,
                    searchResult.UnsupportedSearchParameters,
                    searchResult.UnsupportedSortingParameters,
                    searchResult.ContinuationToken);

                ResourceElement bundle = _bundleFactory.CreateSearchBundle(redactedSearchResult);
                return new SearchResourceResponse(bundle);
            }
        }
    }
}
