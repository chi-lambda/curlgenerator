namespace CurlGenerator.Core;

public interface ISettings
{
    /// <summary>
    /// Sets the path to the Open API (local file or URL)
    /// </summary>
    string OpenApiPath { get; }


    /// <summary>
    /// Sets the name of the log file. `null` disables logging.
    /// </summary>
    string? LogFile { get; }

    /// <summary>
    /// Sets the authorization header to use for all requests
    /// </summary>
    string? AuthorizationHeader { get; }

    /// <summary>
    /// Sets the default Content-Type header to use for all requests
    /// </summary>
    string ContentType { get; }

    /// <summary>
    /// Sets the default BaseUrl to use for all requests
    /// </summary>
    string BaseUrl { get; }

    /// <summary>
    /// Sets a value indicating whether to skip the certificate check in curl.
    /// </summary>
    bool SkipCertificateCheck { get; }

    /// <summary>
    /// Sets a value indicating whether to read the message body from standard input.
    /// </summary>
    bool ReadBodyFromStdin { get; }

    /// <summary>
    /// Sets the name of file where to store and load cookies. Setting it to `null` disables cookies.
    /// </summary>
    string? CookieFile { get; }
    
    /// <summary>
    /// Gets additional options passed to curl.
    /// </summary>
    string? CurlOpts { get; }
}
