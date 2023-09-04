using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using AcrConnect.ServiceRegistry.Api.Client;
using AcrConnect.HomePage.Infrastructure.Config;
using AcrConnect.HomePage.ApplicationCallbacks;
using AcrConnect.Authentication.Api.Client;
using AcrConnect.API.Documentation;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using AcrConnect.HomePage.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NotNullAttribute = JetBrains.Annotations.NotNullAttribute;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authorization;
using AcrConnect.HomePage.Infrastructure.Extensions;
using AcrConnect.HomePage.Data;
using AcrConnect.HomePage.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace AcrConnect.HomePage
{
    public sealed class Startup
    {
        [NotNull] private readonly IConfiguration _configuration;
        [NotNull] private readonly IWebHostEnvironment _environment;

        #region Constructors

        public Startup([NotNull] IConfiguration configuration, [NotNull] IWebHostEnvironment environment)
        {
            Assert.NotNull(configuration, nameof(configuration));
            Assert.NotNull(environment, nameof(environment));

            _configuration = configuration;
            _environment = environment;
        }

        #endregion Constructors

        #region Public Methods
        public void ConfigureServices(IServiceCollection services)
        {
            var authenticationWebApiUri = _configuration.GetAuthenticationWebApiUri();

            services.AddControllers().AddNewtonsoftJson();

            services.AddAuthorization(options =>
            {
                if (!_environment.IsDevelopment())
                {
                    // by default all calls to this service are authenticated
                    options.FallbackPolicy = new AuthorizationPolicyBuilder()
                        .RequireAuthenticatedUser()
                        .Build();
                }
            });

            var identityProviderConfig = _configuration.GetSection("JwtValidationParameters").Get<JwtValidationParameters>();
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.RequireHttpsMetadata = identityProviderConfig.RequireHttpsMetadata;
                    options.Authority = identityProviderConfig.Authority;
                    options.Audience = identityProviderConfig.Audience;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        // TODO: the issuer can very based on internally vs externally requested tokens
                        // TODO: and also the external hostname varies by connect node
                        // TODO: this is going to be disabled for now -- public key validation and authority is good enough
                        // TODO: because we only have one tenant anyway: https://docs.microsoft.com/en-us/dotnet/api/microsoft.identitymodel.tokens.tokenvalidationparameters.validateissuer?view=azure-dotnet#remarks
                        ValidateIssuer = false,
                        ValidIssuers = identityProviderConfig.Issuers,
                        ValidAudiences = identityProviderConfig.Audiences,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromMinutes(1)
                    };
                });

            services.AddHttpClient();

            services.RegisterDbContexts(_configuration);

            services.AddAcrConnectServiceRegistryClient(new Uri(_configuration.GetServiceRegistryWebApiUri()));
            services.AddAcrConnectLocalAuthenticationClient(new Uri(authenticationWebApiUri));
            services.AddTransient<CallbacksService>();
            services.AddSingleton<CallbacksClient>();
            services.AddSingleton<ApplicationInitializer>();

            services.AddHealthChecks();

            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo()
                {
                    Title = "ACR Homepage Service",
                    Version = "v1",
                    Description = "This component provides APIs to list all the ACR Connect Apps on a web page and register callback APIs for the apps to decide the visibiliy and format of app cards based on user token"
                });

                options.DocumentFilter<EnumDocumentFilter>();
                options.DocumentFilter<YamlDocumentFilter>();

                // Set the comments path for the Swagger JSON and UI.
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                options.IncludeXmlComments(xmlPath);
            });

            services.Configure<CookiePolicyOptions>(options => 
            {
                options.HttpOnly = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always;
                options.Secure = CookieSecurePolicy.SameAsRequest;
            });

            services.AddHsts(options =>
            {
                options.Preload = true;
                options.IncludeSubDomains = true;
                options.MaxAge = TimeSpan.FromDays(365);
            });
        }

        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Required by infrastructure.")]
        [UsedImplicitly]
        public void Configure(IApplicationBuilder app)
        {
            IdentityModelEventSource.ShowPII = _environment.IsDevelopment();

            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
            app.UseHealthChecks("/health", new HealthCheckOptions
            {
                ResponseWriter = WriteResponse
            });
        }

        private static Task WriteResponse(HttpContext httpContext, HealthReport result)
        {
            httpContext.Response.ContentType = "application/json";
            httpContext.Response.Headers.Add("Strict-Transport-Security", "max-age=31536000; includeSubDomains");

            var json = new JObject(
                new JProperty("status", result.Status.ToString()),
                new JProperty("results", new JObject(result.Entries.Select(pair =>
                    new JProperty(pair.Key, new JObject(
                        new JProperty("status", pair.Value.Status.ToString()),
                        new JProperty("description", pair.Value.Description),
                        new JProperty("duration", pair.Value.Duration),
                        new JProperty("data", new JObject(pair.Value.Data.Select(p => new JProperty(p.Key, p.Value))))))))));

            return httpContext.Response.WriteAsync(json.ToString(Formatting.Indented));
        }
        #endregion
    }
}