﻿/*
 * Copyright 2014, 2015 Dominick Baier, Brock Allen
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using IdentityModel;
using IdentityServer4.Core.Extensions;
using IdentityServer4.Core.Models;
using IdentityServer4.Core.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace IdentityServer4.Core.Validation
{
    /// <summary>
    /// Validates a shared secret stored in SHA256 or SHA512
    /// </summary>
    public class HashedSharedSecretValidator : ISecretValidator
    {
        private readonly ILogger _logger;

        public HashedSharedSecretValidator(ILogger<HashedSharedSecretValidator> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Validates a secret
        /// </summary>
        /// <param name="secrets">The stored secrets.</param>
        /// <param name="parsedSecret">The received secret.</param>
        /// <returns>
        /// A validation result
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Id or cedential</exception>
        public Task<SecretValidationResult> ValidateAsync(IEnumerable<Secret> secrets, ParsedSecret parsedSecret)
        {
            var fail = Task.FromResult(new SecretValidationResult { Success = false });
            var success = Task.FromResult(new SecretValidationResult { Success = true });

            if (parsedSecret.Type != Constants.ParsedSecretTypes.SharedSecret)
            {
                _logger.LogDebug("Parsed secret should not be of type {type}", parsedSecret.Type ?? "null");
                return fail;
            }

            var sharedSecret = parsedSecret.Credential as string;

            if (parsedSecret.Id.IsMissing() || sharedSecret.IsMissing())
            {
                throw new ArgumentException("Id or Credential is missing.");
            }

            var secretSha256 = sharedSecret.Sha256();
            var secretSha512 = sharedSecret.Sha512();

            foreach (var secret in secrets)
            {
                var secretDescription = string.IsNullOrEmpty(secret.Description) ? "no description" : secret.Description;

                // this validator is only applicable to shared secrets
                if (secret.Type != Constants.SecretTypes.SharedSecret)
                {
                    _logger.LogDebug("Skipping secret: {description}, secret is not of type {type}.", secretDescription, Constants.SecretTypes.SharedSecret);
                    continue;
                }

                bool isValid = false;
                byte[] secretBytes;

                try
                {
                    secretBytes = Convert.FromBase64String(secret.Value);
                }
                catch (FormatException)
                {
                    _logger.LogError("Secret: {description} uses invalid hashing algorithm.", secretDescription);
                    return fail;
                }

                if (secretBytes.Length == 32)
                {
                    isValid = TimeConstantComparer.IsEqual(secret.Value, secretSha256);
                }
                else if (secretBytes.Length == 64)
                {
                    isValid = TimeConstantComparer.IsEqual(secret.Value, secretSha512);
                }
                else
                {
                    _logger.LogError("Secret: {description} uses invalid hashing algorithm.", secretDescription);
                    return fail;
                }

                if (isValid)
                {
                    return success;
                }
            }

            _logger.LogVerbose("No matching hashed secret found.");
            return fail;
        }
    }
}