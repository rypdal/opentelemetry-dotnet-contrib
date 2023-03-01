// <copyright file="AWSMessagingUtilsTests.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using Amazon.Runtime;
using Amazon.Runtime.Internal;
using Moq;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Contrib.Instrumentation.AWS.Implementation;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Contrib.Instrumentation.AWS.Tests.Implementation;

public class AWSMessagingUtilsTests
{
    private const string TraceId = "5759e988bd862e3fe1be46a994272793";
    private const string ParentId = "53995c3f42cd8ad8";
    private const string TraceState = "trace-state";

    public AWSMessagingUtilsTests()
    {
        Sdk.CreateTracerProviderBuilder()
            .Build();
    }

    [Theory]
    [InlineData(AWSServiceType.SQSService)]
    [InlineData(AWSServiceType.SNSService)]
    public void Inject_ParametersCollectionSizeReachesLimit_TraceDataNotInjected(string serviceType)
    {
        AmazonWebServiceRequest originalRequest = TestsHelper.CreateOriginalRequest(serviceType, 10);
        var parameters = TestsHelper.DefaultParameterCollection(serviceType);
        parameters.AddStringParameters(serviceType, originalRequest);

        var request = new Mock<IRequest>();
        request.Setup(x => x.ParameterCollection)
            .Returns(parameters);

        var context = new Mock<IRequestContext>();
        context.Setup(x => x.OriginalRequest)
            .Returns(originalRequest);
        context.Setup(x => x.Request)
            .Returns(request.Object);

        var adapter = TestsHelper.CreateRequestContextAdapter(serviceType, context.Object);

        AWSMessagingUtils.Inject(adapter, CreatePropagationContext());

        Assert.Equal(31, parameters.Count);
    }

    [Theory]
    [InlineData(AWSServiceType.SQSService)]
    [InlineData(AWSServiceType.SNSService)]
    public void Inject_DefaultParametersCollection_TraceDataInjected(string serviceType)
    {
        var expectedParameters = new List<KeyValuePair<string, string>>()
        {
            new KeyValuePair<string, string>("traceparent", $"00-{TraceId}-{ParentId}-00"),
            new KeyValuePair<string, string>("tracestate", "trace-state"),
        };

        AmazonWebServiceRequest originalRequest = TestsHelper.CreateOriginalRequest(serviceType, 0);
        var parameters = TestsHelper.DefaultParameterCollection(serviceType);

        var request = new Mock<IRequest>();
        request.Setup(x => x.ParameterCollection)
            .Returns(parameters);

        var context = new Mock<IRequestContext>();
        context.Setup(x => x.OriginalRequest)
            .Returns(originalRequest);
        context.Setup(x => x.Request)
            .Returns(request.Object);

        var adapter = TestsHelper.CreateRequestContextAdapter(serviceType, context.Object);

        AWSMessagingUtils.Inject(adapter, CreatePropagationContext());

        TestsHelper.AssertStringParameters(serviceType, expectedParameters, parameters);
    }

    [Theory]
    [InlineData(AWSServiceType.SQSService)]
    [InlineData(AWSServiceType.SNSService)]
    public void Inject_ParametersCollectionWithCustomParameter_TraceDataInjected(string serviceType)
    {
        var expectedParameters = new List<KeyValuePair<string, string>>()
        {
            new KeyValuePair<string, string>("name1", "value1"),
            new KeyValuePair<string, string>("traceparent", $"00-{TraceId}-{ParentId}-00"),
            new KeyValuePair<string, string>("tracestate", "trace-state"),
        };

        AmazonWebServiceRequest originalRequest = TestsHelper.CreateOriginalRequest(serviceType, 1);
        var parameters = TestsHelper.DefaultParameterCollection(serviceType);
        parameters.AddStringParameters(serviceType, originalRequest);

        var request = new Mock<IRequest>();
        request.Setup(x => x.ParameterCollection)
            .Returns(parameters);

        var context = new Mock<IRequestContext>();
        context.Setup(x => x.OriginalRequest)
            .Returns(originalRequest);
        context.Setup(x => x.Request)
            .Returns(request.Object);

        var adapter = TestsHelper.CreateRequestContextAdapter(serviceType, context.Object);

        AWSMessagingUtils.Inject(adapter, CreatePropagationContext());

        TestsHelper.AssertStringParameters(serviceType, expectedParameters, parameters);
    }

    [Theory]
    [InlineData(AWSServiceType.SQSService)]
    [InlineData(AWSServiceType.SNSService)]
    public void Inject_ParametersCollectionWithTraceData_TraceDataNotInjected(string serviceType)
    {
        var expectedParameters = new List<KeyValuePair<string, string>>()
        {
            new KeyValuePair<string, string>("traceparent", $"00-{TraceId}-{ParentId}-00"),
            new KeyValuePair<string, string>("tracestate", "trace-state"),
        };

        AmazonWebServiceRequest originalRequest = TestsHelper.CreateOriginalRequest(serviceType, 0);
        originalRequest.AddAttribute("traceparent", $"00-{TraceId}-{ParentId}-00");
        originalRequest.AddAttribute("tracestate", $"trace-state");

        var parameters = TestsHelper.DefaultParameterCollection(serviceType);
        parameters.AddStringParameters(serviceType, originalRequest);

        var request = new Mock<IRequest>();
        request.Setup(x => x.ParameterCollection)
            .Returns(parameters);

        var context = new Mock<IRequestContext>();
        context.Setup(x => x.OriginalRequest)
            .Returns(originalRequest);
        context.Setup(x => x.Request)
            .Returns(request.Object);

        var adapter = TestsHelper.CreateRequestContextAdapter(serviceType, context.Object);

        AWSMessagingUtils.Inject(adapter, CreatePropagationContext());

        TestsHelper.AssertStringParameters(serviceType, expectedParameters, parameters);
    }

    private static PropagationContext CreatePropagationContext()
    {
        var traceId = ActivityTraceId.CreateFromString(TraceId.AsSpan());
        var parentId = ActivitySpanId.CreateFromString(ParentId.AsSpan());
        var traceFlags = ActivityTraceFlags.None;
        var activityContext = new ActivityContext(traceId, parentId, traceFlags, TraceState, isRemote: true);

        return new PropagationContext(activityContext, Baggage.Current);
    }
}
