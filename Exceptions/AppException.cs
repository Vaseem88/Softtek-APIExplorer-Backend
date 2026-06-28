using System.Net;

namespace Softtek_APIExplorer_Backend.Exceptions;

public sealed class AppException : Exception
{
    public AppException(string message, HttpStatusCode statusCode) : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}
