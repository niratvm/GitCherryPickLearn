using AcrConnect.Framework.Api.AspNetCore;
using AcrConnect.HomePage.Infrastructure.Extensions;
using AcrConnect.HomePage.Models;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Globalization;

namespace AcrConnect.HomePage.Controllers
{
    [ApiController]
    [Authorize(Roles = "admin")]
    public sealed class InvitationController : ControllerBase2
    {
        #region Fields

        [NotNull] private readonly ILogger<InvitationController> _log;

        public static readonly string ACRIDSignupEndpoint = "ACRCONNECT_HP_ACRID_SIGNUPENDPOINT";

        [NotNull] private readonly IConfiguration _configuration;

        #endregion

        #region Constructor

        public InvitationController(ILogger<InvitationController> log, IConfiguration configuration)
        {
            _log = log;
            _configuration = configuration;
        }

        #endregion

        #region Actions

        [HttpPost, Route("invitation")]
        public IActionResult InviteUser(UserInviteModel invitationDetails)
        {
            try
            {
                var acrIdSignupUrl = _configuration.GetOktaSignupUrl();
                var emailTemplate = _configuration.GetEmailTemplate();
                var acrconnectUrl = _configuration.GetACRConnectUrl();
                var adminName = $"{invitationDetails.AdminFirstName} {invitationDetails.AdminLastName}";
                var baseExternalUrl = _configuration.GetBaseExternalUrl();
                var verificationLink = $"{baseExternalUrl}invitation/confirm?token={invitationDetails.Token}&email={invitationDetails.Email}";
                if (string.IsNullOrEmpty(emailTemplate))
                {
                    return StatusCode(409, "Email template not configured");
                }
                var emailResult = new InviteEmailDto()
                {
                    EmailText = string.Format(CultureInfo.InvariantCulture, emailTemplate, adminName, acrconnectUrl, acrIdSignupUrl, verificationLink, invitationDetails.AdminEmail)
                };
                return Ok(emailResult);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
        #endregion
    }
}
