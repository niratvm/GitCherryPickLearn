using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AcrConnect.HomePage.Test
{
    public class AuthorizingHandler : DelegatingHandler
    {
        private readonly string _headerValue;
        public AuthorizingHandler(HttpMessageHandler inner, string headerValue)
            : base(inner)
        {
            _headerValue = headerValue;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Add("Authorization", _headerValue);
            return base.SendAsync(request, cancellationToken);
        }
    }
}
