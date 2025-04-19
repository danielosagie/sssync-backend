using System;

namespace sssync_backend.infrastructure.Exceptions
{
    /// <summary>
    /// Custom exception for errors occurring during interactions with platform APIs.
    /// </summary>
    public class PlatformApiException : Exception
    {
        public string? PlatformResponse { get; }

        public PlatformApiException(string message) : base(message)
        { }

        public PlatformApiException(string message, Exception innerException) : base(message, innerException)
        { }

        public PlatformApiException(string message, string? platformResponse) : base(message)
        {
            PlatformResponse = platformResponse;
        }

         public PlatformApiException(string message, string? platformResponse, Exception innerException) : base(message, innerException)
        {
            PlatformResponse = platformResponse;
        }
    }
} 