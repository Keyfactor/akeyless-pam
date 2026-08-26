// Copyright 2025 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
// and limitations under the License.

using Xunit;

// Environment variable override tests (EnvironmentVariableOverrideTests) mutate process-wide environment
// variables (AKEYLESS_API_URL, AKEYLESS_AUTH_TYPE, AKEYLESS_ACCESS_ID, AKEYLESS_ACCESS_KEY). Test classes run
// in parallel by default in xUnit, which could race with other tests that assume these env vars are unset
// (e.g. GetPassword_UsesConfiguredUrl_WhenNoEnvVar). Disabling parallelization keeps env var state
// deterministic across the whole assembly.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
