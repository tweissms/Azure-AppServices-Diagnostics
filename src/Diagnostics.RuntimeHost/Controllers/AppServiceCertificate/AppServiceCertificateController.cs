﻿using Diagnostics.ModelsAndUtils.Models;
using Diagnostics.RuntimeHost.Models;
using Diagnostics.RuntimeHost.Services;
using Diagnostics.RuntimeHost.Services.SourceWatcher;
using Diagnostics.RuntimeHost.Utilities;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Diagnostics.RuntimeHost.Controllers
{
    [Produces("application/json")]
    [Route(UriElements.AppServiceCertResource)]
    public sealed class AppServiceCertificateController : DiagnosticControllerBase<AppServiceCertificate>
    {
        public AppServiceCertificateController(IStampService stampService, ICompilerHostClient compilerHostClient, ISourceWatcherService sourceWatcherService, IInvokerCacheService invokerCache, IDataSourcesConfigurationService dataSourcesConfigService)
            : base(stampService, compilerHostClient, sourceWatcherService, invokerCache, dataSourcesConfigService)
        {
        }

        [HttpPost(UriElements.Query)]
        public async Task<IActionResult> ExecuteQuery(string subscriptionId, string resourceGroupName, string certificateName, [FromBody]CompilationBostBody<dynamic> jsonBody, string startTime = null, string endTime = null, string timeGrain = null)
        {
            return await base.ExecuteQuery(GetResource(subscriptionId, resourceGroupName, certificateName), jsonBody, startTime, endTime, timeGrain);
        }

        [HttpPost(UriElements.Detectors)]
        public async Task<IActionResult> ListDetectors(string subscriptionId, string resourceGroupName, string certificateName, [FromBody] dynamic postBody)
        {
            return await base.ListDetectors(GetResource(subscriptionId, resourceGroupName, certificateName));
        }

        [HttpPost(UriElements.Detectors + UriElements.DetectorResource)]
        public async Task<IActionResult> GetDetector(string subscriptionId, string resourceGroupName, string certificateName, string detectorId, [FromBody] dynamic postBody, string startTime = null, string endTime = null, string timeGrain = null)
        {
            return await base.GetDetector(GetResource(subscriptionId, resourceGroupName, certificateName), detectorId, startTime, endTime, timeGrain);
        }

        [HttpPost(UriElements.Detectors + UriElements.DetectorResource + UriElements.StatisticsQuery)]
        public async Task<IActionResult> ExecuteSystemQuery(string subscriptionId, string resourceGroupName, string certificateName, string detectorId, [FromBody]CompilationBostBody<dynamic> jsonBody, string dataSource = null, string timeRange = null)
        {
            return await base.ExecuteQuery(GetResource(subscriptionId, resourceGroupName, certificateName), jsonBody, null, null, null, detectorId, dataSource, timeRange);
        }

        [HttpPost(UriElements.Detectors + UriElements.DetectorResource + UriElements.Statistics + UriElements.StatisticsResource)]
        public async Task<IActionResult> GetSystemInvoker(string subscriptionId, string resourceGroupName, string certificateName, string detectorId, string invokerId, string dataSource = null, string timeRange = null)
        {
            return await base.GetSystemInvoker(GetResource(subscriptionId, resourceGroupName, certificateName), detectorId, invokerId, dataSource, timeRange);
        }

        [HttpPost(UriElements.Insights)]
        public async Task<IActionResult> GetInsights(string subscriptionId, string resourceGroupName, string certificateName, [FromBody] dynamic postBody, string pesId, string supportTopicId = null, string startTime = null, string endTime = null, string timeGrain = null)
        {
            return await base.GetInsights(GetResource(subscriptionId, resourceGroupName, certificateName), pesId, supportTopicId, startTime, endTime, timeGrain);
        }

        [HttpPost(UriElements.Publish)]
        public async Task<IActionResult> PublishDetector(string subscriptionId, string resourceGroupName, string certificateName, [FromBody] DetectorPackage pkg)
        {
            return await base.PublishDetector(pkg);
        }
    }
}
