namespace CurlGenerator.Core;

public interface ISettings
{
    /// <summary>
    /// Gets or sets the path to the Open API (local file or URL)
    /// </summary>
    string OpenApiPath { get; set; }


    /// <summary>
    /// Gets or sets the name of the log file. `null` disables logging.
    /// </summary>
    string? LogFile { get; set; }

    /// <summary>
    /// Gets or sets the authorization header to use for all requests
    /// </summary>
    string? AuthorizationHeader { get; set; }
    
    /// <summary>
    /// Gets or sets the default Content-Type header to use for all requests
    /// </summary>
    string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the default BaseUrl to use for all requests
    /// </summary>
    string? BaseUrl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to skip the certificate check in curl.
    /// </summary>
    bool SkipCertificateCheck { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to read the message body from standard input.
    /// </summary>
    bool ReadBodyFromStdin { get; set; }
}
