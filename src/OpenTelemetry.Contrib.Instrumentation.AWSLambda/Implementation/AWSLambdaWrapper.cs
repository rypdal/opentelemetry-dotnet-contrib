// <copyright file="AWSLambdaWrapper.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Contrib.Instrumentation.AWSLambda.Implementation
{
    /// <summary>
    /// Wrapper class for AWS Lambda handlers.
    /// </summary>
    public class AWSLambdaWrapper
    {
        private static readonly ActivitySource AWSLambdaActivitySource = new(AWSLambdaUtils.ActivitySourceName);

        /// <summary>
        /// Gets or sets a value indicating whether AWS X-Ray propagation should be ignored. Default value is false.
        /// </summary>
        internal static bool DisableAwsXRayContextExtraction { get; set; }

        /// <summary>
        /// Tracing wrapper for Lambda handler.
        /// </summary>
        /// <typeparam name="TInput">Input.</typeparam>
        /// <typeparam name="TResult">Output result.</typeparam>
        /// <param name="tracerProvider">TracerProvider passed in.</param>
        /// <param name="lambdaHandler">Lambda handler function passed in.</param>
        /// <param name="input">Instance of input.</param>
        /// <param name="context">Instance of lambda context.</param>
        /// <param name="parentContext">
        /// The optional parent context <see cref="ActivityContext"/> is used for Activity object creation.
        /// If no parent context provided, incoming request is used to extract one.
        /// If parent is not extracted from incoming request then X-Ray propagation is used to extract one
        /// unless X-Ray propagation is disabled in the configuration for this wrapper.
        /// </param>
        /// <returns>Instance of output result.</returns>
        public static TResult Trace<TInput, TResult>(
            TracerProvider tracerProvider,
            Func<TInput, ILambdaContext, TResult> lambdaHandler,
            TInput input,
            ILambdaContext context,
            ActivityContext parentContext = default)
        {
            return TraceInternal(tracerProvider, lambdaHandler, input, context, parentContext);
        }

        /// <summary>
        /// Tracing wrapper for Lambda handler.
        /// </summary>
        /// <typeparam name="TInput">Input.</typeparam>
        /// <param name="tracerProvider">TracerProvider passed in.</param>
        /// <param name="lambdaHandler">Lambda handler function passed in.</param>
        /// <param name="input">Instance of input.</param>
        /// <param name="context">Instance of lambda context.</param>
        /// <param name="parentContext">
        /// The optional parent context <see cref="ActivityContext"/> is used for Activity object creation.
        /// If no parent context provided, incoming request is used to extract one.
        /// If parent is not extracted from incoming request then X-Ray propagation is used to extract one
        /// unless X-Ray propagation is disabled in the configuration for this wrapper.
        /// </param>
        public static void Trace<TInput>(
            TracerProvider tracerProvider,
            Action<TInput, ILambdaContext> lambdaHandler,
            TInput input,
            ILambdaContext context,
            ActivityContext parentContext = default)
        {
            Func<TInput, ILambdaContext, object> func = (input, context) =>
            {
                lambdaHandler(input, context);
                return null;
            };
            TraceInternal(tracerProvider, func, input, context, parentContext);
        }

        /// <summary>
        /// Tracing wrapper for async Lambda handler.
        /// </summary>
        /// <typeparam name="TInput">Input.</typeparam>
        /// <param name="tracerProvider">TracerProvider passed in.</param>
        /// <param name="lambdaHandler">Lambda handler function passed in.</param>
        /// <param name="input">Instance of input.</param>
        /// <param name="context">Lambda context (optional, but strongly recommended).</param>
        /// <param name="parentContext">
        /// The optional parent context <see cref="ActivityContext"/> is used for Activity object creation.
        /// If no parent context provided, incoming request is used to extract one.
        /// If parent is not extracted from incoming request then X-Ray propagation is used to extract one
        /// unless X-Ray propagation is disabled in the configuration for this wrapper.
        /// </param>
        /// <returns>Task.</returns>
        public static Task Trace<TInput>(
            TracerProvider tracerProvider,
            Func<TInput, ILambdaContext, Task> lambdaHandler,
            TInput input,
            ILambdaContext context,
            ActivityContext parentContext = default)
        {
            Func<TInput, ILambdaContext, Task<object>> func = async (input, context) =>
            {
                await lambdaHandler(input, context);
                return Task.FromResult<object>(null);
            };
            return TraceInternalAsync(tracerProvider, func, input, context, parentContext);
        }

        /// <summary>
        /// Tracing wrapper for async Lambda handler.
        /// </summary>
        /// <typeparam name="TInput">Input.</typeparam>
        /// <typeparam name="TResult">Output result.</typeparam>
        /// <param name="tracerProvider">TracerProvider passed in.</param>
        /// <param name="lambdaHandler">Lambda handler function passed in.</param>
        /// <param name="input">Instance of input.</param>
        /// <param name="context">Instance of lambda context.</param>
        /// <param name="parentContext">
        /// The optional parent context <see cref="ActivityContext"/> is used for Activity object creation.
        /// If no parent context provided, incoming request is used to extract one.
        /// If parent is not extracted from incoming request then X-Ray propagation is used to extract one
        /// unless X-Ray propagation is disabled in the configuration for this wrapper.
        /// </param>
        /// <returns>Task of result.</returns>
        public static Task<TResult> Trace<TInput, TResult>(
            TracerProvider tracerProvider,
            Func<TInput, ILambdaContext, Task<TResult>> lambdaHandler,
            TInput input,
            ILambdaContext context,
            ActivityContext parentContext = default)
        {
            return TraceInternalAsync(tracerProvider, lambdaHandler, input, context, parentContext);
        }

        /// <summary>
        /// Tracing wrapper for Lambda handler.
        /// </summary>
        /// <param name="tracerProvider">TracerProvider passed in.</param>
        /// <param name="lambdaHandler">Lambda handler function passed in.</param>
        /// <param name="context">Instance of lambda context.</param>
        /// <param name="parentContext">
        /// The optional parent context <see cref="ActivityContext"/> is used for Activity object creation.
        /// If no parent context provided, incoming request is used to extract one.
        /// If parent is not extracted from incoming request then X-Ray propagation is used to extract one
        /// unless X-Ray propagation is disabled in the configuration for this wrapper.
        /// </param>
        public static void Trace(
            TracerProvider tracerProvider,
            Action<ILambdaContext> lambdaHandler,
            ILambdaContext context,
            ActivityContext parentContext = default)
        {
            Func<object, ILambdaContext, object> func = (_, context) =>
            {
                lambdaHandler(context);
                return null;
            };
            TraceInternal(tracerProvider, func, null, context, parentContext);
        }

        /// <summary>
        /// Tracing wrapper for async Lambda handler.
        /// </summary>
        /// <param name="tracerProvider">TracerProvider passed in.</param>
        /// <param name="lambdaHandler">Lambda handler function passed in.</param>
        /// <param name="context">Instance of lambda context.</param>
        /// <param name="parentContext">
        /// The optional parent context <see cref="ActivityContext"/> is used for Activity object creation.
        /// If no parent context provided, incoming request is used to extract one.
        /// If parent is not extracted from incoming request then X-Ray propagation is used to extract one
        /// unless X-Ray propagation is disabled in the configuration.
        /// </param>
        /// <returns>Task.</returns>
        public static Task Trace(
            TracerProvider tracerProvider,
            Func<ILambdaContext, Task> lambdaHandler,
            ILambdaContext context,
            ActivityContext parentContext = default)
        {
            Func<object, ILambdaContext, Task<object>> func = async (_, context) =>
            {
                await lambdaHandler(context);
                return Task.FromResult<object>(null);
            };
            return TraceInternalAsync(tracerProvider, func, null, context, parentContext);
        }

        internal static Activity OnFunctionStart<TInput>(TInput input, ILambdaContext context, ActivityContext parentContext = default)
        {
            if (parentContext == default)
            {
                parentContext = AWSLambdaUtils.ExtractParentContext(input);
                if (parentContext == default && !DisableAwsXRayContextExtraction)
                {
                    parentContext = AWSLambdaUtils.GetXRayParentContext();
                }
            }

            var functionTags = AWSLambdaUtils.GetFunctionTags(input, context);
            var httpTags = HttpSemanticConventions.GetHttpTags(input);

            // We assume that functionTags and httpTags have no intersection.
            var activityName = AWSLambdaUtils.GetFunctionName(context) ?? "AWS Lambda Invoke";
            var activity = AWSLambdaActivitySource.StartActivity(activityName, ActivityKind.Server, parentContext, functionTags.Union(httpTags));

            return activity;
        }

        private static void OnFunctionStop(Activity activity, TracerProvider tracerProvider)
        {
            if (activity != null)
            {
                activity.Stop();
            }

            // force flush before function quit in case of Lambda freeze.
            tracerProvider?.ForceFlush();
        }

        private static void OnException(Activity activity, Exception exception)
        {
            if (activity != null)
            {
                if (activity.IsAllDataRequested)
                {
                    activity.RecordException(exception);
                    activity.SetStatus(Status.Error.WithDescription(exception.Message));
                }
            }
        }

        private static TResult TraceInternal<TInput, TResult>(
            TracerProvider tracerProvider,
            Func<TInput, ILambdaContext, TResult> handler,
            TInput input,
            ILambdaContext context,
            ActivityContext parentContext = default)
        {
            var activity = OnFunctionStart(input, context, parentContext);
            try
            {
                var result = handler(input, context);
                HttpSemanticConventions.SetHttpTagsFromRequest(activity, result);
                return result;
            }
            catch (Exception ex)
            {
                OnException(activity, ex);

                throw;
            }
            finally
            {
                OnFunctionStop(activity, tracerProvider);
            }
        }

        private static async Task<TResult> TraceInternalAsync<TInput, TResult>(
            TracerProvider tracerProvider,
            Func<TInput, ILambdaContext, Task<TResult>> handlerAsync,
            TInput input,
            ILambdaContext context,
            ActivityContext parentContext = default)
        {
            var activity = OnFunctionStart(input, context, parentContext);
            try
            {
                var result = await handlerAsync(input, context);
                HttpSemanticConventions.SetHttpTagsFromRequest(activity, result);
                return result;
            }
            catch (Exception ex)
            {
                OnException(activity, ex);

                throw;
            }
            finally
            {
                OnFunctionStop(activity, tracerProvider);
            }
        }
    }
}
