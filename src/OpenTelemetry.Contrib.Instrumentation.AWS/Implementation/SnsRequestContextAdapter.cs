// <copyright file="SnsRequestContextAdapter.cs" company="OpenTelemetry Authors">
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

using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.Runtime;
using Amazon.Runtime.Internal;
using Amazon.SimpleNotificationService.Model;

namespace OpenTelemetry.Contrib.Instrumentation.AWS.Implementation;
internal class SnsRequestContextAdapter
{
    // SQS/SNS message attributes collection size limit according to
    // https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-message-metadata.html and
    // https://docs.aws.amazon.com/sns/latest/dg/sns-message-attributes.html
    private const int MaxMessageAttributes = 10;

    private readonly ParameterCollection parameters;
    private readonly PublishRequest originalRequest;

    private SnsRequestContextAdapter(ParameterCollection parameters, PublishRequest originalRequest)
    {
        this.parameters = parameters;
        this.originalRequest = originalRequest;
    }

    public void AddAttributes(IReadOnlyDictionary<string, string> attributes)
    {
        if (attributes.Keys.Any(k => this.ContainsAttribute(k)))
        {
            // If at least one attribute is already present in the request then we skip the injection.
            return;
        }

        int attributesCount = this.originalRequest.MessageAttributes.Count;
        if (attributes.Count + attributesCount > MaxMessageAttributes)
        {
            // TODO: add logging (event source).
            return;
        }

        int nextAttributeIndex = attributesCount + 1;
        foreach (var param in attributes)
        {
            this.AddAttribute(param.Key, param.Value, nextAttributeIndex);
            nextAttributeIndex++;
        }
    }

    internal static Action<IReadOnlyDictionary<string, string>>? CreateAddAttributesAction(IRequestContext context)
    {
        var parameters = context.Request?.ParameterCollection;
        var originalRequest = context.OriginalRequest as PublishRequest;
        if (originalRequest?.MessageAttributes == null || parameters == null)
        {
            return null;
        }

        var request = new SnsRequestContextAdapter(parameters, originalRequest);
        return request.AddAttributes;
    }

    private void AddAttribute(string name, string value, int nextAttributeIndex)
    {
        var prefix = "MessageAttributes.entry." + nextAttributeIndex;
        this.parameters.Add(prefix + ".Name", name);
        this.parameters.Add(prefix + ".Value.DataType", "String");
        this.parameters.Add(prefix + ".Value.StringValue", value);

        // Add injected attributes to the original request as well.
        // This dictionary must be in sync with parameters collection to pass through the MD5 hash matching check.
        this.originalRequest.MessageAttributes.Add(name, new MessageAttributeValue { DataType = "String", StringValue = value });
    }

    private bool ContainsAttribute(string name)
        => this.originalRequest.MessageAttributes.ContainsKey(name);
}
