using System;
using AcrConnect.Framework.Api.AspNetCore;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using AcrConnect.HomePage.Infrastructure.Extensions;

namespace AcrConnect.HomePage.Controllers
{
    [ApiController]
    public sealed class VersionController : ControllerBase2
    {
        [NotNull] private readonly IWebHostEnvironment _environment;
        [NotNull] private readonly IConfiguration _configuration;

        public InternalVersionController([NotNull] IWebHostEnvironment environment, [NotNull] IConfiguration configuration)
        {
            Assert.NotNull(environment, nameof(environment));
            Assert.NotNull(configuration, nameof(configuration));

            _environment = environment;
            _configuration = configuration;
        }


        /// <summary>
        /// Get Version
        /// </summary>
        /// <returns></returns>
        /// <response code="200">Ok</response>
        [NotNull]
        [HttpGet, Route("internal/version")]
        [ProducesResponseType(typeof(Documentation.Schemas.AppVersion), 200)]
        public IActionResult Get()
        {
            var assembly = GetType().Assembly;
            var version = assembly.GetName().Version.ToString();
            var logsPath = _configuration.GetLogsPath();
            var dockerized = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
            var authenticationUri = _configuration.GetAuthenticationWebApiUri();
            var baseExternalUri = _configuration.GetBaseExternalUrl();
            
            return Ok(new Documentation.Schemas.AppVersion
            {
                clrVersion = Environment.Version,
                osVersion = Environment.OSVersion.VersionString,
                currentVersion = version,
                environment = _environment.EnvironmentName,
                name = _environment.ApplicationName,
                authenticationUri = authenticationUri,
                baseExternalUri = baseExternalUri,
                logsPath = logsPath,
                dockerized = dockerized
            });
        }
    }
}
