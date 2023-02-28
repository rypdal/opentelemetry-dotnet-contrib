// <copyright file="SqsMessageAttributeFormatter.cs" company="OpenTelemetry Authors">
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

using System.Text.RegularExpressions;

namespace OpenTelemetry.Contrib.Instrumentation.AWS.Implementation;

internal class SqsMessageAttributeFormatter : IAWSMessageAttributeFormatter
{
    Regex IAWSMessageAttributeFormatter.AttributeNameRegex => new(@"MessageAttribute\.\d+\.Name");

    string IAWSMessageAttributeFormatter.AttributeNamePrefix => "MessageAttribute";

    int? IAWSMessageAttributeFormatter.GetAttributeIndex(string attributeName)
    {
        var parts = attributeName.Split('.');
        return (parts.Length >= 2 && int.TryParse(parts[1], out int index)) ?
            index :
            null;
    }
}
