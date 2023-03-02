// <copyright file="AWSMessagingUtils.cs" company="OpenTelemetry Authors">
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
using Amazon.Lambda.SNSEvents;
using Amazon.Lambda.SQSEvents;
using Newtonsoft.Json;
using OpenTelemetry.Context.Propagation;

namespace OpenTelemetry.Instrumentation.AWSLambda.Implementation;

internal class AWSMessagingUtils
{
    // SNS attribute types: https://docs.aws.amazon.com/sns/latest/dg/sns-message-attributes.html
    private const string SnsAttributeTypeString = "String";
    private const string SnsAttributeTypeStringArray = "String.Array";
    private const string SnsMessageAttributes = "MessageAttributes";

    internal static PropagationContext ExtractParentContext(SQSEvent sqsEvent)
    {
        if (sqsEvent == null)
        {
            return default;
        }

        // We assume there can be only one parent that's why we consider only a single (the last) record as the carrier.
        var message = sqsEvent.Records.LastOrDefault();
        return ExtractParentContext(message);
    }

    internal static PropagationContext ExtractParentContext(SQSEvent.SQSMessage sqsMessage)
    {
        if (sqsMessage == null)
        {
            return default;
        }

        var parentContext = Propagators.DefaultTextMapPropagator.Extract(default, sqsMessage.MessageAttributes, SqsMessageAttributeGetter);
        if (parentContext == default)
        {
            // SQS subscribed to SNS topic with raw delivery disabled case, i.e. SNS record serialized into SQS body.
            // https://docs.aws.amazon.com/sns/latest/dg/sns-large-payload-raw-message-delivery.html
            SNSEvent.SNSMessage snsMessage = GetSnsMessage(sqsMessage);
            if (snsMessage != null)
            {
                parentContext = ExtractParentContext(snsMessage);
            }
        }

        return parentContext;
    }

    internal static PropagationContext ExtractParentContext(SNSEvent snsEvent)
    {
        if (snsEvent == null)
        {
            return default;
        }

        // We assume there can be only one parent that's why we consider only a single (the last) record as the carrier.
        var record = snsEvent.Records.LastOrDefault();
        return ExtractParentContext(record);
    }

    internal static PropagationContext ExtractParentContext(SNSEvent.SNSRecord record)
    {
        return (record?.Sns != null) ?
            Propagators.DefaultTextMapPropagator.Extract(default, record.Sns.MessageAttributes, SnsMessageAttributeGetter) :
            default;
    }

    internal static PropagationContext ExtractParentContext(SNSEvent.SNSMessage message)
    {
        return (message != null) ?
            Propagators.DefaultTextMapPropagator.Extract(default, message.MessageAttributes, SnsMessageAttributeGetter) :
            default;
    }

    private static IEnumerable<string> SqsMessageAttributeGetter(IDictionary<string, SQSEvent.MessageAttribute> attributes, string attributeName)
    {
        SQSEvent.MessageAttribute attribute = attributes.GetValueByKeyIgnoringCase(attributeName);
        if (attribute == null)
        {
            return null;
        }

        return attribute.StringValue != null ?
            new[] { attribute.StringValue } :
            attribute.StringListValues;
    }

    private static IEnumerable<string> SnsMessageAttributeGetter(IDictionary<string, SNSEvent.MessageAttribute> attributes, string attributeName)
    {
        SNSEvent.MessageAttribute attribute = attributes.GetValueByKeyIgnoringCase(attributeName);
        if (attribute == null)
        {
            return null;
        }

        switch (attribute.Type)
        {
            case SnsAttributeTypeString when attribute.Value != null:
                return new[] { attribute.Value };
            case SnsAttributeTypeStringArray when attribute.Value != null:
                // Multiple values are stored as CSV (https://docs.aws.amazon.com/sns/latest/dg/sns-message-attributes.html).
                return attribute.Value.Split(',');
            default:
                return null;
        }
    }

    private static SNSEvent.SNSMessage GetSnsMessage(SQSEvent.SQSMessage sqsMessage)
    {
        SNSEvent.SNSMessage snsMessage = null;

        var body = sqsMessage.Body;
        if (body != null &&
            body.TrimStart().StartsWith("{") &&
            body.Contains(SnsMessageAttributes))
        {
            try
            {
                snsMessage = JsonConvert.DeserializeObject<SNSEvent.SNSMessage>(body);
            }
            catch (Exception)
            {
                // TODO: log exception.
                return null;
            }
        }

        return snsMessage;
    }
}
