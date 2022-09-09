// <copyright file="HttpSemanticConventions.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Amazon.Lambda.APIGatewayEvents;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.AWSLambda.Implementation
{
    internal class HttpSemanticConventions
    {
        // x-forwarded-... headres are described here https://docs.aws.amazon.com/elasticloadbalancing/latest/classic/x-forwarded-headers.html
        private const string HeaderXForwardedProto = "X-Forwarded-Proto";
        private const string HeaderHost = "Host";

        internal static IEnumerable<KeyValuePair<string, object>> GetHttpTags<TInput>(TInput input)
        {
            var tags = new List<KeyValuePair<string, object>>();

            string httpScheme = null;
            string httpTarget = null;
            string httpMethod = null;
            string hostName = null;
            string hostPort = null;

            switch (input)
            {
                case APIGatewayProxyRequest request:
                    httpScheme = GetHeaderValue(request, HeaderXForwardedProto);
                    httpTarget = request.Path;
                    httpMethod = request.HttpMethod;
                    var hostHeader = GetHeaderValue(request, HeaderHost);
                    (hostName, hostPort) = GetHostAndPort(httpScheme, hostHeader);
                    break;
                case APIGatewayHttpApiV2ProxyRequest requestV2:
                    httpScheme = GetHeaderValue(requestV2, HeaderXForwardedProto);
                    httpTarget = requestV2?.RequestContext?.Http?.Path;
                    httpMethod = requestV2?.RequestContext?.Http?.Method;
                    var hostHeaderV2 = GetHeaderValue(requestV2, HeaderHost);
                    (hostName, hostPort) = GetHostAndPort(httpScheme, hostHeaderV2);
                    break;
            }

            tags.AddStringTagIfNotNull(SemanticConventions.AttributeHttpScheme, httpScheme);
            tags.AddStringTagIfNotNull(SemanticConventions.AttributeHttpTarget, httpTarget);
            tags.AddStringTagIfNotNull(SemanticConventions.AttributeHttpMethod, httpMethod);
            tags.AddStringTagIfNotNull(SemanticConventions.AttributeNetHostName, hostName);
            tags.AddStringTagIfNotNull(SemanticConventions.AttributeNetHostPort, hostPort);

            return tags;
        }

        internal static void SetHttpTagsFromResult<TResult>(Activity activity, TResult result)
        {
            switch (result)
            {
                case APIGatewayProxyResponse response:
                    activity.SetTag(SemanticConventions.AttributeHttpStatusCode, response.StatusCode.ToString());
                    break;
                case APIGatewayHttpApiV2ProxyResponse responseV2:
                    activity.SetTag(SemanticConventions.AttributeHttpStatusCode, responseV2.StatusCode.ToString());
                    break;
            }
        }

        internal static (string Host, string Port) GetHostAndPort(string httpScheme, string hostHeaders)
        {
            if (hostHeaders == null)
            {
                return (null, null);
            }

            // In case of multiple headres we consider only the 1st.
            var hostHeader = hostHeaders.Split(',').First();
            var hostAndPort = hostHeader.Split(':');
            if (hostAndPort.Length > 1)
            {
                return (hostAndPort[0], hostAndPort[1]);
            }
            else
            {
                string defaultPort = null;
                switch (httpScheme)
                {
                    case "http":
                        defaultPort = "80";
                        break;
                    case "https":
                        defaultPort = "443";
                        break;
                }

                return (hostAndPort[0], defaultPort);
            }
        }

        private static string GetHeaderValue(APIGatewayProxyRequest request, string name)
        {
            if (request.MultiValueHeaders != null &&
                request.MultiValueHeaders.TryGetValueIgnoringCase(name, out var values))
            {
                return string.Join(",", values);
            }
            else if (request.Headers != null &&
                     request.Headers.TryGetValueIgnoringCase(name, out var value))
            {
                return value;
            }

            return null;
        }

        private static string GetHeaderValue(APIGatewayHttpApiV2ProxyRequest request, string name)
        {
            if (request.Headers != null &&
                request.Headers.TryGetValueIgnoringCase(name, out var header))
            {
                // Multiple values for the same header will be separated by a comma.
                return header;
            }

            return null;
        }
    }
}